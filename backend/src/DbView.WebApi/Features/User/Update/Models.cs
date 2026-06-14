
using FastEndpoints;
using FluentValidation;

namespace Users.Updates
{
    internal sealed class UpdateUserRequest
    {
        [QueryParam]
        public long Id { get; set; }
   

    }

    internal sealed class UpdateUserResponse
    {
        public string Message { get; set; } = "This endpoint hasn't been implemented yet!";
    }

    internal class UpdateUserValidator : Validator<UpdateUserRequest>
    {
        public UpdateUserValidator()
        {
            //RuleFor(x => x.Username)
            //    .NotEmpty().WithMessage("用户名不能为空")
            //    .MinimumLength(3).WithMessage("用户名至少3个字符")
            //    .MaximumLength(50).WithMessage("用户名最多50个字符")
            //   .Matches("^[a-zA-Z0-9_]+$").WithMessage("用户名只能包含字母、数字和下划线");

            //RuleFor(x => x.Email)
            //    .NotEmpty().WithMessage("邮箱不能为空")
            //    .EmailAddress().WithMessage("邮箱格式不正确");

            //RuleFor(x => x.Password)
            //    .NotEmpty().WithMessage("密码不能为空")
            //    .MinimumLength(8).WithMessage("密码至少8位")
            //    .Matches("[A-Z]").WithMessage("密码必须包含大写字母")
            //    .Matches("[a-z]").WithMessage("密码必须包含小写字母")
            //    .Matches("[0-9]").WithMessage("密码必须包含数字");

            //RuleFor(x => x.ConfirmPassword)
            //    .Equal(x => x.Password).WithMessage("两次密码不一致");
        }
    }
}


