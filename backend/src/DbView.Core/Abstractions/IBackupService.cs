using DbView.Core.Models;

namespace DbView.Core.Abstractions
{
    /// <summary>
    /// 一次备份操作产出的文件信息
    /// </summary>
    public class BackupFileInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>稳定的备份 Id（与元数据旁车文件中记录的一致）</summary>
        public long Id { get; set; }

        /// <summary>备份文件内容的 SHA256，用于恢复前完整性校验</summary>
        public string Sha256 { get; set; } = string.Empty;

        /// <summary>负载是否经过 gzip 压缩</summary>
        public bool Compressed { get; set; }

        /// <summary>负载是否经过 AES-256-GCM 加密</summary>
        public bool Encrypted { get; set; }
    }

    /// <summary>
    /// 数据库备份服务：生成全量 INSERT 脚本并写入 .sql 文件
    /// </summary>
    public interface IBackupService
    {
        Task<BackupFileInfo> CreateBackupAsync(Connection connection, string? name, CancellationToken cancellationToken = default);
    }
}
