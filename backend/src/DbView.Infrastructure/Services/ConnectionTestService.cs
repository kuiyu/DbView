using DbView.Core;
using DbView.Core.Models;
using FastEndpoints;

namespace DbView.Infrastructure.Services
{
    [RegisterService<IConnectionTestService>(LifeTime.Scoped)]
    public class ConnectionTestService : IConnectionTestService
    {
        private readonly DatabaseService _databaseService;

        public ConnectionTestService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<bool> TestConnectionAsync(Connection connection, CancellationToken cancellationToken = default)
        {
            try
            {
                var tables = await _databaseService.GetTablesAsync(connection, cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<TableInfo>> GetTablesAsync(Connection connection, CancellationToken cancellationToken = default)
        {
            return (await _databaseService.GetTablesAsync(connection, cancellationToken)).ToList();
        }
    }
}