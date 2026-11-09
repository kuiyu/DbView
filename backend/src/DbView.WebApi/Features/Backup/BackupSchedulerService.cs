using DbView.Core;
using DbView.Core.Abstractions;
using DbView.Core.Backup;
using DbView.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DbView.WebApi.Features.Backup
{
    /// <summary>
    /// 后台托管服务：按 BackupConfig 的计划（启用、间隔小时、上次备份时间、保留天数）
    /// 自动对数据库执行备份，并清理超过保留天数的历史备份文件。
    /// </summary>
    public sealed class BackupSchedulerService : BackgroundService
    {
        // 轮询间隔（实际备份频率由每个配置的 IntervalHours 决定）
        private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

        /// <summary>保留天数的默认值：配置值非法（小于等于 0）时使用</summary>
        public const int DefaultRetentionDays = 7;

        /// <summary>手动备份最多保留个数（超出后删除最旧的），避免磁盘被手动备份撑满</summary>
        public const int MaxManualBackups = 20;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BackupSchedulerService> _logger;

        public BackupSchedulerService(IServiceScopeFactory scopeFactory, ILogger<BackupSchedulerService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            BackupLogger.CleanupOldLogs();
            Track($"自动备份调度服务已启动，轮询间隔={PollInterval.TotalMinutes} 分钟，备份目录={BackupStorage.RootDir}，日志目录={BackupLogger.LogDir}");

            using var timer = new PeriodicTimer(PollInterval);

            do
            {
                try
                {
                    await RunDueBackupsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Track("自动备份调度执行出错", ex);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task RunDueBackupsAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var backupConfigRepo = scope.ServiceProvider.GetRequiredService<IBackupConfigRepository>();
            var connectionRepo = scope.ServiceProvider.GetRequiredService<IConnectionRepository>();
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();

            // 仅处理启用中的计划
            var configs = await backupConfigRepo.GetAllAsync(c => c.Enabled, stoppingToken);

            // 时间基准统一用 UTC 存储，避免跨时区/夏令时导致 daily 模式重复触发或漏触发
            var nowUtc = DateTime.UtcNow;

            if (configs.Count == 0)
            {
                // 静默路径：没有任何启用配置时也要留下痕迹，避免"既无备份也无日志"
                Track("自动备份：未找到任何已启用的备份配置，本次跳过");
                return;
            }

            foreach (var config in configs)
            {
                // 每次轮询都按保留策略清理过期备份，即使本次未触发新备份
                try
                {
                    CleanupExpiredBackups(config.ConnectionId, config.RetentionDays, nowUtc);
                }
                catch (Exception ex)
                {
                    Track($"清理过期备份失败：连接 {config.ConnectionId}", ex);
                }

                // 失败退避：连续失败时按 2^n 分钟退避，避免持续失败反复压垮数据库
                var backoffMinutes = GetBackoffMinutes(config.ConsecutiveFailures);
                if (backoffMinutes > 0 && config.LastAttemptTime.HasValue &&
                    (nowUtc - config.LastAttemptTime.Value).TotalMinutes < backoffMinutes)
                {
                    continue;
                }

                if (!IsDue(config, nowUtc))
                {
                    continue;
                }

                try
                {
                    var connection = await connectionRepo.GetByIdAsync(config.ConnectionId, stoppingToken);
                    if (connection == null)
                    {
                        Track($"自动备份跳过：连接 {config.ConnectionId} 不存在");
                        continue;
                    }

                    var backupName = $"auto_{config.ConnectionId}_{nowUtc:yyyyMMdd_HHmmss}Z";
                    await backupService.CreateBackupAsync(connection, backupName, stoppingToken);

                    config.LastBackupTime = nowUtc;
                    config.LastAttemptTime = nowUtc;
                    config.LastError = null;
                    config.ConsecutiveFailures = 0;
                    config.UpdatedAt = nowUtc;
                    await backupConfigRepo.UpdateAsync(config, stoppingToken);

                    Track($"自动备份完成：连接 {config.ConnectionId}，文件 {backupName}");
                }
                catch (Exception ex)
                {
                    // 记录失败原因与连续失败次数（界面上可见），并进入退避
                    config.LastAttemptTime = nowUtc;
                    config.ConsecutiveFailures = config.ConsecutiveFailures + 1;
                    config.LastError = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {ex.Message}";
                    config.UpdatedAt = nowUtc;
                    try
                    {
                        await backupConfigRepo.UpdateAsync(config, stoppingToken);
                    }
                    catch
                    {
                        // 记录失败状态本身失败不影响主流程
                    }

                    Track($"自动备份失败：连接 {config.ConnectionId}（连续失败 {config.ConsecutiveFailures} 次，{GetBackoffMinutes(config.ConsecutiveFailures)} 分钟后重试）", ex);
                }
            }
        }

        /// <summary>
        /// 失败退避时长：连续失败 n 次后等待 min(2^n, 60) 分钟。
        /// </summary>
        private static int GetBackoffMinutes(int consecutiveFailures)
        {
            if (consecutiveFailures <= 0)
            {
                return 0;
            }

            var minutes = 1 << Math.Min(consecutiveFailures, 6);   // 2,4,8,16,32,64
            return Math.Min(minutes, 60);
        }

        /// <summary>
        /// 同时输出到控制台（ILogger）与本地日志文件（{程序基目录}/logs/scheduler.log），
        /// 这样即使程序以无控制台的方式运行，也能在文件里看到调度轨迹。
        /// </summary>
        private void Track(string message, Exception? ex = null)
        {
            if (ex != null)
                _logger.LogError(ex, message);
            else
                _logger.LogInformation(message);

            // 同时落本地文件（按天轮转、保留 14 天），便于无控制台环境下排查
            BackupLogger.Write(message, ex);
        }

        /// <summary>
        /// 判断某个备份计划是否到期需要执行。
        /// - interval 模式：距上次备份已超过 IntervalHours 小时（从未备份则立即执行）。
        /// - daily 模式：每天到达指定 BackupTime 后执行一次（每个自然日最多一次）。
        /// </summary>
        private bool IsDue(BackupConfig config, DateTime nowUtc)
        {
            if (config.ScheduleMode == "daily")
            {
                if (!TimeSpan.TryParse(config.BackupTime, out var scheduledTime))
                {
                    return false;
                }

                // 触发时间按本地时区理解（用户期望"每天本地时间这个点跑"），再换算成 UTC 比较
                var localNow = nowUtc.ToLocalTime();
                var scheduledTodayUtc = localNow.Date.Add(scheduledTime).ToUniversalTime();
                if (nowUtc < scheduledTodayUtc)
                {
                    return false;
                }

                // 今天是否已备份过：LastBackupTime 存的是 UTC，转回本地后比较日期
                if (config.LastBackupTime == null)
                {
                    return true;
                }

                return config.LastBackupTime.Value.ToLocalTime().Date < localNow.Date;
            }

            // 默认 interval 模式（两边都是 UTC，直接比较）
            if (config.IntervalHours <= 0)
            {
                return false;
            }

            if (config.LastBackupTime == null)
            {
                return true;
            }

            return (nowUtc - config.LastBackupTime.Value).TotalHours >= config.IntervalHours;
        }

        /// <summary>
        /// 规范化保留天数：非法值（小于等于 0，如旧数据迁移后的默认值）回退到默认值。
        /// </summary>
        public static int NormalizeRetentionDays(int retentionDays)
        {
            return retentionDays > 0 ? retentionDays : DefaultRetentionDays;
        }

        /// <summary>
        /// 清理策略：
        /// 1. 自动备份（auto_{connectionId}_ 前缀）按保留天数清理；
        /// 2. 手动备份按数量上限清理（默认保留最近 20 个），避免磁盘被手动备份撑满；
        /// 3. 恢复前快照（pre_restore_ 前缀）永不自动清理 —— 它是误操作后的救命稻草。
        /// </summary>
        private static void CleanupExpiredBackups(long connectionId, int retentionDays, DateTime nowUtc)
        {
            var days = NormalizeRetentionDays(retentionDays);
            var cutoff = nowUtc.AddDays(-days);

            // 新布局：每个连接一个目录，避免清理时误删其它连接的备份
            var backupDir = BackupStorage.GetConnectionDir(connectionId);
            if (!Directory.Exists(backupDir))
            {
                return;
            }

            var autoPrefix = $"auto_{connectionId}_";
            var snapshotPrefix = "pre_restore_";

            var payloadFiles = Directory.GetFiles(backupDir)
                .Where(p => BackupStorage.IsPayloadFile(Path.GetFileName(p)))
                .ToList();

            // 1) 自动备份按天数清理
            foreach (var path in payloadFiles)
            {
                var name = Path.GetFileName(path);
                if (!name.StartsWith(autoPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    // 用最后写入时间判断新旧，比 CreationTime 稳定（Linux 上 CreationTime 语义不可靠）
                    if (File.GetLastWriteTime(path) < cutoff)
                    {
                        // 同时清理元数据旁车文件，避免留下孤儿元数据
                        BackupStorage.DeleteFiles(connectionId, name);
                    }
                }
                catch
                {
                    // 忽略单个文件删除失败，继续清理其余文件
                }
            }

            // 2) 手动备份按数量上限清理
            var manualFiles = payloadFiles
                .Where(p =>
                {
                    var n = Path.GetFileName(p);
                    return !n.StartsWith(autoPrefix, StringComparison.Ordinal)
                        && !n.StartsWith(snapshotPrefix, StringComparison.Ordinal);
                })
                .OrderBy(p => File.GetLastWriteTime(p))
                .ToList();

            var keep = MaxManualBackups;
            if (manualFiles.Count > keep)
            {
                foreach (var path in manualFiles.Take(manualFiles.Count - keep))
                {
                    try
                    {
                        BackupStorage.DeleteFiles(connectionId, Path.GetFileName(path));
                    }
                    catch
                    {
                        // 忽略单个文件删除失败
                    }
                }
            }
        }
    }
}
