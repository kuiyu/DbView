namespace DbView.Core.Models
{
    /// <summary>
    /// 备份中除"表结构 + 数据"之外的数据库对象定义。
    ///
    /// 生产级备份不能只备份表：索引（含 pgvector 的 HNSW/IVFFlat 向量索引）、唯一/外键/CHECK 约束、
    /// 序列、视图、函数、触发器缺失，都会让恢复后的库"能跑但不对"
    /// （例如向量索引丢失后，相似度检索退化为全表扫描）。
    ///
    /// 各类对象在恢复时需要按依赖顺序执行，顺序由 BackupService 决定：
    ///   序列 -> 表结构/数据 -> 函数 -> 视图 -> 索引 -> 约束 -> 触发器
    /// </summary>
    public class BackupSchemaObjects
    {
        /// <summary>
        /// 需要预先创建的 schema 名（PostgreSQL）。序列/表/视图都可能落在非 public 的 schema 里，
        /// 必须先 CREATE SCHEMA，否则 CREATE SEQUENCE / CREATE TABLE 会失败。
        /// </summary>
        public List<string> Schemas { get; set; } = new();

        /// <summary>用户自定义类型（PostgreSQL ENUM 等）。列类型引用它们时，类型不存在会导致建表失败</summary>
        public List<string> Types { get; set; } = new();

        /// <summary>序列（PostgreSQL/Oracle 常见；PG 的 serial 列默认值依赖它，必须先建）</summary>
        public List<string> Sequences { get; set; } = new();

        /// <summary>函数 / 存储过程</summary>
        public List<string> Functions { get; set; } = new();

        /// <summary>视图 / 物化视图</summary>
        public List<string> Views { get; set; } = new();

        /// <summary>普通索引（含 PostgreSQL 的 hnsw / ivfflat 向量索引）</summary>
        public List<string> Indexes { get; set; } = new();

        /// <summary>唯一约束、外键、CHECK 约束</summary>
        public List<string> Constraints { get; set; } = new();

        /// <summary>触发器</summary>
        public List<string> Triggers { get; set; } = new();

        /// <summary>采集过程中被跳过的对象（用于日志与告警，不中断备份）</summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>对象总数（不含 schema）</summary>
        public int TotalCount =>
            Types.Count + Sequences.Count + Functions.Count + Views.Count + Indexes.Count + Constraints.Count + Triggers.Count;
    }
}
