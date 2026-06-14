using DbView.Core.Abstractions;
using DbView.Core.Models;

namespace DbView.Core
{
    public interface IConnectionRepository : IRepository<Connection, long>
    {
        Task<IReadOnlyList<Connection>> GetByUserIdAsync(long userId, CancellationToken cancellationToken = default);
        Task<Connection?> GetByNameAsync(string name, long userId, CancellationToken cancellationToken = default);
    }
}