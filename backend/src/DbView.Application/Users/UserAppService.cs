using FastEndpoints;
using DbView.Application.Users.DTOs;
using Microsoft.Extensions.Configuration;
using DbView.Core;

namespace DbView.Application.Users
{
    [RegisterService<IUserAppService>(LifeTime.Scoped)]
    public class UserAppService : IUserAppService
    {
        private readonly IConfiguration _configuration;
        private readonly IUserRepository _userRepository;

        public UserAppService(IConfiguration configuration, IUserRepository userRepository)
        {
            _configuration = configuration;
            _userRepository = userRepository;
        }

        public async Task<LoginResultDto> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByUserNameAsync(dto.Username, cancellationToken);

            // 数据库中没有该用户时，兼容旧配置：配置中存在且密码匹配则自动创建数据库用户
            if (user == null)
            {
                var configUser = GetConfigUsers()?.FirstOrDefault(u =>
                    u.Username.Equals(dto.Username, StringComparison.OrdinalIgnoreCase) &&
                    u.Password == dto.Password);

                if (configUser != null)
                {
                    user = new User()
                    {
                        UserName = configUser.Username,
                        Password = configUser.Password,
                        Role = configUser.Role
                    };
                    await _userRepository.AddAsync(user, cancellationToken);
                }
            }

            if (user == null || user.Password != dto.Password)
            {
                return new LoginResultDto
                {
                    Success = false,
                    Message = "用户名或密码错误"
                };
            }

            return new LoginResultDto
            {
                Success = true,
                Message = "登录成功",
                Username = user.UserName,
                Role = user.Role
            };
        }

        private List<AppUserDto>? GetConfigUsers()
        {
            return _configuration.GetSection("AppUsers").Get<List<AppUserDto>>();
        }
    }

    public class AppUserDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "user";
    }
}
