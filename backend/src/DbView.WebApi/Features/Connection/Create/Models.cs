using FastEndpoints;
using FluentValidation;

namespace DbView.WebApi.Features.Connection.Create
{
    public class CreateConnectionRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string DatabaseName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string DbType { get; set; } = string.Empty;
    }

    public class CreateConnectionResponse
    {
        public long Id { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class CreateConnectionValidator : Validator<CreateConnectionRequest>
    {
        public CreateConnectionValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("连接名称不能为空")
                .MaximumLength(100).WithMessage("连接名称最多100个字符");

            RuleFor(x => x.Host)
                .NotEmpty().WithMessage("主机地址不能为空");

            RuleFor(x => x.Port)
                .InclusiveBetween(1, 65535).WithMessage("端口必须在1-65535之间");

            RuleFor(x => x.DatabaseName)
                .NotEmpty().WithMessage("数据库名称不能为空");

            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("用户名不能为空");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("密码不能为空");

            RuleFor(x => x.DbType)
                .NotEmpty().WithMessage("数据库类型不能为空")
                .Must(x => new[] { "postgresql", "mysql", "sqlite", "sqlserver", "oracle" }.Contains(x.ToLower()))
                .WithMessage("不支持的数据库类型");
        }
    }
}
