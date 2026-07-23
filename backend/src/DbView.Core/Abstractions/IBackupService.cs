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
    }

    /// <summary>
    /// 数据库备份服务：生成全量 INSERT 脚本并写入 .sql 文件
    /// </summary>
    public interface IBackupService
    {
        Task<BackupFileInfo> CreateBackupAsync(Connection connection, string? name, CancellationToken cancellationToken = default);
    }
}
