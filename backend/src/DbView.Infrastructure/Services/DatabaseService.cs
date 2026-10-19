using DbView.Core.Models;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text;

namespace DbView.Infrastructure.Services
{
    public class DatabaseService
    {
        public async Task<IReadOnlyList<TableInfo>> GetTablesAsync(Connection connection, CancellationToken cancellationToken = default)
        {
            var tables = new List<TableInfo>();
            
            using var conn = CreateConnection(connection);
            await conn.OpenAsync(cancellationToken);
            
            var command = conn.CreateCommand();
            command.CommandText = GetTablesQuery(connection.DbType, connection.DatabaseName);
            
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                tables.Add(new TableInfo
                {
                    TableName = reader.GetString(0),
                    SchemaName = reader.IsDBNull(1) ? "public" : reader.GetString(1),
                    Comment = reader.IsDBNull(2) ? string.Empty : reader.GetString(2)
                });
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

        public async Task<IReadOnlyList<ColumnInfo>> GetTableColumnsAsync(Connection connection, string tableName, CancellationToken cancellationToken = default)
        {
            var columns = new List<ColumnInfo>();
            
            using var conn = CreateConnection(connection);
            await conn.OpenAsync(cancellationToken);
            
            var command = conn.CreateCommand();
            command.CommandText = GetColumnsQuery(connection.DbType, tableName, connection.DatabaseName);
            
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
                "postgresql" => $"\"{tableName}\"",
                "mysql" => $"`{tableName}`",
                "sqlite" => $"\"{tableName}\"",
                "sqlserver" => $"[{tableName}]",
                "oracle" => $"\"{tableName}\"",
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
        /// 在单一数据库连接上顺序执行多条 SQL 语句。
        /// 用于备份恢复：保证 SET FOREIGN_KEY_CHECKS / SET IDENTITY_INSERT 等连接级设置对整批生效，
        /// 并容忍 DROP TABLE（目标表不存在）失败而继续后续语句。
        /// </summary>
        public async Task<(int Executed, int Skipped)> ExecuteBatchAsync(Connection connection, IEnumerable<string> statements, CancellationToken cancellationToken = default)
        {
            using var conn = CreateConnection(connection);
            await conn.OpenAsync(cancellationToken);

            var executed = 0;
            var skipped = 0;
            foreach (var raw in statements)
            {
                var statement = raw.Trim();
                if (string.IsNullOrWhiteSpace(statement))
                {
                    continue;
                }

                try
                {
                    var command = conn.CreateCommand();
                    command.CommandText = statement;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                    executed++;
                }
                catch (Exception) when (IsDropTableStatement(statement))
                {
                    // 表不存在时 DROP 失败可忽略（如 Oracle 不支持 IF EXISTS）
                    skipped++;
                }
            }

            return (executed, skipped);
        }

        private static bool IsDropTableStatement(string statement)
        {
            return statement.Length >= 10 && statement.Substring(0, 10).ToUpperInvariant().StartsWith("DROP TABLE");
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

        private string GetColumnsQuery(string dbType, string tableName, string databaseName)
        {
            return dbType.ToLower() switch
            {
                "postgresql" => $@"
                    SELECT 
                        c.column_name,
                        c.data_type,
                        CASE WHEN c.is_nullable = 'YES' THEN true ELSE false END as is_nullable,
                        CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END as is_primary_key,
                        CASE WHEN c.column_default LIKE 'nextval%' THEN true ELSE false END as is_auto_increment,
                        c.column_default,
                        col_description('{tableName}'::regclass, c.ordinal_position) as column_comment
                    FROM information_schema.columns c
                    LEFT JOIN (
                        SELECT ku.column_name
                        FROM information_schema.table_constraints tc
                        JOIN information_schema.key_column_usage ku
                            ON tc.constraint_name = ku.constraint_name
                        WHERE tc.constraint_type = 'PRIMARY KEY'
                        AND tc.table_name = '{tableName}'
                    ) pk ON c.column_name = pk.column_name
                    WHERE c.table_name = '{tableName}'
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

        public async Task<List<string>> GetTableColumnNamesAsync(Connection connection, string tableName, CancellationToken cancellationToken = default)
        {
            var columns = new List<string>();

            using var conn = CreateConnection(connection);
            await conn.OpenAsync(cancellationToken);

            var command = conn.CreateCommand();
            command.CommandText = GetColumnsNameQuery(connection.DbType, tableName, connection.DatabaseName);

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
        /// </summary>
        public async IAsyncEnumerable<object[]> StreamTableDataAsync(Connection connection, string tableName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var conn = CreateConnection(connection);
            await conn.OpenAsync(cancellationToken);

            var quotedTableName = GetQuotedTableName(connection.DbType, tableName);

            var columnNames = await GetTableColumnNamesAsync(connection, tableName, cancellationToken);
            var quotedColumns = columnNames.Select(c => GetQuotedColumnName(connection.DbType, c));
            var selectColumns = connection.DbType.ToLower() == "postgresql"
                ? string.Join(", ", quotedColumns.Select(c => $"CAST({c} AS TEXT)"))
                : "*";

            var command = conn.CreateCommand();
            command.CommandText = $"SELECT {selectColumns} FROM {quotedTableName}";

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
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

        /// <summary>
        /// 将读取器当前行第 i 列的值安全地取出为 object：优先 GetValue，
        /// 失败时回退到 GetFieldValue&lt;string&gt;，再失败时用 ToString()。
        /// 避免个别驱动（如 Microsoft.Data.Sqlite 对 integer 列调用 GetFieldValue&lt;string&gt; 抛错）导致整行失败。
        /// </summary>
        private static object ReadCellValue(DbDataReader reader, int i)
        {
            if (reader.IsDBNull(i))
                return null!;

            try
            {
                return reader.GetValue(i);
            }
            catch
            {
                try
                {
                    return reader.GetFieldValue<string>(i);
                }
                catch
                {
                    return reader[i]?.ToString();
                }
            }
        }

        private string GetColumnsNameQuery(string dbType, string tableName, string databaseName)
        {
            return dbType.ToLower() switch
            {
                "postgresql" => $@"
            SELECT column_name 
            FROM information_schema.columns 
            WHERE table_name = '{tableName}'
            ORDER BY ordinal_position",
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

        public async Task<(string Sql, List<string> AutoIncrementColumns)> GenerateCreateTableSqlAsync(Connection connection, string tableName, CancellationToken cancellationToken = default)
        {
            var dbType = connection.DbType.ToLower();
            var quotedTableName = GetQuotedTableName(connection.DbType, tableName);

            using var conn = CreateConnection(connection);
            await conn.OpenAsync(cancellationToken);

            var command = conn.CreateCommand();
            command.CommandText = GetFullColumnsQuery(dbType, tableName, connection.DatabaseName);

            var columnDefs = new List<string>();
            var pkColumns = new List<string>();
            var autoColumns = new List<string>();

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
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
                if (isAutoIncrement)
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

        private string GetFullColumnsQuery(string dbType, string tableName, string databaseName)
        {
            return dbType switch
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
                           c.data_type || COALESCE('(' || NULLIF(c.character_maximum_length, 0)::text || ')', ''),
                           CASE WHEN c.is_nullable = 'YES' THEN 1 ELSE 0 END,
                           CASE WHEN pk.column_name IS NOT NULL THEN 1 ELSE 0 END,
                           CASE WHEN c.column_default LIKE 'nextval%' THEN 1 ELSE 0 END,
                           c.column_default
                    FROM information_schema.columns c
                    LEFT JOIN (
                        SELECT ku.column_name
                        FROM information_schema.table_constraints tc
                        JOIN information_schema.key_column_usage ku
                            ON tc.constraint_name = ku.constraint_name
                        WHERE tc.constraint_type = 'PRIMARY KEY'
                        AND tc.table_name = '{tableName}'
                    ) pk ON c.column_name = pk.column_name
                    WHERE c.table_name = '{tableName}'
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
