using FastEndpoints;
using DbView.Core;
using DbView.Infrastructure.Services;

namespace DbView.WebApi.Features.Table.CreateTable
{
    internal sealed class CreateTableEndpoint : Endpoint<CreateTableRequest>
    {
        private readonly IConnectionRepository _connectionRepository;
        private readonly DatabaseService _databaseService;

        public CreateTableEndpoint(IConnectionRepository connectionRepository, DatabaseService databaseService)
        {
            _connectionRepository = connectionRepository;
            _databaseService = databaseService;
        }

        public override void Configure()
        {
            Post("/connections/{ConnectionId}/tables");
        }

        public override async Task HandleAsync(CreateTableRequest r, CancellationToken c)
        {
            var connection = await _connectionRepository.GetByIdAsync(Route<long>("ConnectionId"), c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = "连接不存在" }, 404, cancellation: c);
                return;
            }

            try
            {
                await _databaseService.CreateTableAsync(connection, r.TableName, r.Columns, c);
                await HttpContext.Response.SendAsync(new { success = true, message = $"表 {r.TableName} 创建成功" }, cancellation: c);
            }
            catch (Exception ex)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = $"创建失败: {ex.Message}" }, 500, cancellation: c);
            }
        }
    }
}
