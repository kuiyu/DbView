using FastEndpoints;
using DbView.Core;
using DbView.Core.Models;

namespace DbView.WebApi.Features.Sql.History
{
    internal sealed class SaveSqlEndpoint : Endpoint<SaveSqlRequest, SaveSqlResponse>
    {
        private readonly IConnectionRepository _connectionRepository;
        private readonly ISqlHistoryRepository _sqlHistoryRepository;

        public SaveSqlEndpoint(IConnectionRepository connectionRepository, ISqlHistoryRepository sqlHistoryRepository)
        {
            _connectionRepository = connectionRepository;
            _sqlHistoryRepository = sqlHistoryRepository;
        }

        public override void Configure()
        {
            Post("/connections/{ConnectionId}/sql/save");
        }

        public override async Task HandleAsync(SaveSqlRequest r, CancellationToken c)
        {
            var connectionId = Route<long>("ConnectionId");

            var connection = await _connectionRepository.GetByIdAsync(connectionId, c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = "连接不存在" }, 404, cancellation: c);
                return;
            }

            var history = new SqlHistory
            {
                ConnectionId = connectionId,
                UserId = 1, // TODO: 接入真实登录用户后替换为当前用户 Id
                SqlText = r.Sql ?? string.Empty,
                ExecutionTime = DateTime.Now,
                Status = "saved"
            };

            await _sqlHistoryRepository.AddAsync(history, c);

            Response = new SaveSqlResponse
            {
                Success = true,
                Message = "保存成功",
                Id = history.Id
            };

            await Task.CompletedTask;
        }
    }

    public class SaveSqlRequest
    {
        public string? Sql { get; set; }
    }

    public class SaveSqlResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public long Id { get; set; }
    }
}
