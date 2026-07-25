namespace DbView.Core.Models
{
    /// <summary>
    /// 表结构编辑中的字段定义（与前端 structure.ts 的字段 payload 对应）
    /// </summary>
    public class ColumnDefinition
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 逻辑类型：integer / varchar / text / boolean / timestamp（由 DatabaseService 按库类型映射为物理类型）
        /// </summary>
        public string Type { get; set; } = "varchar";

        public bool Nullable { get; set; } = true;
        public bool IsPrimaryKey { get; set; }
        public bool AutoIncrement { get; set; }
        public string? DefaultValue { get; set; }
    }
}
