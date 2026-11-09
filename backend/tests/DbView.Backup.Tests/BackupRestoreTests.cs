using System.Globalization;
using System.Text;
using DbView.Core.Abstractions;
using DbView.Core.Backup;
using DbView.Core.Models;
using DbView.Infrastructure.Services;
using DbView.WebApi.Features.Backup;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DbView.Backup.Tests;

/// <summary>
/// 备份/恢复的生产级回归测试，覆盖：
/// 原子写入、按连接隔离、元数据+SHA256、行数清单、不变文化数值编码、
/// 二进制按方言编码、流式恢复、事务回滚、SQL 拆分器正确性、
/// 数据库对象（索引/视图/触发器/约束）纳入备份、压缩、加密。
/// </summary>
// 同一个 Collection 内的用例串行执行：加密用例会修改进程级环境变量，不能与其它用例并行
[Collection("Backup")]
public class BackupRestoreTests : IDisposable
{
    private static long _seq = 880001;

    private readonly string _workDir;
    private readonly string _dbPath;
    private readonly Connection _connection;
    private readonly DatabaseService _dbService = new();
    private readonly BackupService _backupService;

    public BackupRestoreTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(), "dbview_bt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDir);
        _dbPath = Path.Combine(_workDir, "test.db");
        // 每个用例使用独立的连接 Id：备份按连接分目录存放，避免用例之间互相污染
        // （用随机值而非递增，避免上次运行的残留文件落到同一目录）
        var connId = Random.Shared.NextInt64(1_000_000, 9_000_000);
        _connection = new Connection { Id = connId, Name = "test", DbType = "sqlite", DatabaseName = _dbPath };
        _backupService = new BackupService(_dbService);

        Execute(@"
CREATE TABLE ""items"" (
  ""id""      INTEGER NOT NULL,
  ""name""    TEXT NULL,
  ""price""   REAL NULL,
  ""qty""     INTEGER NULL,
  ""payload"" BLOB NULL,
  ""note""    TEXT NULL,
  PRIMARY KEY (""id"")
);
INSERT INTO ""items"" VALUES (1, 'O''Brien; with semicolon', 1.5, 10, X'00FF10', 'multi
line -- not a comment');
INSERT INTO ""items"" VALUES (2, '中文测试', 0.25, 3, NULL, NULL);
INSERT INTO ""items"" VALUES (3, 'quote''s and -- dashes', 2.75, 7, X'DEADBEEF', 'end');
");
    }

    public void Dispose()
    {
        try { Directory.Delete(_workDir, true); } catch { /* ignore */ }
    }

    private void Execute(string sql)
    {
        using var sqlite = new SqliteConnection($"Data Source={_dbPath}");
        sqlite.Open();
        using var cmd = sqlite.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private T Scalar<T>(string sql)
    {
        using var sqlite = new SqliteConnection($"Data Source={_dbPath}");
        sqlite.Open();
        using var cmd = sqlite.CreateCommand();
        cmd.CommandText = sql;
        var v = cmd.ExecuteScalar();
        return v == null || v == DBNull.Value ? default : (T)Convert.ChangeType(v, typeof(T));
    }

    private byte[] Blob(string sql)
    {
        using var sqlite = new SqliteConnection($"Data Source={_dbPath}");
        sqlite.Open();
        using var cmd = sqlite.CreateCommand();
        cmd.CommandText = sql;
        return (byte[])cmd.ExecuteScalar();
    }

    /// <summary>备份负载的完整路径</summary>
    private string PayloadPath(BackupFileInfo info) => BackupStorage.GetSqlPath(_connection.Id, info.FileName);

    private static bool IsCompressed(BackupFileInfo info) => info.Compressed;

    private static bool IsEncrypted(BackupFileInfo info) => info.Encrypted;

    /// <summary>读取备份负载文本（自动处理压缩/加密）</summary>
    private async Task<string> ReadBackupTextAsync(BackupFileInfo info)
    {
        using var payload = BackupStorage.OpenPayloadRead(PayloadPath(info), IsCompressed(info), IsEncrypted(info));
        using var reader = new StreamReader(payload, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    /// <summary>按备份文件恢复（自动处理压缩/加密）</summary>
    private async Task<(int Executed, int Skipped)> RestoreAsync(BackupFileInfo info)
    {
        using var payload = BackupStorage.OpenPayloadRead(PayloadPath(info), IsCompressed(info), IsEncrypted(info));
        using var reader = new StreamReader(payload, Encoding.UTF8);
        var splitter = new SqlStatementSplitter(reader);
        return await _dbService.ExecuteBatchAsync(_connection, splitter.SplitAsync(), default);
    }

    /// <summary>按纯文本 SQL 文件恢复（用于构造失败场景）</summary>
    private async Task<(int Executed, int Skipped)> RestoreTextAsync(string sqlPath)
    {
        using var reader = new StreamReader(sqlPath, Encoding.UTF8);
        var splitter = new SqlStatementSplitter(reader);
        return await _dbService.ExecuteBatchAsync(_connection, splitter.SplitAsync(), default);
    }

    [Fact]
    public async Task Backup_WritesAtomically_WithMetadataChecksumAndRowCounts()
    {
        var info = await _backupService.CreateBackupAsync(_connection, "case1");

        var sqlPath = PayloadPath(info);
        Assert.True(File.Exists(sqlPath));
        Assert.False(File.Exists(sqlPath + ".tmp"));
        Assert.True(File.Exists(BackupStorage.GetMetadataPath(_connection.Id, info.FileName)));

        var meta = BackupStorage.ReadMetadata(_connection.Id, info.FileName);
        Assert.NotNull(meta);
        Assert.True(meta!.Completed);
        Assert.Equal(BackupStorage.ComputeSha256(sqlPath), meta.Sha256);
        Assert.Equal(3, meta.TableRowCounts["main.items"]);
        Assert.Equal(meta.Id, info.Id);
    }

    [Fact]
    public async Task Backup_EncodesNumbersWithInvariantCulture()
    {
        var prev = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var info = await _backupService.CreateBackupAsync(_connection, "case2");
            var text = await ReadBackupTextAsync(info);
            Assert.Contains("1.5", text);
            Assert.DoesNotContain("1,5", text);
        }
        finally
        {
            CultureInfo.CurrentCulture = prev;
        }
    }

    [Fact]
    public async Task Backup_EncodesBinaryWithDialectSpecificLiteral()
    {
        var info = await _backupService.CreateBackupAsync(_connection, "case3");
        var text = await ReadBackupTextAsync(info);
        Assert.Contains("X'00FF10'", text);
    }

    [Fact]
    public async Task Restore_RoundTripsDataExactly()
    {
        var info = await _backupService.CreateBackupAsync(_connection, "case4");

        // 篡改数据：删掉一行、插入一行不同的
        Execute("DELETE FROM \"items\" WHERE id = 2; INSERT INTO \"items\" VALUES (99, 'tampered', 9.99, 9, NULL, NULL);");
        Assert.Equal(3, await _dbService.GetTableRowCountAsync(_connection, "items"));

        var (executed, _) = await RestoreAsync(info);
        Assert.True(executed > 0);

        // 原始数据完整还原，篡改数据被覆盖
        Assert.Equal(1, Scalar<long>("SELECT COUNT(*) FROM \"items\" WHERE id = 2 AND name = '中文测试'"));
        Assert.Equal(0, Scalar<long>("SELECT COUNT(*) FROM \"items\" WHERE id = 99"));
        Assert.Equal(1.5d, Scalar<double>("SELECT price FROM \"items\" WHERE id = 1"), 6);
        Assert.Equal(new byte[] { 0x00, 0xFF, 0x10 }, Blob("SELECT payload FROM \"items\" WHERE id = 1"));

        // 行数清单逐表比对无差异（证明没有丢数据）
        var meta = BackupStorage.ReadMetadata(_connection.Id, info.FileName)!;
        foreach (var kv in meta.TableRowCounts)
        {
            var dot = kv.Key.IndexOf('.');
            var schema = dot > 0 ? kv.Key.Substring(0, dot) : null;
            var table = dot > 0 ? kv.Key.Substring(dot + 1) : kv.Key;
            Assert.Equal(kv.Value, await _dbService.GetTableRowCountAsync(_connection, table, schema));
        }
    }

    [Fact]
    public async Task Restore_RollsBackWhenAStatementFails()
    {
        var badPath = Path.Combine(_workDir, "bad.sql");
        await File.WriteAllTextAsync(badPath,
            "DROP TABLE IF EXISTS \"items\";\nCREATE TABLE \"items\" (\"id\" INTEGER);\nINSERT INTO \"no_such_table\" VALUES (1);\n");

        await Assert.ThrowsAnyAsync<Exception>(async () => await RestoreTextAsync(badPath));

        // 回滚后原表与数据完好，不会出现"表被重建、数据只剩一半"
        Assert.Equal(3, Scalar<long>("SELECT COUNT(*) FROM \"items\""));
    }

    [Fact]
    public async Task Splitter_IgnoresComments_AndKeepsSemicolonsInsideLiterals()
    {
        var tricky = "-- 行注释; 不该被当成语句\n/* 块注释 ; */\nINSERT INTO \"t\" VALUES ('a;b');\nCREATE FUNCTION f() RETURNS int AS $$ BEGIN RETURN 1; END; $$ LANGUAGE plpgsql;\n";
        var parts = new List<string>();
        using (var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(tricky)), Encoding.UTF8))
        {
            var splitter = new SqlStatementSplitter(reader);
            await foreach (var s in splitter.SplitAsync()) { parts.Add(s); }
        }

        Assert.Equal(2, parts.Count);
        Assert.Contains("'a;b'", parts[0]);
        Assert.Contains("$$ BEGIN RETURN 1; END; $$", parts[1]);
        Assert.DoesNotContain(parts, p => p.TrimStart().StartsWith("--"));
    }

    [Fact]
    public async Task Splitter_KeepsTriggerBodyBlockIntact()
    {
        // SQLite / MySQL 触发器体是 BEGIN ... END; 块，内部的分号不是语句结束符
        var sql = "CREATE TRIGGER t AFTER INSERT ON x BEGIN UPDATE x SET q = q WHERE id = NEW.id; END;\nSELECT 1;\n";
        var parts = new List<string>();
        using (var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(sql)), Encoding.UTF8))
        {
            var splitter = new SqlStatementSplitter(reader);
            await foreach (var s in splitter.SplitAsync()) { parts.Add(s); }
        }

        Assert.Equal(2, parts.Count);
        Assert.Contains("BEGIN UPDATE x SET q = q WHERE id = NEW.id; END", parts[0]);
    }

    [Fact]
    public async Task Backups_AreIsolatedPerConnection()
    {
        var info = await _backupService.CreateBackupAsync(_connection, "case7");

        var mine = BackupStorage.ListBackups(_connection.Id);
        Assert.Single(mine);
        Assert.Equal(info.Id, mine[0].Metadata.Id);

        // 用一个必然没有备份的连接号
        var other = BackupStorage.ListBackups(_connection.Id + 500_000_000);
        Assert.Empty(other);
    }

    [Fact]
    public async Task Backup_IncludesIndexesViewsAndTriggers()
    {
        Execute(@"
CREATE TABLE ""items2"" (""id"" INTEGER PRIMARY KEY, ""code"" TEXT UNIQUE, ""qty"" INTEGER);
CREATE INDEX ""idx_items2_qty"" ON ""items2"" (""qty"");
CREATE VIEW ""v_items2"" AS SELECT ""id"" FROM ""items2"";
CREATE TRIGGER ""trg_items2"" AFTER INSERT ON ""items2"" BEGIN UPDATE ""items2"" SET ""qty"" = ""qty"" WHERE ""id"" = NEW.""id""; END;
INSERT INTO ""items2"" VALUES (1, 'a', 5);
");

        var info = await _backupService.CreateBackupAsync(_connection, "case_objects");
        var text = await ReadBackupTextAsync(info);

        Assert.Contains("CREATE INDEX", text);
        Assert.Contains("idx_items2_qty", text);
        Assert.Contains("CREATE VIEW", text);
        Assert.Contains("v_items2", text);
        Assert.Contains("CREATE TRIGGER", text);
        Assert.Contains("trg_items2", text);
        // UNIQUE 约束来自 sqlite_master 的原始建表语句，应被完整保留
        Assert.Contains("UNIQUE", text);

        // 恢复后对象都应存在（触发器体内的分号不能把语句切断）
        await RestoreAsync(info);
        Assert.Equal(1, Scalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'idx_items2_qty'"));
        Assert.Equal(1, Scalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'view' AND name = 'v_items2'"));
        Assert.Equal(1, Scalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'trigger' AND name = 'trg_items2'"));
    }

    [Fact]
    public async Task Backup_CompressesPayload_AndRestoreStillWorks()
    {
        var info = await _backupService.CreateBackupAsync(_connection, "case_compress");

        // 默认开启压缩：文件名以 .sql.gz 结尾
        Assert.EndsWith(".sql.gz", info.FileName);
        Assert.True(File.Exists(PayloadPath(info)));

        var text = await ReadBackupTextAsync(info);
        Assert.Contains("CREATE TABLE", text);
        Assert.Contains("O''Brien", text);

        var (executed, _) = await RestoreAsync(info);
        Assert.True(executed > 0);
        Assert.Equal(3, Scalar<long>("SELECT COUNT(*) FROM \"items\""));
    }

    [Fact]
    public async Task Backup_EncryptsPayload_WhenKeyConfigured()
    {
        var prevKey = Environment.GetEnvironmentVariable(BackupCrypto.KeyEnvironmentVariable);
        try
        {
            // 32 字节 AES-256 密钥
            Environment.SetEnvironmentVariable(BackupCrypto.KeyEnvironmentVariable,
                Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray()));

            var info = await _backupService.CreateBackupAsync(_connection, "case_encrypt");
            Assert.EndsWith(".sql.gz.enc", info.FileName);

            // 密文中不应出现明文
            var raw = await File.ReadAllTextAsync(PayloadPath(info), Encoding.UTF8);
            Assert.DoesNotContain("CREATE TABLE", raw);

            // 解密后能还原
            var text = await ReadBackupTextAsync(info);
            Assert.Contains("CREATE TABLE", text);
        }
        finally
        {
            Environment.SetEnvironmentVariable(BackupCrypto.KeyEnvironmentVariable, prevKey);
        }
    }

    [Fact]
    public async Task Backup_EmitsObjectsInSafeOrder()
    {
        Execute(@"
CREATE TABLE ""ord"" (""id"" INTEGER PRIMARY KEY, ""v"" TEXT);
CREATE INDEX ""idx_ord_v"" ON ""ord"" (""v"");
CREATE VIEW ""v_ord"" AS SELECT ""id"" FROM ""ord"";
CREATE TRIGGER ""trg_ord"" AFTER INSERT ON ""ord"" BEGIN UPDATE ""ord"" SET ""v"" = ""v"" WHERE ""id"" = NEW.""id""; END;
INSERT INTO ""ord"" VALUES (1, 'x');
");

        var info = await _backupService.CreateBackupAsync(_connection, "case_order");
        var text = await ReadBackupTextAsync(info);

        // 索引必须在数据之后（先灌数据再建索引，恢复更快且唯一索引不冲突）
        var insertPos = text.LastIndexOf("INSERT INTO \"ord\"", StringComparison.Ordinal);
        var indexPos = text.IndexOf("CREATE INDEX \"idx_ord_v\"", StringComparison.Ordinal);
        Assert.True(insertPos > 0 && indexPos > insertPos, "索引应位于数据之后");

        // 视图/触发器在表之后
        var createTablePos = text.IndexOf("CREATE TABLE \"ord\"", StringComparison.Ordinal);
        Assert.True(createTablePos > 0);
        Assert.True(text.IndexOf("CREATE VIEW", StringComparison.Ordinal) > createTablePos);
        Assert.True(text.IndexOf("CREATE TRIGGER", StringComparison.Ordinal) > createTablePos);
    }

    [Fact]
    public async Task Restore_IsIdempotent_CanRunTwice()
    {
        var info = await _backupService.CreateBackupAsync(_connection, "case_idem");

        // 连续恢复两次都应成功（对象 DROP 前缀保证不会因"已存在"失败）
        await RestoreAsync(info);
        await RestoreAsync(info);

        Assert.Equal(3, Scalar<long>("SELECT COUNT(*) FROM \"items\""));
        Assert.Equal(1, Scalar<long>("SELECT COUNT(*) FROM \"items\" WHERE id = 2 AND name = '中文测试'"));
    }
}
