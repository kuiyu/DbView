using FastEndpoints;
using DbView.Core;
using DbView.Core.Abstractions;

namespace DbView.WebApi.Features.Backup
{
    internal sealed class CreateBackupEndpoint : Endpoint<CreateBackupRequest, CreateBackupResponse>
    {
        private readonly IConnectionRepository _connectionRepository;
        private readonly IBackupService _backupService;

        public CreateBackupEndpoint(
            IConnectionRepository connectionRepository,
            IBackupService backupService)
        {
            _connectionRepository = connectionRepository;
            _backupService = backupService;
        }

        public override void Configure()
        {
            Post("/connections/{ConnectionId}/backups");

        }

        public override async Task HandleAsync(CreateBackupRequest r, CancellationToken c)
        {
            var connection = await _connectionRepository.GetByIdAsync(Route<long>("ConnectionId"), c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = "连接不存在" }, 404, cancellation: c);
                return;
            }

            try
            {
                var backupName = r.Name ?? $"backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                var result = await _backupService.CreateBackupAsync(connection, backupName, c);

                var response = new CreateBackupResponse
                {
                    Success = true,
                    Message = "备份成功",
                    Backup = new BackupDto
                    {
                        Id = DateTime.Now.Ticks,
                        Name = backupName,
                        ConnectionId = connection.Id,
                        FileName = result.FileName,
                        FileSize = result.FileSize,
                        CreatedAt = result.CreatedAt
                    }
                };

                await HttpContext.Response.SendAsync(response, cancellation: c);
            }
            catch (Exception ex)
            {
                await HttpContext.Response.SendAsync(new
                {
                    success = false,
                    message = $"备份失败: {ex.Message}"
                }, 500, cancellation: c);
            }
        }
    }
}