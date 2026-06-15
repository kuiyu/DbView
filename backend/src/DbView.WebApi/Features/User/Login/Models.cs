using DbView.Core;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Data;
using System.Security.Claims;

namespace DbView.WebApi.Features.User.Login
{
    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

      
    }
}