namespace DbView.WebApi.Features.Connection.Get
{
    public class GetConnectionRequest
    {
        public long Id { get; set; }
    }

    public class GetConnectionResponse
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string DatabaseName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string DbType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
