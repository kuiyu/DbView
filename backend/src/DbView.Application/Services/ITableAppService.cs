using DbView.Core.Models;

namespace DbView.Application.Services
{
    public interface ITableAppService
    {
        Task<IReadOnlyList<TableInfo>> GetTablesAsync(long connectionId, CancellationToken cancellationToken = default);
        Task<TableInfo?> GetTableStructureAsync(long connectionId, string tableName, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ColumnInfo>> GetTableColumnsAsync(long connectionId, string tableName, CancellationToken cancellationToken = default);
        Task<object> GetTableDataAsync(long connectionId, string tableName, int page, int pageSize, CancellationToken cancellationToken = default);
    }
}
