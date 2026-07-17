namespace DbView.WebApi.Features.User.ChangePassword
{
    public class ChangePasswordRequest
    {
        /// <summary>
        /// 旧密码（修改自己的密码时必填）
        /// </summary>
        public string OldPassword { get; set; } = string.Empty;

        /// <summary>
        /// 新密码
        /// </summary>
        public string NewPassword { get; set; } = string.Empty;

        /// <summary>
        /// 目标用户名；仅管理员可指定，用于修改其他用户的密码。留空表示修改当前登录用户。
        /// </summary>
        public string Username { get; set; } = string.Empty;
    }

    public class ChangePasswordResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
