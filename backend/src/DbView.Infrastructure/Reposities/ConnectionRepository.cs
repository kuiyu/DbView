using DbView.Core;
using DbView.Core.Abstractions;
using DbView.Core.Models;
using DbView.Infrastructure.Entities;
using FastEndpoints;
using Mapster;

namespace DbView.Infrastructure
{
    [RegisterService<IConnectionRepository>(LifeTime.Scoped)]
    public class ConnectionRepository : GenericRepository<ConnectionEntity, Connection, long>, IConnectionRepository
    {
        public ConnectionRepository(IFreeSql freeSql) : base(freeSql)
        {
        }

        public async Task<IReadOnlyList<Connection>> GetByUserIdAsync(long userId, CancellationToken cancellationToken = default)
        {
            var entities = await sql.Select<ConnectionEntity>()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);
            return entities.Adapt<List<Connection>>();
        }

        public async Task<Connection?> GetByNameAsync(string name, long userId, CancellationToken cancellationToken = default)
        {
            var entity = await sql.Select<ConnectionEntity>()
                .Where(x => x.Name == name && x.UserId == userId)
                .ToOneAsync(cancellationToken);
            return entity?.Adapt<Connection>();
        }
    }
}
