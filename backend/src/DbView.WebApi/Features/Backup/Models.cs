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

        /// <summary>是否压缩存储</summary>
        public bool Compressed { get; set; }

        /// <summary>是否加密存储</summary>
        public bool Encrypted { get; set; }

        /// <summary>来源：auto（自动调度）/ manual（手动）/ pre_restore（恢复前快照）</summary>
        public string Trigger { get; set; } = "manual";

        /// <summary>备份覆盖的表数量</summary>
        public int TableCount { get; set; }

        /// <summary>备份覆盖的总行数</summary>
        public long TotalRows { get; set; }

        /// <summary>包含的数据库对象数量（索引/约束/序列/视图/函数/触发器）</summary>
        public Dictionary<string, int> SchemaObjectCounts { get; set; } = new();

        /// <summary>备份告警（例如某些对象采集失败被跳过）</summary>
        public List<string> Warnings { get; set; } = new();
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
        public int RetentionDays { get; set; }
        public string ScheduleMode { get; set; } = "interval";
        public string? BackupTime { get; set; }
        public DateTime? LastBackupTime { get; set; }

        /// <summary>最近一次自动备份失败的原因（成功时为空）</summary>
        public string? LastError { get; set; }

        /// <summary>连续失败次数（成功时清零）</summary>
        public int ConsecutiveFailures { get; set; }
    }

    public class UpdateBackupConfigRequest
    {
        public bool Enabled { get; set; }
        public int IntervalHours { get; set; }
        public int RetentionDays { get; set; }
        public string ScheduleMode { get; set; } = "interval";
        public string? BackupTime { get; set; }
    }
}