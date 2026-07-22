using FastEndpoints;
using DbView.Application;
using DbView.Application.Users.DTOs;
using DbView.Core;
using DbView.Core.Exceptions;

namespace DbView.WebApi.Features.User.Login
{
    internal sealed class LoginEndpoint : Endpoint<LoginRequest, object>
    {
        private readonly IUserAppService _userAppService;
        private readonly IConfiguration _configuration;

        public LoginEndpoint(IUserAppService userAppService, IConfiguration configuration)
        {
            _userAppService = userAppService;
            _configuration = configuration;
        }

        public override void Configure()
        {
            Post("/auth/login");
            AllowAnonymous();
        }

        public override async Task HandleAsync(LoginRequest r, CancellationToken c)
        {
            var result = await _userAppService.LoginAsync(new LoginDto
            {
                Username = r.Username,
                Password = r.Password
            }, c);

            if (!result.Success)
            {
 
                throw new DomainException(result.Message);
                
            }

            var user=new DbView.Core.User();
            user.UserName= result.Username;
            user.Role = result.Role;
            // 生成JWT Token
            var token =JwtHelper.GenerateToken(user.GetClaims() ,new JwtSettings
            {
                SecretKey = _configuration["Jwt:SecurityKey"].ToString(),
                Audience = _configuration["Jwt:Audience"].ToString(),
                Issuer = _configuration["Jwt:Issuer"].ToString(),
                Expire = 60 * 24 * 365
            });

            Response = new { token = token };
        }

        
    }
}