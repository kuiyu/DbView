using DbView.Core;
using DbView.Core.Abstractions;
using DbView.Core.Models;
using FastEndpoints;

namespace DbView.Application.Services
{
    [RegisterService<ISqlAppService>(LifeTime.Scoped)]
    public class SqlAppService : ISqlAppService
    {
        private readonly ISqlHistoryRepository _sqlHistoryRepository;

        public SqlAppService(ISqlHistoryRepository sqlHistoryRepository)
        {
            _sqlHistoryRepository = sqlHistoryRepository;
        }

        public async Task<object> ExecuteSqlAsync(long connectionId, string sql, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return new
            {
                Success = true,
                Message = "SQL executed successfully",
                RowsAffected = 0,
                Data = new List<object>()
            };
        }

        public async Task<IReadOnlyList<SqlHistory>> GetSqlHistoryAsync(long connectionId, CancellationToken cancellationToken = default)
        {
            return await _sqlHistoryRepository.GetByConnectionIdAsync(connectionId, cancellationToken: cancellationToken);
        }

        public async Task<IReadOnlyList<SqlHistory>> GetUserSqlHistoryAsync(long userId, CancellationToken cancellationToken = default)
        {
            return await _sqlHistoryRepository.GetByUserIdAsync(userId, cancellationToken: cancellationToken);
        }
    }
}
