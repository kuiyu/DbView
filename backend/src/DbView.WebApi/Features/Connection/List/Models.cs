namespace DbView.WebApi.Features.Connection.List
{
    public class GetConnectionListResponse
    {
        public List<ConnectionDto> Items { get; set; } = new();
        public int Total { get; set; }
    }

    public class ConnectionDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string DatabaseName { get; set; } = string.Empty;
        public string DbType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
