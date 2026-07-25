using FastEndpoints;
using DbView.Core;
using DbView.Core.Models;

namespace DbView.WebApi.Features.Sql.History
{
    internal sealed class GetSqlHistoryEndpoint : EndpointWithoutRequest<GetSqlHistoryResponse>
    {
        private readonly IConnectionRepository _connectionRepository;
        private readonly ISqlHistoryRepository _sqlHistoryRepository;

        public GetSqlHistoryEndpoint(IConnectionRepository connectionRepository, ISqlHistoryRepository sqlHistoryRepository)
        {
            _connectionRepository = connectionRepository;
            _sqlHistoryRepository = sqlHistoryRepository;
        }

        public override void Configure()
        {
            Get("/connections/{ConnectionId}/sql/history");
        }

        public override async Task HandleAsync(CancellationToken c)
        {
            var connectionId = Route<long>("ConnectionId");

            var connection = await _connectionRepository.GetByIdAsync(connectionId, c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = "连接不存在" }, 404, cancellation: c);
                return;
            }

            var histories = await _sqlHistoryRepository.GetByConnectionIdAsync(connectionId, 100, c);

            Response = new GetSqlHistoryResponse
            {
                Items = histories.Select(h => new SqlHistoryDto
                {
                    Id = h.Id,
                    ConnectionId = h.ConnectionId,
                    SqlText = h.SqlText,
                    ExecutionTime = h.ExecutionTime,
                    Status = h.Status
                }).ToList(),
                Total = histories.Count
            };

            await Task.CompletedTask;
        }
    }

    public class GetSqlHistoryResponse
    {
        public List<SqlHistoryDto> Items { get; set; } = new();
        public int Total { get; set; }
    }

    public class SqlHistoryDto
    {
        public long Id { get; set; }
        public long ConnectionId { get; set; }
        public string SqlText { get; set; } = string.Empty;
        public DateTime ExecutionTime { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
