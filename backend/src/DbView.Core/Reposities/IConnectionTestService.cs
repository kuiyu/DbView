using DbView.Core.Models;

namespace DbView.Core
{
    public interface IConnectionTestService
    {
        Task<bool> TestConnectionAsync(Connection connection, CancellationToken cancellationToken = default);
        Task<List<TableInfo>> GetTablesAsync(Connection connection, CancellationToken cancellationToken = default);
    }
}