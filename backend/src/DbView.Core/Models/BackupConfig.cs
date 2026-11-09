using DbView.Core.Abstractions;

namespace DbView.Core.Models
{
    public class BackupConfig : IEntity<long>
    {
        public long Id { get; set; }
        public long ConnectionId { get; set; }
        public bool Enabled { get; set; }
        public int IntervalHours { get; set; } = 24;

        /// <summary>
        /// 备份保留天数：仅保留该天数以内产生的自动备份，更早的自动备份会被自动删除。
        /// </summary>
        public int RetentionDays { get; set; } = 7;
        public string ScheduleMode { get; set; } = "interval";
        public string? BackupTime { get; set; }
        public DateTime? LastBackupTime { get; set; }

        /// <summary>上次尝试备份的时间（UTC）</summary>
        public DateTime? LastAttemptTime { get; set; }

        /// <summary>最近一次自动备份失败的原因（成功时清空）</summary>
        public string? LastError { get; set; }

        /// <summary>连续失败次数（成功时清零）</summary>
        public int ConsecutiveFailures { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
