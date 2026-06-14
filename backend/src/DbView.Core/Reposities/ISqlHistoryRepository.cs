using DbView.Core.Abstractions;
using DbView.Core.Models;

namespace DbView.Core
{
    public interface ISqlHistoryRepository : IRepository<SqlHistory, long>
    {
        Task<IReadOnlyList<SqlHistory>> GetByConnectionIdAsync(long connectionId, int limit = 50, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SqlHistory>> GetByUserIdAsync(long userId, int limit = 50, CancellationToken cancellationToken = default);
    }
}