using FastEndpoints;
using DbView.Application.Services;

namespace DbView.WebApi.Features.Connection.Get
{
    internal sealed class GetConnectionEndpoint : Endpoint<GetConnectionRequest, GetConnectionResponse>
    {
        private readonly IConnectionAppService _connectionAppService;

        public GetConnectionEndpoint(IConnectionAppService connectionAppService)
        {
            _connectionAppService = connectionAppService;
        }

        public override void Configure()
        {
            Get("/connections/{Id}");
            AllowAnonymous();
        }

        public override async Task HandleAsync(GetConnectionRequest r, CancellationToken c)
        {
            var connection = await _connectionAppService.GetConnectionByIdAsync(r.Id, c);

            if (connection == null)
            {
                await HttpContext.Response.SendAsync(ApiResponse.Fail("Connection not found"), 404, null, c);
                return;
            }

            Response = new GetConnectionResponse
            {
                Id = connection.Id,
                Name = connection.Name,
                Host = connection.Host,
                Port = connection.Port,
                DatabaseName = connection.DatabaseName,
                Username = connection.Username,
                DbType = connection.DbType,
                CreatedAt = connection.CreatedAt,
                UpdatedAt = connection.UpdatedAt
            };
        }
    }
}
