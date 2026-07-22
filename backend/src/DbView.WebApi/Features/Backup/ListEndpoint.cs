using FastEndpoints;

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
            var backupDir = Path.Combine(AppContext.BaseDirectory, "backups");

            var backups = new List<BackupDto>();

            if (Directory.Exists(backupDir))
            {
                var files = Directory.GetFiles(backupDir, "*.sql");
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    backups.Add(new BackupDto
                    {
                        Id = fileInfo.CreationTime.Ticks,
                        Name = Path.GetFileNameWithoutExtension(fileInfo.Name),
                        ConnectionId = connectionId,
                        FileName = fileInfo.Name,
                        FileSize = fileInfo.Length,
                        CreatedAt = fileInfo.CreationTime
                    });
                }
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