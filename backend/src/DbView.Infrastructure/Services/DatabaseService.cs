using DbView.Core.Models;
using System.Data.Common;

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
            command.CommandText = GetTablesQuery(connection.DbType);
            
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

        public async Task<IReadOnlyList<ColumnInfo>> GetTableColumnsAsync(Connection connection, string tableName, CancellationToken cancellationToken = default)
        {
            var columns = new List<ColumnInfo>();
            
            using var conn = CreateConnection(connection);
            await conn.OpenAsync(cancellationToken);
            
            var command = conn.CreateCommand();
            command.CommandText = GetColumnsQuery(connection.DbType, tableName);
            
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(new ColumnInfo
                {
                    ColumnName = reader.GetString(0),
                    DataType = reader.GetString(1),
                    IsNullable = reader.GetBoolean(2),
                    IsPrimaryKey = reader.GetBoolean(3),
                    IsAutoIncrement = reader.GetBoolean(4),
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
            
            var primaryKeys = await GetPrimaryKeyColumnsAsync(conn, connection.DbType, tableName, cancellationToken);
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
                    if (reader.IsDBNull(i))
                    {
                        row[i] = null;
                    }
                    else
                    {
                        try
                        {
                            row[i] = reader.GetValue(i);
                        }
                        catch
                        {
                            row[i] = reader.GetFieldValue<string>(i);
                        }
                    }
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

        private async Task<List<string>> GetPrimaryKeyColumnsAsync(DbConnection conn, string dbType, string tableName, CancellationToken cancellationToken)
        {
            var primaryKeys = new List<string>();
            
            var command = conn.CreateCommand();
            command.CommandText = GetPrimaryKeysQuery(dbType, tableName);
            
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
                _ => columnName
            };
        }

        private string GetPrimaryKeysQuery(string dbType, string tableName)
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

        private DbConnection CreateConnection(Connection connection)
        {
            return connection.DbType.ToLower() switch
            {
                "postgresql" => new Npgsql.NpgsqlConnection(GetConnectionString(connection)),
                "mysql" => new MySqlConnector.MySqlConnection(GetConnectionString(connection)),
                "sqlite" => new Microsoft.Data.Sqlite.SqliteConnection(GetConnectionString(connection)),
                "sqlserver" => new Microsoft.Data.SqlClient.SqlConnection(GetConnectionString(connection)),
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
                _ => throw new NotSupportedException($"Database type {connection.DbType} is not supported")
            };
        }

        private string GetTablesQuery(string dbType)
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
                "mysql" => @"
                    SELECT 
                        TABLE_NAME,
                        TABLE_SCHEMA,
                        TABLE_COMMENT
                    FROM information_schema.TABLES
                    WHERE TABLE_TYPE = 'BASE TABLE'
                    AND TABLE_SCHEMA NOT IN ('mysql', 'information_schema', 'performance_schema', 'sys')
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
                _ => throw new NotSupportedException($"Database type {dbType} is not supported")
            };
        }

        private string GetColumnsQuery(string dbType, string tableName)
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
                _ => throw new NotSupportedException($"Database type {dbType} is not supported")
            };
        }

        public async Task<List<string>> GetTableColumnNamesAsync(Connection connection, string tableName, CancellationToken cancellationToken = default)
        {
            var columns = new List<string>();

            using var conn = CreateConnection(connection);
            await conn.OpenAsync(cancellationToken);

            var command = conn.CreateCommand();
            command.CommandText = GetColumnsNameQuery(connection.DbType, tableName);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(0));
            }

            return columns;
        }

        private string GetColumnsNameQuery(string dbType, string tableName)
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
                _ => throw new NotSupportedException($"Database type {dbType} is not supported")
            };
        }
    }
}
