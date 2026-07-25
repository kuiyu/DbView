using FastEndpoints;
using DbView.Core;
using DbView.Infrastructure.Services;

namespace DbView.WebApi.Features.Table.RenameTable
{
    internal sealed class RenameTableEndpoint : Endpoint<RenameTableRequest>
    {
        private readonly IConnectionRepository _connectionRepository;
        private readonly DatabaseService _databaseService;

        public RenameTableEndpoint(IConnectionRepository connectionRepository, DatabaseService databaseService)
        {
            _connectionRepository = connectionRepository;
            _databaseService = databaseService;
        }

        public override void Configure()
        {
            Put("/connections/{ConnectionId}/tables/{TableName}");
        }

        public override async Task HandleAsync(RenameTableRequest r, CancellationToken c)
        {
            var connection = await _connectionRepository.GetByIdAsync(Route<long>("ConnectionId"), c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = "连接不存在" }, 404, cancellation: c);
                return;
            }

            try
            {
                await _databaseService.RenameTableAsync(connection, Route<string>("TableName"), r.NewName, c);
                await HttpContext.Response.SendAsync(new { success = true, message = $"表已重命名为 {r.NewName}" }, cancellation: c);
            }
            catch (Exception ex)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = $"重命名失败: {ex.Message}" }, 500, cancellation: c);
            }
        }
    }
}
