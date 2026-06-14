using FastEndpoints;
using DbView.Application.Services;

namespace DbView.WebApi.Features.Connection.Test
{
    internal sealed class TestConnectionEndpoint : Endpoint<TestConnectionRequest, TestConnectionResponse>
    {
        private readonly IConnectionAppService _connectionAppService;

        public TestConnectionEndpoint(IConnectionAppService connectionAppService)
        {
            _connectionAppService = connectionAppService;
        }

        public override void Configure()
        {
            Post("/connections/test");
            AllowAnonymous();
            Validator<TestConnectionValidator>();
        }

        public override async Task HandleAsync(TestConnectionRequest r, CancellationToken c)
        {
            var connection = new DbView.Core.Models.Connection
            {
                Name = "Test",
                Host = r.Host,
                Port = r.Port,
                DatabaseName = r.DatabaseName,
                Username = r.Username,
                Password = r.Password,
                DbType = r.DbType
            };

            var success = await _connectionAppService.TestConnectionAsync(connection, c);

            Response = new TestConnectionResponse
            {
                Success = success,
                Message = success ? "Connection successful" : "Connection failed"
            };
        }
    }
}
