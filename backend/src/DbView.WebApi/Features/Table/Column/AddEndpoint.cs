using FastEndpoints;
using DbView.Core;
using DbView.Core.Models;
using DbView.Infrastructure.Services;

namespace DbView.WebApi.Features.Table.Column
{
    internal sealed class AddColumnEndpoint : Endpoint<ColumnRequest>
    {
        private readonly IConnectionRepository _connectionRepository;
        private readonly DatabaseService _databaseService;

        public AddColumnEndpoint(IConnectionRepository connectionRepository, DatabaseService databaseService)
        {
            _connectionRepository = connectionRepository;
            _databaseService = databaseService;
        }

        public override void Configure()
        {
            Post("/connections/{ConnectionId}/tables/{TableName}/columns");
        }

        public override async Task HandleAsync(ColumnRequest r, CancellationToken c)
        {
            var connection = await _connectionRepository.GetByIdAsync(Route<long>("ConnectionId"), c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = "连接不存在" }, 404, cancellation: c);
                return;
            }

            try
            {
                var column = new ColumnDefinition { Name = r.Name, Type = r.Type, Nullable = r.Nullable };
                await _databaseService.AddColumnAsync(connection, Route<string>("TableName"), column, c);
                await HttpContext.Response.SendAsync(new { success = true, message = $"字段 {r.Name} 添加成功" }, cancellation: c);
            }
            catch (Exception ex)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = $"添加失败: {ex.Message}" }, 500, cancellation: c);
            }
        }
    }
}
