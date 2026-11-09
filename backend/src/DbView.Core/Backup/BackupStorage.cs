using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using DbView.Core.Models;

namespace DbView.Core.Backup
{
    /// <summary>
    /// 备份文件存储布局与元数据读写（Core 层，供 Infrastructure 写入、WebApi 读取/恢复共用）。
    ///
    /// 目录结构（按连接隔离，避免 A 连接看到/恢复 B 连接的备份）：
    ///   {程序基目录}/backups/{connectionId}/{name}.sql
    ///   {程序基目录}/backups/{connectionId}/{name}.json   ← 元数据旁车文件
    ///
    /// 写入约定：
    ///   先写 {name}.sql.tmp（完整内容 + SHA256）→ 再写 {name}.json → 最后把 .tmp 原子改名为 .sql。
    ///   因此"最终 .sql 存在"即代表"内容完整"，中途崩溃只会留下 .tmp（不会被列出，也不会被恢复）。
    /// </summary>
    public static class BackupStorage
    {
        /// <summary>当前备份格式版本</summary>
        public const int CurrentFormatVersion = 2;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        /// <summary>备份根目录</summary>
        public static string RootDir
        {
            get
            {
                // 可通过环境变量指向其它磁盘/挂载点（NAS、对象存储挂载目录等），便于异地留存
                var custom = Environment.GetEnvironmentVariable("DBVIEW_BACKUP_DIR");
                if (!string.IsNullOrWhiteSpace(custom))
                {
                    return Path.GetFullPath(custom.Trim());
                }
                return Path.Combine(AppContext.BaseDirectory, "backups");
            }
        }

        /// <summary>是否启用压缩（默认开启；PG 文本备份通常可压 5~10 倍）</summary>
        public static bool CompressionEnabled
        {
            get
            {
                var v = Environment.GetEnvironmentVariable("DBVIEW_BACKUP_COMPRESS");
                return string.IsNullOrWhiteSpace(v) || !string.Equals(v.Trim(), "0", StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// 计算负载文件名（不含路径）。
        /// 规则：{name}.sql → 压缩后 {name}.sql.gz → 加密后 {name}.sql.gz.enc
        /// </summary>
        public static string GetPayloadFileName(string baseName, bool compressed, bool encrypted)
        {
            var name = baseName + ".sql";
            if (compressed) { name += ".gz"; }
            if (encrypted) { name += ".enc"; }
            return name;
        }

        /// <summary>是否是备份负载文件（.sql / .sql.gz / .sql.gz.enc）</summary>
        public static bool IsPayloadFile(string fileName)
        {
            return fileName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".sql.gz", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".sql.gz.enc", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>从负载文件名还原基础名（去掉 .sql / .gz / .enc 后缀）</summary>
        public static string GetBaseName(string fileName)
        {
            var name = Path.GetFileName(fileName);
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var suffix in new[] { ".enc", ".gz", ".sql" })
                {
                    if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        name = name[..^suffix.Length];
                        changed = true;
                        break;
                    }
                }
            }
            return name;
        }

        /// <summary>
        /// 打开负载读取流，自动按需解密 + 解压，返回可直接读出 SQL 文本的流。
        /// </summary>
        public static Stream OpenPayloadRead(string path, bool compressed, bool encrypted)
        {
            var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
            Stream stream = fileStream;

            try
            {
                if (encrypted)
                {
                    var key = BackupCrypto.TryGetKey();
                    if (key == null)
                    {
                        throw new InvalidOperationException(
                            $"备份文件已加密，但未配置密钥（环境变量 {BackupCrypto.KeyEnvironmentVariable}），无法解密");
                    }
                    stream = BackupCrypto.CreateDecryptStream(stream, key);
                }

                if (compressed)
                {
                    stream = new GZipStream(stream, CompressionMode.Decompress);
                }

                return new BufferedStream(stream);
            }
            catch
            {
                stream.Dispose();
                fileStream.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 创建负载写入流，按需压缩 + 加密。调用方负责关闭流以完成落盘。
        /// </summary>
        public static Stream CreatePayloadWrite(string path, bool compressed, bool encrypted, byte[]? key)
        {
            var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
            Stream stream = fileStream;

            try
            {
                if (encrypted)
                {
                    if (key == null)
                    {
                        throw new InvalidOperationException(
                            $"已请求加密但未配置密钥（环境变量 {BackupCrypto.KeyEnvironmentVariable}）");
                    }
                    stream = BackupCrypto.CreateEncryptStream(stream, key);
                }

                if (compressed)
                {
                    stream = new GZipStream(stream, CompressionLevel.Optimal);
                }

                return new BufferedStream(stream);
            }
            catch
            {
                stream.Dispose();
                fileStream.Dispose();
                throw;
            }
        }

        /// <summary>某个连接的备份目录</summary>
        public static string GetConnectionDir(long connectionId)
            => Path.Combine(RootDir, connectionId.ToString(CultureInfo.InvariantCulture));

        /// <summary>确保某个连接的备份目录存在</summary>
        public static void EnsureConnectionDir(long connectionId)
            => Directory.CreateDirectory(GetConnectionDir(connectionId));

        /// <summary>备份 SQL 文件完整路径</summary>
        public static string GetSqlPath(long connectionId, string fileName)
            => Path.Combine(GetConnectionDir(connectionId), fileName);

        /// <summary>元数据（旁车）文件完整路径</summary>
        public static string GetMetadataPath(long connectionId, string fileName)
        {
            return Path.Combine(GetConnectionDir(connectionId), GetBaseName(fileName) + ".json");
        }

        /// <summary>计算文件内容的 SHA256（小写十六进制）</summary>
        public static string ComputeSha256(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>读取元数据；文件不存在或损坏时返回 null</summary>
        public static BackupMetadata? ReadMetadata(long connectionId, string fileName)
        {
            var path = GetMetadataPath(connectionId, fileName);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<BackupMetadata>(json, JsonOptions);
            }
            catch
            {
                // 元数据损坏不应阻断备份列表/恢复，退化为"无元数据"处理
                return null;
            }
        }

        /// <summary>写入元数据（原子：先写 .tmp 再改名）</summary>
        public static void WriteMetadata(BackupMetadata metadata)
        {
            var path = GetMetadataPath(metadata.ConnectionId, metadata.FileName);
            var tmpPath = path + ".tmp";
            var json = JsonSerializer.Serialize(metadata, JsonOptions);
            File.WriteAllText(tmpPath, json, new System.Text.UTF8Encoding(false));
            File.Move(tmpPath, path, overwrite: true);
        }

        /// <summary>删除备份的负载文件与 .json 元数据两个文件</summary>
        public static void DeleteFiles(long connectionId, string fileName)
        {
            TryDelete(GetSqlPath(connectionId, fileName));
            TryDelete(GetMetadataPath(connectionId, fileName));
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // 删除失败不影响主流程
            }
        }

        /// <summary>
        /// 列出某个连接的所有完整备份（只认最终 .sql，.tmp 视为未完成不会列出）。
        /// 元数据缺失时（老备份）用文件 CreationTime.Ticks 作为 Id 兜底。
        /// </summary>
        public static List<(FileInfo File, BackupMetadata Metadata)> ListBackups(long connectionId)
        {
            var result = new List<(FileInfo, BackupMetadata)>();
            var dir = GetConnectionDir(connectionId);
            if (!Directory.Exists(dir))
            {
                return result;
            }

            foreach (var path in Directory.GetFiles(dir))
            {
                var file = new FileInfo(path);
                if (!IsPayloadFile(file.Name))
                {
                    continue;
                }

                var meta = ReadMetadata(connectionId, file.Name);
                if (meta == null)
                {
                    // 老备份（无旁车元数据）：用创建时间兜底，校验和留空表示不校验
                    meta = new BackupMetadata
                    {
                        FormatVersion = 1,
                        Id = file.CreationTime.Ticks,
                        ConnectionId = connectionId,
                        Name = GetBaseName(file.Name),
                        FileName = file.Name,
                        CreatedAt = file.CreationTime,
                        FileSize = file.Exists ? file.Length : 0,
                        Compressed = file.Name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase),
                        Encrypted = file.Name.EndsWith(".enc", StringComparison.OrdinalIgnoreCase),
                        Completed = true
                    };
                }
                else if (!meta.Completed)
                {
                    // 明确标记为未完成的备份不可用于恢复
                    continue;
                }

                result.Add((file, meta));
            }

            return result;
        }

        /// <summary>
        /// 生成一个在该连接目录内唯一的备份 Id（基于 UTC ticks，冲突时递增）。
        /// </summary>
        public static long GenerateBackupId(long connectionId)
        {
            var dir = GetConnectionDir(connectionId);
            var used = new HashSet<long>();
            if (Directory.Exists(dir))
            {
                foreach (var path in Directory.GetFiles(dir, "*.json"))
                {
                    var meta = ReadMetadata(connectionId, Path.GetFileName(path));
                    if (meta != null)
                    {
                        used.Add(meta.Id);
                    }
                }
            }

            var id = DateTime.UtcNow.Ticks;
            while (used.Contains(id))
            {
                id++;
            }
            return id;
        }

        /// <summary>
        /// 一次性迁移旧版布局（所有备份平铺在 backups/ 根目录）：
        ///   auto_{connectionId}_xxx.sql → backups/{connectionId}/auto_{connectionId}_xxx.sql
        /// 无法从文件名判断归属的手动备份（backup_xxx.sql）保留在根目录，不会被列出，
        /// 以免张冠李戴地恢复到错误的库。
        /// </summary>
        /// <returns>迁移的文件数量</returns>
        public static int MigrateLegacyBackups()
        {
            if (!Directory.Exists(RootDir))
            {
                return 0;
            }

            var moved = 0;
            foreach (var path in Directory.GetFiles(RootDir))
            {
                var fileName = Path.GetFileName(path);
                if (!IsPayloadFile(fileName))
                {
                    continue;
                }

                var prefix = "auto_";
                if (!fileName.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                // auto_{connectionId}_{timestamp}.sql
                var rest = fileName.Substring(prefix.Length);
                var sep = rest.IndexOf('_');
                if (sep <= 0)
                {
                    continue;
                }

                if (!long.TryParse(rest.Substring(0, sep), NumberStyles.Integer, CultureInfo.InvariantCulture, out var connectionId))
                {
                    continue;
                }

                try
                {
                    EnsureConnectionDir(connectionId);
                    var target = GetSqlPath(connectionId, fileName);
                    if (File.Exists(target))
                    {
                        continue;
                    }

                    File.Move(path, target);

                    // 为迁移过来的备份补一份元数据（保留原 Id，避免前端已有引用失效）
                    var metaFile = GetMetadataPath(connectionId, fileName);
                    if (!File.Exists(metaFile))
                    {
                        var info = new FileInfo(target);
                        WriteMetadata(new BackupMetadata
                        {
                            FormatVersion = 1,
                            Id = info.CreationTime.Ticks,
                            ConnectionId = connectionId,
                            Name = Path.GetFileNameWithoutExtension(fileName),
                            FileName = fileName,
                            CreatedAt = info.CreationTime,
                            FileSize = info.Length,
                            Completed = true
                        });
                    }

                    moved++;
                }
                catch
                {
                    // 单个文件迁移失败不影响其它文件
                }
            }

            return moved;
        }
    }
}
