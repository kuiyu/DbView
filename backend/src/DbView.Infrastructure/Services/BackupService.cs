using DbView.Core.Abstractions;
using DbView.Core.Backup;
using DbView.Core.Models;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace DbView.Infrastructure.Services
{
    public class BackupService : IBackupService
    {
        /// <summary>
        /// 正在进行的备份（按连接 Id）。用于防止同一连接的并发备份：
        /// 大库一次备份常超过调度轮询间隔，若不加互斥会出现多个备份同时压库、互相抢连接。
        /// </summary>
        private static readonly ConcurrentDictionary<long, byte> RunningBackups = new();

        private readonly DatabaseService _databaseService;

        public BackupService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// 生成一份完整备份。
        ///
        /// 生产级要点：
        /// 1. 原子写入 —— 先写 {name}.sql.tmp，全部内容写完并算出 SHA256 后再原子改名为 {name}.sql。
        ///    中途失败/进程被杀只会留下 .tmp（不会被列出、不可被恢复），杜绝"半截备份被当成完整备份恢复成半个库"。
        /// 2. 按连接隔离 —— 落在 backups/{connectionId}/ 目录，避免 A 连接看到并误恢复 B 连接的备份。
        /// 3. 一致性快照 —— 全程使用同一个连接（PostgreSQL 为 REPEATABLE READ 事务），
        ///    保证多张表的数据来自同一时点，不会出现"订单表已更新、库存表还是旧值"的自相矛盾。
        /// 4. 可验证 —— 元数据记录每表行数与 SHA256，恢复后可逐表比对行数，证明没有丢数据。
        /// </summary>
        public async Task<BackupFileInfo> CreateBackupAsync(Connection connection, string? name, CancellationToken cancellationToken = default)
        {
            var backupName = SanitizeFileName(name ?? $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}");

            // 压缩默认开启；加密仅在配置了密钥时启用
            var compressed = BackupStorage.CompressionEnabled;
            var key = BackupCrypto.TryGetKey();
            var encrypted = key != null;
            var fileName = BackupStorage.GetPayloadFileName(backupName, compressed, encrypted);

            BackupStorage.EnsureConnectionDir(connection.Id);

            var finalPath = BackupStorage.GetSqlPath(connection.Id, fileName);
            var tmpPath = finalPath + ".tmp";
            var metaPath = BackupStorage.GetMetadataPath(connection.Id, fileName);
            var tmpMetaPath = metaPath + ".tmp";

            var metadata = new BackupMetadata
            {
                Id = BackupStorage.GenerateBackupId(connection.Id),
                ConnectionId = connection.Id,
                Name = backupName,
                FileName = fileName,
                DatabaseName = connection.DatabaseName,
                DbType = connection.DbType,
                CreatedAt = DateTime.UtcNow,
                Compressed = compressed,
                Encrypted = encrypted,
                Trigger = backupName.StartsWith("auto_", StringComparison.Ordinal) ? "auto"
                    : (backupName.StartsWith("pre_restore_", StringComparison.Ordinal) ? "pre_restore" : "manual")
            };

            // 同一连接不允许并发备份：手动备份与自动调度都走这里，天然互斥
            if (!RunningBackups.TryAdd(connection.Id, 0))
            {
                throw new InvalidOperationException($"连接「{connection.Name}」已有备份正在进行中，已跳过本次备份");
            }

            try
            {
                // 1) 流式写入临时文件（边查边写，内存占用与库大小无关；压缩/加密同样是流式的）
                //    leaveOpen: true —— 必须由 payload 自己负责关闭（否则 StreamWriter 会抢先关闭底层流，
                //    导致文件仍被占用、后续原子改名失败）
                var rowCounts = new Dictionary<string, long>(StringComparer.Ordinal);
                BackupSchemaObjects schemaObjects;
                await using (var payload = BackupStorage.CreatePayloadWrite(tmpPath, compressed, encrypted, key))
                {
                    await using (var writer = new StreamWriter(payload, new UTF8Encoding(false), bufferSize: 65536, leaveOpen: true))
                    {
                        schemaObjects = await GenerateBackupSqlAsync(writer, connection, rowCounts, cancellationToken);
                    }
                }

                // 2) 统计校验和与大小
                metadata.FileSize = new FileInfo(tmpPath).Length;
                metadata.Sha256 = BackupStorage.ComputeSha256(tmpPath);
                metadata.TableRowCounts = rowCounts;
                metadata.SchemaObjectCounts = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["Sequences"] = schemaObjects.Sequences.Count,
                    ["Functions"] = schemaObjects.Functions.Count,
                    ["Views"] = schemaObjects.Views.Count,
                    ["Indexes"] = schemaObjects.Indexes.Count,
                    ["Constraints"] = schemaObjects.Constraints.Count,
                    ["Triggers"] = schemaObjects.Triggers.Count,
                    ["Types"] = schemaObjects.Types.Count
                };
                metadata.Warnings = schemaObjects.Warnings;
                metadata.Completed = true;

                // 3) 先落元数据，再原子改名 .sql → 最终"存在即完整"
                var json = System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(tmpMetaPath, json, new UTF8Encoding(false), cancellationToken);
                File.Move(tmpMetaPath, metaPath, overwrite: true);
                File.Move(tmpPath, finalPath, overwrite: true);

                return new BackupFileInfo
                {
                    Id = metadata.Id,
                    FileName = fileName,
                    FilePath = finalPath,
                    FileSize = metadata.FileSize,
                    CreatedAt = metadata.CreatedAt,
                    Sha256 = metadata.Sha256,
                    Compressed = compressed,
                    Encrypted = encrypted
                };
            }
            catch
            {
                // 失败清理：临时文件与未完成的元数据都不应残留
                TryDelete(tmpPath);
                TryDelete(tmpMetaPath);
                TryDelete(metaPath);
                throw;
            }
            finally
            {
                RunningBackups.TryRemove(connection.Id, out _);
            }
        }

        private async Task<BackupSchemaObjects> GenerateBackupSqlAsync(
            StreamWriter writer,
            Connection connection,
            Dictionary<string, long> rowCounts,
            CancellationToken cancellationToken)
        {
            var dbType = connection.DbType.ToLower();
            var isMySql = dbType == "mysql";
            var isSqlServer = dbType == "sqlserver";
            var isPostgreSql = dbType == "postgresql";

            // 全程复用同一个会话：单连接 +（PG）REPEATABLE READ 事务 → 一致性快照
            await using var session = await _databaseService.OpenBackupSessionAsync(connection, cancellationToken);

            var tables = await _databaseService.GetTablesAsync(connection, cancellationToken, session);

            await writer.WriteLineAsync($"-- 数据库备份: {connection.DatabaseName}");
            await writer.WriteLineAsync($"-- 备份时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            await writer.WriteLineAsync($"-- 数据库类型: {connection.DbType}");
            await writer.WriteLineAsync($"-- 备份格式版本: {BackupStorage.CurrentFormatVersion}");
            await writer.WriteLineAsync("-- 自包含备份：先重建表结构再导入数据，可直接恢复到空库");
            await writer.WriteLineAsync();

            // MySQL 恢复前关闭外键检查，避免 DROP/CREATE/INSERT 顺序受外键约束影响
            if (isMySql)
            {
                await writer.WriteLineAsync("SET FOREIGN_KEY_CHECKS=0;");
                await writer.WriteLineAsync();
            }

            // SQL Server 没有 DROP TABLE ... CASCADE：若目标库已存在外键，DROP TABLE 会被阻塞。
            // 这里先动态删除全部外键（随后由备份中的约束段重建）。
            // 用 BEGIN ... END 包成一个语句块，避免被按分号拆开导致变量作用域失效。
            if (isSqlServer)
            {
                await writer.WriteLineAsync("-- SQL Server：先删除全部外键，避免 DROP TABLE 被外键阻塞（随后会重建）");
                await writer.WriteLineAsync("BEGIN");
                await writer.WriteLineAsync("  DECLARE @dbview_sql nvarchar(max) = N'';");
                await writer.WriteLineAsync("  SELECT @dbview_sql = @dbview_sql + N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(parent_object_id)) + N' DROP CONSTRAINT ' + QUOTENAME(name) + N';' + NCHAR(10) FROM sys.foreign_keys;");
                await writer.WriteLineAsync("  IF @dbview_sql <> N'' EXEC sp_executesql @dbview_sql;");
                await writer.WriteLineAsync("END;");
                await writer.WriteLineAsync();
            }

            // PostgreSQL：若库中存在向量（vector）列，恢复前确保 vector 扩展已安装，
            // 否则 CREATE TABLE ... vector 会因"类型 vector 不存在"而失败，导致整批恢复中断。
            if (isPostgreSql)
            {
                bool hasVector = false;
                try
                {
                    hasVector = await HasPgVectorColumnAsync(session, cancellationToken);
                }
                catch
                {
                    hasVector = false;
                }
                if (hasVector)
                {
                    await writer.WriteLineAsync("CREATE EXTENSION IF NOT EXISTS vector;");
                    await writer.WriteLineAsync();
                }
            }

            // 采集数据库对象（schema/类型/索引/约束/序列/视图/函数/触发器）。
            // 只备份"表结构 + 数据"是不够的：向量索引丢了检索退化为全表扫描，外键/唯一约束
            // 丢了数据完整性失效，序列丢了 serial 列无法插入新行。
            // 对象定义采集走独立连接，不复用备份的 REPEATABLE READ 事务：
            // PostgreSQL 中任一语句报错都会把整个事务置为 aborted（25P02），若在系统表查询里
            // 出现一处 SQL 兼容问题，会把后续所有数据查询一起拖垮，导致整次备份失败。
            // DDL 采集与数据快照相差几毫秒无关紧要，但备份的健壮性因此大幅提升。
            var schemaObjects = await _databaseService.GetSchemaObjectsAsync(connection, cancellationToken);

            // PostgreSQL 的 schema 必须最先建出来：序列、表、视图都可能落在非 public 的 schema 里，
            // 若放到表循环里再建，前面的 CREATE SEQUENCE 会因 schema 不存在而失败（整批回滚）。
            if (isPostgreSql)
            {
                var allSchemas = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in schemaObjects.Schemas)
                {
                    if (!string.IsNullOrWhiteSpace(s) && !string.Equals(s, "public", StringComparison.OrdinalIgnoreCase))
                    {
                        allSchemas.Add(s);
                    }
                }
                foreach (var t in tables)
                {
                    if (!string.IsNullOrWhiteSpace(t.SchemaName) && !string.Equals(t.SchemaName, "public", StringComparison.OrdinalIgnoreCase))
                    {
                        allSchemas.Add(t.SchemaName);
                    }
                }

                if (allSchemas.Count > 0)
                {
                    await writer.WriteLineAsync("-- ===== schema（必须先于序列/表创建）=====");
                    foreach (var s in allSchemas)
                    {
                        await writer.WriteLineAsync($"CREATE SCHEMA IF NOT EXISTS \"{s.Replace("\"", "\"\"")}\";");
                    }
                    await writer.WriteLineAsync();
                }
            }

            // 用户自定义类型（ENUM 等）必须先于表创建：列类型引用它时，类型不存在会导致建表失败
            if (schemaObjects.Types.Count > 0)
            {
                await writer.WriteLineAsync("-- ===== 自定义类型（必须先于表创建）=====");
                foreach (var sql in schemaObjects.Types)
                {
                    await writer.WriteLineAsync(EnsureTerminated(sql));
                }
                await writer.WriteLineAsync();
            }

            // 序列必须先于表创建：PostgreSQL 的 serial 列默认值是 nextval('seq'::regclass)，
            // 序列不存在时 CREATE TABLE 会直接失败（恢复到空库时必然踩到）。
            if (schemaObjects.Sequences.Count > 0)
            {
                await writer.WriteLineAsync("-- ===== 序列（必须先于表创建）=====");
                foreach (var sql in schemaObjects.Sequences)
                {
                    await writer.WriteLineAsync(EnsureTerminated(sql));
                }
                await writer.WriteLineAsync();
            }

            foreach (var table in tables)
            {
                var schema = string.IsNullOrWhiteSpace(table.SchemaName) ? null : table.SchemaName;

                var tableKey = schema == null ? table.TableName : schema + "." + table.TableName;
                await writer.WriteLineAsync($"-- 表: {tableKey}");

                // 先删除目标库中可能存在的同名表（表不存在时 IF EXISTS 不会报错；Oracle 不支持 IF EXISTS，由恢复端点容错）
                await writer.WriteLineAsync(GetDropTableSql(dbType, table.TableName, schema));

                // 生成建表语句（含完整类型、主键、自增、默认值），并取回自增列信息
                var (createSql, autoColumns) = await _databaseService.GenerateCreateTableSqlAsync(
                    connection, table.TableName, cancellationToken, schema, session);
                await writer.WriteLineAsync(createSql);

                var quotedTable = GetQualifiedTableName(dbType, table.TableName, schema);
                var hasAuto = autoColumns.Count > 0;

                // SQL Server 显式插入自增列值需先开启 IDENTITY_INSERT（整批在同一连接执行时生效）
                if (hasAuto && isSqlServer)
                {
                    await writer.WriteLineAsync($"SET IDENTITY_INSERT {quotedTable} ON;");
                }

                var columns = await _databaseService.GetTableColumnNamesAsync(connection, table.TableName, cancellationToken, schema, session);

                if (columns.Count > 0)
                {
                    var quotedColumns = columns.Select(col => GetQuotedColumnName(dbType, col));
                    var columnList = string.Join(", ", quotedColumns);

                    // 流式读取数据，逐行写入 INSERT，内存占用与单表大小无关
                    long rowCount = 0;
                    await foreach (var row in _databaseService.StreamTableDataAsync(connection, table.TableName, cancellationToken, schema, session))
                    {
                        var values = new List<string>(row.Length);
                        for (int i = 0; i < row.Length; i++)
                        {
                            values.Add(FormatValue(row[i], dbType));
                        }
                        await writer.WriteLineAsync($"INSERT INTO {quotedTable} ({columnList}) VALUES ({string.Join(", ", values)});");
                        rowCount++;
                    }

                    rowCounts[tableKey] = rowCount;
                }

                if (hasAuto && isSqlServer)
                {
                    await writer.WriteLineAsync($"SET IDENTITY_INSERT {quotedTable} OFF;");
                }

                // PostgreSQL 恢复后重置序列当前值，避免后续插入因序列未前进而主键冲突
                if (hasAuto && isPostgreSql)
                {
                    foreach (var col in autoColumns)
                    {
                        var qc = GetQuotedColumnName(dbType, col);
                        var regClass = schema == null
                            ? $"\"{table.TableName.Replace("\"", "\"\"")}\""
                            : $"\"{schema.Replace("\"", "\"\"")}\".\"{table.TableName.Replace("\"", "\"\"")}\"";

                        var quotedCol = col.Replace("'", "''");

                        // serial 列：直接从 column_default 解析序列名并显式 setval。
                        // pg_get_serial_sequence 依赖 OWNED BY 关系，恢复时序列是独立创建的，
                        // 它会返回 NULL，导致序列停在 1、后续插入主键冲突。
                        var seqName = await _databaseService.GetPgSerialSequenceNameAsync(
                            connection, table.TableName, col, cancellationToken, schema, session);

                        if (!string.IsNullOrWhiteSpace(seqName))
                        {
                            var quotedSeq = seqName!.Replace("'", "''");
                            await writer.WriteLineAsync(
                                $"SELECT setval('{quotedSeq}'::regclass, COALESCE((SELECT MAX({qc}) FROM {quotedTable}), 1), true);");
                        }
                        else
                        {
                            // identity 列（无 column_default）走 pg_get_serial_sequence；
                            // 用 DO 块容错，避免返回 NULL 时 setval(NULL, ...) 报错导致整批回滚。
                            await writer.WriteLineAsync(
                                $"DO $dbview$ BEGIN IF pg_get_serial_sequence('{regClass}', '{quotedCol}') IS NOT NULL THEN " +
                                $"PERFORM setval(pg_get_serial_sequence('{regClass}', '{quotedCol}'), " +
                                $"COALESCE((SELECT MAX({qc}) FROM {quotedTable}), 1), true); END IF; END $dbview$;");
                        }
                    }
                }

                await writer.WriteLineAsync();
            }

            // 其余对象在数据导入完成后再创建：
            // - 索引放在数据之后可显著加快恢复（避免每插一行都维护索引），唯一索引也不会因导入顺序冲突；
            // - 函数先于视图（视图可能引用函数），触发器最后（避免导入过程中触发业务副作用）。
            await WriteObjectSectionAsync(writer, "函数", schemaObjects.Functions);
            await WriteObjectSectionAsync(writer, "视图", schemaObjects.Views);
            await WriteObjectSectionAsync(writer, "索引", schemaObjects.Indexes);
            await WriteObjectSectionAsync(writer, "约束（唯一/外键/CHECK）", schemaObjects.Constraints);
            await WriteObjectSectionAsync(writer, "触发器", schemaObjects.Triggers);

            if (isMySql)
            {
                await writer.WriteLineAsync("SET FOREIGN_KEY_CHECKS=1;");
            }

            // 行数清单：写入文件尾部（注释形式），恢复后可据此逐表比对行数，证明没有丢数据。
            // 同时也保存在元数据旁车文件里，两处互为冗余。
            await writer.WriteLineAsync("-- ===== DBVIEW_BACKUP_MANIFEST_BEGIN =====");
            foreach (var kv in rowCounts)
            {
                await writer.WriteLineAsync($"-- DBVIEW_TABLE_ROW_COUNT|{kv.Key}|{kv.Value}");
            }
            await writer.WriteLineAsync("-- ===== DBVIEW_BACKUP_MANIFEST_END =====");

            return schemaObjects;
        }

        /// <summary>
        /// 输出一组数据库对象的 DDL（带分节注释，便于人工阅读与排错）。
        /// 每条语句统一补齐结尾分号 —— SQLite 的 sqlite_master.sql 等来源本身不带分号，
        /// 不补分号会让相邻两条语句被拼接成一条，恢复时报语法错误。
        /// </summary>
        private static async Task WriteObjectSectionAsync(StreamWriter writer, string title, List<string> statements)
        {
            if (statements.Count == 0)
            {
                return;
            }

            await writer.WriteLineAsync($"-- ===== {title}（{statements.Count} 个）=====");
            foreach (var sql in statements)
            {
                await writer.WriteLineAsync(EnsureTerminated(sql));
            }
            await writer.WriteLineAsync();
        }

        /// <summary>确保语句以分号结尾</summary>
        private static string EnsureTerminated(string sql)
        {
            var s = (sql ?? string.Empty).Trim();
            if (s.Length == 0)
            {
                return s;
            }
            return s.EndsWith(";", StringComparison.Ordinal) ? s : s + ";";
        }

        /// <summary>
        /// 探测 PostgreSQL 库中是否存在向量（vector）列，用于在备份头部加
        /// CREATE EXTENSION IF NOT EXISTS vector，使生成的备份可自包含恢复。
        /// 探测失败（如权限不足）时返回 false，不阻断备份。
        /// </summary>
        private async Task<bool> HasPgVectorColumnAsync(BackupSession session, CancellationToken cancellationToken)
        {
            var command = session.CreateCommand();
            command.CommandText = "SELECT 1 FROM information_schema.columns WHERE udt_name = 'vector' LIMIT 1";
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result != null;
        }

        /// <summary>
        /// 把备份名规整成安全的文件名（去掉路径分隔符与非法字符），防止路径穿越。
        /// </summary>
        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (var ch in name)
            {
                sb.Append(invalid.Contains(ch) || ch == '/' || ch == '\\' || ch == ':' ? '_' : ch);
            }

            var result = sb.ToString().Trim().Trim('.');
            if (string.IsNullOrEmpty(result))
            {
                result = $"backup_{DateTime.Now:yyyyMMdd_HHmmss}";
            }

            return result.Length > 120 ? result[..120] : result;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // 清理失败不应掩盖原始异常
            }
        }

        /// <summary>
        /// 生成删除表的语句。
        ///
        /// 关键点：目标库中若已有"被其它表外键引用"或"被视图依赖"的表，
        /// 普通的 DROP TABLE 会失败并让整批恢复回滚。因此：
        /// - PostgreSQL 用 CASCADE（级联删除依赖的视图/外键；这些对象随后会由备份重建）；
        /// - Oracle 用 CASCADE CONSTRAINTS；
        /// - MySQL 依靠头部 SET FOREIGN_KEY_CHECKS=0；
        /// - SQLite 默认不检查外键；
        /// - SQL Server 依靠头部动态删除全部外键（见 WriteSqlServerForeignKeyDropsAsync）。
        /// </summary>
        private string GetDropTableSql(string dbType, string tableName, string? schema = null)
        {
            var q = GetQualifiedTableName(dbType, tableName, schema);
            return dbType switch
            {
                "postgresql" => $"DROP TABLE IF EXISTS {q} CASCADE;",
                "oracle" => $"DROP TABLE {q} CASCADE CONSTRAINTS;",
                _ => $"DROP TABLE IF EXISTS {q};"
            };
        }

        private string GetQuotedColumnName(string dbType, string columnName)
        {
            return dbType.ToLower() switch
            {
                "postgresql" => $"\"{columnName.Replace("\"", "\"\"")}\"",
                "mysql" => $"`{columnName.Replace("`", "``")}`",
                "sqlite" => $"\"{columnName.Replace("\"", "\"\"")}\"",
                "sqlserver" => $"[{columnName.Replace("]", "]]")}]",
                "oracle" => $"\"{columnName.Replace("\"", "\"\"")}\"",
                _ => columnName
            };
        }

        private string GetQuotedTableName(string dbType, string tableName)
        {
            return dbType.ToLower() switch
            {
                "postgresql" => $"\"{tableName.Replace("\"", "\"\"")}\"",
                "mysql" => $"`{tableName.Replace("`", "``")}`",
                "sqlite" => $"\"{tableName.Replace("\"", "\"\"")}\"",
                "sqlserver" => $"[{tableName.Replace("]", "]]")}]",
                "oracle" => $"\"{tableName.Replace("\"", "\"\"")}\"",
                _ => tableName
            };
        }

        /// <summary>
        /// 带 schema 限定的表名（PostgreSQL 多 schema 必须限定，否则恢复后表会落到默认 search_path）。
        /// </summary>
        private string GetQualifiedTableName(string dbType, string tableName, string? schema)
        {
            if (dbType.ToLower() == "postgresql" && !string.IsNullOrWhiteSpace(schema))
            {
                return $"\"{schema.Replace("\"", "\"\"")}\".\"{tableName.Replace("\"", "\"\"")}\"";
            }

            return GetQuotedTableName(dbType, tableName);
        }

        /// <summary>
        /// 把列值编码为各数据库可用的 SQL 字面量。
        ///
        /// 关键点：
        /// - 数值一律使用不变文化（InvariantCulture）：否则 de-DE 等环境下 1.5 会被格式化成 "1,5"，
        ///   生成出非法 SQL 或数值错乱；double/float 用 "R"（往返）格式避免精度丢失。
        /// - 二进制按各库语法分别编码：PG 用 '\x..'::bytea、MySQL/SQL Server 用 0x..、
        ///   SQLite 用 X'..'、Oracle 用 HEXTORAW('..')，否则二进制列恢复后即损坏。
        /// - 时间保留毫秒与时区偏移，避免恢复后时间漂移。
        /// </summary>
        private string FormatValue(object value, string dbType)
        {
            if (value == null || value == DBNull.Value)
                return "NULL";

            var db = dbType.ToLower();

            if (value is string strValue)
                return $"'{EscapeStringLiteral(strValue, db)}'";

            if (value is char charValue)
                return $"'{EscapeStringLiteral(charValue.ToString(), db)}'";

            if (value is bool boolValue)
                return db == "oracle" ? (boolValue ? "1" : "0") : (boolValue ? "TRUE" : "FALSE");

            if (value is byte[] byteArray)
                return FormatBinary(byteArray, db);

            if (value is DateTime dtValue)
                return $"'{FormatDateTime(dtValue, db)}'";

            if (value is DateTimeOffset dtoValue)
                return $"'{FormatDateTimeOffset(dtoValue, db)}'";

            if (value is TimeSpan tsValue)
                return $"'{EscapeStringLiteral(tsValue.ToString("c", CultureInfo.InvariantCulture), db)}'";

            // GUID：写成字符串字面量，PostgreSQL uuid / SQL Server uniqueidentifier 均可隐式转换
            if (value is Guid guidValue)
                return $"'{EscapeStringLiteral(guidValue.ToString("D"), db)}'";

            // NaN / Infinity 在多数库需要写成带引号的字面量
            if (value is double dblValue)
            {
                if (double.IsNaN(dblValue)) return "'NaN'";
                if (double.IsPositiveInfinity(dblValue)) return "'Infinity'";
                if (double.IsNegativeInfinity(dblValue)) return "'-Infinity'";
                return dblValue.ToString("R", CultureInfo.InvariantCulture);
            }

            if (value is float fltValue)
            {
                if (float.IsNaN(fltValue)) return "'NaN'";
                if (float.IsPositiveInfinity(fltValue)) return "'Infinity'";
                if (float.IsNegativeInfinity(fltValue)) return "'-Infinity'";
                return fltValue.ToString("R", CultureInfo.InvariantCulture);
            }

            if (value is decimal decValue)
                return decValue.ToString(CultureInfo.InvariantCulture);

            // 其余数值类型（int/long/short/byte 等）用不变文化输出
            if (value is IFormattable formattable)
                return formattable.ToString(null, CultureInfo.InvariantCulture);

            // 兜底：按字符串处理并加引号，避免生成数据库无法识别的裸值
            return $"'{EscapeStringLiteral(value.ToString() ?? string.Empty, db)}'";
        }

        /// <summary>
        /// 二进制字面量：各数据库语法不同，用错会导致二进制列恢复后数据损坏。
        /// </summary>
        private static string FormatBinary(byte[] bytes, string db)
        {
            var hex = Convert.ToHexString(bytes);
            return db switch
            {
                "postgresql" => $"'\\x{hex}'::bytea",
                "sqlite" => $"X'{hex}'",
                "oracle" => $"HEXTORAW('{hex}')",
                _ => $"0x{hex}"
            };
        }

        /// <summary>
        /// 时间字面量：保留 7 位小数（微秒级），PostgreSQL 下带上时区偏移以避免时间漂移。
        /// </summary>
        private static string FormatDateTime(DateTime dt, string db)
        {
            const string pattern = "yyyy-MM-dd HH:mm:ss.fffffff";
            if (db == "postgresql")
            {
                if (dt.Kind == DateTimeKind.Utc)
                {
                    return dt.ToString(pattern, CultureInfo.InvariantCulture) + "+00";
                }
                if (dt.Kind == DateTimeKind.Local)
                {
                    var offset = TimeZoneInfo.Local.GetUtcOffset(dt);
                    return dt.ToString(pattern, CultureInfo.InvariantCulture) + offset.ToString("zzz", CultureInfo.InvariantCulture);
                }
            }

            return dt.ToString(pattern, CultureInfo.InvariantCulture);
        }

        private static string FormatDateTimeOffset(DateTimeOffset dto, string db)
        {
            const string pattern = "yyyy-MM-dd HH:mm:ss.fffffff";
            var text = dto.ToString(pattern, CultureInfo.InvariantCulture);
            if (db == "postgresql")
            {
                text += dto.Offset.ToString("zzz", CultureInfo.InvariantCulture);
            }
            return text;
        }

        /// <summary>
        /// 转义 SQL 字符串字面量中的特殊字符。
        /// - 单引号 ' 在所有数据库中都需转义为 ''。
        /// - 反斜杠 \ 在 MySQL/MariaDB 中是字符串字面量的转义符，必须加倍为 \\，
        ///   否则 JSON 等文本中的 \n、\"、\\ 会被 MySQL 当成转义序列解析，破坏原数据
        ///   导致恢复时报 #3140（Invalid escape character in JSON）。
        ///   PostgreSQL（standard_conforming_strings=on）、SQLite、SQL Server、Oracle 中
        ///   \ 不是转义符，保持原样即可。
        /// </summary>
        private string EscapeStringLiteral(string value, string dbType)
        {
            var sb = new StringBuilder(value.Length + 16);
            var needsBackslashEscape = dbType == "mysql";

            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '\'':
                        sb.Append("''");
                        break;
                    case '\\' when needsBackslashEscape:
                        sb.Append("\\\\");
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
