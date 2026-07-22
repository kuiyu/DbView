using FastEndpoints;
using DbView.Infrastructure.Services;
using DbView.Infrastructure;
using DbView.Core.Models;
using DbView.Core;

namespace DbView.WebApi.Features.Table.InsertData
{
    internal sealed class InsertTableDataEndpoint : Endpoint<InsertTableDataRequest, InsertTableDataResponse>
    {
        private readonly DatabaseService _databaseService;
        private readonly IConnectionRepository _connectionRepository;

        public InsertTableDataEndpoint(DatabaseService databaseService, IConnectionRepository connectionRepository)
        {
            _databaseService = databaseService;
            _connectionRepository = connectionRepository;
        }

        public override void Configure()
        {
            Post("/connections/{ConnectionId}/tables/{TableName}/data");
       
        }

        public override async Task HandleAsync(InsertTableDataRequest r, CancellationToken c)
        {
            var connection = await _connectionRepository.GetByIdAsync(r.ConnectionId, c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(ApiResponse.Fail("Connection not found"), 404, null, c);
                return;
            }

            if (r.Data == null || r.Data.Count == 0)
            {
                await HttpContext.Response.SendAsync(ApiResponse.Fail("插入数据不能为空"), 400, null, c);
                return;
            }

            var sql = GenerateInsertSql(connection.DbType, r.TableName, r.Data);
            var result = await _databaseService.ExecuteSqlAsync(connection, sql, c);

            Response = new InsertTableDataResponse
            {
                Success = true,
                Message = "Data inserted successfully"
            };
        }

        private string GenerateInsertSql(string dbType, string tableName, Dictionary<string, object> data)
        {
            var quotedTableName = GetQuotedTableName(dbType, tableName);
            var columns = data.Keys.Select(col => GetQuotedColumnName(dbType, col));
            var values = data.Values.Select(FormatValue);
            return $"INSERT INTO {quotedTableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})";
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
