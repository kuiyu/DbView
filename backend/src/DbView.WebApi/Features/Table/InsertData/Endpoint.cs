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
            AllowAnonymous();
        }

        public override async Task HandleAsync(InsertTableDataRequest r, CancellationToken c)
        {
            var connection = await _connectionRepository.GetByIdAsync(r.ConnectionId, c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(ApiResponse.Fail("Connection not found"), 404, null, c);
                return;
            }

            var sql = GenerateInsertSql(r.TableName, r.Data);
            var result = await _databaseService.ExecuteSqlAsync(connection, sql, c);

            Response = new InsertTableDataResponse
            {
                Success = true,
                Message = "Data inserted successfully"
            };
        }

        private string GenerateInsertSql(string tableName, Dictionary<string, object> data)
        {
            var columns = string.Join(", ", data.Keys);
            var values = string.Join(", ", data.Values.Select(v => $"'{v}'"));
            return $"INSERT INTO {tableName} ({columns}) VALUES ({values})";
        }
    }
}
