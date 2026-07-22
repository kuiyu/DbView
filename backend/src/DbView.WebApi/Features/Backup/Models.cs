namespace DbView.WebApi.Features.Backup
{
    public class CreateBackupRequest
    {
        public string? Name { get; set; }
    }

    public class CreateBackupResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public BackupDto? Backup { get; set; }
    }

    public class BackupDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public long ConnectionId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class GetBackupsResponse
    {
        public List<BackupDto> Items { get; set; } = new();
        public int Total { get; set; }
    }

    public class BackupConfigDto
    {
        public long Id { get; set; }
        public long ConnectionId { get; set; }
        public bool Enabled { get; set; }
        public int IntervalHours { get; set; }
        public int MaxBackups { get; set; }
        public DateTime? LastBackupTime { get; set; }
    }

    public class UpdateBackupConfigRequest
    {
        public bool Enabled { get; set; }
        public int IntervalHours { get; set; }
        public int MaxBackups { get; set; }
    }
}