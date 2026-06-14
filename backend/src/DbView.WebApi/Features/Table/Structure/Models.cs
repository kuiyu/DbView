namespace DbView.WebApi.Features.Table.Structure
{
    public class GetTableStructureRequest
    {
        public long ConnectionId { get; set; }
        public string TableName { get; set; } = string.Empty;
    }

    public class ColumnDto
    {
        public string ColumnName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsAutoIncrement { get; set; }
        public string DefaultValue { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }
}