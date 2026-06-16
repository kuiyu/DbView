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

        public int MaxBackups { get; set; } = 10;

        public DateTime? LastBackupTime { get; set; }

        [Column(CanUpdate = false)]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
