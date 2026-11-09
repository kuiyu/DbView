using FastEndpoints;
using DbView.Core.Backup;

namespace DbView.WebApi.Features.Backup
{
    internal sealed class DeleteBackupEndpoint : EndpointWithoutRequest
    {
        public override void Configure()
        {
            Delete("/connections/{ConnectionId}/backups/{BackupId}");
        }

        public override async Task HandleAsync(CancellationToken c)
        {
            var backupId = Route<long>("BackupId");
            var connectionId = Route<long>("ConnectionId");

            // 只允许删除该连接自己的备份（按连接目录查找，避免误删其它连接的备份）
            var found = BackupFileHelper.FindByBackupId(connectionId, backupId);
            if (found == null)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = "备份文件不存在" }, 404, cancellation: c);
                return;
            }

            try
            {
                // 同时删除 .sql 与元数据旁车文件
                BackupStorage.DeleteFiles(connectionId, found.Value.Metadata.FileName);
                await HttpContext.Response.SendAsync(new { success = true, message = "删除成功" }, cancellation: c);
            }
            catch (Exception ex)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = $"删除失败: {ex.Message}" }, 500, cancellation: c);
            }
        }
    }
}
