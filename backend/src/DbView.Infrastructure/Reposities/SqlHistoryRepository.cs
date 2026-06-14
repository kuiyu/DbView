using DbView.Core;
using DbView.Core.Abstractions;
using DbView.Core.Models;
using DbView.Infrastructure.Entities;
using FastEndpoints;
using Mapster;

namespace DbView.Infrastructure
{
    [RegisterService<ISqlHistoryRepository>(LifeTime.Scoped)]
    public class SqlHistoryRepository : GenericRepository<SqlHistoryEntity, SqlHistory, long>, ISqlHistoryRepository
    {
        public SqlHistoryRepository(IFreeSql freeSql) : base(freeSql)
        {
        }

        public async Task<IReadOnlyList<SqlHistory>> GetByConnectionIdAsync(long connectionId, int limit = 50, CancellationToken cancellationToken = default)
        {
            var entities = await sql.Select<SqlHistoryEntity>()
                .Where(x => x.ConnectionId == connectionId)
                .OrderByDescending(x => x.ExecutionTime)
                .Limit(limit)
                .ToListAsync(cancellationToken);
            return entities.Adapt<List<SqlHistory>>();
        }

        public async Task<IReadOnlyList<SqlHistory>> GetByUserIdAsync(long userId, int limit = 50, CancellationToken cancellationToken = default)
        {
            var entities = await sql.Select<SqlHistoryEntity>()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.ExecutionTime)
                .Limit(limit)
                .ToListAsync(cancellationToken);
            return entities.Adapt<List<SqlHistory>>();
        }
    }
}
