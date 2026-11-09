using DbView.Core.Models;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text;

namespace DbView.Infrastructure.Services
{
    public class DatabaseService
    {
        public async Task<IReadOnlyList<TableInfo>> GetTablesAsync(Connection connection, CancellationToken cancellationToken = default, BackupSession? session = null)
        {
            var tables = new List<TableInfo>();

            var owned = session == null;
            var conn = session?.Db ?? CreateConnection(connection);
            if (owned)
            {
                await conn.OpenAsync(cancellationToken);
            }

            try
            {
                var command = session?.CreateCommand() ?? conn.CreateCommand();
                command.CommandText = GetTablesQuery(connection.DbType, connection.DatabaseName);

                using (command)
                using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        tables.Add(new TableInfo
                        {
                            TableName = reader.GetString(0),
                            SchemaName = reader.IsDBNull(1) ? "public" : reader.GetString(1),
                            Comment = reader.IsDBNull(2) ? string.Empty : reader.GetString(2)
                        });
                    }
                }
            }
            finally
            {
                if (owned)
                {
                    await conn.DisposeAsync();
                }
            }

            return tables;
        }

        /// <summary>
        /// 将列值安全地读取为 bool。部分驱动（如 Microsoft.Data.Sqlite 的 PRAGMA 返回的
        /// notnull/pk 是 integer 类型）对 GetBoolean() 直接抛
        /// "Reading as 'System.Boolean' is not supported for fields having DataTypeName 'integer'"。
        /// 这里统一从原始值（int/long/string/byte 等）做真值判断，跨数据库通用。
        /// </summary>
        private static bool ReadBool(DbDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal))
                return false;

            var value = reader.GetValue(ordinal);
            return value switch
            {
                bool b => b,
                sbyte or byte or short or ushort or int or uint or long or ulong => Convert.ToInt64(value) != 0,
                float or double or decimal => Convert.ToDouble(value) != 0,
                string s => !string.IsNullOrWhiteSpace(s) && s != "0" && s.ToLowerInvariant() != "false",
                _ => Convert.ToBoolean(value)
            };
        }

        public async Task<IReadOnlyList<ColumnInfo>> GetTableColumnsAsync(Connection connection, string tableName, CancellationToken cancellationToken = default, string? tableSchema = null)
        {
            var columns = new List<ColumnInfo>();
            
            using var conn = CreateConnection(connection);
            await conn.OpenAsync(cancellationToken);
            
            var command = conn.CreateCommand();
            command.CommandText = GetColumnsQuery(connection.DbType, tableName, connection.DatabaseName, tableSchema);
            
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(new ColumnInfo
                {
                    ColumnName = reader.GetString(0),
                    DataType = reader.GetString(1),
                    IsNullable = ReadBool(reader, 2),
                    IsPrimaryKey = ReadBool(reader, 3),
                    IsAutoIncrement = ReadBool(reader, 4),
                    DefaultValue = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    Comment = reader.IsDBNull(6) ? string.Empty : reader.GetString(6)
                });
            }
            
            return columns;
        }

        public async Task<object> GetTableDataAsync(Connection connection, string tableName, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            using var conn = CreateConnection(connection);
            await conn.OpenAsync(cancellationToken);
            
            var quotedTableName = GetQuotedTableName(connection.DbType, tableName);
            
            var countCommand = conn.CreateCommand();
            countCommand.CommandText = $"SELECT COUNT(*) FROM {quotedTableName}";
            var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
            
            var primaryKeys = await GetPrimaryKeyColumnsAsync(conn, connection.DbType, tableName, connection.DatabaseName, cancellationToken);
            var orderByClause = primaryKeys.Count > 0 
                ? $"ORDER BY {string.Join(", ", primaryKeys.Select(k => GetQuotedColumnName(connection.DbType, k)))}" 
                : string.Empty;
            
            var command = conn.CreateCommand();
            command.CommandText = $"SELECT * FROM {quotedTableName} {orderByClause} LIMIT {pageSize} OFFSET {(page - 1) * pageSize}";
            
            var items = new List<object[]>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new object[reader.FieldCount];
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[i] = ReadCellValue(reader, i);
                }
                items.Add(row);
            }
            
            return new
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<object> QueryTableDataAsync(Connection connection, string tableName, List<QueryFilter> filters, string orderBy, string orderDirection, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            using var conn = CreateConnection(connection);
            await conn.OpenAsync(cancellationToken);

            var quotedTableName = GetQuotedTableName(connection.DbType, tableName);
            var whereClause = BuildWhereClause(connection.DbType, filters);

            var countCommand = conn.CreateCommand();
            countCommand.CommandText = $"SELECT COUNT(*) FROM {quotedTableName}{whereClause}";
            var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

            var primaryKeys = await GetPrimaryKeyColumnsAsync(conn, connection.DbType, tableName, connection.DatabaseName, cancellationToken);
            var orderByClause = BuildOrderByClause(connection.DbType, orderBy, orderDirection, primaryKeys);

            var command = conn.CreateCommand();
            command.CommandText = $"SELECT * FROM {quotedTableName}{whereClause}{orderByClause} LIMIT {pageSize} OFFSET {(page - 1) * pageSize}";

            var items = new List<object[]>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new object[reader.FieldCount];
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[i] = ReadCellValue(reader, i);
                }
                items.Add(row);
            }

            return new
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        private string BuildWhereClause(string dbType, List<QueryFilter> filters)
        {
            if (filters == null || filters.Count == 0)
                return string.Empty;

            var conditions = new List<string>();
            foreach (var filter in filters)
            {
                if (string.IsNullOrWhiteSpace(filter.ColumnName) || string.IsNullOrWhiteSpace(filter.Operator))
                    continue;

                var quotedColumn = GetQuotedColumnName(dbType, filter.ColumnName);
                var condition = filter.Operator.ToLower() switch
                {
                    "eq" => $"{quotedColumn} = {FormatFilterValue(filter.Value, dbType)}",
                    "neq" => $"{quotedColumn} != {FormatFilterValue(filter.Value, dbType)}",
                    "gt" => $"{quotedColumn} > {FormatFilterValue(filter.Value, dbType)}",
                    "gte" => $"{quotedColumn} >= {FormatFilterValue(filter.Value, dbType)}",
                    "lt" => $"{quotedColumn} < {FormatFilterValue(filter.Value, dbType)}",
                    "lte" => $"{quotedColumn} <= {FormatFilterValue(filter.Value, dbType)}",
                    "like" => $"{quotedColumn} LIKE {FormatFilterValue(filter.Value, dbType)}",
                    "notlike" => $"{quotedColumn} NOT LIKE {FormatFilterValue(filter.Value, dbType)}",
                    "isnull" => $"{quotedColumn} IS NULL",
                    "isnotnull" => $"{quotedColumn} IS NOT NULL",
                    "isempty" => $"{quotedColumn} = ''",
                    "isnotempty" => $"{quotedColumn} != ''",
                    _ => $"{quotedColumn} = {FormatFilterValue(filter.Value, dbType)}"
                };
                conditions.Add(condition);
            }

            if (conditions.Count == 0)
                return string.Empty;

            return $" WHERE {string.Join(" AND ", conditions)}";
        }

        /// <summary>
        /// 将过滤值格式化为 SQL 字面量。
        /// 统一按「字符串字面量」输出：SQL 字符串字面量属于 unknown 类型，各数据库都会按列的实际类型
        /// 做隐式转换（varchar 按文本比较、int/numeric/date 等按对应类型转换），
        /// 因此既能匹配 "00003585" 这类纯数字文本，也不会像数值字面量那样在 PostgreSQL 触发
        /// "operator does not exist: character varying = integer"。
        /// </summary>
        private static string FormatFilterValue(string? value, string dbType)
        {
            if (string.IsNullOrEmpty(value))
                return "NULL";
            if (value.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                return "NULL";

            var type = (dbType ?? string.Empty).ToLowerInvariant();

            // 布尔字面量：SQL Server / Oracle 不支持 TRUE / FALSE 关键字
            if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
                return type == "oracle" || type == "sqlserver" ? "1" : "TRUE";
            if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
                return type == "oracle" || type == "sqlserver" ? "0" : "FALSE";

            return $"'{value.Replace("'", "''")}'";
        }

        private string BuildOrderByClause(string dbType, string orderBy, string orderDirection, List<string> primaryKeys)
        {
            if (string.IsNullOrWhiteSpace(orderBy))
            {
                if (primaryKeys.Count > 0)
                    return $" ORDER BY {string.Join(", ", primaryKeys.Select(k => GetQuotedColumnName(dbType, k)))}";
                return string.Empty;
            }

            var quotedColumn = GetQuotedColumnName(dbType, orderBy);
            var direction = string.Equals(orderDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
            return $" ORDER BY {quotedColumn} {direction}";
        }

        private async Task<List<string>> GetPrimaryKeyColumnsAsync(DbConnection conn, string dbType, string tableName, string databaseName, CancellationToken cancellationToken)
        {
            var primaryKeys = new List<string>();
            
            var command = conn.CreateCommand();
            command.CommandText = GetPrimaryKeysQuery(dbType, tableName, databaseName);
            
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                primaryKeys.Add(reader.GetString(0));
            }
            
            return primaryKeys;
        }

        /// <summary>
        /// PostgreSQL 的 schema 过滤条件。
        /// 未指定 schema 时限定在当前 search_path（current_schemas(false)）内，
        /// 避免跨 schema 的同名表被错配列结构。
        /// </summary>
        private static string PgSchemaFilter(string? tableSchema, string alias = "c")
        {
            if (string.IsNullOrWhiteSpace(tableSchema))
            {
                return $"AND {alias}.table_schema = ANY(current_schemas(false))";
            }

            return $"AND {alias}.table_schema = '{tableSchema.Replace("'", "''")}'";
        }

        /// <summary>
        /// PostgreSQL 的 regclass 限定串（用于 col_description 等接受 regclass 的函数）。
        /// </summary>
        private static string PgRegClass(string tableName, string? tableSchema)
        {
            if (string.IsNullOrWhiteSpace(tableSchema))
            {
                return tableName;
            }

            return $"{tableSchema}.{tableName}";
        }

        private string GetQuotedColumnName(string dbType, string columnName)
        {
            // 转义内嵌的定界符，避免列名本身含引号时拼出非法 SQL
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

        /// <summary>
        /// 生成带 schema 限定的表名。PostgreSQL 多 schema 场景必须限定 schema：
        /// 否则建表/读写会落到默认 search_path，既丢失表归属，也可能读写到别的 schema 的同名表。
        /// 其余数据库的 schema 由连接/库决定，忽略即可。
        /// </summary>
        private string GetQualifiedTableName(string dbType, string tableName, string? schema)
        {
            if (dbType.ToLower() == "postgresql" && !string.IsNullOrWhiteSpace(schema))
            {
                var s = schema.Replace("\"", "\"\"");
                var t = tableName.Replace("\"", "\"\"");
                return $"\"{s}\".\"{t}\"";
            }

            return GetQuotedTableName(dbType, tableName);
        }

        private string GetPrimaryKeysQuery(string dbType, string tableName, string databaseName)
        {
            return dbType.ToLower() switch
            {
                "postgresql" => $@"
                    SELECT ku.column_name
                    FROM information_schema.table_constraints tc
                    JOIN information_schema.key_column_usage ku
                        ON tc.constraint_name = ku.constraint_name
                    WHERE tc.constraint_type = 'PRIMARY KEY'
                    AND tc.table_name = '{tableName}'
                    ORDER BY ku.ordinal_position",
                "mysql" => $@"
                    SELECT COLUMN_NAME
                    FROM information_schema.KEY_COLUMN_USAGE
                    WHERE TABLE_NAME = '{tableName}'
                    AND TABLE_SCHEMA = '{databaseName}'
                    AND CONSTRAINT_NAME = 'PRIMARY'
                    ORDER BY ORDINAL_POSITION",
                "sqlite" => $@"
                    SELECT name
                    FROM pragma_table_info('{tableName}')
                    WHERE pk = 1
                    ORDER BY cid",
                "sqlserver" => $@"
                    SELECT ku.COLUMN_NAME
                    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                        ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                    WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                    AND tc.TABLE_NAME = '{tableName}'
                    ORDER BY ku.ORDINAL_POSITION",
                "oracle" => $@"
                    SELECT cols.column_name
                    FROM all_constraints cons
                    JOIN all_cons_columns cols ON cons.constraint_name = cols.constraint_name
                    WHERE cons.constraint_type = 'P'
                    AND cons.table_name = UPPER('{tableName}')
                    AND cols.position = 1",
                _ => throw new NotSupportedException($"Database type {dbType} is not supported")
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

        public async Task<object> ExecuteSqlAsync(Connection connection, string sql, CancellationToken cancellationToken = default)
        {
            using var conn = CreateConnection(connection);
            await conn.OpenAsync(cancellationToken);
            
            var command = conn.CreateCommand();
            command.CommandText = sql;
            
            var startTime = DateTime.Now;
            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
            var executionTime = DateTime.Now - startTime;
            
            return new
            {
                Success = true,
                Message = $"SQL executed successfully in {executionTime.TotalMilliseconds:F2}ms",
                RowsAffected = rowsAffected
            };
        }

        /// <summary>
        /// 采集数据库对象（序列/函数/视图/索引/约束/触发器）的 DDL。
        /// 备份时调用，把这些对象一并写入备份文件，保证恢复后的库"能跑且正确"
        /// （尤其是 PostgreSQL 的向量索引 HNSW/IVFFlat，丢失会导致向量检索退化为全表扫描）。
        /// </summary>
        public async Task<BackupSchemaObjects> GetSchemaObjectsAsync(Connection connection, CancellationToken cancellationToken = default, BackupSession? session = null)
        {
            var owned = session == null;
            var conn = session?.Db ?? CreateConnection(connection);
            if (owned)
            {
                await conn.OpenAsync(cancellationToken);
            }

            try
            {
                return await SchemaObjectCollector.CollectAsync(conn, connection.DbType, cancellationToken);
            }
            finally
            {
                if (owned)
                {
                    await conn.DisposeAsync();
                }
            }
        }

        /// <summary>
        /// 在单一数据库连接上顺序执行多条 SQL 语句（同步枚举版本）。
        /// 用于备份恢复：保证 SET FOREIGN_KEY_CHECKS / SET IDENTITY_INSERT 等连接级设置对整批生效，
        /// 并容忍 DROP TABLE（目标表不存在）失败而继续后续语句。
        /// </summary>
        public Task<(int Executed, int Skipped)> ExecuteBatchAsync(Connection connection, IEnumerable<string> statements, CancellationToken cancellationToken = default)
        {
            return ExecuteBatchAsync(connection, ToAsyncEnumerable(statements), cancellationToken);
        }

        /// <summary>
        /// 在单一数据库连接上顺序执行多条 SQL 语句（流式版本）。
        ///
        /// 生产级要点：
        /// - 事务化：PostgreSQL / SQLite / SQL Server 下整批放在同一个事务里，任一条失败即整体回滚，
        ///   绝不留下"表已重建、数据只插了一半"的半恢复状态。
        ///   MySQL（DDL 隐式提交）与 Oracle（DDL 自动提交）无法整体回滚，保持逐条提交的原有行为。
        /// - 保留 DROP 容错：目标表不存在时 DROP 失败可忽略。
        /// </summary>
        public async Task<(int Executed, int Skipped)> ExecuteBatchAsync(Connection connection, IAsyncEnumerable<string> statements, CancellationToken cancellationToken = default)
        {
            using var conn = CreateConnection(connection);
            await conn.OpenAsync(cancellationToken);

            var dbType = connection.DbType.ToLower();
            var supportsTransactionalDdl = dbType is "postgresql" or "sqlite" or "sqlserver";

            DbTransaction? tx = null;
            if (supportsTransactionalDdl)
            {
                try
                {
                    tx = await conn.BeginTransactionAsync(cancellationToken);
                }
                catch
                {
                    // 某些驱动/场景不支持事务，降级为非事务执行
                    tx = null;
                }
            }

            var executed = 0;
            var skipped = 0;

            try
            {
                await foreach (var raw in statements.WithCancellation(cancellationToken))
                {
                    var statement = raw.Trim();
                    if (statement.Length == 0)
                    {
                        continue;
                    }

                    try
                    {
                        var command = conn.CreateCommand();
                        command.Transaction = tx;
                        command.CommandText = statement;
                        await command.ExecuteNonQueryAsync(cancellationToken);
                        executed++;
                    }
                    catch (Exception) when (IsDropStatement(statement))
                    {
                        // DROP 失败可忽略：目标库中对象不存在、或该库不支持 IF EXISTS 语法
                        skipped++;
                    }
                }

                if (tx != null)
                {
                    await tx.CommitAsync(cancellationToken);
                }
            }
            catch
            {
                if (tx != null)
                {
                    try
                    {
                        await tx.RollbackAsync(CancellationToken.None);
                    }
                    catch
                    {
                        // 回滚失败时保留原始异常
                    }
                }
                throw;
            }
            finally
            {
                if (tx != null)
                {
                    await tx.DisposeAsync();
                }
            }

            return (executed, skipped);
        }

        private static async IAsyncEnumerable<string> ToAsyncEnumerable(IEnumerable<string> items)
        {
            foreach (var item in items)
            {
                yield return item;
            }
        }

        /// <summary>
        /// 统计指定表的行数，用于恢复后与备份清单比对，验证"没有丢数据"。
        /// 表不存在或查询失败时返回 null（不视为不一致，由调用方决定如何提示）。
        /// </summary>
        public async Task<long?> GetTableRowCountAsync(Connection connection, string tableName, string? tableSchema = null, CancellationToken cancellationToken = default)
        {
            using var conn = CreateConnection(connection);
            await conn.OpenAsync(cancellationToken);

            var command = conn.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {GetQualifiedTableName(connection.DbType, tableName, tableSchema)}";

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result == null || result == DBNull.Value ? null : Convert.ToInt64(result);
        }

        private static bool IsDropTableStatement(string statement)
        {
            return statement.Length >= 10 && statement.Substring(0, 10).ToUpperInvariant().StartsWith("DROP TABLE");
        }

        /// <summary>
        /// 是否是 DROP 语句。恢复时 DROP 失败（对象不存在、语法不被目标库支持）应可忽略，
        /// 否则一条无用的 DROP 会让整批恢复中断。
        /// </summary>
        private static bool IsDropStatement(string statement)
        {
            return statement.Length >= 5 && statement.Substring(0, 5).ToUpperInvariant().StartsWith("DROP ");
        }

        private DbConnection CreateConnection(Connection connection)
        {
            return connection.DbType.ToLower() switch
            {
                "postgresql" => new Npgsql.NpgsqlConnection(GetConnectionString(connection)),
                "mysql" => new MySqlConnector.MySqlConnection(GetConnectionString(connection)),
                "sqlite" => new Microsoft.Data.Sqlite.SqliteConnection(GetConnectionString(connection)),
                "sqlserver" => new Microsoft.Data.SqlClient.SqlConnection(GetConnectionString(connection)),
                "oracle" => new Oracle.ManagedDataAccess.Client.OracleConnection(GetConnectionString(connection)),
                _ => throw new NotSupportedException($"Database type {connection.DbType} is not supported")
            };
        }

        /// <summary>
        /// 供备份等场景复用连接字符串与驱动映射创建连接（调用方负责 Open/Dispose）。
        /// </summary>
        public DbConnection CreateReadOnlyConnection(Connection connection) => CreateConnection(connection);

        private string GetConnectionString(Connection connection)
        {
            return connection.DbType.ToLower() switch
            {
                "postgresql" => $"Host={connection.Host};Port={connection.Port};Database={connection.DatabaseName};Username={connection.Username};Password={connection.Password}",
                "mysql" => $"Server={connection.Host};Port={connection.Port};Database={connection.DatabaseName};User={connection.Username};Password={connection.Password}",
                "sqlite" => $"Data Source={connection.DatabaseName}",
                "sqlserver" => $"Server={connection.Host},{connection.Port};Database={connection.DatabaseName};User Id={connection.Username};Password={connection.Password}",
                "oracle" => $"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={connection.Host})(PORT={connection.Port}))(CONNECT_DATA=(SERVICE_NAME={connection.DatabaseName})));User Id={connection.Username};Password={connection.Password}",
                _ => throw new NotSupportedException($"Database type {connection.DbType} is not supported")
            };
        }

        private string GetTablesQuery(string dbType, string databaseName)
        {
            return dbType.ToLower() switch
            {
                "postgresql" => @"
                    SELECT 
                        t.table_name,
                        t.table_schema,
                        obj_description((t.table_schema || '.' || t.table_name)::regclass) as table_comment
                    FROM information_schema.tables t
                    WHERE t.table_type = 'BASE TABLE'
                    AND t.table_schema NOT IN ('pg_catalog', 'information_schema')
                    ORDER BY t.table_name",
                "mysql" => $@"
                    SELECT 
                        TABLE_NAME,
                        TABLE_SCHEMA,
                        TABLE_COMMENT
                    FROM information_schema.TABLES
                    WHERE TABLE_TYPE = 'BASE TABLE'
                    AND TABLE_SCHEMA = '{databaseName}'
                    ORDER BY TABLE_NAME",
                "sqlite" => @"
                    SELECT 
                        name,
                        'main',
                        ''
                    FROM sqlite_master
                    WHERE type = 'table'
                    AND name NOT LIKE 'sqlite_%'
                    ORDER BY name",
                "sqlserver" => @"
                    SELECT 
                        t.TABLE_NAME,
                        t.TABLE_SCHEMA,
                        ep.value as table_comment
                    FROM INFORMATION_SCHEMA.TABLES t
                    LEFT JOIN sys.extended_properties ep
                        ON ep.major_id = OBJECT_ID(t.TABLE_SCHEMA + '.' + t.TABLE_NAME)
                        AND ep.minor_id = 0
                    WHERE t.TABLE_TYPE = 'BASE TABLE'
                    ORDER BY t.TABLE_NAME",
                "oracle" => @"
                    SELECT 
                        t.table_name,
                        t.owner as table_schema,
                        '' as table_comment
                    FROM all_tables t
                    WHERE t.owner = USER
                    ORDER BY t.table_name",
                _ => throw new NotSupportedException($"Database type {dbType} is not supported")
            };
        }

        private string GetColumnsQuery(string dbType, string tableName, string databaseName, string? tableSchema = null)
        {
            return dbType.ToLower() switch
            {
                "postgresql" => $@"
                    SELECT 
                        c.column_name,
                        CASE WHEN c.data_type IN ('USER-DEFINED', 'ARRAY') THEN c.udt_name ELSE c.data_type END as data_type,
                        CASE WHEN c.is_nullable = 'YES' THEN true ELSE false END as is_nullable,
                        CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END as is_primary_key,
                        CASE WHEN c.column_default LIKE 'nextval%' THEN true ELSE false END as is_auto_increment,
                        c.column_default,
                        col_description('{PgRegClass(tableName, tableSchema)}'::regclass, c.ordinal_position) as column_comment
                    FROM information_schema.columns c
                    LEFT JOIN (
                        SELECT ku.column_name
                        FROM information_schema.table_constraints tc
                        JOIN information_schema.key_column_usage ku
                            ON tc.constraint_name = ku.constraint_name
                        WHERE tc.constraint_type = 'PRIMARY KEY'
                        AND tc.table_name = '{tableName}'
                        {PgSchemaFilter(tableSchema, "tc")}
                    ) pk ON c.column_name = pk.column_name
                    WHERE c.table_name = '{tableName}'
                    {PgSchemaFilter(tableSchema, "c")}
                    ORDER BY c.ordinal_position",
                "mysql" => $@"
                    SELECT 
                        COLUMN_NAME,
                        DATA_TYPE,
                        CASE WHEN IS_NULLABLE = 'YES' THEN 1 ELSE 0 END as is_nullable,
                        CASE WHEN COLUMN_KEY = 'PRI' THEN 1 ELSE 0 END as is_primary_key,
                        CASE WHEN EXTRA = 'auto_increment' THEN 1 ELSE 0 END as is_auto_increment,
                        COLUMN_DEFAULT,
                        COLUMN_COMMENT
                    FROM information_schema.COLUMNS
                    WHERE TABLE_NAME = '{tableName}'
                    AND TABLE_SCHEMA = '{databaseName}'
                    ORDER BY ORDINAL_POSITION",
                "sqlite" => $@"
                    SELECT 
                        name,
                        type,
                        CASE WHEN ""notnull"" = 0 THEN 1 ELSE 0 END as is_nullable,
                        CASE WHEN pk = 1 THEN 1 ELSE 0 END as is_primary_key,
                        CASE WHEN type LIKE '%AUTOINCREMENT%' THEN 1 ELSE 0 END as is_auto_increment,
                        dflt_value,
                        '' as column_comment
                    FROM pragma_table_info('{tableName}')
                    ORDER BY cid",
                "sqlserver" => $@"
                    SELECT 
                        c.COLUMN_NAME,
                        c.DATA_TYPE,
                        CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END as is_nullable,
                        CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END as is_primary_key,
                        CASE WHEN COLUMNPROPERTY(OBJECT_ID('{tableName}'), c.COLUMN_NAME, 'IsIdentity') = 1 THEN 1 ELSE 0 END as is_auto_increment,
                        c.COLUMN_DEFAULT,
                        ep.value as column_comment
                    FROM INFORMATION_SCHEMA.COLUMNS c
                    LEFT JOIN (
                        SELECT ku.COLUMN_NAME
                        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                        JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                            ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                        WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                        AND tc.TABLE_NAME = '{tableName}'
                    ) pk ON c.COLUMN_NAME = pk.COLUMN_NAME
                    LEFT JOIN sys.extended_properties ep
                        ON ep.major_id = OBJECT_ID('{tableName}')
                        AND ep.minor_id = c.ORDINAL_POSITION
                    WHERE c.TABLE_NAME = '{tableName}'
                    ORDER BY c.ORDINAL_POSITION",
                "oracle" => $@"
                    SELECT 
                        cols.column_name,
                        cols.data_type,
                        CASE WHEN cols.nullable = 'Y' THEN 1 ELSE 0 END as is_nullable,
                        CASE WHEN cons.constraint_type = 'P' THEN 1 ELSE 0 END as is_primary_key,
                        CASE WHEN cols.data_default IS NOT NULL AND UPPER(cols.data_default) LIKE 'SEQ%' THEN 1 ELSE 0 END as is_auto_increment,
                        cols.data_default,
                        '' as column_comment
                    FROM all_tab_columns cols
                    LEFT JOIN (
                        SELECT cc.column_name, c.constraint_type
                        FROM all_constraints c
                        JOIN all_cons_columns cc ON c.constraint_name = cc.constraint_name
                        WHERE c.constraint_type = 'P'
                        AND c.table_name = UPPER('{tableName}')
                    ) cons ON cols.column_name = cons.column_name
                    WHERE cols.table_name = UPPER('{tableName}')
                    ORDER BY cols.column_id",
                _ => throw new NotSupportedException($"Database type {dbType} is not supported")
            };
        }

        /// <summary>
        /// 打开备份会话：单连接贯穿整个备份；PostgreSQL 下开启 REPEATABLE READ 事务，
        /// 使所有表的数据来自同一个快照，避免备份内容跨表自相矛盾。
        /// </summary>
        public async Task<BackupSession> OpenBackupSessionAsync(Connection connection, CancellationToken cancellationToken = default)
        {
            var db = CreateConnection(connection);
            await db.OpenAsync(cancellationToken);

            DbTransaction? tx = null;
            if (connection.DbType.ToLower() == "postgresql")
            {
                tx = await db.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
            }

            return new BackupSession(db, tx);
        }

        /// <summary>
        /// 取得 PG 某列 serial 默认值引用的序列名（如 app.items_id_seq）。
        ///
        /// 为什么不用 pg_get_serial_sequence：它依赖序列与列的 OWNED BY 依赖关系，
        /// 而恢复时序列是独立 CREATE SEQUENCE、表只写了 DEFAULT nextval(...)，
        /// 并没有建立 OWNED BY → 它返回 NULL → setval 被跳过 → 序列停在 1 → 后续插入主键冲突。
        /// </summary>
        public async Task<string?> GetPgSerialSequenceNameAsync(Connection connection, string tableName, string columnName, CancellationToken cancellationToken = default, string? tableSchema = null, BackupSession? session = null)
        {
            var owned = session == null;
            var conn = session?.Db ?? CreateConnection(connection);
            if (owned)
            {
                await conn.OpenAsync(cancellationToken);
            }

            DbCommand command = session?.CreateCommand() ?? conn.CreateCommand();
            command.CommandText = $@"
SELECT c.column_default
FROM information_schema.columns c
WHERE c.table_name = '{tableName.Replace("'", "''")}'
  AND c.column_name = '{columnName.Replace("'", "''")}'
  {PgSchemaFilter(tableSchema, "c")}
LIMIT 1";

            try
            {
                using (command)
                {
                    var value = await command.ExecuteScalarAsync(cancellationToken);
                    var def = value == null || value == DBNull.Value ? null : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
                    if (string.IsNullOrWhiteSpace(def) || !def.StartsWith("nextval(", StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    // nextval('app.items_id_seq'::regclass) -> app.items_id_seq
                    var first = def.IndexOf('\'');
                    var last = def.LastIndexOf('\'');
                    return first >= 0 && last > first ? def.Substring(first + 1, last - first - 1) : null;
                }
            }
            finally
            {
                if (owned)
                {
                    await conn.DisposeAsync();
                }
            }
        }

        public async Task<List<string>> GetTableColumnNamesAsync(Connection connection, string tableName, CancellationToken cancellationToken = default, string? tableSchema = null, BackupSession? session = null)
        {
            var columns = new List<string>();

            if (session != null)
            {
                var cmd2 = session.CreateCommand();
                cmd2.CommandText = GetColumnsNameQuery(connection.DbType, tableName, connection.DatabaseName, tableSchema);
                using (cmd2)
                using (var reader2 = await cmd2.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader2.ReadAsync(cancellationToken))
                    {
                        columns.Add(reader2.GetString(0));
                    }
                }

                return columns;
            }

            using var conn = CreateConnection(connection);
            await conn.OpenAsync(cancellationToken);

            var command = conn.CreateCommand();
            command.CommandText = GetColumnsNameQuery(connection.DbType, tableName, connection.DatabaseName, tableSchema);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(0));
            }

            return columns;
        }

        /// <summary>
        /// 流式读取整张表的数据，逐行 yield 返回，避免一次性把所有行加载进内存导致大表 OOM。
        /// 枚举期间会保持数据库连接与读取器打开，枚举结束（或提前 break / dispose）后自动释放。
        /// 传入 session 时复用该连接/事务（保证与其它表读到同一快照）。
        /// </summary>
        public async IAsyncEnumerable<object[]> StreamTableDataAsync(Connection connection, string tableName, [EnumeratorCancellation] CancellationToken cancellationToken = default, string? tableSchema = null, BackupSession? session = null)
        {
            var owned = session == null;
            var conn = session?.Db ?? CreateConnection(connection);
            if (owned)
            {
                await conn.OpenAsync(cancellationToken);
            }

            var quotedTableName = GetQualifiedTableName(connection.DbType, tableName, tableSchema);

            var columnNames = await GetTableColumnNamesAsync(connection, tableName, cancellationToken, tableSchema, session);
            var quotedColumns = columnNames.Select(c => GetQuotedColumnName(connection.DbType, c));
            var selectColumns = connection.DbType.ToLower() == "postgresql"
                ? string.Join(", ", quotedColumns.Select(c => $"CAST({c} AS TEXT)"))
                : "*";

            DbCommand command = session?.CreateCommand() ?? conn.CreateCommand();
            command.CommandText = $"SELECT {selectColumns} FROM {quotedTableName}";

            try
            {
                using (command)
                using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var row = new object[reader.FieldCount];
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[i] = ReadCellValue(reader, i);
                        }
                        yield return row;
                    }
                }
            }
            finally
            {
                if (owned)
                {
                    await conn.DisposeAsync();
                }
            }
        }

        /// <summary>
        /// 将读取器当前行第 i 列的值安全地取出为 object：优先 GetValue，
        /// 失败时回退到 GetFieldValue&lt;string&gt;，再失败时用 ToString()。
        /// 避免个别驱动（如 Microsoft.Data.Sqlite 对 integer 列调用 GetFieldValue&lt;string&gt; 抛错）导致整行失败。
        /// </summary>
        private static object ReadCellValue(DbDataReader reader, int i)
        {
            try
            {
                if (reader.IsDBNull(i))
                    return null!;

                var value = reader.GetValue(i);
                // pgvector 的 vector 类型读取为 Pgvector.Vector，转成文本便于展示/序列化；
                // 用类型全名判断避免直接依赖具体程序集。
                if (value != null)
                {
                    var tn = value.GetType().FullName;
                    if (tn != null && (tn.EndsWith("Vector") || tn.Contains("Pgvector")))
                        return value.ToString()!;
                }
                return value;
            }
            catch
            {
                try
                {
                    return reader.GetFieldValue<string>(i);
                }
                catch
                {
                    try
                    {
                        return reader[i]?.ToString();
                    }
                    catch
                    {
                        return "<无法读取该列>";
                    }
                }
            }
        }

        private string GetColumnsNameQuery(string dbType, string tableName, string databaseName, string? tableSchema = null)
        {
            return dbType.ToLower() switch
            {
                "postgresql" => $@"
            SELECT c.column_name
            FROM information_schema.columns c
            WHERE c.table_name = '{tableName}'
            {PgSchemaFilter(tableSchema, "c")}
            ORDER BY c.ordinal_position",
                "mysql" => $@"
            SELECT COLUMN_NAME 
            FROM information_schema.COLUMNS 
            WHERE TABLE_NAME = '{tableName}'
            AND TABLE_SCHEMA = '{databaseName}'
            ORDER BY ORDINAL_POSITION",
                "sqlite" => $@"
            SELECT name 
            FROM pragma_table_info('{tableName}')
            ORDER BY cid",
                "sqlserver" => $@"
            SELECT COLUMN_NAME 
            FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_NAME = '{tableName}'
            ORDER BY ORDINAL_POSITION",
                "oracle" => $@"
            SELECT column_name 
            FROM all_tab_columns 
            WHERE table_name = UPPER('{tableName}')
            ORDER BY column_id",
                _ => throw new NotSupportedException($"Database type {dbType} is not supported")
            };
        }

        // ============ DDL 操作（建表 / 增删改字段 / 改名） ============

        public async Task<int> ExecuteDdlAsync(Connection connection, string sql, CancellationToken cancellationToken = default)
        {
            using var conn = CreateConnection(connection);
            await conn.OpenAsync(cancellationToken);

            var command = conn.CreateCommand();
            command.CommandText = sql;

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task CreateTableAsync(Connection connection, string tableName, List<ColumnDefinition> columns, CancellationToken cancellationToken = default)
        {
            if (columns == null || columns.Count == 0)
            {
                throw new ArgumentException("至少需要一个字段");
            }

            var qt = GetQuotedTableName(connection.DbType, tableName);
            var defs = columns.Select(col => BuildColumnDefinition(connection.DbType, col));
            var sql = $"CREATE TABLE {qt} (\n  {string.Join(",\n  ", defs)}\n)";

            await ExecuteDdlAsync(connection, sql, cancellationToken);
        }

        // ============ 备份用：生成建表语句（保留完整类型/主键/自增/默认值） ============

        public async Task<(string Sql, List<string> AutoIncrementColumns)> GenerateCreateTableSqlAsync(Connection connection, string tableName, CancellationToken cancellationToken = default, string? tableSchema = null, BackupSession? session = null)
        {
            var dbType = connection.DbType.ToLower();
            var quotedTableName = GetQualifiedTableName(connection.DbType, tableName, tableSchema);

            var owned = session == null;
            var conn = session?.Db ?? CreateConnection(connection);
            if (owned)
            {
                await conn.OpenAsync(cancellationToken);
            }

            try
            {
                // SQLite：直接使用 sqlite_master 里保存的原始建表语句。
                // 用 pragma 重建会丢掉 UNIQUE / CHECK / NOT NULL / DEFAULT / AUTOINCREMENT 等约束，
                // 导致恢复后的库"结构看着对、约束全没了"。
                if (dbType == "sqlite")
                {
                    var rawSql = await GetSqliteOriginalCreateTableSqlAsync(conn, session, tableName, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(rawSql))
                    {
                        // SQLite 的自增列无需 setval 复位（INTEGER PRIMARY KEY 即 rowid 别名），
                        // 因此不返回自增列信息。
                        return (rawSql.Trim() + ";", new List<string>());
                    }
                }

                var command = session?.CreateCommand() ?? conn.CreateCommand();
                command.CommandText = GetFullColumnsQuery(dbType, tableName, connection.DatabaseName, tableSchema);

                var columnDefs = new List<string>();
                var pkColumns = new List<string>();
                var autoColumns = new List<string>();

                using (command)
                using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var colName = reader.GetString(0);
                        var colType = reader.GetString(1);
                        var isNullable = ReadBool(reader, 2);
                        var isPrimaryKey = ReadBool(reader, 3);
                        var isAutoIncrement = ReadBool(reader, 4);
                        var defaultValue = reader.IsDBNull(5) ? null : reader.GetString(5);

                        var qc = GetQuotedColumnName(connection.DbType, colName);
                        var parts = new List<string> { $"{qc} {colType}" };
                        if (!isNullable)
                        {
                            parts.Add("NOT NULL");
                        }

                        // 自动增长关键字仅 MySQL/SQLServer 需要；PG 靠 DEFAULT nextval、SQLite 靠 INTEGER PRIMARY KEY、Oracle 靠序列/默认值实现
                        var autoKeyword = dbType switch
                        {
                            "mysql" => isAutoIncrement ? "AUTO_INCREMENT" : null,
                            "sqlserver" => isAutoIncrement ? "IDENTITY(1,1)" : null,
                            _ => null
                        };
                        if (autoKeyword != null)
                        {
                            parts.Add(autoKeyword);
                        }

                        // PG 的 identity 列：类型文本里已带上 GENERATED BY DEFAULT AS IDENTITY，
                        // 也必须计入 autoColumns —— 否则恢复后内部序列不前进，新插入会主键冲突。
                        var isPgIdentity = colType.Contains("IDENTITY", StringComparison.OrdinalIgnoreCase);

                        if (isAutoIncrement || isPgIdentity)
                        {
                            autoColumns.Add(colName);
                        }

                        if (!string.IsNullOrWhiteSpace(defaultValue))
                        {
                            parts.Add($"DEFAULT {FormatDefaultValue(defaultValue, dbType)}");
                        }
                        columnDefs.Add(string.Join(" ", parts));
                        if (isPrimaryKey)
                        {
                            pkColumns.Add(qc);
                        }
                    }
                }

                if (columnDefs.Count == 0)
                {
                    throw new InvalidOperationException($"无法获取表 {tableName} 的列信息，可能该表不存在或无权访问");
                }

                if (pkColumns.Count > 0)
                {
                    columnDefs.Add($"PRIMARY KEY ({string.Join(", ", pkColumns)})");
                }

                var sql = $"CREATE TABLE {quotedTableName} (\n  {string.Join(",\n  ", columnDefs)}\n);";
                return (sql, autoColumns);
            }
            finally
            {
                if (owned)
                {
                    await conn.DisposeAsync();
                }
            }
        }

        /// <summary>
        /// 读取 SQLite 原始建表语句（sqlite_master.sql），保留 UNIQUE/CHECK/NOT NULL/DEFAULT 等约束。
        /// 找不到时返回 null，由调用方回退到 pragma 重建。
        /// </summary>
        private async Task<string?> GetSqliteOriginalCreateTableSqlAsync(DbConnection conn, BackupSession? session, string tableName, CancellationToken cancellationToken)
        {
            try
            {
                var command = session?.CreateCommand() ?? conn.CreateCommand();
                command.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = @t";
                var p = command.CreateParameter();
                p.ParameterName = "@t";
                p.Value = tableName;
                command.Parameters.Add(p);

                var result = await command.ExecuteScalarAsync(cancellationToken);
                return result == null || result == DBNull.Value ? null : result.ToString();
            }
            catch
            {
                // 读取失败时回退到 pragma 重建
                return null;
            }
        }

        private string GetFullColumnsQuery(string dbType, string tableName, string databaseName, string? tableSchema = null)
        {            return dbType switch
            {
                "mysql" => $@"
                    SELECT COLUMN_NAME, COLUMN_TYPE,
                           CASE WHEN IS_NULLABLE = 'YES' THEN 1 ELSE 0 END,
                           CASE WHEN COLUMN_KEY = 'PRI' THEN 1 ELSE 0 END,
                           CASE WHEN EXTRA LIKE '%auto_increment%' THEN 1 ELSE 0 END,
                           COLUMN_DEFAULT
                    FROM information_schema.COLUMNS
                    WHERE TABLE_NAME = '{tableName}'
                    AND TABLE_SCHEMA = '{databaseName}'
                    ORDER BY ORDINAL_POSITION",
                "sqlite" => $@"
                    SELECT name, type,
                           CASE WHEN ""notnull"" = 0 THEN 1 ELSE 0 END,
                           CASE WHEN pk = 1 THEN 1 ELSE 0 END,
                           CASE WHEN type LIKE '%AUTOINCREMENT%' THEN 1 ELSE 0 END,
                           dflt_value
                    FROM pragma_table_info('{tableName}')
                    ORDER BY cid",
                "postgresql" => $@"
                    SELECT c.column_name,
                           CASE WHEN c.is_identity = 'YES'
                                THEN ft.full_type || ' GENERATED BY DEFAULT AS IDENTITY'
                                ELSE ft.full_type
                           END,
                           CASE WHEN c.is_nullable = 'YES' THEN 1 ELSE 0 END,
                           CASE WHEN pk.column_name IS NOT NULL THEN 1 ELSE 0 END,
                           CASE WHEN c.column_default LIKE 'nextval%' THEN 1 ELSE 0 END,
                           c.column_default
                    FROM information_schema.columns c
                    -- format_type 能保留类型修饰符：vector(3)、numeric(10,2)、text[]、
                    -- 以及 ENUM/域类型的 schema 限定名，information_schema 的 data_type 全都做不到。
                    LEFT JOIN LATERAL (
                        SELECT COALESCE(
                            (SELECT format_type(a.atttypid, a.atttypmod)
                             FROM pg_attribute a
                             WHERE a.attrelid = format('%I.%I', c.table_schema, c.table_name)::regclass
                               AND a.attname = c.column_name
                               AND a.attnum > 0 AND NOT a.attisdropped),
                            c.udt_name) AS full_type
                    ) ft ON true
                    LEFT JOIN (
                        SELECT ku.column_name
                        FROM information_schema.table_constraints tc
                        JOIN information_schema.key_column_usage ku
                            ON tc.constraint_name = ku.constraint_name
                        WHERE tc.constraint_type = 'PRIMARY KEY'
                        AND tc.table_name = '{tableName}'
                        {PgSchemaFilter(tableSchema, "tc")}
                    ) pk ON c.column_name = pk.column_name
                    WHERE c.table_name = '{tableName}'
                    {PgSchemaFilter(tableSchema, "c")}
                    ORDER BY c.ordinal_position",
                "sqlserver" => $@"
                    SELECT c.COLUMN_NAME,
                           c.DATA_TYPE + CASE WHEN c.CHARACTER_MAXIMUM_LENGTH = -1 THEN '(max)'
                                              ELSE ISNULL('(' + CAST(c.CHARACTER_MAXIMUM_LENGTH AS VARCHAR) + ')', '') END,
                           CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END,
                           CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END,
                           CASE WHEN COLUMNPROPERTY(OBJECT_ID('{tableName}'), c.COLUMN_NAME, 'IsIdentity') = 1 THEN 1 ELSE 0 END,
                           c.COLUMN_DEFAULT
                    FROM INFORMATION_SCHEMA.COLUMNS c
                    LEFT JOIN (
                        SELECT ku.COLUMN_NAME
                        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                        JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                            ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                        WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                        AND tc.TABLE_NAME = '{tableName}'
                    ) pk ON c.COLUMN_NAME = pk.COLUMN_NAME
                    WHERE c.TABLE_NAME = '{tableName}'
                    ORDER BY c.ORDINAL_POSITION",
                "oracle" => $@"
                    SELECT cols.column_name,
                           cols.data_type || NVL2(cols.data_length, '(' || cols.data_length || ')', ''),
                           CASE WHEN cols.nullable = 'Y' THEN 1 ELSE 0 END,
                           CASE WHEN cons.constraint_type = 'P' THEN 1 ELSE 0 END,
                           CASE WHEN cols.data_default IS NOT NULL AND UPPER(cols.data_default) LIKE '%NEXTVAL%' THEN 1 ELSE 0 END,
                           cols.data_default
                    FROM all_tab_columns cols
                    LEFT JOIN (
                        SELECT cc.column_name, c.constraint_type
                        FROM all_constraints c
                        JOIN all_cons_columns cc ON c.constraint_name = cc.constraint_name
                        WHERE c.constraint_type = 'P'
                        AND c.table_name = UPPER('{tableName}')
                    ) cons ON cols.column_name = cons.column_name
                    WHERE cols.table_name = UPPER('{tableName}')
                    ORDER BY cols.column_id",
                _ => throw new NotSupportedException($"Database type {dbType} is not supported")
            };
        }

        private static readonly HashSet<string> SqlFunctionKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CURRENT_TIMESTAMP", "CURRENT_DATE", "CURRENT_TIME",
            "LOCALTIME", "LOCALTIMESTAMP",
            "SYSDATE", "SYSTIMESTAMP", "UTC_TIMESTAMP", "UTC_DATE", "UTC_TIME",
            "UUID", "GETDATE", "GETUTCDATE", "SYS_GUID",
            "CURDATE", "CURTIME", "NOW", "USER", "SYSTEM_USER", "SESSION_USER"
        };

        private static string FormatDefaultValue(string defaultValue, string dbType)
        {
            var s = defaultValue.Trim();
            if (string.Equals(s, "NULL", StringComparison.OrdinalIgnoreCase))
            {
                return "NULL";
            }
            if (double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                return s;
            }
            // 函数/表达式（含括号、类型转换 ::、或已是带引号的字面量）不额外加引号
            if (s.Contains("(") || s.Contains("::") || s.Contains("'"))
            {
                return s;
            }

            var upper = s.ToUpperInvariant();
            // 无括号的 SQL 函数/关键字（如 CURRENT_TIMESTAMP、SYSDATE）不可加引号，否则会被当成字符串字面量导致无效默认值（#1067）
            if (SqlFunctionKeywords.Contains(upper) || SqlFunctionKeywords.Any(k => upper.StartsWith(k + " ")))
            {
                return s;
            }
            // Oracle/PostgreSQL 的序列默认值（如 seq.nextval / schema.seq.nextval）
            if (upper.Contains("NEXTVAL"))
            {
                return s;
            }

            return $"'{s.Replace("'", "''")}'";
        }

        public async Task AddColumnAsync(Connection connection, string tableName, ColumnDefinition column, CancellationToken cancellationToken = default)
        {
            var qt = GetQuotedTableName(connection.DbType, tableName);
            var qc = GetQuotedColumnName(connection.DbType, column.Name);
            var type = MapColumnType(connection.DbType, column.Type);
            var nullClause = column.Nullable ? "NULL" : "NOT NULL";

            var sql = $"ALTER TABLE {qt} ADD COLUMN {qc} {type} {nullClause}";
            await ExecuteDdlAsync(connection, sql, cancellationToken);
        }

        public async Task DropColumnAsync(Connection connection, string tableName, string columnName, CancellationToken cancellationToken = default)
        {
            var qt = GetQuotedTableName(connection.DbType, tableName);
            var qc = GetQuotedColumnName(connection.DbType, columnName);

            var sql = $"ALTER TABLE {qt} DROP COLUMN {qc}";
            await ExecuteDdlAsync(connection, sql, cancellationToken);
        }

        public async Task AlterColumnAsync(Connection connection, string tableName, string columnName, ColumnDefinition column, CancellationToken cancellationToken = default)
        {
            var qt = GetQuotedTableName(connection.DbType, tableName);
            var qc = GetQuotedColumnName(connection.DbType, columnName);
            var type = MapColumnType(connection.DbType, column.Type);
            var nullClause = column.Nullable ? "NULL" : "NOT NULL";

            var sql = connection.DbType.ToLower() switch
            {
                "mysql" => $"ALTER TABLE {qt} MODIFY COLUMN {qc} {type} {nullClause}",
                "postgresql" => $"ALTER TABLE {qt} ALTER COLUMN {qc} TYPE {type}",
                "sqlserver" => $"ALTER TABLE {qt} ALTER COLUMN {qc} {type} {nullClause}",
                "oracle" => $"ALTER TABLE {qt} MODIFY ({qc} {type} {nullClause})",
                "sqlite" => throw new NotSupportedException("SQLite 不直接支持修改列类型，请通过重建表实现"),
                _ => throw new NotSupportedException($"Database type {connection.DbType} is not supported")
            };

            await ExecuteDdlAsync(connection, sql, cancellationToken);
        }

        public async Task RenameTableAsync(Connection connection, string oldName, string newName, CancellationToken cancellationToken = default)
        {
            var qOld = GetQuotedTableName(connection.DbType, oldName);
            var qNew = GetQuotedTableName(connection.DbType, newName);

            var sql = $"ALTER TABLE {qOld} RENAME TO {qNew}";
            await ExecuteDdlAsync(connection, sql, cancellationToken);
        }

        private string BuildColumnDefinition(string dbType, ColumnDefinition col)
        {
            var qc = GetQuotedColumnName(dbType, col.Name);
            var type = MapColumnType(dbType, col.Type);

            var parts = new List<string> { $"{qc} {type}" };
            if (!col.Nullable)
            {
                parts.Add("NOT NULL");
            }
            if (col.AutoIncrement)
            {
                parts.Add(dbType.ToLower() == "mysql" ? "AUTO_INCREMENT" : "AUTOINCREMENT");
            }
            if (col.IsPrimaryKey)
            {
                parts.Add("PRIMARY KEY");
            }
            if (!string.IsNullOrWhiteSpace(col.DefaultValue))
            {
                parts.Add($"DEFAULT {col.DefaultValue}");
            }

            return string.Join(" ", parts);
        }

        private string MapColumnType(string dbType, string type)
        {
            var t = (type ?? "varchar").ToLower();

            return dbType.ToLower() switch
            {
                "sqlite" => t switch
                {
                    "integer" => "INTEGER",
                    "varchar" => "TEXT",
                    "text" => "TEXT",
                    "boolean" => "INTEGER",
                    "timestamp" => "TEXT",
                    _ => t.ToUpper()
                },
                "mysql" => t switch
                {
                    "integer" => "INT",
                    "varchar" => "VARCHAR(255)",
                    "text" => "TEXT",
                    "boolean" => "TINYINT(1)",
                    "timestamp" => "TIMESTAMP",
                    _ => t.ToUpper()
                },
                "postgresql" => t switch
                {
                    "integer" => "INTEGER",
                    "varchar" => "VARCHAR(255)",
                    "text" => "TEXT",
                    "boolean" => "BOOLEAN",
                    "timestamp" => "TIMESTAMP",
                    _ => t.ToUpper()
                },
                "sqlserver" => t switch
                {
                    "integer" => "INT",
                    "varchar" => "NVARCHAR(255)",
                    "text" => "NVARCHAR(MAX)",
                    "boolean" => "BIT",
                    "timestamp" => "DATETIME",
                    _ => t.ToUpper()
                },
                "oracle" => t switch
                {
                    "integer" => "NUMBER(10)",
                    "varchar" => "VARCHAR2(255)",
                    "text" => "CLOB",
                    "boolean" => "NUMBER(1)",
                    "timestamp" => "TIMESTAMP",
                    _ => t.ToUpper()
                },
                _ => t.ToUpper()
            };
        }
    }
}
