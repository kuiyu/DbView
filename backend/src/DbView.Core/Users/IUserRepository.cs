using DbView.Core.Abstractions;

namespace DbView.Core
{
    public interface IUserRepository : IRepository<User,long>
    {
        Task<User> GetByIdAsync(string id);
        Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);
    }
}
