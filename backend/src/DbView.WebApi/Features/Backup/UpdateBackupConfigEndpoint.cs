using FastEndpoints;
using DbView.Core;
using DbView.Core.Models;

namespace DbView.WebApi.Features.Backup
{
    internal sealed class UpdateBackupConfigEndpoint : Endpoint<UpdateBackupConfigRequest>
    {
        private readonly IConnectionRepository _connectionRepository;
        private readonly IBackupConfigRepository _backupConfigRepository;

        public UpdateBackupConfigEndpoint(
            IConnectionRepository connectionRepository,
            IBackupConfigRepository backupConfigRepository)
        {
            _connectionRepository = connectionRepository;
            _backupConfigRepository = backupConfigRepository;
        }

        public override void Configure()
        {
            Put("/connections/{ConnectionId}/backup-config");
        }

        public override async Task HandleAsync(UpdateBackupConfigRequest r, CancellationToken c)
        {
            var connectionId = Route<long>("ConnectionId");

            var connection = await _connectionRepository.GetByIdAsync(connectionId, c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = "连接不存在" }, 404, cancellation: c);
                return;
            }

            var existing = await _backupConfigRepository.GetByConnectionIdAsync(connectionId, c);
            BackupConfig config;

            if (existing == null)
            {
                config = new BackupConfig
                {
                    ConnectionId = connectionId,
                    Enabled = r.Enabled,
                    IntervalHours = r.IntervalHours,
                    MaxBackups = r.MaxBackups,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                config = await _backupConfigRepository.AddAsync(config, c);
            }
            else
            {
                existing.Enabled = r.Enabled;
                existing.IntervalHours = r.IntervalHours;
                existing.MaxBackups = r.MaxBackups;
                existing.UpdatedAt = DateTime.Now;
                await _backupConfigRepository.UpdateAsync(existing, c);
                config = existing;
            }

            var dto = new BackupConfigDto
            {
                Id = config.Id,
                ConnectionId = config.ConnectionId,
                Enabled = config.Enabled,
                IntervalHours = config.IntervalHours,
                MaxBackups = config.MaxBackups,
                LastBackupTime = config.LastBackupTime
            };

            await HttpContext.Response.SendAsync(new { success = true, config = dto }, cancellation: c);
        }
    }
}
