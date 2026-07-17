using FastEndpoints;
using DbView.Core;
using DbView.Core.Models;
using DbView.Infrastructure;

namespace DbView.WebApi.Features.User.ChangePassword
{
    internal sealed class ChangePasswordEndpoint : Endpoint<ChangePasswordRequest, ChangePasswordResponse>
    {
        private readonly IUserRepository _userRepository;

        public ChangePasswordEndpoint(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public override void Configure()
        {
            Post("/auth/change-password");
            // 需要登录（不调用 AllowAnonymous，依赖全局 JWT 鉴权）
            Summary(s =>
            {
                s.Summary = "修改密码";
                s.Description = "管理员可修改任意用户密码；普通用户只能修改自己的密码（需校验旧密码）。";
            });
        }

        public override async Task HandleAsync(ChangePasswordRequest r, CancellationToken c)
        {
            var currentUserName = HttpContext.User.FindFirst("UserName")?.Value;
            if (string.IsNullOrWhiteSpace(currentUserName))
            {
                await HttpContext.Response.SendAsync(ApiResponse.Fail("未获取到当前用户信息，请重新登录"), 401, null, c);
                return;
            }

            var isAdmin = HttpContext.User.IsInRole("admin");

            // 目标用户：管理员可指定其他用户，否则只能修改自己
            var targetUserName = (!string.IsNullOrWhiteSpace(r.Username) && isAdmin)
                ? r.Username
                : currentUserName;

            var user = await _userRepository.GetByUserNameAsync(targetUserName, c);
            if (user == null)
            {
                await HttpContext.Response.SendAsync(ApiResponse.Fail("用户不存在"), 404, null, c);
                return;
            }

            // 修改自己时必须校验旧密码
            if (string.Equals(targetUserName, currentUserName, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(r.OldPassword) || user.Password != r.OldPassword)
                {
                    await HttpContext.Response.SendAsync(ApiResponse.Fail("旧密码不正确"), 400, null, c);
                    return;
                }
            }
            else if (!isAdmin)
            {
                await HttpContext.Response.SendAsync(ApiResponse.Fail("无权限修改其他用户的密码"), 403, null, c);
                return;
            }

            if (string.IsNullOrWhiteSpace(r.NewPassword))
            {
                await HttpContext.Response.SendAsync(ApiResponse.Fail("新密码不能为空"), 400, null, c);
                return;
            }

            user.Password = r.NewPassword;
            await _userRepository.UpdateAsync(user, c);

            await HttpContext.Response.SendAsync(ApiResponse.Ok("密码修改成功"), 200, null, c);
        }
    }
}
