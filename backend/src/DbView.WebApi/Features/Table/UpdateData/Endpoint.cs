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

            var sql = GenerateUpdateSql(r.TableName, r.Data, r.Where);
            var result = await _databaseService.ExecuteSqlAsync(connection, sql, c);

            Response = new UpdateTableDataResponse
            {
                Success = true,
                Message = "Data updated successfully"
            };
        }

        private string GenerateUpdateSql(string tableName, Dictionary<string, object> data, Dictionary<string, object> where)
        {
            var setClauses = data.Select(kv => $"{kv.Key} = '{kv.Value}'");
            var whereClauses = where.Select(kv => $"{kv.Key} = '{kv.Value}'");
            return $"UPDATE {tableName} SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)}";
        }
    }
}
