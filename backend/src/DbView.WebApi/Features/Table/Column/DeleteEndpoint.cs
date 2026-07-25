using FastEndpoints;
using DbView.Core;
using DbView.Infrastructure.Services;

namespace DbView.WebApi.Features.Table.Column
{
    internal sealed class DeleteColumnEndpoint : EndpointWithoutRequest
    {
        private readonly IConnectionRepository _connectionRepository;
        private readonly DatabaseService _databaseService;

        public DeleteColumnEndpoint(IConnectionRepository connectionRepository, DatabaseService databaseService)
        {
            _connectionRepository = connectionRepository;
            _databaseService = databaseService;
        }

        public override void Configure()
        {
            Delete("/connections/{ConnectionId}/tables/{TableName}/columns/{ColumnName}");
        }

        public override async Task HandleAsync(CancellationToken c)
        {
            var connection = await _connectionRepository.GetByIdAsync(Route<long>("ConnectionId"), c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = "连接不存在" }, 404, cancellation: c);
                return;
            }

            try
            {
                await _databaseService.DropColumnAsync(connection, Route<string>("TableName"), Route<string>("ColumnName"), c);
                await HttpContext.Response.SendAsync(new { success = true, message = $"字段 {Route<string>("ColumnName")} 删除成功" }, cancellation: c);
            }
            catch (Exception ex)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = $"删除失败: {ex.Message}" }, 500, cancellation: c);
            }
        }
    }
}
