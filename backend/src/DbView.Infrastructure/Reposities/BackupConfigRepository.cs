using DbView.Core;
using DbView.Core.Models;
using DbView.Infrastructure.Entities;
using FastEndpoints;
using Mapster;

namespace DbView.Infrastructure
{
    [RegisterService<IBackupConfigRepository>(LifeTime.Scoped)]
    public class BackupConfigRepository : GenericRepository<BackupConfigEntity, BackupConfig, long>, IBackupConfigRepository
    {
        public BackupConfigRepository(IFreeSql freeSql) : base(freeSql)
        {
        }

        public async Task<BackupConfig?> GetByConnectionIdAsync(long connectionId, CancellationToken cancellationToken = default)
        {
            var entity = await sql.Select<BackupConfigEntity>()
                .Where(x => x.ConnectionId == connectionId)
                .ToOneAsync(cancellationToken);
            return entity?.Adapt<BackupConfig>();
        }
    }
}
