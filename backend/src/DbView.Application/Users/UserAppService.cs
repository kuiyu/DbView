using FastEndpoints;
using DbView.Application.Users.DTOs;
using Microsoft.Extensions.Configuration;

namespace DbView.Application.Users
{
    [RegisterService<IUserAppService>(LifeTime.Scoped)]
    public class UserAppService : IUserAppService
    {
        private readonly IConfiguration _configuration;

        public UserAppService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task<LoginResultDto> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default)
        {
            var users = _configuration.GetSection("AppUsers").Get<List<AppUserDto>>();
            
            if (users == null)
            {
                return Task.FromResult(new LoginResultDto
                {
                    Success = false,
                    Message = "未配置用户信息"
                });
            }

            var user = users.FirstOrDefault(u => 
                u.Username.Equals(dto.Username, StringComparison.OrdinalIgnoreCase) && 
                u.Password == dto.Password);

            if (user == null)
            {
                return Task.FromResult(new LoginResultDto
                {
                    Success = false,
                    Message = "用户名或密码错误"
                });
            }

            return Task.FromResult(new LoginResultDto
            {
                Success = true,
                Message = "登录成功",
                Username = user.Username,
                Role = user.Role
            });
        }
    }

    public class AppUserDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "user";
    }
}