using FastEndpoints;
using DbView.Infrastructure.Services;
using DbView.Infrastructure;
using DbView.Core.Models;
using DbView.Core;

namespace DbView.WebApi.Features.Table.GetData
{
    internal sealed class GetTableDataEndpoint : Endpoint<GetTableDataRequest, GetTableDataResponse>
    {
        private readonly DatabaseService _databaseService;
        private readonly IConnectionRepository _connectionRepository;

        public GetTableDataEndpoint(DatabaseService databaseService, IConnectionRepository connectionRepository)
        {
            _databaseService = databaseService;
            _connectionRepository = connectionRepository;
        }

        public override void Configure()
        {
            Get("/connections/{ConnectionId}/tables/{TableName}/data");
          
        }

        public override async Task HandleAsync(GetTableDataRequest r, CancellationToken c)
        {
            var connection = await _connectionRepository.GetByIdAsync(r.ConnectionId, c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = "Connection not found" }, 404, cancellation: c);
                return;
            }

            var result = await _databaseService.GetTableDataAsync(
                connection,
                r.TableName,
                r.Page,
                r.PageSize,
                c);

            // 使用反射获取匿名类型的属性
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
