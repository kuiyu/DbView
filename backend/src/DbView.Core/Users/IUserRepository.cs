using System.Linq.Expressions;
using DbView.Core.Models;
using DbView.Core.Specifications;

using DbView.Core.Abstractions;

namespace DbView.Core
{
    public interface IUserRepository : IRepository<User,long>
    {
        Task<User> GetByIdAsync(string id);
    }
}



