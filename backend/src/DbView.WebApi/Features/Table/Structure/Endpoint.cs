using FastEndpoints;
using DbView.Infrastructure.Services;
using DbView.Core;

namespace DbView.WebApi.Features.Table.Structure
{
    internal sealed class GetTableStructureEndpoint : Endpoint<GetTableStructureRequest, List<ColumnDto>>
    {
        private readonly DatabaseService _databaseService;
        private readonly IConnectionRepository _connectionRepository;

        public GetTableStructureEndpoint(DatabaseService databaseService, IConnectionRepository connectionRepository)
        {
            _databaseService = databaseService;
            _connectionRepository = connectionRepository;
        }

        public override void Configure()
        {
            Get("/connections/{ConnectionId}/tables/{TableName}");
         
        }

        public override async Task HandleAsync(GetTableStructureRequest r, CancellationToken c)
        {
            var connection = await _connectionRepository.GetByIdAsync(r.ConnectionId, c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = "Connection not found" }, 404, cancellation: c);
                return;
            }

            var columns = await _databaseService.GetTableColumnsAsync(connection, r.TableName, c);

            var response = columns.Select(col => new ColumnDto
            {
                ColumnName = col.ColumnName,
                DataType = col.DataType,
                IsNullable = col.IsNullable,
                IsPrimaryKey = col.IsPrimaryKey,
                IsAutoIncrement = col.IsAutoIncrement,
                DefaultValue = col.DefaultValue,
                Comment = col.Comment
            }).ToList();

            HttpContext.Items["__manual_send"] = true;
            await HttpContext.Response.SendAsync(response, cancellation: c);
        }
    }
}