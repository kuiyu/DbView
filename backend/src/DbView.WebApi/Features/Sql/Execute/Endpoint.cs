using FastEndpoints;
using DbView.Infrastructure.Services;
using DbView.Infrastructure;
using DbView.Core;
using System.Data.Common;

namespace DbView.WebApi.Features.Sql.Execute
{
    internal sealed class ExecuteSqlEndpoint : Endpoint<ExecuteSqlRequest, ExecuteSqlResponse>
    {
        private readonly DatabaseService _databaseService;
        private readonly IConnectionRepository _connectionRepository;

        public ExecuteSqlEndpoint(DatabaseService databaseService, IConnectionRepository connectionRepository)
        {
            _databaseService = databaseService;
            _connectionRepository = connectionRepository;
        }

        public override void Configure()
        {
            Post("/connections/{ConnectionId}/sql/execute");
            AllowAnonymous();
        }

        public override async Task HandleAsync(ExecuteSqlRequest r, CancellationToken c)
        {
            var connection = await _connectionRepository.GetByIdAsync(r.ConnectionId, c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = "Connection not found" }, 404, cancellation: c);
                return;
            }

            try
            {
                using var conn = CreateConnection(connection);
                await conn.OpenAsync(c);

                var command = conn.CreateCommand();
                command.CommandText = r.Sql;

                var startTime = DateTime.Now;
                var isQuery = r.Sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
                              r.Sql.TrimStart().StartsWith("SHOW", StringComparison.OrdinalIgnoreCase) ||
                              r.Sql.TrimStart().StartsWith("DESCRIBE", StringComparison.OrdinalIgnoreCase) ||
                              r.Sql.TrimStart().StartsWith("EXPLAIN", StringComparison.OrdinalIgnoreCase);

                if (isQuery)
                {
                    var columns = new List<string>();
                    var items = new List<object[]>();
                    using var reader = await command.ExecuteReaderAsync(c);
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        columns.Add(reader.GetName(i));
                    }
                    while (await reader.ReadAsync(c))
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
                    var executionTime = DateTime.Now - startTime;

                    HttpContext.Items["__manual_send"] = true;
                    await HttpContext.Response.SendAsync(new
                    {
                        success = true,
                        message = $"Query executed in {executionTime.TotalMilliseconds:F2}ms, {items.Count} rows returned",
                        columns = columns,
                        data = items
                    }, cancellation: c);
                }
                else
                {
                    var rowsAffected = await command.ExecuteNonQueryAsync(c);
                    var executionTime = DateTime.Now - startTime;

                    HttpContext.Items["__manual_send"] = true;
                    await HttpContext.Response.SendAsync(new
                    {
                        success = true,
                        message = $"Statement executed in {executionTime.TotalMilliseconds:F2}ms, {rowsAffected} rows affected",
                        rowsAffected = rowsAffected
                    }, cancellation: c);
                }
            }
            catch (Exception ex)
            {
                HttpContext.Items["__manual_send"] = true;
                await HttpContext.Response.SendAsync(new
                {
                    success = false,
                    message = $"SQL execution failed: {ex.Message}"
                }, 400, cancellation: c);
            }
        }

        private DbConnection CreateConnection(Core.Models.Connection connection)
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

        private string GetConnectionString(Core.Models.Connection connection)
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
    }
}
