using FastEndpoints;
using DbView.Infrastructure.Services;
using DbView.Infrastructure;
using DbView.Core.Models;
using DbView.Core;

namespace DbView.WebApi.Features.Table.UpdateData
{
    internal sealed class UpdateTableDataEndpoint : Endpoint<UpdateTableDataRequest, UpdateTableDataResponse>
    {
        private readonly DatabaseService _databaseService;
        private readonly IConnectionRepository _connectionRepository;

        public UpdateTableDataEndpoint(DatabaseService databaseService, IConnectionRepository connectionRepository)
        {
            _databaseService = databaseService;
            _connectionRepository = connectionRepository;
        }

        public override void Configure()
        {
            Put("/connections/{ConnectionId}/tables/{TableName}/data");
            AllowAnonymous();
        }

        public override async Task HandleAsync(UpdateTableDataRequest r, CancellationToken c)
        {
            var connection = await _connectionRepository.GetByIdAsync(r.ConnectionId, c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(ApiResponse.Fail("Connection not found"), 404, null, c);
                return;
            }

            var quotedTableName = GetQuotedTableName(connection.DbType, r.TableName);
            var sql = GenerateUpdateSql(quotedTableName, r.Data, r.Where);
            var result = await _databaseService.ExecuteSqlAsync(connection, sql, c);

            Response = new UpdateTableDataResponse
            {
                Success = true,
                Message = "Data updated successfully"
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

        private string GenerateUpdateSql(string tableName, Dictionary<string, object> data, Dictionary<string, object> where)
        {
            var setClauses = data.Select(kv => $"{kv.Key} = {FormatValue(kv.Value)}");
            var whereClauses = where.Select(kv => $"{kv.Key} = {FormatValue(kv.Value)}");
            return $"UPDATE {tableName} SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)}";
        }

        private string FormatValue(object? value)
        {
            if (value == null)
            {
                return "NULL";
            }
            string strValue = value.ToString() ?? "";
            if (strValue.Equals("NULL", StringComparison.OrdinalIgnoreCase))
            {
                return "NULL";
            }
            if (IsNumeric(strValue))
            {
                return strValue;
            }
            if (IsBoolean(strValue))
            {
                return strValue.ToLower() == "true" ? "TRUE" : "FALSE";
            }
            return $"'{strValue.Replace("'", "''")}'";
        }

        private bool IsNumeric(string value)
        {
            return int.TryParse(value, out _) ||
                   long.TryParse(value, out _) ||
                   double.TryParse(value, out _) ||
                   decimal.TryParse(value, out _);
        }

        private bool IsBoolean(string value)
        {
            return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("false", StringComparison.OrdinalIgnoreCase);
        }
    }
}
