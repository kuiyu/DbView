namespace DbView.Core.Models
{
    public class TableInfo
    {
        public string TableName { get; set; } = string.Empty;
        public string SchemaName { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public long RowCount { get; set; }
        public string Size { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }
}