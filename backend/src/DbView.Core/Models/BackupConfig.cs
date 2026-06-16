using DbView.Core.Abstractions;

namespace DbView.Core.Models
{
    public class BackupConfig : IEntity<long>
    {
        public long Id { get; set; }
        public long ConnectionId { get; set; }
        public bool Enabled { get; set; }
        public int IntervalHours { get; set; } = 24;
        public int MaxBackups { get; set; } = 10;
        public DateTime? LastBackupTime { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
