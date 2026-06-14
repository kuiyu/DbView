using DbView.Core;
using DbView.Core.Abstractions;
using DbView.Core.Models;
using FastEndpoints;

namespace DbView.Application.Services
{
    [RegisterService<ITableAppService>(LifeTime.Scoped)]
    public class TableAppService : ITableAppService
    {
        private readonly IConnectionRepository _connectionRepository;

        public TableAppService(IConnectionRepository connectionRepository)
        {
            _connectionRepository = connectionRepository;
        }

        public async Task<IReadOnlyList<TableInfo>> GetTablesAsync(long connectionId, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return new List<TableInfo>();
        }

        public async Task<TableInfo?> GetTableStructureAsync(long connectionId, string tableName, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return null;
        }

        public async Task<IReadOnlyList<ColumnInfo>> GetTableColumnsAsync(long connectionId, string tableName, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return new List<ColumnInfo>();
        }

        public async Task<object> GetTableDataAsync(long connectionId, string tableName, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return new { Items = new List<object>(), Total = 0 };
        }
    }
}
