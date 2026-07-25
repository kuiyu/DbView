using DbView.Core.Abstractions;
using DbView.Core.Models;
using System.Text;

namespace DbView.Infrastructure.Services
{
    public class BackupService : IBackupService
    {
        private readonly DatabaseService _databaseService;

        public BackupService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<BackupFileInfo> CreateBackupAsync(Connection connection, string? name, CancellationToken cancellationToken = default)
        {
            var backupName = name ?? $"backup_{DateTime.Now:yyyyMMdd_HHmmss}";
            var backupDir = Path.Combine(AppContext.BaseDirectory, "backups");
            Directory.CreateDirectory(backupDir);

            var fileName = $"{backupName}.sql";
            var filePath = Path.Combine(backupDir, fileName);

            var sql = await GenerateBackupSql(connection, cancellationToken);
            await File.WriteAllTextAsync(filePath, sql, cancellationToken);

            var fileInfo = new FileInfo(filePath);
            return new BackupFileInfo
            {
                FileName = fileName,
                FilePath = filePath,
                FileSize = fileInfo.Length,
                CreatedAt = DateTime.Now
            };
        }

        private async Task<string> GenerateBackupSql(Connection connection, CancellationToken cancellationToken)
        {
            var tables = await _databaseService.GetTablesAsync(connection, cancellationToken);
            var sb = new StringBuilder();

            var dbType = connection.DbType.ToLower();
            var isMySql = dbType == "mysql";
            var isSqlServer = dbType == "sqlserver";
            var isPostgreSql = dbType == "postgresql";

            sb.AppendLine($"-- 数据库备份: {connection.DatabaseName}");
            sb.AppendLine($"-- 备份时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"-- 数据库类型: {connection.DbType}");
            sb.AppendLine("-- 自包含备份：先重建表结构再导入数据，可直接恢复到空库");
            sb.AppendLine();

            // MySQL 恢复前关闭外键检查，避免 DROP/CREATE/INSERT 顺序受外键约束影响
            if (isMySql)
            {
                sb.AppendLine("SET FOREIGN_KEY_CHECKS=0;");
                sb.AppendLine();
            }

            foreach (var table in tables)
            {
                sb.AppendLine($"-- 表: {table.TableName}");

                // 先删除目标库中可能存在的同名表（表不存在时 IF EXISTS 不会报错；Oracle 不支持 IF EXISTS，由恢复端点容错）
                sb.AppendLine(GetDropTableSql(dbType, table.TableName));

                // 生成建表语句（含完整类型、主键、自增、默认值），并取回自增列信息
                var (createSql, autoColumns) = await _databaseService.GenerateCreateTableSqlAsync(connection, table.TableName, cancellationToken);
                sb.AppendLine(createSql);

                var quotedTable = GetQuotedTableName(dbType, table.TableName);
                var hasAuto = autoColumns.Count > 0;

                // SQL Server 显式插入自增列值需先开启 IDENTITY_INSERT（整批在同一连接执行时生效）
                if (hasAuto && isSqlServer)
                {
                    sb.AppendLine($"SET IDENTITY_INSERT {quotedTable} ON;");
                }

                var columns = await _databaseService.GetTableColumnNamesAsync(connection, table.TableName, cancellationToken);
                var rows = await _databaseService.GetAllTableDataAsync(connection, table.TableName, cancellationToken);

                if (columns.Count > 0 && rows.Count > 0)
                {
                    var quotedColumns = columns.Select(col => GetQuotedColumnName(dbType, col));
                    var columnList = string.Join(", ", quotedColumns);

                    foreach (var row in rows)
                    {
                        var values = new List<string>();
                        for (int i = 0; i < row.Length; i++)
                        {
                            values.Add(FormatValue(row[i], dbType));
                        }
                        sb.AppendLine($"INSERT INTO {quotedTable} ({columnList}) VALUES ({string.Join(", ", values)});");
                    }
                }

                if (hasAuto && isSqlServer)
                {
                    sb.AppendLine($"SET IDENTITY_INSERT {quotedTable} OFF;");
                }

                // PostgreSQL 恢复后重置序列当前值，避免后续插入因序列未前进而主键冲突
                if (hasAuto && isPostgreSql)
                {
                    foreach (var col in autoColumns)
                    {
                        var qc = GetQuotedColumnName(dbType, col);
                        sb.AppendLine($"SELECT setval(pg_get_serial_sequence('{table.TableName}','{col}'), COALESCE((SELECT MAX({qc}) FROM {quotedTable}), 1));");
                    }
                }

                sb.AppendLine();
            }

            if (isMySql)
            {
                sb.AppendLine("SET FOREIGN_KEY_CHECKS=1;");
            }

            return sb.ToString();
        }

        private string GetDropTableSql(string dbType, string tableName)
        {
            var q = GetQuotedTableName(dbType, tableName);
            if (dbType == "oracle")
            {
                return $"DROP TABLE {q};";
            }
            return $"DROP TABLE IF EXISTS {q};";
        }

        private string GetQuotedColumnName(string dbType, string columnName)
        {
            return dbType.ToLower() switch
            {
                "postgresql" => $"\"{columnName}\"",
                "mysql" => $"`{columnName}`",
                "sqlite" => $"\"{columnName}\"",
                "sqlserver" => $"[{columnName}]",
                "oracle" => $"\"{columnName}\"",
                _ => columnName
            };
        }

        private string GetQuotedTableName(string dbType, string tableName)
        {
            return dbType.ToLower() switch
            {
                "postgresql" => $"\"{tableName}\"",
                "mysql" => $"`{tableName}`",
                "sqlite" => $"\"{tableName}\"",
                "sqlserver" => $"[{tableName}]",
                "oracle" => $"\"{tableName}\"",
                _ => tableName
            };
        }

        private string FormatValue(object value, string dbType)
        {
            if (value == null || value == DBNull.Value)
                return "NULL";

            var db = dbType.ToLower();

            if (value is string strValue)
                return $"'{EscapeStringLiteral(strValue, db)}'";

            if (value is DateTime dtValue)
                return $"'{dtValue:yyyy-MM-dd HH:mm:ss}'";

            if (value is bool boolValue)
                return db == "oracle" ? (boolValue ? "1" : "0") : (boolValue ? "TRUE" : "FALSE");

            if (value is byte[] byteArray)
                return $"0x{BitConverter.ToString(byteArray).Replace("-", "")}";

            return value.ToString() ?? "NULL";
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
