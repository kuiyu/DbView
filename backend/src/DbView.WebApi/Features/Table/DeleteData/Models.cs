namespace DbView.WebApi.Features.Table.DeleteData
{
    public class DeleteTableDataRequest
    {
        public long ConnectionId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public Dictionary<string, object> Where { get; set; } = new();
    }

    public class DeleteTableDataResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
