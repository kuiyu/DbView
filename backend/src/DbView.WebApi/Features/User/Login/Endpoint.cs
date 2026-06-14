using FastEndpoints;
using DbView.Application;
using DbView.Application.Users.DTOs;

namespace DbView.WebApi.Features.User.Login
{
    internal sealed class LoginEndpoint : Endpoint<LoginRequest, LoginResponse>
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
                Response = new LoginResponse
                {
                    Success = false,
                    Message = result.Message
                };
                return;
            }

            // 生成JWT Token
            var token = GenerateToken(result.Username, result.Role);

            Response = new LoginResponse
            {
                Success = true,
                Message = result.Message,
                Token = token,
                Username = result.Username
            };
        }

        private string GenerateToken(string username, string role)
        {
            var issuer = _configuration["Jwt:Issuer"] ?? "";
            var audience = _configuration["Jwt:Audience"] ?? "";
            var securityKey = _configuration["Jwt:SecurityKey"] ?? "";
            var expireMinutes = int.Parse(_configuration["Jwt:Expire"] ?? "60");

            var signingCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(securityKey)),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, username),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role),
                new System.Security.Claims.Claim("username", username)
            };

            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expireMinutes),
                signingCredentials: signingCredentials);

            return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}