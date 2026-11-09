using System.Globalization;
using System.Text;

namespace DbView.WebApi.Features.Backup
{
    /// <summary>
    /// 备份调度日志写入器。
    ///
    /// 为什么要自己写而不用 ILogger 文件提供器：
    /// - 日志文件可能位于只读的程序目录（容器场景），需要可配置目录；
    /// - 需要按天 + 大小轮转，且保留有限天数，否则日志会无限增长把磁盘写满
    ///   （备份目录通常与日志目录在同一块盘，磁盘写满会直接导致备份失败）；
    /// - 需要线程安全（调度器与 HTTP 请求会并发写）。
    ///
    /// 目录优先级：环境变量 DBVIEW_LOG_DIR > 程序基目录/logs
    /// </summary>
    internal static class BackupLogger
    {
        private static readonly object Sync = new();

        /// <summary>单个日志文件大小上限（超过后滚动）</summary>
        private const long MaxFileSize = 10 * 1024 * 1024;

        /// <summary>日志保留天数</summary>
        private const int RetentionDays = 14;

        /// <summary>日志目录</summary>
        public static string LogDir
        {
            get
            {
                var custom = Environment.GetEnvironmentVariable("DBVIEW_LOG_DIR");
                if (!string.IsNullOrWhiteSpace(custom))
                {
                    return Path.GetFullPath(custom.Trim());
                }
                return Path.Combine(AppContext.BaseDirectory, "logs");
            }
        }

        private static string CurrentFile => Path.Combine(LogDir, $"backup-{DateTime.Now:yyyyMMdd}.log");

        /// <summary>写一条日志（同时输出到控制台）</summary>
        public static void Write(string message, Exception? ex = null)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {(ex == null ? "INFO" : "ERROR")} {message}";
            if (ex != null)
            {
                line += Environment.NewLine + ex;
            }

            lock (Sync)
            {
                try
                {
                    Directory.CreateDirectory(LogDir);
                    RollIfNeeded();
                    File.AppendAllText(CurrentFile, line + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                    // 日志写失败不应影响备份主流程
                }
            }
        }

        /// <summary>文件超过上限时滚动为 .1</summary>
        private static void RollIfNeeded()
        {
            var file = CurrentFile;
            if (!File.Exists(file))
            {
                return;
            }

            var info = new FileInfo(file);
            if (info.Length < MaxFileSize)
            {
                return;
            }

            var rolled = Path.Combine(LogDir, $"backup-{DateTime.Now:yyyyMMdd}.log.1");
            if (File.Exists(rolled))
            {
                File.Delete(rolled);
            }
            File.Move(file, rolled);
        }

        /// <summary>清理超过保留天数的日志文件（启动时调用一次）</summary>
        public static void CleanupOldLogs()
        {
            lock (Sync)
            {
                try
                {
                    if (!Directory.Exists(LogDir))
                    {
                        return;
                    }

                    var cutoff = DateTime.Now.AddDays(-RetentionDays);
                    foreach (var path in Directory.GetFiles(LogDir, "backup-*.log*"))
                    {
                        try
                        {
                            if (File.GetLastWriteTime(path) < cutoff)
                            {
                                File.Delete(path);
                            }
                        }
                        catch
                        {
                            // 单个文件清理失败不影响其它
                        }
                    }
                }
                catch
                {
                    // 忽略
                }
            }
        }
    }
}
