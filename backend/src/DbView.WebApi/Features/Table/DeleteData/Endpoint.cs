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
            Delete("/connections/{ConnectionId}/tables/{TableName}/data");
            AllowAnonymous();
        }

        public override async Task HandleAsync(DeleteTableDataRequest r, CancellationToken c)
        {
            var connection = await _connectionRepository.GetByIdAsync(r.ConnectionId, c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(ApiResponse.Fail("Connection not found"), 404, null, c);
                return;
            }

            var sql = GenerateDeleteSql(r.TableName, r.Where);
            var result = await _databaseService.ExecuteSqlAsync(connection, sql, c);

            Response = new DeleteTableDataResponse
            {
                Success = true,
                Message = "Data deleted successfully"
            };
        }

        private string GenerateDeleteSql(string tableName, Dictionary<string, object> where)
        {
            var whereClauses = where.Select(kv => $"{kv.Key} = '{kv.Value}'");
            return $"DELETE FROM {tableName} WHERE {string.Join(" AND ", whereClauses)}";
        }
    }
}
