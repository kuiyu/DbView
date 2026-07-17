using FastEndpoints;
using Mapster;
using DbView.Core;
using DbView.Infrastructure.Entities;

namespace DbView.Infrastructure
{
    [RegisterService<IUserRepository>(LifeTime.Scoped)]
    public class UserRepository :  GenericRepository<Entities.UserEntity, Core.User, long>, IUserRepository
    {
        MapsterMapper.IMapper _mapper;
        public UserRepository(IFreeSql freeSql, MapsterMapper.IMapper mapper) : base(freeSql)
        {
            _mapper = mapper;
        }

        public async Task<User> GetByIdAsync(string id)
        {
            var entity = await sql.Select<UserEntity>().Where(w => w.UserId == id).ToOneAsync();
            return entity == null ? null : entity.Adapt<User>();
        }

        public async Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return null;
            }

            var entity = await sql.Select<UserEntity>().Where(w => w.UserName == userName).ToOneAsync(cancellationToken);
            return entity == null ? null : entity.Adapt<User>();
        }
    }
}
