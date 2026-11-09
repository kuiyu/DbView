using FastEndpoints;
using DbView.Core;
using DbView.Core.Abstractions;
using DbView.Core.Backup;
using DbView.Core.Models;
using DbView.Infrastructure.Services;
using System.Text;

namespace DbView.WebApi.Features.Backup
{
    /// <summary>
    /// 备份恢复端点。
    ///
    /// 生产级要点：
    /// 1. 按连接隔离 —— 只允许恢复该连接自己目录下的备份，杜绝误恢复到别的库；
    /// 2. 完整性校验 —— 恢复前比对 SHA256，文件损坏直接拒绝恢复（避免用半个文件覆盖库）；
    /// 3. 流式执行 —— 逐句读取执行，不再把整个备份文件读进内存；
    /// 4. 事务化 —— PostgreSQL/SQLite/SQL Server 下整批在一个事务里，失败整体回滚；
    /// 5. 结果校验 —— 恢复后逐表比对行数与备份清单是否一致，明确报告"有没有丢数据"。
    /// </summary>
    internal sealed class RestoreBackupEndpoint : EndpointWithoutRequest
    {
        private readonly IConnectionRepository _connectionRepository;
        private readonly DatabaseService _databaseService;
        private readonly IBackupService _backupService;

        public RestoreBackupEndpoint(
            IConnectionRepository connectionRepository,
            DatabaseService databaseService,
            IBackupService backupService)
        {
            _connectionRepository = connectionRepository;
            _databaseService = databaseService;
            _backupService = backupService;
        }

        public override void Configure()
        {
            Post("/connections/{ConnectionId}/backups/{BackupId}/restore");
        }

        public override async Task HandleAsync(CancellationToken c)
        {
            var connectionId = Route<long>("ConnectionId");
            var backupId = Route<long>("BackupId");

            var connection = await _connectionRepository.GetByIdAsync(connectionId, c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = "连接不存在" }, 404, cancellation: c);
                return;
            }

            var found = BackupFileHelper.FindByBackupId(connectionId, backupId);
            if (found == null)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = "备份文件不存在" }, 404, cancellation: c);
                return;
            }

            var file = found.Value.File;
            var metadata = found.Value.Metadata;

            try
            {
                // 0) 恢复前自动快照：恢复是破坏性操作（会 DROP 目标库同名表），
                //    先把目标库当前状态备份一份，误操作时还有得退。
                string snapshotName = null;
                try
                {
                    snapshotName = $"pre_restore_{DateTime.UtcNow:yyyyMMdd_HHmmssZ}";
                    await _backupService.CreateBackupAsync(connection, snapshotName, c);
                    BackupLogger.Write($"恢复前快照已创建：连接 {connectionId}，备份 {snapshotName}");
                }
                catch (Exception snapEx)
                {
                    // 快照失败不阻断恢复（可能是磁盘满/权限问题），但必须记录下来
                    BackupLogger.Write($"恢复前快照创建失败（继续恢复）：连接 {connectionId}", snapEx);
                }

                // 1) 完整性校验：SHA256 不匹配说明文件被截断/损坏，拒绝恢复
                if (!string.IsNullOrEmpty(metadata.Sha256))
                {
                    var actual = BackupStorage.ComputeSha256(file.FullName);
                    if (!string.Equals(actual, metadata.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        await HttpContext.Response.SendAsync(new
                        {
                            success = false,
                            message = "备份文件校验失败（SHA256 不匹配），文件可能已损坏，已拒绝恢复"
                        }, 400, cancellation: c);
                        return;
                    }
                }

                // 2) 备份清单（每表行数），用于恢复后比对
                var expectedCounts = metadata.TableRowCounts ?? new Dictionary<string, long>();

                // 3) 流式执行（内部事务化，失败整体回滚）
                //    负载可能是 .sql / .sql.gz / .sql.gz.enc，统一由存储层负责解码
                using (var payload = BackupStorage.OpenPayloadRead(file.FullName, metadata.Compressed, metadata.Encrypted))
                using (var reader = new StreamReader(payload, Encoding.UTF8))
                {
                    var splitter = new SqlStatementSplitter(reader);
                    var (executed, skipped) = await _databaseService.ExecuteBatchAsync(connection, splitter.SplitAsync(c), c);

                    // 4) 恢复后逐表比对行数
                    var mismatches = await CompareRowCountsAsync(connection, expectedCounts, c);

                    var message = $"恢复成功，执行 {executed} 条语句";
                    if (skipped > 0)
                    {
                        message += $"，跳过 {skipped} 条 DROP（目标表不存在）";
                    }
                    if (mismatches.Count > 0)
                    {
                        message += $"；注意：{mismatches.Count} 张表的行数与备份清单不一致（{string.Join("；", mismatches)}）";
                    }

                    await HttpContext.Response.SendAsync(new
                    {
                        success = true,
                        message,
                        restored = executed,
                        skipped,
                        rowCountMismatches = mismatches,
                        verified = expectedCounts.Count > 0,
                        preRestoreSnapshot = snapshotName
                    }, cancellation: c);
                }
            }
            catch (Exception ex)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = $"恢复失败: {ex.Message}" }, 500, cancellation: c);
            }
        }

        /// <summary>
        /// 逐表比对行数：返回不一致的描述列表（空列表表示全部一致，即没有丢数据）。
        /// </summary>
        private async Task<List<string>> CompareRowCountsAsync(DbView.Core.Models.Connection connection, Dictionary<string, long> expected, CancellationToken cancellationToken)
        {
            var mismatches = new List<string>();
            if (expected.Count == 0)
            {
                return mismatches;
            }

            foreach (var kv in expected)
            {
                // 清单键格式："schema.表名" 或 "表名"
                string? schema = null;
                var table = kv.Key;
                var dot = kv.Key.IndexOf('.');
                if (dot > 0)
                {
                    schema = kv.Key.Substring(0, dot);
                    table = kv.Key.Substring(dot + 1);
                }

                long? actual;
                try
                {
                    actual = await _databaseService.GetTableRowCountAsync(connection, table, schema, cancellationToken);
                }
                catch
                {
                    // 表不存在或查询失败：无法判定，单独提示
                    mismatches.Add($"{kv.Key}(无法统计)");
                    continue;
                }

                if (actual == null)
                {
                    mismatches.Add($"{kv.Key}(无法统计)");
                }
                else if (actual.Value != kv.Value)
                {
                    mismatches.Add($"{kv.Key}(备份 {kv.Value} 行，实际 {actual.Value} 行)");
                }
            }

            return mismatches;
        }
    }
}
