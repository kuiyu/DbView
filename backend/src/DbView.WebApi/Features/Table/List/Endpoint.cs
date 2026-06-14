using FastEndpoints;
using DbView.Infrastructure.Services;
using DbView.Core;

namespace DbView.WebApi.Features.Table.List
{
    internal sealed class GetTableListEndpoint : Endpoint<GetTableListRequest, GetTableListResponse>
    {
        private readonly DatabaseService _databaseService;
        private readonly IConnectionRepository _connectionRepository;

        public GetTableListEndpoint(DatabaseService databaseService, IConnectionRepository connectionRepository)
        {
            _databaseService = databaseService;
            _connectionRepository = connectionRepository;
        }

        public override void Configure()
        {
            Get("/connections/{ConnectionId}/tables");
            AllowAnonymous();
        }

        public override async Task HandleAsync(GetTableListRequest r, CancellationToken c)
        {
            var connection = await _connectionRepository.GetByIdAsync(r.ConnectionId, c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(new { error = "Connection not found" }, 404, cancellation: c);
                return;
            }

            var tables = await _databaseService.GetTablesAsync(connection, c);

            var response = new GetTableListResponse
            {
                Items = tables.Select(t => new TableDto
                {
                    TableName = t.TableName,
                    SchemaName = t.SchemaName,
                    Comment = t.Comment
                }).ToList(),
                Total = tables.Count
            };

            HttpContext.Items["__manual_send"] = true;
            await HttpContext.Response.SendAsync(response, cancellation: c);
        }
    }
}