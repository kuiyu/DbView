using FastEndpoints;
using DbView.Infrastructure.Services;
using DbView.Core;

namespace DbView.WebApi.Features.Table.DropTable
{
    internal sealed class DropTableEndpoint : Endpoint<DropTableRequest, DropTableResponse>
    {
        private readonly DatabaseService _databaseService;
        private readonly IConnectionRepository _connectionRepository;

        public DropTableEndpoint(DatabaseService databaseService, IConnectionRepository connectionRepository)
        {
            _databaseService = databaseService;
            _connectionRepository = connectionRepository;
        }

        public override void Configure()
        {
            Delete("/connections/{ConnectionId}/tables/{TableName}");
        }

        public override async Task HandleAsync(DropTableRequest r, CancellationToken c)
        {
            var connection = await _connectionRepository.GetByIdAsync(r.ConnectionId, c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(ApiResponse.Fail("Connection not found"), 404, null, c);
                return;
            }

            var quotedTableName = GetQuotedTableName(connection.DbType, r.TableName);
            var sql = $"DROP TABLE {quotedTableName}";
            await _databaseService.ExecuteSqlAsync(connection, sql, c);

            Response = new DropTableResponse
            {
                Success = true,
                Message = "Table dropped successfully"
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
    }
}
