using DbView.Core.Abstractions;
using DbView.Core.Models;

namespace DbView.Core
{
    public interface IBackupConfigRepository : IRepository<BackupConfig, long>
    {
        Task<BackupConfig?> GetByConnectionIdAsync(long connectionId, CancellationToken cancellationToken = default);
    }
}
