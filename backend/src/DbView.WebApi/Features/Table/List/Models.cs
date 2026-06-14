namespace DbView.WebApi.Features.Table.List
{
    public class GetTableListRequest
    {
        public long ConnectionId { get; set; }
    }

    public class GetTableListResponse
    {
        public List<TableDto> Items { get; set; } = new();
        public int Total { get; set; }
    }

    public class TableDto
    {
        public string TableName { get; set; } = string.Empty;
        public string SchemaName { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }
}