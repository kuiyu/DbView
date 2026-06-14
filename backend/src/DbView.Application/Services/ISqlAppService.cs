using DbView.Core.Models;

namespace DbView.Application.Services
{
    public interface ISqlAppService
    {
        Task<object> ExecuteSqlAsync(long connectionId, string sql, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SqlHistory>> GetSqlHistoryAsync(long connectionId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SqlHistory>> GetUserSqlHistoryAsync(long userId, CancellationToken cancellationToken = default);
    }
}
