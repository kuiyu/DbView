using FastEndpoints;
using DbView.Core;
using DbView.Core.Models;

namespace DbView.WebApi.Features.Backup
{
    internal sealed class GetBackupConfigEndpoint : EndpointWithoutRequest
    {
        private readonly IConnectionRepository _connectionRepository;
        private readonly IBackupConfigRepository _backupConfigRepository;

        public GetBackupConfigEndpoint(
            IConnectionRepository connectionRepository,
            IBackupConfigRepository backupConfigRepository)
        {
            _connectionRepository = connectionRepository;
            _backupConfigRepository = backupConfigRepository;
        }

        public override void Configure()
        {
            Get("/connections/{ConnectionId}/backup-config");
        }

        public override async Task HandleAsync(CancellationToken c)
        {
            var connectionId = Route<long>("ConnectionId");

            var connection = await _connectionRepository.GetByIdAsync(connectionId, c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = "连接不存在" }, 404, cancellation: c);
                return;
            }

            var config = await _backupConfigRepository.GetByConnectionIdAsync(connectionId, c);
            var dto = config == null
                ? new BackupConfigDto
                {
                    ConnectionId = connectionId,
                    Enabled = false,
                    IntervalHours = 24,
                    MaxBackups = 10
                }
                : new BackupConfigDto
                {
                    Id = config.Id,
                    ConnectionId = config.ConnectionId,
                    Enabled = config.Enabled,
                    IntervalHours = config.IntervalHours,
                    MaxBackups = config.MaxBackups,
                    LastBackupTime = config.LastBackupTime
                };

            await HttpContext.Response.SendAsync(new { config = dto }, cancellation: c);
        }
    }
}
