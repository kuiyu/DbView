namespace DbView.WebApi.Features.Table.GetData
{
    public class GetTableDataRequest
    {
        public long ConnectionId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class GetTableDataResponse
    {
        public List<object[]> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
