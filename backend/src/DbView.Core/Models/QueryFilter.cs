namespace DbView.Core.Models
{
    public class QueryFilter
    {
        public string ColumnName { get; set; } = string.Empty;
        public string Operator { get; set; } = "eq";
        public string Value { get; set; } = string.Empty;
    }
}
