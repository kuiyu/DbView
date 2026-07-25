using DbView.Core.Models;

namespace DbView.WebApi.Features.Table
{
    public class CreateTableRequest
    {
        public string TableName { get; set; } = string.Empty;
        public List<ColumnDefinition> Columns { get; set; } = new();
    }

    public class RenameTableRequest
    {
        public string NewName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 与前端 structure.ts 字段 payload（{ name, type, nullable }）对应
    /// </summary>
    public class ColumnRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "varchar";
        public bool Nullable { get; set; } = true;
    }
}
