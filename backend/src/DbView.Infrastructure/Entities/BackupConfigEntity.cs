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
        /// </summary>
        public int RetentionDays { get; set; } = 7;

        public string ScheduleMode { get; set; } = "interval";

        public string? BackupTime { get; set; }

        public DateTime? LastBackupTime { get; set; }

        [Column(CanUpdate = false)]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
