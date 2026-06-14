namespace DbView.WebApi.Features.Connection.Delete
{
    public class DeleteConnectionRequest
    {
        public long Id { get; set; }
    }

    public class DeleteConnectionResponse
    {
        public string Message { get; set; } = string.Empty;
    }
}
