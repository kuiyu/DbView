using DbView.Core.Models;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;

namespace DbView.Infrastructure.Services
{
    /// <summary>
    /// 采集数据库对象（序列/函数/视图/索引/约束/触发器）的 DDL。
    ///
    /// 为什么必须采集：只备份"表结构 + 数据"恢复出来的库是"能跑但不对"的——
    /// 索引（含 pgvector 的 HNSW/IVFFlat 向量索引）丢失会让向量检索退化为全表扫描，
    /// 外键/唯一约束丢失会让数据完整性失效，序列丢失会让 serial 列无法插入新行。
    ///
    /// 设计约束：
    /// - 任何一类对象采集失败都只记录 Warning，绝不中断备份（备份优先级高于对象完整性）；
    /// - PostgreSQL 用原生函数生成 DDL（pg_indexes / pg_get_constraintdef / pg_get_viewdef /
    ///   pg_get_functiondef / pg_get_triggerdef），能完整还原包括向量索引在内的定义；
    /// - SQLite 直接取 sqlite_master.sql（原始建表/索引/视图/触发器语句，最完整）；
    /// - SQL Server 用 sys.indexes / sys.foreign_keys / sys.sql_modules；
    /// - MySQL 用 information_schema.STATISTICS / KEY_COLUMN_USAGE / VIEWS / TRIGGERS；
    /// - Oracle 用 DBMS_METADATA.GET_DDL。
    /// </summary>
    public static class SchemaObjectCollector
    {
        public static async Task<BackupSchemaObjects> CollectAsync(DbConnection conn, string dbType, CancellationToken cancellationToken = default)
        {
            var result = new BackupSchemaObjects();
            try
            {
                switch (dbType.ToLower())
                {
                    case "postgresql":
                        await CollectPostgreSqlAsync(conn, result, cancellationToken);
                        break;
                    case "mysql":
                        await CollectMySqlAsync(conn, result, cancellationToken);
                        break;
                    case "sqlite":
                        await CollectSqliteAsync(conn, result, cancellationToken);
                        break;
                    case "sqlserver":
                        await CollectSqlServerAsync(conn, result, cancellationToken);
                        break;
                    case "oracle":
                        await CollectOracleAsync(conn, result, cancellationToken);
                        break;
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"采集数据库对象失败：{ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 执行查询并取第一列的字符串集合（兼容 CLOB/BLOB 等非 string 返回类型）。
        /// </summary>
        private static async Task<List<string>> QueryStringsAsync(DbConnection conn, string sql, CancellationToken cancellationToken)
        {
            var list = new List<string>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.IsDBNull(0))
                {
                    continue;
                }

                var s = ReadAsText(reader.GetValue(0));
                if (!string.IsNullOrWhiteSpace(s))
                {
                    list.Add(s.Trim());
                }
            }
            return list;
        }

        private static string ReadAsText(object value)
        {
            switch (value)
            {
                case string s:
                    return s;
                case byte[] bytes:
                    return Encoding.UTF8.GetString(bytes);
                case DBNull _:
                    return string.Empty;
            }

            // Oracle CLOB 等大对象类型
            if (value.GetType().Name == "OracleClob")
            {
                try
                {
                    var prop = value.GetType().GetProperty("Value");
                    return prop?.GetValue(value)?.ToString() ?? string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        /// <summary>
        /// 执行查询；失败时记录警告并返回空集合（不中断备份）。
        /// </summary>
        private static async Task<List<string>> TryQueryAsync(DbConnection conn, BackupSchemaObjects result, string label, string sql, CancellationToken ct)
        {
            try
            {
                return await QueryStringsAsync(conn, sql, ct);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"{label}采集失败：{ex.Message}");
                return new List<string>();
            }
        }

        // ==================== PostgreSQL ====================

        private static async Task CollectPostgreSqlAsync(DbConnection conn, BackupSchemaObjects result, CancellationToken ct)
        {
            // 序列：必须在建表之前恢复 —— serial 列的 DEFAULT nextval('seq'::regclass) 依赖序列已存在，
            // 否则恢复空库时 CREATE TABLE 会直接失败。
            result.Sequences = await TryQueryAsync(conn, result, "PostgreSQL 序列", @"
SELECT 'CREATE SEQUENCE IF NOT EXISTS ' || quote_ident(schemaname) || '.' || quote_ident(sequencename)
       || ' INCREMENT BY ' || increment_by
       || ' MINVALUE ' || COALESCE(min_value::text, '1')
       || ' MAXVALUE ' || COALESCE(max_value::text, '9223372036854775807')
       || ' START WITH ' || COALESCE(start_value::text, '1')
       || ' CACHE ' || cache_size
       || CASE WHEN cycle THEN ' CYCLE' ELSE '' END
       || ';'
FROM pg_sequences
WHERE schemaname IN (SELECT nspname FROM pg_namespace WHERE nspname NOT IN ('pg_catalog','information_schema') AND nspname NOT LIKE 'pg_toast%' AND nspname NOT LIKE 'pg_temp%')", ct);

            // schema 列表：序列/表/视图可能落在非 public 的 schema 里，恢复时必须先建出来
            result.Schemas = await TryQueryAsync(conn, result, "PostgreSQL schema", @"
SELECT nspname FROM pg_namespace
WHERE nspname NOT LIKE 'pg_%' AND nspname <> 'information_schema'
ORDER BY nspname", ct);

            // 用户自定义 ENUM 类型：列类型引用它时，恢复阶段类型不存在会导致 CREATE TABLE 失败。
            // 用 DO 块容错（类型已存在时跳过），避免重复恢复时报 duplicate_object 中断整批。
            result.Types = await TryQueryAsync(conn, result, "PostgreSQL ENUM 类型", @"
SELECT 'DO $dbview$ BEGIN CREATE TYPE ' || quote_ident(n.nspname) || '.' || quote_ident(t.typname)
       || ' AS ENUM (' || string_agg(quote_literal(e.enumlabel), ', ' ORDER BY e.enumsortorder) || ');'
       || ' EXCEPTION WHEN duplicate_object THEN NULL; END $dbview$;'
FROM pg_type t
JOIN pg_namespace n ON n.oid = t.typnamespace
JOIN pg_enum e ON e.enumtypid = t.oid
WHERE n.nspname IN (SELECT nspname FROM pg_namespace WHERE nspname NOT IN ('pg_catalog','information_schema') AND nspname NOT LIKE 'pg_toast%' AND nspname NOT LIKE 'pg_temp%')
GROUP BY n.nspname, t.typname
ORDER BY n.nspname, t.typname", ct);

            // 分区表：本轮只备份"结构 + 数据"，不重建分区定义（PARTITION BY），
            // 恢复后该表会退化为普通表（数据不丢，但没有分区裁剪）。这里只做告警提示。
            var partitioned = await TryQueryAsync(conn, result, "PostgreSQL 分区表", @"
SELECT n.nspname || '.' || c.relname
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'p' AND n.nspname IN (SELECT nspname FROM pg_namespace WHERE nspname NOT IN ('pg_catalog','information_schema') AND nspname NOT LIKE 'pg_toast%' AND nspname NOT LIKE 'pg_temp%')
ORDER BY 1", ct);
            foreach (var p in partitioned)
            {
                result.Warnings.Add($"分区表 {p} 将按普通表恢复（不重建 PARTITION BY，数据不丢但失去分区裁剪）");
            }

            // 索引：排除"属于约束"的索引（它们由约束语句重建），其余全部保留。
            // 注意不能按 indisunique 排除 —— 独立创建的 CREATE UNIQUE INDEX 不是约束，
            // 若被漏掉会让恢复后的库丢失唯一性约束（数据完整性风险）。
            // 这里保留的当然也包含 pgvector 的 HNSW / IVFFlat 向量索引 —— 向量检索性能的关键。
            result.Indexes = await TryQueryAsync(conn, result, "PostgreSQL 索引", @"
SELECT i.indexdef
FROM pg_indexes i
JOIN pg_class t ON t.relname = i.tablename
JOIN pg_namespace tn ON tn.oid = t.relnamespace AND tn.nspname = i.schemaname
JOIN pg_class ic ON ic.relname = i.indexname
JOIN pg_namespace ins ON ins.oid = ic.relnamespace AND ins.nspname = i.schemaname
JOIN pg_index ix ON ix.indexrelid = ic.oid
WHERE i.schemaname IN (SELECT nspname FROM pg_namespace WHERE nspname NOT IN ('pg_catalog','information_schema') AND nspname NOT LIKE 'pg_toast%' AND nspname NOT LIKE 'pg_temp%')
  AND i.tablename IS NOT NULL
  AND NOT ix.indisprimary
  AND NOT EXISTS (SELECT 1 FROM pg_constraint k WHERE k.conindid = ic.oid)
ORDER BY i.schemaname, i.tablename, i.indexname", ct);

            // 唯一约束 / 外键 / CHECK 约束
            result.Constraints = await TryQueryAsync(conn, result, "PostgreSQL 约束", @"
SELECT 'ALTER TABLE ' || quote_ident(n.nspname) || '.' || quote_ident(c.relname)
       || ' ADD CONSTRAINT ' || quote_ident(con.conname) || ' ' || pg_get_constraintdef(con.oid) || ';'
FROM pg_constraint con
JOIN pg_class c ON c.oid = con.conrelid
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE con.contype IN ('u', 'f', 'c')
  AND n.nspname IN (SELECT nspname FROM pg_namespace WHERE nspname NOT IN ('pg_catalog','information_schema') AND nspname NOT LIKE 'pg_toast%' AND nspname NOT LIKE 'pg_temp%')
ORDER BY n.nspname, c.relname, con.conname", ct);

            // 视图与物化视图。视图是"独立对象"（DROP TABLE 不会删掉它），
            // 因此必须先 DROP VIEW IF EXISTS，否则在已有该视图的库上恢复会因重名失败。
            result.Views = await TryQueryAsync(conn, result, "PostgreSQL 视图", @"
SELECT 'DROP VIEW IF EXISTS ' || quote_ident(n.nspname) || '.' || quote_ident(c.relname) || ';' || CHR(10) ||
       'CREATE VIEW ' || quote_ident(n.nspname) || '.' || quote_ident(c.relname) || ' AS ' || pg_get_viewdef(c.oid, true) || ';'
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'v' AND n.nspname IN (SELECT nspname FROM pg_namespace WHERE nspname NOT IN ('pg_catalog','information_schema') AND nspname NOT LIKE 'pg_toast%' AND nspname NOT LIKE 'pg_temp%')
UNION ALL
SELECT 'DROP MATERIALIZED VIEW IF EXISTS ' || quote_ident(n.nspname) || '.' || quote_ident(c.relname) || ';' || CHR(10) ||
       'CREATE MATERIALIZED VIEW ' || quote_ident(n.nspname) || '.' || quote_ident(c.relname) || ' AS ' || pg_get_viewdef(c.oid, true) || ';'
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'm' AND n.nspname IN (SELECT nspname FROM pg_namespace WHERE nspname NOT IN ('pg_catalog','information_schema') AND nspname NOT LIKE 'pg_toast%' AND nspname NOT LIKE 'pg_temp%')", ct);

            // 函数（prokind='f' 标量/普通函数；聚合函数 prokind='a' 不单独备份）
            // 必须排除"属于扩展"的函数（pg_depend.deptype='e'）：
            // pgvector 会在 public 下创建上百个函数，若把它们写进备份，
            // 恢复时 CREATE EXTENSION 已经建好，再重复 CREATE OR REPLACE 既冗余又可能覆盖扩展实现。
            result.Functions = await TryQueryAsync(conn, result, "PostgreSQL 函数", @"
SELECT pg_get_functiondef(p.oid)
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE p.prokind = 'f'
  AND n.nspname IN (SELECT nspname FROM pg_namespace WHERE nspname NOT IN ('pg_catalog','information_schema') AND nspname NOT LIKE 'pg_toast%' AND nspname NOT LIKE 'pg_temp%')
  AND NOT EXISTS (
      SELECT 1 FROM pg_depend d
      WHERE d.objid = p.oid AND d.classid = 'pg_proc'::regclass AND d.deptype = 'e'
  )
ORDER BY n.nspname, p.proname", ct);

            // 触发器（排除内部约束触发器）
            result.Triggers = await TryQueryAsync(conn, result, "PostgreSQL 触发器", @"
SELECT pg_get_triggerdef(t.oid, true)
FROM pg_trigger t
JOIN pg_class c ON c.oid = t.tgrelid
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE NOT t.tgisinternal AND n.nspname IN (SELECT nspname FROM pg_namespace WHERE nspname NOT IN ('pg_catalog','information_schema') AND nspname NOT LIKE 'pg_toast%' AND nspname NOT LIKE 'pg_temp%')
ORDER BY n.nspname, c.relname, t.tgname", ct);
        }
        // ==================== SQLite ====================

        private static async Task CollectSqliteAsync(DbConnection conn, BackupSchemaObjects result, CancellationToken ct)
        {
            // sqlite_master.sql 保存的就是原始建表/索引/视图/触发器语句，最完整
            // 视图是独立对象（DROP TABLE 不会删掉），需要先 DROP VIEW IF EXISTS
            var objects = await TryQueryAsync(conn, result, "SQLite 对象", @"
SELECT type || '|' || name || '|' || replace(sql, char(10), ' ')
FROM sqlite_master
WHERE sql IS NOT NULL
  AND type IN ('index', 'view', 'trigger')
  AND name NOT LIKE 'sqlite_%'
ORDER BY type, name", ct);

            foreach (var item in objects)
            {
                var parts = item.Split('|', 3);
                if (parts.Length < 3)
                {
                    continue;
                }

                var type = parts[0];
                var name = parts[1];
                var sql = parts[2];

                if (type == "index")
                {
                    result.Indexes.Add(sql);
                }
                else if (type == "view")
                {
                    result.Views.Add($"DROP VIEW IF EXISTS \"{name.Replace("\"", "\"\"")}\";\n{sql}");
                }
                else if (type == "trigger")
                {
                    result.Triggers.Add(sql);
                }
            }
        }

        // ==================== MySQL ====================

        private static async Task CollectMySqlAsync(DbConnection conn, BackupSchemaObjects result, CancellationToken ct)
        {
            result.Indexes = await TryQueryAsync(conn, result, "MySQL 索引", @"
SELECT CONCAT('CREATE ', IF(NON_UNIQUE = 0, 'UNIQUE ', ''), 'INDEX `', INDEX_NAME, '` ON `', TABLE_NAME, '` (',
       GROUP_CONCAT(CONCAT('`', COLUMN_NAME, '`') ORDER BY SEQ_IN_INDEX SEPARATOR ', '), ');')
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND INDEX_NAME <> 'PRIMARY'
GROUP BY TABLE_NAME, INDEX_NAME, NON_UNIQUE
ORDER BY TABLE_NAME, INDEX_NAME", ct);

            result.Constraints = await TryQueryAsync(conn, result, "MySQL 外键", @"
SELECT CONCAT('ALTER TABLE `', rc.TABLE_NAME, '` ADD CONSTRAINT `', rc.CONSTRAINT_NAME, '` FOREIGN KEY (',
       (SELECT GROUP_CONCAT(CONCAT('`', kcu.COLUMN_NAME, '`') ORDER BY kcu.ORDINAL_POSITION SEPARATOR ', ')
        FROM information_schema.KEY_COLUMN_USAGE kcu
        WHERE kcu.CONSTRAINT_SCHEMA = rc.CONSTRAINT_SCHEMA AND kcu.CONSTRAINT_NAME = rc.CONSTRAINT_NAME
          AND kcu.REFERENCED_TABLE_NAME IS NOT NULL),
       ') REFERENCES `', rc.REFERENCED_TABLE_NAME, '` (',
       (SELECT GROUP_CONCAT(CONCAT('`', kcu.COLUMN_NAME, '`') ORDER BY kcu.ORDINAL_POSITION SEPARATOR ', ')
        FROM information_schema.KEY_COLUMN_USAGE kcu
        WHERE kcu.CONSTRAINT_SCHEMA = rc.CONSTRAINT_SCHEMA AND kcu.CONSTRAINT_NAME = rc.CONSTRAINT_NAME
          AND kcu.REFERENCED_TABLE_NAME IS NOT NULL AND kcu.REFERENCED_COLUMN_NAME IS NOT NULL),
       ');')
FROM information_schema.REFERENTIAL_CONSTRAINTS rc
WHERE rc.CONSTRAINT_SCHEMA = DATABASE()
  AND EXISTS (SELECT 1 FROM information_schema.KEY_COLUMN_USAGE k
              WHERE k.CONSTRAINT_SCHEMA = rc.CONSTRAINT_SCHEMA AND k.CONSTRAINT_NAME = rc.CONSTRAINT_NAME)
GROUP BY rc.TABLE_NAME, rc.CONSTRAINT_NAME, rc.REFERENCED_TABLE_NAME", ct);

            // 视图（独立对象，恢复前先 DROP）
            result.Views = await TryQueryAsync(conn, result, "MySQL 视图", @"
SELECT CONCAT('DROP VIEW IF EXISTS `', TABLE_NAME, '`;', CHAR(10),
              'CREATE VIEW `', TABLE_NAME, '` AS ', VIEW_DEFINITION, ';')
FROM information_schema.VIEWS
WHERE TABLE_SCHEMA = DATABASE()", ct);

            result.Triggers = await TryQueryAsync(conn, result, "MySQL 触发器", @"
SELECT CONCAT('CREATE TRIGGER `', TRIGGER_NAME, '` ', ACTION_TIMING, ' ', EVENT_MANIPULATION,
       ' ON `', EVENT_OBJECT_TABLE, '` FOR EACH ROW ', ACTION_STATEMENT, ';')
FROM information_schema.TRIGGERS
WHERE TRIGGER_SCHEMA = DATABASE()", ct);
        }
        // ==================== SQL Server ====================

        private static async Task CollectSqlServerAsync(DbConnection conn, BackupSchemaObjects result, CancellationToken ct)
        {
            result.Indexes = await TryQueryAsync(conn, result, "SQL Server 索引", @"
SELECT CONCAT('CREATE ', CASE WHEN i.is_unique = 1 THEN 'UNIQUE ' ELSE '' END,
       CASE WHEN i.type = 2 THEN 'NONCLUSTERED ' WHEN i.type = 1 THEN 'CLUSTERED ' ELSE '' END,
       'INDEX [', i.name, '] ON ', QUOTENAME(s.name), '.', QUOTENAME(t.name), ' (',
       STRING_AGG('[' + c.name + '] ' + CASE WHEN ic.is_descending_key = 1 THEN 'DESC' ELSE 'ASC' END, ', ')
           WITHIN GROUP (ORDER BY ic.key_ordinal),
       ')')
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.name IS NOT NULL
  AND i.type > 0
  AND i.is_primary_key = 0
  AND i.filter_definition IS NULL
  AND i.has_included_column = 0
  AND t.is_ms_shipped = 0
GROUP BY i.name, i.is_unique, i.type, s.name, t.name", ct);

            result.Constraints = await TryQueryAsync(conn, result, "SQL Server 外键", @"
SELECT 'ALTER TABLE ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name) + ' ADD CONSTRAINT ' + fk.name + ' FOREIGN KEY (' +
       (SELECT STRING_AGG('[' + c.name + ']', ', ') WITHIN GROUP (ORDER BY fkc.constraint_column_id)
          FROM sys.foreign_key_columns fkc
          JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
         WHERE fkc.constraint_object_id = fk.object_id) +
       ') REFERENCES ' + QUOTENAME(rs.name) + '.' + QUOTENAME(rt.name) + ' (' +
       (SELECT STRING_AGG('[' + c.name + ']', ', ') WITHIN GROUP (ORDER BY fkc.constraint_column_id)
          FROM sys.foreign_key_columns fkc
          JOIN sys.columns c ON c.object_id = fkc.referenced_object_id AND c.column_id = fkc.referenced_column_id
         WHERE fkc.constraint_object_id = fk.object_id) + ')'
FROM sys.foreign_keys fk
JOIN sys.tables t ON t.object_id = fk.parent_object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id
JOIN sys.schemas rs ON rs.schema_id = rt.schema_id", ct);

            var modules = await TryQueryAsync(conn, result, "SQL Server 模块", @"
SELECT CONCAT(o.type, '|', OBJECT_SCHEMA_NAME(o.object_id), '|', o.name, '|', m.definition)
FROM sys.sql_modules m
JOIN sys.objects o ON o.object_id = m.object_id
WHERE m.definition IS NOT NULL AND o.type IN ('V', 'FN', 'TF', 'IF', 'P', 'TR')", ct);

            foreach (var item in modules)
            {
                var parts = item.Split('|', 4);
                if (parts.Length < 4)
                {
                    continue;
                }

                var type = parts[0];
                var schema = parts[1];
                var name = parts[2];
                var def = parts[3].Trim();
                if (def.Length == 0)
                {
                    continue;
                }

                var qualified = $"[{schema}].[{name}]";
                if (type == "V")
                {
                    // 视图与触发器在 SQL Server 中是独立对象，DROP TABLE 不会删除，需要先 DROP
                    result.Views.Add($"DROP VIEW IF EXISTS {qualified};\n{def}");
                }
                else if (type == "TR")
                {
                    // SQL Server 2016+ 支持 DROP TRIGGER IF EXISTS；不带 IF EXISTS 时，
                    // 触发器不存在会让整批（单事务）恢复中断
                    result.Triggers.Add($"DROP TRIGGER IF EXISTS {qualified};\n{def}");
                }
                else
                {
                    result.Functions.Add(def);
                }
            }
        }

        // ==================== Oracle ====================

        private static async Task CollectOracleAsync(DbConnection conn, BackupSchemaObjects result, CancellationToken ct)
        {
            result.Sequences = await TryQueryAsync(conn, result, "Oracle 序列", @"
SELECT DBMS_METADATA.GET_DDL('SEQUENCE', sequence_name, owner) FROM all_sequences WHERE owner = USER", ct);

            result.Indexes = await TryQueryAsync(conn, result, "Oracle 索引", @"
SELECT DBMS_METADATA.GET_DDL('INDEX', i.index_name, i.owner)
FROM all_indexes i
WHERE i.owner = USER
  AND i.index_name NOT IN (SELECT constraint_name FROM all_constraints WHERE owner = USER AND constraint_type IN ('P', 'U'))
  AND i.generated = 'N'", ct);

            result.Constraints = await TryQueryAsync(conn, result, "Oracle 约束", @"
SELECT DBMS_METADATA.GET_DDL('CONSTRAINT', constraint_name, owner)
FROM all_constraints
WHERE owner = USER AND constraint_type IN ('U', 'R', 'C')", ct);

            result.Views = await TryQueryAsync(conn, result, "Oracle 视图", @"
SELECT DBMS_METADATA.GET_DDL('VIEW', view_name, owner) FROM all_views WHERE owner = USER", ct);

            result.Triggers = await TryQueryAsync(conn, result, "Oracle 触发器", @"
SELECT DBMS_METADATA.GET_DDL('TRIGGER', trigger_name, owner)
FROM all_triggers WHERE owner = USER AND status = 'ENABLED'", ct);
        }
    }
}
