using DbView.Core.Models;

namespace DbView.WebApi.Features.Table.Query
{
    public class QueryTableDataRequest
    {
        public long ConnectionId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public List<QueryFilter> Filters { get; set; } = new();
        public string OrderBy { get; set; } = string.Empty;
        public string OrderDirection { get; set; } = "asc";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class QueryTableDataResponse
    {
        public List<object[]> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
