namespace DbView.WebApi.Features.Table.InsertData
{
    public class InsertTableDataRequest
    {
        public long ConnectionId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public Dictionary<string, object> Data { get; set; } = new();
    }

    public class InsertTableDataResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
