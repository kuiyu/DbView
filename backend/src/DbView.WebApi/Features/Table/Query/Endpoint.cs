using FastEndpoints;
using DbView.Infrastructure.Services;
using DbView.Infrastructure;
using DbView.Core.Models;
using DbView.Core;

namespace DbView.WebApi.Features.Table.Query
{
    internal sealed class QueryTableDataEndpoint : Endpoint<QueryTableDataRequest, QueryTableDataResponse>
    {
        private readonly DatabaseService _databaseService;
        private readonly IConnectionRepository _connectionRepository;

        public QueryTableDataEndpoint(DatabaseService databaseService, IConnectionRepository connectionRepository)
        {
            _databaseService = databaseService;
            _connectionRepository = connectionRepository;
        }

        public override void Configure()
        {
            Post("/connections/{ConnectionId}/tables/{TableName}/query");
            AllowAnonymous();
        }

        public override async Task HandleAsync(QueryTableDataRequest r, CancellationToken c)
        {
            var connection = await _connectionRepository.GetByIdAsync(r.ConnectionId, c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = "Connection not found" }, 404, cancellation: c);
                return;
            }

            var result = await _databaseService.QueryTableDataAsync(
                connection,
                r.TableName,
                r.Filters,
                r.OrderBy,
                r.OrderDirection,
                r.Page,
                r.PageSize,
                c);

            var itemType = result.GetType();
            var itemsProp = itemType.GetProperty("Items");
            var totalProp = itemType.GetProperty("Total");

            var items = itemsProp?.GetValue(result) as List<object[]> ?? new List<object[]>();
            var total = totalProp != null ? (int)totalProp.GetValue(result)! : 0;

            var response = new
            {
                success = true,
                items = items,
                total = total,
                page = r.Page,
                pageSize = r.PageSize
            };

            HttpContext.Items["__manual_send"] = true;
            await HttpContext.Response.SendAsync(response, cancellation: c);
        }
    }
}
