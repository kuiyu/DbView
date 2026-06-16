namespace DbView.WebApi.Features.Table.DropTable
{
    public class DropTableRequest
    {
        public long ConnectionId { get; set; }
        public string TableName { get; set; } = string.Empty;
    }

    public class DropTableResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
