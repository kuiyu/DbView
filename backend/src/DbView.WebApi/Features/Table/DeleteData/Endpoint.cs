using FastEndpoints;
using DbView.Infrastructure.Services;
using DbView.Infrastructure;
using DbView.Core.Models;
using DbView.Core;

namespace DbView.WebApi.Features.Table.DeleteData
{
    internal sealed class DeleteTableDataEndpoint : Endpoint<DeleteTableDataRequest, DeleteTableDataResponse>
    {
        private readonly DatabaseService _databaseService;
        private readonly    IConnectionRepository _connectionRepository;

        public DeleteTableDataEndpoint(DatabaseService databaseService, IConnectionRepository connectionRepository)
        {
            _databaseService = databaseService;
            _connectionRepository = connectionRepository;
        }

        public override void Configure()
        {
            Delete("/connections/{ConnectionId}/tables/{TableName}/data/{Id?}");
          
        }

        public override async Task HandleAsync(DeleteTableDataRequest r, CancellationToken c)
        {
            var connection = await _connectionRepository.GetByIdAsync(r.ConnectionId, c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(ApiResponse.Fail("Connection not found"), 404, null, c);
                return;
            }

            var columnNames = await _databaseService.GetTableColumnNamesAsync(connection, r.TableName, c);
            Console.WriteLine($"Available columns: {string.Join(", ", columnNames)}");

            // ²éÕÒÖ÷¼üÁÐ
            var columns = await _databaseService.GetTableColumnsAsync(connection, r.TableName, c);
            var pkColumn = columns.FirstOrDefault(col => col.IsPrimaryKey);
            if (pkColumn == null)
            {
                await HttpContext.Response.SendAsync(ApiResponse.Fail($"Table {r.TableName} does not have a primary key"), 400, null, c);
                return;
            }
            //var quotedTableName = GetQuotedTableName(connection.DbType, r.TableName);
            //var sql = GenerateDeleteSql(quotedTableName, r.Where, r.Id);
            var quotedPkColumn = GetQuotedColumnName(connection.DbType, pkColumn.ColumnName);
            var sql = $"DELETE FROM {GetQuotedTableName(connection.DbType, r.TableName)} WHERE {quotedPkColumn} = {r.Id.Value}";
            var result = await _databaseService.ExecuteSqlAsync(connection, sql, c);

            Response = new DeleteTableDataResponse
            {
                Success = true,
                Message = "Data deleted successfully"
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
        private string GenerateDeleteSql(string tableName, Dictionary<string, object> where, long? id)
        {
            if (id.HasValue)
            {
                return $"DELETE FROM {tableName} WHERE id = {id.Value}";
            }
            var whereClauses = where.Select(kv => $"{kv.Key} = {FormatValue(kv.Value)}");
            return $"DELETE FROM {tableName} WHERE {string.Join(" AND ", whereClauses)}";
        }

        private string FormatValue(object? value)
        {
            if (value == null)
            {
                return "NULL";
            }
            if (value is string str)
            {
                return $"'{str.Replace("'", "''")}'";
            }
            return value.ToString() ?? "NULL";
        }
    }
}
