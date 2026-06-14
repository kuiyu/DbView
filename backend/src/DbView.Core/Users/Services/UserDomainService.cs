using FastEndpoints;

namespace DbView.Core.Users
{
    [RegisterService<IUserDomainService>(LifeTime.Scoped)]
    public class UserDomainService:IUserDomainService
    {
        IUserRepository _userRepository;
        public UserDomainService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }
    }
}


