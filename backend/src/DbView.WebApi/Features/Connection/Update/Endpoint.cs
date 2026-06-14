using FastEndpoints;
using DbView.Application.Services;

namespace DbView.WebApi.Features.Connection.Update
{
    internal sealed class UpdateConnectionEndpoint : Endpoint<UpdateConnectionRequest, UpdateConnectionResponse>
    {
        private readonly IConnectionAppService _connectionAppService;

        public UpdateConnectionEndpoint(IConnectionAppService connectionAppService)
        {
            _connectionAppService = connectionAppService;
        }

        public override void Configure()
        {
            Put("/connections/{Id}");
            AllowAnonymous();
            Validator<UpdateConnectionValidator>();
        }

        public override async Task HandleAsync(UpdateConnectionRequest r, CancellationToken c)
        {
            var existing = await _connectionAppService.GetConnectionByIdAsync(r.Id, c);

            if (existing == null)
            {
                await HttpContext.Response.SendAsync(ApiResponse.Fail("Connection not found"), 404, null, c);
                return;
            }

            var connection = new DbView.Core.Models.Connection
            {
                Id = r.Id,
                Name = r.Name,
                Host = r.Host,
                Port = r.Port,
                DatabaseName = r.DatabaseName,
                Username = r.Username,
                Password = r.Password,
                DbType = r.DbType,
                UserId = existing.UserId,
                CreatedAt = existing.CreatedAt
            };

            await _connectionAppService.UpdateConnectionAsync(connection, c);

            Response = new UpdateConnectionResponse
            {
                Message = "Connection updated successfully"
            };
        }
    }
}
