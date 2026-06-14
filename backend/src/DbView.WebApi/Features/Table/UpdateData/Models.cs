namespace DbView.WebApi.Features.Table.UpdateData
{
    public class UpdateTableDataRequest
    {
        public long ConnectionId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public Dictionary<string, object> Data { get; set; } = new();
        public Dictionary<string, object> Where { get; set; } = new();
    }

    public class UpdateTableDataResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
