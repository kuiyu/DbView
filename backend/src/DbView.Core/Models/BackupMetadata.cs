namespace DbView.Core.Models
{
    /// <summary>
    /// 备份文件的元数据（与同名 .sql 并列存放的 .json 旁车文件）。
    ///
    /// 作用：
    /// 1. 提供稳定的备份 Id（不再依赖文件 CreationTime，避免同刻创建冲突与重启后变化）；
    /// 2. 记录 SHA256 校验和，恢复前可校验文件是否损坏；
    /// 3. 记录每张表的行数清单，恢复后逐表比对，用于证明"没有丢数据"；
    /// 4. 记录所属连接、库名、库类型，避免不同连接的备份互相串用。
    /// </summary>
    public class BackupMetadata
    {
        /// <summary>备份格式版本（2 = 支持向量列、行数清单、SHA256 校验）</summary>
        public int FormatVersion { get; set; } = 2;

        /// <summary>稳定的备份 Id，由创建时生成并持久化</summary>
        public long Id { get; set; }

        /// <summary>所属连接 Id</summary>
        public long ConnectionId { get; set; }

        /// <summary>备份名称（不含扩展名）</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>备份文件名（含 .sql）</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>来源数据库名</summary>
        public string DatabaseName { get; set; } = string.Empty;

        /// <summary>来源数据库类型（postgresql / mysql / ...）</summary>
        public string DbType { get; set; } = string.Empty;

        /// <summary>备份生成时间</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>备份文件字节数</summary>
        public long FileSize { get; set; }

        /// <summary>备份文件内容的 SHA256（小写十六进制）；为空表示未计算（老备份）</summary>
        public string Sha256 { get; set; } = string.Empty;

        /// <summary>是否完整生成。false 表示该备份不可用于恢复</summary>
        public bool Completed { get; set; }

        /// <summary>
        /// 每张表的行数清单，键为 "schema.表名"（无 schema 时为 "表名"）。
        /// 恢复后与实际行数比对，不一致即说明数据未完整还原。
        /// </summary>
        public Dictionary<string, long> TableRowCounts { get; set; } = new();

        /// <summary>
        /// 备份中包含的数据库对象数量（键为对象类别：Sequences/Indexes/Constraints/Views/Functions/Triggers）。
        /// 用于快速了解备份覆盖范围，以及排查"某个索引/约束是否被备份"。
        /// </summary>
        public Dictionary<string, int> SchemaObjectCounts { get; set; } = new();

        /// <summary>
        /// 备份过程中的告警（例如某些对象采集失败被跳过）。
        /// 备份本身成功，但这些告警说明备份内容有缺口，需要人工确认。
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>是否经过压缩（.sql.gz）</summary>
        public bool Compressed { get; set; }

        /// <summary>是否经过加密（AES-256-GCM）</summary>
        public bool Encrypted { get; set; }

        /// <summary>备份来源类型：auto（自动调度）/ manual（手动触发）</summary>
        public string Trigger { get; set; } = "manual";
    }
}
