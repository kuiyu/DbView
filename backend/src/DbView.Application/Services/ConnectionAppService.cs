using DbView.Core;
using DbView.Core.Abstractions;
using DbView.Core.Models;
using FastEndpoints;

namespace DbView.Application.Services
{
    [RegisterService<IConnectionAppService>(LifeTime.Scoped)]
    public class ConnectionAppService : IConnectionAppService
    {
        private readonly IConnectionRepository _connectionRepository;
        private readonly IConnectionTestService _connectionTestService;

        public ConnectionAppService(IConnectionRepository connectionRepository, IConnectionTestService connectionTestService)
        {
            _connectionRepository = connectionRepository;
            _connectionTestService = connectionTestService;
        }

        public async Task<IReadOnlyList<Connection>> GetConnectionsByUserIdAsync(long userId, CancellationToken cancellationToken = default)
        {
            return await _connectionRepository.GetByUserIdAsync(userId, cancellationToken);
        }

        public async Task<Connection?> GetConnectionByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            return await _connectionRepository.GetByIdAsync(id, cancellationToken);
        }

        public async Task<Connection> CreateConnectionAsync(Connection connection, CancellationToken cancellationToken = default)
        {
            connection.CreatedAt = DateTime.Now;
            connection.UpdatedAt = DateTime.Now;
            await _connectionRepository.AddAsync(connection, cancellationToken);
            return connection;
        }

        public async Task<Connection> UpdateConnectionAsync(Connection connection, CancellationToken cancellationToken = default)
        {
            connection.UpdatedAt = DateTime.Now;
            await _connectionRepository.UpdateAsync(connection, cancellationToken);
            return connection;
        }

        public async Task DeleteConnectionAsync(long id, CancellationToken cancellationToken = default)
        {
            await _connectionRepository.DeleteByIdAsync(id);
        }

        public async Task<bool> TestConnectionAsync(Connection connection, CancellationToken cancellationToken = default)
        {
            return await _connectionTestService.TestConnectionAsync(connection, cancellationToken);
        }
    }
}
