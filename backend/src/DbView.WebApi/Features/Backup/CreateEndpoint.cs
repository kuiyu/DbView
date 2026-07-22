using FastEndpoints;
using DbView.Core;

using DbView.Infrastructure.Services;

namespace DbView.WebApi.Features.Backup
{
    internal sealed class CreateBackupEndpoint : Endpoint<CreateBackupRequest, CreateBackupResponse>
    {
        private readonly IConnectionRepository _connectionRepository;
        private readonly DatabaseService _databaseService;
        private readonly IConfiguration _configuration;

        public CreateBackupEndpoint(
            IConnectionRepository connectionRepository,
            DatabaseService databaseService,
            IConfiguration configuration)
        {
            _connectionRepository = connectionRepository;
            _databaseService = databaseService;
            _configuration = configuration;
        }

        public override void Configure()
        {
            Post("/connections/{ConnectionId}/backups");

        }

        public override async Task HandleAsync(CreateBackupRequest r, CancellationToken c)
        {
            var connection = await _connectionRepository.GetByIdAsync(Route<long>("ConnectionId"), c);
            if (connection == null)
            {
                await HttpContext.Response.SendAsync(new { success = false, message = "连接不存在" }, 404, cancellation: c);
                return;
            }

            try
            {
                var backupName = r.Name ?? $"backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                var backupDir = Path.Combine(AppContext.BaseDirectory, "backups");
                Directory.CreateDirectory(backupDir);

                var fileName = $"{backupName}.sql";
                var filePath = Path.Combine(backupDir, fileName);

                // 生成备份SQL
                var sql = await GenerateBackupSql(connection, c);
                await File.WriteAllTextAsync(filePath, sql, c);

                var fileInfo = new FileInfo(filePath);

                var response = new CreateBackupResponse
                {
                    Success = true,
                    Message = "备份成功",
                    Backup = new BackupDto
                    {
                        Id = DateTime.Now.Ticks,
                        Name = backupName,
                        ConnectionId = connection.Id,
                        FileName = fileName,
                        FileSize = fileInfo.Length,
                        CreatedAt = DateTime.Now
                    }
                };

                await HttpContext.Response.SendAsync(response, cancellation: c);
            }
            catch (Exception ex)
            {
                await HttpContext.Response.SendAsync(new
                {
                    success = false,
                    message = $"备份失败: {ex.Message}"
                }, 500, cancellation: c);
            }
        }

        private async Task<string> GenerateBackupSql(DbView.Core.Models.Connection connection, CancellationToken c)
        {
            var tables = await _databaseService.GetTablesAsync(connection, c);
            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"-- 数据库备份: {connection.DatabaseName}");
            sb.AppendLine($"-- 备份时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            foreach (var table in tables)
            {
                sb.AppendLine($"-- 表: {table.TableName}");

                var columns = await _databaseService.GetTableColumnNamesAsync(connection, table.TableName, c);
                var rows = await _databaseService.GetAllTableDataAsync(connection, table.TableName, c);

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