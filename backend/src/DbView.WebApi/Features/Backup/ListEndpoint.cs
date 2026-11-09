using FastEndpoints;
using DbView.Core.Backup;

namespace DbView.WebApi.Features.Backup
{
    internal sealed class GetBackupsEndpoint : EndpointWithoutRequest<GetBackupsResponse>
    {
        public override void Configure()
        {
            Get("/connections/{ConnectionId}/backups");
          
        }

        public override async Task HandleAsync(CancellationToken c)
        {
            var connectionId = Route<long>("ConnectionId");

            // 旧布局（所有备份平铺在根目录）的一次性迁移，保证历史备份不丢失可见性
            BackupStorage.MigrateLegacyBackups();

            // 只列出该连接自己的备份，避免跨连接串库
            var backups = new List<BackupDto>();
            foreach (var (file, meta) in BackupStorage.ListBackups(connectionId))
            {
                backups.Add(new BackupDto
                {
                    Id = meta.Id,
                    Name = meta.Name,
                    ConnectionId = connectionId,
                    FileName = meta.FileName,
                    FileSize = file.Exists ? file.Length : meta.FileSize,
                    CreatedAt = meta.CreatedAt,
                    Compressed = meta.Compressed,
                    Encrypted = meta.Encrypted,
                    Trigger = meta.Trigger,
                    TableCount = meta.TableRowCounts?.Count ?? 0,
                    TotalRows = meta.TableRowCounts?.Values.Sum() ?? 0,
                    SchemaObjectCounts = meta.SchemaObjectCounts ?? new Dictionary<string, int>(),
                    Warnings = meta.Warnings ?? new List<string>()
                });
            }

            Response = new GetBackupsResponse
            {
                Items = backups.OrderByDescending(b => b.CreatedAt).ToList(),
                Total = backups.Count
            };

            await Task.CompletedTask;
        }
    }
}