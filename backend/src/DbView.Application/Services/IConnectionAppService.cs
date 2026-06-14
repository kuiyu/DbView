using DbView.Core.Models;

namespace DbView.Application.Services
{
    public interface IConnectionAppService
    {
        Task<IReadOnlyList<Connection>> GetConnectionsByUserIdAsync(long userId, CancellationToken cancellationToken = default);
        Task<Connection?> GetConnectionByIdAsync(long id, CancellationToken cancellationToken = default);
        Task<Connection> CreateConnectionAsync(Connection connection, CancellationToken cancellationToken = default);
        Task<Connection> UpdateConnectionAsync(Connection connection, CancellationToken cancellationToken = default);
        Task DeleteConnectionAsync(long id, CancellationToken cancellationToken = default);
        Task<bool> TestConnectionAsync(Connection connection, CancellationToken cancellationToken = default);
    }
}
