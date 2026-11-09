using System.Text;
using DbView.Core.Backup;
using DbView.Core.Models;
using DbView.Infrastructure.Services;
using Microsoft.Data.Sqlite;

// ===== 从本地配置库读取真实 PG 连接（只读）=====
var configDb = @"D:\work\tools\DbView\backend\src\DbView.WebApi\bin\Debug\net10.0\DbView.db";
using var cfg = new SqliteConnection($"Data Source={configDb}");
await cfg.OpenAsync();

var cmdCfg = cfg.CreateCommand();
cmdCfg.CommandText = @"SELECT Id, Name, Host, Port, DatabaseName, Username, Password
FROM dbview_connections WHERE DbType = 'postgresql' LIMIT 1";
using var r = await cmdCfg.ExecuteReaderAsync();
if (!await r.ReadAsync()) { Console.WriteLine("NO_PG_CONNECTION"); return 2; }

var connection = new Connection
{
    Id = r.GetInt64(0),
    Name = r.GetString(1),
    Host = r.GetString(2),
    Port = r.GetInt32(3),
    DatabaseName = r.GetString(4),
    Username = r.GetString(5),
    Password = r.IsDBNull(6) ? "" : r.GetString(6),
    DbType = "postgresql"
};
Console.WriteLine($"CONN name={connection.Name} host={connection.Host} port={connection.Port} db={connection.DatabaseName}");

var svc = new DatabaseService();
var vectorColumnCount = 0;
var failures = 0;
void Check(bool ok, string name, string detail = "")
{
    Console.WriteLine((ok ? "  [PASS] " : "  [FAIL] ") + name + (string.IsNullOrEmpty(detail) ? "" : " -> " + detail));
    if (!ok) failures++;
}

try
{
    // ===== 0. pgvector 扩展可用性 =====
    using (var probeConn = svc.CreateReadOnlyConnection(connection))
    {
        await probeConn.OpenAsync();
        using var c0 = probeConn.CreateCommand();
        c0.CommandText = "SELECT name, default_version, installed_version FROM pg_available_extensions WHERE name = 'vector'";
        using var r0 = await c0.ExecuteReaderAsync();
        if (await r0.ReadAsync())
        {
            Console.WriteLine($"PGVECTOR_AVAILABLE name={r0.GetString(0)} version={r0.GetString(1)} installed={(r0.IsDBNull(2) ? "no" : r0.GetString(2))}");
        }
        else
        {
            Console.WriteLine("PGVECTOR_AVAILABLE no");
        }
    }

    // ===== 1. 表清单 =====
    var tables = await svc.GetTablesAsync(connection);
    Check(tables.Count > 0, "PG 表清单查询", $"{tables.Count} 张表");
    foreach (var t in tables.Take(8)) Console.WriteLine($"         {t.SchemaName}.{t.TableName}");

    // ===== 2. 备份会话（REPEATABLE READ）=====
    await using var session = await svc.OpenBackupSessionAsync(connection);
    Check(true, "REPEATABLE READ 备份会话建立");

    // ===== 3. 向量列探测 =====
    using (var c = session.CreateCommand())
    {
        c.CommandText = @"SELECT table_schema, table_name, column_name, udt_name
FROM information_schema.columns
WHERE udt_name = 'vector' AND table_schema NOT IN ('pg_catalog','information_schema')
ORDER BY 1,2,3";
        using var rr = await c.ExecuteReaderAsync();
        vectorColumnCount = 0;
        while (await rr.ReadAsync())
        {
            vectorColumnCount++;
            Console.WriteLine($"         VECTOR {rr.GetString(0)}.{rr.GetString(1)}.{rr.GetString(2)} ({rr.GetString(3)})");
        }
        Console.WriteLine($"VECTOR_COLUMNS={vectorColumnCount}");
    }

    // ===== 4. 对象采集查询在真实 PG 上是否全部跑通 =====
    var objs = await svc.GetSchemaObjectsAsync(connection, default, session);
    Console.WriteLine($"OBJECTS Schemas={objs.Schemas.Count} Types={objs.Types.Count} Sequences={objs.Sequences.Count} " +
                      $"Indexes={objs.Indexes.Count} Constraints={objs.Constraints.Count} Views={objs.Views.Count} " +
                      $"Functions={objs.Functions.Count} Triggers={objs.Triggers.Count}");

    // 采集器把失败记进 Warnings，而不是抛异常 —— 必须逐条检查
    Check(objs.Warnings.Count == 0, "对象采集无失败/无告警",
          objs.Warnings.Count == 0 ? "" : string.Join(" | ", objs.Warnings));
    foreach (var w in objs.Warnings) Console.WriteLine("         WARN " + w);

    var vectorIndexes = objs.Indexes.Count(i =>
        i.Contains("hnsw", StringComparison.OrdinalIgnoreCase) ||
        i.Contains("ivfflat", StringComparison.OrdinalIgnoreCase));
    Console.WriteLine($"VECTOR_INDEXES={vectorIndexes}");
    foreach (var i in objs.Indexes.Where(i => i.Contains("hnsw", StringComparison.OrdinalIgnoreCase) || i.Contains("ivfflat", StringComparison.OrdinalIgnoreCase)).Take(5))
        Console.WriteLine("         IDX " + i);

    await session.DisposeAsync();

    // ===== 5. 真实执行完整备份 =====
    Console.WriteLine("== 开始真实备份（只读库，写本地文件）==");
    var backupService = new BackupService(svc);
    var name = "pgverify_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
    var info = await backupService.CreateBackupAsync(connection, name);
    Check(true, "完整备份执行成功", $"{info.FileName} {info.FileSize} bytes");

    var path = BackupStorage.GetSqlPath(connection.Id, info.FileName);
    var meta = BackupStorage.ReadMetadata(connection.Id, info.FileName);
    Check(meta != null && meta.Completed, "元数据完整");
    Console.WriteLine($"         tables={meta!.TableRowCounts.Count} rows={meta.TableRowCounts.Values.Sum()} " +
                      $"objects={string.Join(",", meta.SchemaObjectCounts.Select(kv => kv.Key + "=" + kv.Value))}");
    foreach (var w in meta.Warnings) Console.WriteLine("         BACKUP_WARN " + w);

    string text;
    using (var payload = BackupStorage.OpenPayloadRead(path, info.Compressed, info.Encrypted))
    using (var reader = new StreamReader(payload, Encoding.UTF8))
        text = await reader.ReadToEndAsync();

    // ===== 6. 备份内容关键断言 =====
    if (vectorColumnCount > 0)
    {
        Check(text.Contains("CREATE EXTENSION IF NOT EXISTS vector"), "备份头含 CREATE EXTENSION vector");
        Check(text.Contains("vector", StringComparison.OrdinalIgnoreCase), "备份含 vector 列定义");
    }
    else
    {
        Check(!text.Contains("CREATE EXTENSION IF NOT EXISTS vector"),
              "本库无向量列时不写 CREATE EXTENSION（按需生成）");
    }
    Check(text.Contains("DROP TABLE IF EXISTS") && text.Contains("CASCADE"), "DROP TABLE 带 CASCADE（避免外键阻塞）");
    Check(text.Contains("CREATE SCHEMA IF NOT EXISTS") == false ||
          text.IndexOf("CREATE SCHEMA IF NOT EXISTS", StringComparison.Ordinal) <
          Math.Max(0, text.IndexOf("CREATE SEQUENCE", StringComparison.Ordinal)),
          "schema 先于序列（若有序列）");

    if (vectorIndexes > 0)
    {
        Check(text.Contains("hnsw", StringComparison.OrdinalIgnoreCase) ||
              text.Contains("ivfflat", StringComparison.OrdinalIgnoreCase),
              "备份含向量索引定义（HNSW/IVFFlat）");
    }

    // 备份文件中的顺序：EXTENSION -> 表 -> 索引
    var extPos = text.IndexOf("CREATE EXTENSION", StringComparison.Ordinal);
    var tblPos = text.IndexOf("CREATE TABLE", StringComparison.Ordinal);
    var idxPos = text.IndexOf("CREATE INDEX", StringComparison.Ordinal);
    if (vectorColumnCount > 0)
    {
        Check(extPos >= 0 && tblPos > extPos, "扩展在表之前");
    }
    else
    {
        Check(tblPos > 0, "表 DDL 存在");
    }
    Console.WriteLine($"         POSITIONS ext={extPos} table={tblPos} index={idxPos}");

    Console.WriteLine($"BACKUP_FILE={path}");
    Console.WriteLine($"BACKUP_LINES={text.Split('\n').Length}");
}
catch (Exception ex)
{
    Console.WriteLine("FATAL: " + ex);
    failures++;
}

Console.WriteLine(failures == 0 ? "===== 真实 PG 验证全部通过 =====" : $"===== 有 {failures} 项失败 =====");
return failures == 0 ? 0 : 1;
