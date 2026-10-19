using FreeSql.DataAnnotations;

namespace DbView.Infrastructure.Entities
{
    [Table(Name = "dbview_backup_configs")]
    public class BackupConfigEntity
    {
        [Column(IsIdentity = true, IsPrimary = true)]
        public long Id { get; set; }

        public long ConnectionId { get; set; }

        public bool Enabled { get; set; }

        public int IntervalHours { get; set; } = 24;

        /// <summary>
        /// 备份保留天数：仅保留该天数以内产生的自动备份，更早的自动备份会被自动删除。
        /// 使用可空类型，便于 FreeSql 自动同步（AutoSync）在已存在表上顺利 ALTER ADD 该列，
        /// 避免“列不存在”导致自动备份调度整体失效；读取值时由 NormalizeRetentionDays 兜底默认值。
        /// </summary>
        public int? RetentionDays { get; set; }

        public string ScheduleMode { get; set; } = "interval";

        public string? BackupTime { get; set; }

        public DateTime? LastBackupTime { get; set; }

        [Column(CanUpdate = false)]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
