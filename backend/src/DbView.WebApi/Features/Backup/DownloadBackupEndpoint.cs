using FastEndpoints;

namespace DbView.WebApi.Features.Backup
{
    internal sealed class DownloadBackupEndpoint : EndpointWithoutRequest
    {
        public override void Configure()
        {
            Get("/connections/{ConnectionId}/backups/{BackupId}/download");
        }

        public override async Task HandleAsync(CancellationToken c)
        {
            var backupId = Route<long>("BackupId");
            var connectionId = Route<long>("ConnectionId");

            // 只允许下载该连接自己的备份（按连接目录查找）
            var found = BackupFileHelper.FindByBackupId(connectionId, backupId);
            if (found == null)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = "备份文件不存在" }, 404, cancellation: c);
                return;
            }

            var file = found.Value.File;
            var bytes = await File.ReadAllBytesAsync(file.FullName, c);
            HttpContext.Response.Headers.ContentDisposition = $"attachment; filename=\"{file.Name}\"";
            HttpContext.Response.ContentType = file.Name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                ? "application/gzip"
                : (file.Name.EndsWith(".enc", StringComparison.OrdinalIgnoreCase)
                    ? "application/octet-stream"
                    : "application/sql");
            await HttpContext.Response.SendAsync(bytes, cancellation: c);
        }
    }
}
