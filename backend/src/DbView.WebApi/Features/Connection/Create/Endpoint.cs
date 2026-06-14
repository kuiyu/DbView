using FastEndpoints;
using DbView.Application.Services;

namespace DbView.WebApi.Features.Connection.Create
{
    internal sealed class CreateConnectionEndpoint : Endpoint<CreateConnectionRequest, CreateConnectionResponse>
    {
        private readonly IConnectionAppService _connectionAppService;

        public CreateConnectionEndpoint(IConnectionAppService connectionAppService)
        {
            _connectionAppService = connectionAppService;
        }

        public override void Configure()
        {
            Post("/connections");

            Validator<CreateConnectionValidator>();
        }

        public override async Task HandleAsync(CreateConnectionRequest r, CancellationToken c)
        {
            var userId = User.Claims.FirstOrDefault(x=>x.Type== "UserId")?.Value;
            var connection = new DbView.Core.Models.Connection
            {
                Name = r.Name,
                Host = r.Host,
                Port = r.Port,
                DatabaseName = r.DatabaseName,
                Username = r.Username,
                Password = r.Password,
                DbType = r.DbType,
                UserId = 1
            };

            var created = await _connectionAppService.CreateConnectionAsync(connection, c);

            Response = new CreateConnectionResponse
            {
                Id = created.Id,
                Message = "Connection created successfully"
            };
        }
    }
}
