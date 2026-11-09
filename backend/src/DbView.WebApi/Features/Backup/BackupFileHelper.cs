using DbView.Core.Backup;
using DbView.Core.Models;

namespace DbView.WebApi.Features.Backup
{
    internal static class BackupFileHelper
    {
        /// <summary>
        /// 备份根目录
        /// </summary>
        public static string BackupDir => BackupStorage.RootDir;

        /// <summary>
        /// 在指定连接的备份目录内按 Id 查找备份。
        ///
        /// 重要：查找被限定在该连接自己的目录内，避免出现"A 连接看到并误恢复 B 连接备份"
        /// 的串库问题（旧实现按全局文件枚举，带来了严重缺陷）。
        /// </summary>
        public static (FileInfo File, BackupMetadata Metadata)? FindByBackupId(long connectionId, long backupId)
        {
            foreach (var item in BackupStorage.ListBackups(connectionId))
            {
                if (item.Metadata.Id == backupId)
                {
                    return item;
                }
            }

            return null;
        }
    }
}
