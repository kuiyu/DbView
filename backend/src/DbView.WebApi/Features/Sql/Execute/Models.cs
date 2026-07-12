namespace DbView.WebApi.Features.Sql.Execute
{
    public class ExecuteSqlRequest
    {
        public long ConnectionId { get; set; }
        public string Sql { get; set; } = string.Empty;
    }

    public class ExecuteSqlResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int RowsAffected { get; set; }
        public List<object[]> Data { get; set; } = new();
    }
}
