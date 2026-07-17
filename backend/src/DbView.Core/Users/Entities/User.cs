using DbView.Core.Abstractions;
using DbView.Core.Exceptions;
using System.Security.Claims;
using System.Xml.Linq;

namespace DbView.Core
{
    public partial class User : AggregateRoot<long>
    {
        #region 属性
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "admin";
        #endregion

        #region 方法
        // 业务方法 - 只操作自身状态 
        public User(long id) : base(id)
        {

        }

        public List<Claim> GetClaims()
        {
            List<Claim> claims = new List<Claim>();

            claims.Add(new Claim("UserId", Guid.NewGuid().ToString()));
            claims.Add(new Claim("UserName", UserName));
            var role = string.IsNullOrWhiteSpace(Role) ? "admin" : Role;
            claims.Add(new Claim("role", role));
            claims.Add(new Claim(ClaimTypes.Role, role));  // 使用标准 Role 类型

            return claims;
        }
        #endregion
    }
}
