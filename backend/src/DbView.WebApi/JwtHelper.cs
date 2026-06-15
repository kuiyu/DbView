
using FastEndpoints.Security;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public class JwtHelper
{
   public static string GenerateToken(List<Claim> claims, JwtSettings settings)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: DateTime.Now.AddMinutes(settings.Expire),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public IEnumerable<Claim> GetClaims(string accessToken)
    {
        try
        {
            JwtSecurityToken encodeToken = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
            var claims = encodeToken.Payload.Claims;
            return claims;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

public class JwtSettings
{
    public string SecretKey { get; set; }

    public string Issuer { get; set; }

    public string Audience { get; set; }
    /// <summary>
    /// 过期时间，单位为分钟
    /// </summary>
    public int Expire { get; set; }
}

