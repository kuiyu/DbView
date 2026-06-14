using FastEndpoints;
using DbView.Application.Services;

namespace DbView.WebApi.Features.Connection.Delete
{
    internal sealed class DeleteConnectionEndpoint : Endpoint<DeleteConnectionRequest, DeleteConnectionResponse>
    {
        private readonly IConnectionAppService _connectionAppService;

        public DeleteConnectionEndpoint(IConnectionAppService connectionAppService)
        {
            _connectionAppService = connectionAppService;
        }

        public override void Configure()
        {
            Delete("/connections/{Id}");
            AllowAnonymous();
        }

        public override async Task HandleAsync(DeleteConnectionRequest r, CancellationToken c)
        {
            var existing = await _connectionAppService.GetConnectionByIdAsync(r.Id, c);

            if (existing == null)
            {
                await HttpContext.Response.SendAsync(ApiResponse.Fail("Connection not found"), 404, null, c);
                return;
            }

            await _connectionAppService.DeleteConnectionAsync(r.Id, c);

            Response = new DeleteConnectionResponse
            {
                Message = "Connection deleted successfully"
            };
        }
    }
}
