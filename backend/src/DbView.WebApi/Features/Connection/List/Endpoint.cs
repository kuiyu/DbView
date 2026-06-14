using FastEndpoints;
using DbView.Application.Services;

namespace DbView.WebApi.Features.Connection.List
{
    internal sealed class GetConnectionListEndpoint : EndpointWithoutRequest<GetConnectionListResponse>
    {
        private readonly IConnectionAppService _connectionAppService;

        public GetConnectionListEndpoint(IConnectionAppService connectionAppService)
        {
            _connectionAppService = connectionAppService;
        }

        public override void Configure()
        {
            Get("/connections");
            AllowAnonymous();
        }

        public override async Task HandleAsync(CancellationToken c)
        {
            var userId = 1L;
            var connections = await _connectionAppService.GetConnectionsByUserIdAsync(userId, c);

            var response = new GetConnectionListResponse
            {
                Items = connections.Select(x => new ConnectionDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Host = x.Host,
                    Port = x.Port,
                    DatabaseName = x.DatabaseName,
                    DbType = x.DbType,
                    CreatedAt = x.CreatedAt
                }).ToList(),
                Total = connections.Count
            };

            await HttpContext.Response.SendAsync(response, cancellation: c);
        }
    }
}
