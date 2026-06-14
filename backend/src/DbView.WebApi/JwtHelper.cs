
using FastEndpoints.Security;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

public class JwtHelper
{
   public static string GenerateToken(List<Claim> claims, JwtSettings settings)
    {
        var token=JwtBearer.CreateToken(x =>
        {
            x.SigningKey=settings.SecretKey;
            x.Issuer=settings.Issuer;
            x.Audience=settings.Audience;
           
            x.ExpireAt=DateTime.UtcNow.AddMinutes(settings.Expire);
            x.User.Claims.AddRange(claims);
        });
        return token;
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

