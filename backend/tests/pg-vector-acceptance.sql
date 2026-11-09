-- ============================================================================
-- DbView 备份/恢复验收脚本（PostgreSQL + pgvector）
-- 在「源库」和「恢复后的目标库」分别执行，diff 两次输出：
--   一致 => 行数/行内容（含 vector 取值）完整还原，没有丢数据
-- 用法：
--   psql "$SOURCE_URL" -f pg-vector-acceptance.sql > source.txt
--   psql "$TARGET_URL" -f pg-vector-acceptance.sql > target.txt
--   diff source.txt target.txt
-- ============================================================================
\pset pager off

\echo '=== 1. 环境 ==='
SELECT current_database() AS database,
       (SELECT extversion FROM pg_extension WHERE extname = 'vector') AS pgvector_version;

\echo ''
\echo '=== 2. 向量列定义（维度）==='
SELECT c.table_schema, c.table_name, c.column_name, a.atttypmod AS vector_dim
FROM information_schema.columns c
JOIN pg_attribute a
  ON a.attrelid = format('%I.%I', c.table_schema, c.table_name)::regclass AND a.attname = c.column_name
WHERE c.udt_name = 'vector'
  AND c.table_schema NOT IN ('pg_catalog', 'information_schema')
ORDER BY 1, 2, 3;

\echo ''
\echo '=== 3. 含向量列的表：行数 + 全表内容指纹（关键）==='
DROP TABLE IF EXISTS _dbview_fingerprint;
CREATE TEMP TABLE _dbview_fingerprint (schema_name text, table_name text, row_count bigint, content_md5 text);

DO $$
DECLARE r record; cnt bigint; h text;
BEGIN
    FOR r IN
        SELECT DISTINCT c.table_schema, c.table_name
        FROM information_schema.columns c
        WHERE c.udt_name = 'vector'
          AND c.table_schema NOT IN ('pg_catalog', 'information_schema')
        ORDER BY 1, 2
    LOOP
        EXECUTE format('SELECT count(*) FROM %I.%I', r.table_schema, r.table_name) INTO cnt;
        EXECUTE format('SELECT coalesce(md5(string_agg(t::text, %L ORDER BY t::text)), %L) FROM %I.%I t',
                       '|', 'empty', r.table_schema, r.table_name) INTO h;
        INSERT INTO _dbview_fingerprint VALUES (r.table_schema, r.table_name, cnt, h);
    END LOOP;
END $$;

SELECT * FROM _dbview_fingerprint ORDER BY 1, 2;

\echo ''
\echo '=== 4. 向量索引（HNSW / IVFFlat）==='
SELECT schemaname, tablename, indexname, indexdef
FROM pg_indexes
WHERE indexdef ILIKE '%hnsw%' OR indexdef ILIKE '%ivfflat%'
ORDER BY 1, 2, 3;

\echo ''
\echo '=== 5. 各 schema 索引数量（检查是否丢索引）==='
SELECT schemaname, count(*) AS index_count
FROM pg_indexes
WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
GROUP BY schemaname
ORDER BY 1;

\echo ''
\echo '=== 6. 约束数量（主键/唯一/外键/CHECK）==='
SELECT n.nspname AS schema_name, con.contype, count(*) AS cnt
FROM pg_constraint con
JOIN pg_class c ON c.oid = con.conrelid
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE con.contype IN ('u', 'f', 'c', 'p')
  AND n.nspname NOT IN ('pg_catalog', 'information_schema')
GROUP BY 1, 2
ORDER BY 1, 2;

\echo ''
\echo '=== 7. 序列当前值（检查自增是否接续）==='
SELECT schemaname, sequencename, last_value
FROM pg_sequences
WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
ORDER BY 1, 2;

\echo ''
\echo '=== 8. 视图 / 函数 / 触发器数量 ==='
SELECT 'view' AS kind, count(*) AS cnt
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'v' AND n.nspname NOT IN ('pg_catalog', 'information_schema')
UNION ALL
SELECT 'function', count(*)
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
UNION ALL
SELECT 'trigger', count(*)
FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE NOT t.tgisinternal AND n.nspname NOT IN ('pg_catalog', 'information_schema');
