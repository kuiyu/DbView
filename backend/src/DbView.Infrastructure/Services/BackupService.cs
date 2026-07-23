using DbView.Core.Abstractions;
using DbView.Core.Models;
using System.Text;

namespace DbView.Infrastructure.Services
{
    public class BackupService : IBackupService
    {
        private readonly DatabaseService _databaseService;

        public BackupService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<BackupFileInfo> CreateBackupAsync(Connection connection, string? name, CancellationToken cancellationToken = default)
        {
            var backupName = name ?? $"backup_{DateTime.Now:yyyyMMdd_HHmmss}";
            var backupDir = Path.Combine(AppContext.BaseDirectory, "backups");
            Directory.CreateDirectory(backupDir);

            var fileName = $"{backupName}.sql";
            var filePath = Path.Combine(backupDir, fileName);

            var sql = await GenerateBackupSql(connection, cancellationToken);
            await File.WriteAllTextAsync(filePath, sql, cancellationToken);

            var fileInfo = new FileInfo(filePath);
            return new BackupFileInfo
            {
                FileName = fileName,
                FilePath = filePath,
                FileSize = fileInfo.Length,
                CreatedAt = DateTime.Now
            };
        }

        private async Task<string> GenerateBackupSql(Connection connection, CancellationToken cancellationToken)
        {
            var tables = await _databaseService.GetTablesAsync(connection, cancellationToken);
            var sb = new StringBuilder();

            sb.AppendLine($"-- 数据库备份: {connection.DatabaseName}");
            sb.AppendLine($"-- 备份时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            foreach (var table in tables)
            {
                sb.AppendLine($"-- 表: {table.TableName}");

                var columns = await _databaseService.GetTableColumnNamesAsync(connection, table.TableName, cancellationToken);
                var rows = await _databaseService.GetAllTableDataAsync(connection, table.TableName, cancellationToken);

                if (columns.Count > 0 && rows.Count > 0)
                {
                    var quotedColumns = columns.Select(col => GetQuotedColumnName(connection.DbType, col));
                    var columnList = string.Join(", ", quotedColumns);

                    foreach (var row in rows)
                    {
                        var values = new List<string>();
                        for (int i = 0; i < row.Length; i++)
                        {
                            values.Add(FormatValue(row[i], connection.DbType));
                        }
                        sb.AppendLine($"INSERT INTO {GetQuotedTableName(connection.DbType, table.TableName)} ({columnList}) VALUES ({string.Join(", ", values)});");
                    }
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string GetQuotedColumnName(string dbType, string columnName)
        {
            return dbType.ToLower() switch
            {
                "postgresql" => $"\"{columnName}\"",
                "mysql" => $"`{columnName}`",
                "sqlite" => $"\"{columnName}\"",
                "sqlserver" => $"[{columnName}]",
                "oracle" => $"\"{columnName}\"",
                _ => columnName
            };
        }

        private string GetQuotedTableName(string dbType, string tableName)
        {
            return dbType.ToLower() switch
            {
                "postgresql" => $"\"{tableName}\"",
                "mysql" => $"`{tableName}`",
                "sqlite" => $"\"{tableName}\"",
                "sqlserver" => $"[{tableName}]",
                "oracle" => $"\"{tableName}\"",
                _ => tableName
            };
        }

        private string FormatValue(object value, string dbType)
        {
            if (value == null || value == DBNull.Value)
                return "NULL";

            if (value is string strValue)
                return $"'{strValue.Replace("'", "''")}'";

            if (value is DateTime dtValue)
                return $"'{dtValue:yyyy-MM-dd HH:mm:ss}'";

            if (value is bool boolValue)
                return dbType.ToLower() == "oracle" ? (boolValue ? "1" : "0") : (boolValue ? "TRUE" : "FALSE");

            if (value is byte[] byteArray)
                return $"0x{BitConverter.ToString(byteArray).Replace("-", "")}";

            return value.ToString() ?? "NULL";
        }
    }
}
