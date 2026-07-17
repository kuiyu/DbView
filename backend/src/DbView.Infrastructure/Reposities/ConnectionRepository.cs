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

        /// <summary>
        /// SQLite 下 ExecuteAffrowsAsync 不回填自增 Id，
        /// 通过 ExecuteIdentityAsync 获取 newId 后重新查询完整实体返回。
        /// </summary>
        public override async Task<Connection> AddAsync(Connection domain, CancellationToken cancellationToken = default)
        {
            var entity = domain.Adapt<ConnectionEntity>();
            var newId = await sql.Insert<ConnectionEntity>(entity).ExecuteIdentityAsync(cancellationToken);
            var inserted = await sql.Select<ConnectionEntity>()
                .Where(x => x.Id == newId)
                .ToOneAsync(cancellationToken);

            return inserted!.Adapt<Connection>();
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
