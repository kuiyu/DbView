# DbView 数据库管理工具实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现一个类似pgAdmin和阿里DMS的数据库管理工具，支持多数据库的表管理、SQL执行、用户管理等功能。

**Architecture:** 后端采用DDD架构（WebApi、Application、Core、Infrastructure），前端采用Vue3 + TDesign的IDE风格界面。使用FreeSql作为ORM，支持多种数据库类型。

**Tech Stack:** ASP.NET Core, FreeSql, Vue3, TDesign, TypeScript, JWT Authentication

---

## 文件结构映射

### 后端文件结构
```
DbView.Core/
├── Models/
│   ├── Connection.cs          # 连接配置领域模型
│   ├── TableInfo.cs           # 表信息领域模型
│   ├── ColumnInfo.cs          # 列信息领域模型
│   └── SqlHistory.cs          # SQL历史领域模型
├── Abstractions/
│   └── IRepository.cs         # 仓储接口（已有）

DbView.Infrastructure/
├── Entities/
│   ├── ConnectionEntity.cs    # 连接配置实体
│   ├── SqlHistoryEntity.cs    # SQL历史实体
│   └── UserEntity.cs          # 用户实体（已有）
├── Reposities/
│   ├── ConnectionRepository.cs # 连接配置仓储
│   └── SqlHistoryRepository.cs # SQL历史仓储
├── Services/
│   ├── DatabaseService.cs     # 数据库操作服务
│   └── ConnectionTestService.cs # 连接测试服务

DbView.Application/
├── Services/
│   ├── IConnectionAppService.cs # 连接应用服务接口
│   ├── ConnectionAppService.cs  # 连接应用服务实现
│   ├── ITableAppService.cs      # 表应用服务接口
│   ├── TableAppService.cs       # 表应用服务实现
│   ├── ISqlAppService.cs        # SQL应用服务接口
│   └── SqlAppService.cs         # SQL应用服务实现
├── Mappers/
│   └── ConnectionMapper.cs      # 连接映射配置

DbView.WebApi/
├── Features/
│   ├── Connection/              # 连接管理功能
│   │   ├── Create/
│   │   ├── Delete/
│   │   ├── Get/
│   │   ├── List/
│   │   ├── Update/
│   │   └── Test/
│   ├── Table/                   # 表管理功能
│   │   ├── List/
│   │   ├── Structure/
│   │   ├── Data/
│   │   ├── Create/
│   │   ├── Update/
│   │   └── Delete/
│   ├── Sql/                     # SQL执行功能
│   │   ├── Execute/
│   │   ├── History/
│   │   └── Save/
│   └── User/                    # 用户管理功能（已有）
```

### 前端文件结构
```
frontend/
├── src/
│   ├── api/
│   │   ├── connection.ts        # 连接API
│   │   ├── table.ts             # 表API
│   │   ├── sql.ts               # SQL API
│   │   └── user.ts              # 用户API
│   ├── components/
│   │   ├── DatabaseTree.vue     # 数据库树组件
│   │   ├── SqlEditor.vue        # SQL编辑器组件
│   │   ├── DataTable.vue        # 数据表格组件
│   │   └── ConnectionForm.vue   # 连接表单组件
│   ├── layouts/
│   │   └── MainLayout.vue       # 主布局（IDE风格）
│   ├── views/
│   │   ├── Login.vue            # 登录页面
│   │   ├── Dashboard.vue        # 主界面
│   │   ├── connection/
│   │   │   ├── ConnectionList.vue
│   │   │   └── ConnectionForm.vue
│   │   ├── table/
│   │   │   ├── TableList.vue
│   │   │   ├── TableStructure.vue
│   │   │   └── TableData.vue
│   │   └── sql/
│   │       └── SqlConsole.vue
│   ├── stores/
│   │   ├── connection.ts        # 连接状态管理
│   │   ├── table.ts             # 表状态管理
│   │   └── user.ts              # 用户状态管理
│   ├── router/
│   │   └── index.ts             # 路由配置
│   └── utils/
│       └── request.ts           # HTTP请求工具
```

---

## 实现任务

### Task 1: 后端基础架构搭建

**Files:**
- Create: `DbView.Core/Models/Connection.cs`
- Create: `DbView.Core/Models/TableInfo.cs`
- Create: `DbView.Core/Models/ColumnInfo.cs`
- Create: `DbView.Core/Models/SqlHistory.cs`
- Modify: `DbView.Core/DbView.Core.csproj`

- [ ] **Step 1: 创建连接配置领域模型**

```csharp
// DbView.Core/Models/Connection.cs
namespace DbView.Core.Models
{
    public class Connection
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string DatabaseName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string DbType { get; set; } = string.Empty;
        public long UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
```

- [ ] **Step 2: 创建表信息领域模型**

```csharp
// DbView.Core/Models/TableInfo.cs
namespace DbView.Core.Models
{
    public class TableInfo
    {
        public string TableName { get; set; } = string.Empty;
        public string SchemaName { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public long RowCount { get; set; }
        public string Size { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }
}
```

- [ ] **Step 3: 创建列信息领域模型**

```csharp
// DbView.Core/Models/ColumnInfo.cs
namespace DbView.Core.Models
{
    public class ColumnInfo
    {
        public string ColumnName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsAutoIncrement { get; set; }
        public string DefaultValue { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public long? MaxLength { get; set; }
        public int? Precision { get; set; }
        public int? Scale { get; set; }
    }
}
```

- [ ] **Step 4: 创建SQL历史领域模型**

```csharp
// DbView.Core/Models/SqlHistory.cs
namespace DbView.Core.Models
{
    public class SqlHistory
    {
        public long Id { get; set; }
        public long ConnectionId { get; set; }
        public long UserId { get; set; }
        public string SqlText { get; set; } = string.Empty;
        public DateTime ExecutionTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public int ResultRows { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 5: 验证编译**

Run: `dotnet build DbView.Core/DbView.Core.csproj`
Expected: Build succeeded

- [ ] **Step 6: 提交代码**

```bash
git add DbView.Core/Models/
git commit -m "feat: add core domain models for database management"
```

### Task 2: 基础设施层实体和仓储

**Files:**
- Create: `DbView.Infrastructure/Entities/ConnectionEntity.cs`
- Create: `DbView.Infrastructure/Entities/SqlHistoryEntity.cs`
- Create: `DbView.Infrastructure/Reposities/ConnectionRepository.cs`
- Create: `DbView.Infrastructure/Reposities/SqlHistoryRepository.cs`
- Modify: `DbView.Infrastructure/DbView.Infrastructure.csproj`

- [ ] **Step 1: 创建连接配置实体**

```csharp
// DbView.Infrastructure/Entities/ConnectionEntity.cs
using FreeSql.DataAnnotations;

namespace DbView.Infrastructure.Entities
{
    [Table(Name = "db_connections")]
    public class ConnectionEntity
    {
        [Column(IsIdentity = true, IsPrimary = true)]
        public long Id { get; set; }

        [Column(StringLength = 100, IsNullable = false)]
        public string Name { get; set; } = string.Empty;

        [Column(StringLength = 255, IsNullable = false)]
        public string Host { get; set; } = string.Empty;

        [Column(IsNullable = false)]
        public int Port { get; set; }

        [Column(StringLength = 100, IsNullable = false)]
        public string DatabaseName { get; set; } = string.Empty;

        [Column(StringLength = 100, IsNullable = false)]
        public string Username { get; set; } = string.Empty;

        [Column(StringLength = 255, IsNullable = false)]
        public string Password { get; set; } = string.Empty;

        [Column(StringLength = 50, IsNullable = false)]
        public string DbType { get; set; } = string.Empty;

        public long UserId { get; set; }

        [Column(CanUpdate = false)]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
```

- [ ] **Step 2: 创建SQL历史实体**

```csharp
// DbView.Infrastructure/Entities/SqlHistoryEntity.cs
using FreeSql.DataAnnotations;

namespace DbView.Infrastructure.Entities
{
    [Table(Name = "sql_history")]
    public class SqlHistoryEntity
    {
        [Column(IsIdentity = true, IsPrimary = true)]
        public long Id { get; set; }

        public long ConnectionId { get; set; }

        public long UserId { get; set; }

        [Column(StringLength = -1, IsNullable = false)]
        public string SqlText { get; set; } = string.Empty;

        [Column(CanUpdate = false)]
        public DateTime ExecutionTime { get; set; } = DateTime.Now;

        [Column(StringLength = 20)]
        public string Status { get; set; } = string.Empty;

        public int ResultRows { get; set; }

        [Column(StringLength = -1)]
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 3: 创建连接配置仓储**

```csharp
// DbView.Infrastructure/Reposities/ConnectionRepository.cs
using DbView.Core.Models;
using DbView.Infrastructure.Entities;
using Mapster;

namespace DbView.Infrastructure
{
    public class ConnectionRepository : GenericRepository<ConnectionEntity, Connection, long>
    {
        public ConnectionRepository(IFreeSql freeSql) : base(freeSql)
        {
        }

        public async Task<IReadOnlyList<Connection>> GetByUserIdAsync(long userId, CancellationToken cancellationToken = default)
        {
            var entities = await sql.Select<ConnectionEntity>()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);
            return entities.Adapt<List<Connection>>();
        }

        public async Task<Connection?> GetByNameAsync(string name, long userId, CancellationToken cancellationToken = default)
        {
            var entity = await sql.Select<ConnectionEntity>()
                .Where(x => x.Name == name && x.UserId == userId)
                .ToOneAsync(cancellationToken);
            return entity?.Adapt<Connection>();
        }
    }
}
```

- [ ] **Step 4: 创建SQL历史仓储**

```csharp
// DbView.Infrastructure/Reposities/SqlHistoryRepository.cs
using DbView.Core.Models;
using DbView.Infrastructure.Entities;
using Mapster;

namespace DbView.Infrastructure
{
    public class SqlHistoryRepository : GenericRepository<SqlHistoryEntity, SqlHistory, long>
    {
        public SqlHistoryRepository(IFreeSql freeSql) : base(freeSql)
        {
        }

        public async Task<IReadOnlyList<SqlHistory>> GetByConnectionIdAsync(long connectionId, int limit = 50, CancellationToken cancellationToken = default)
        {
            var entities = await sql.Select<SqlHistoryEntity>()
                .Where(x => x.ConnectionId == connectionId)
                .OrderByDescending(x => x.ExecutionTime)
                .Limit(limit)
                .ToListAsync(cancellationToken);
            return entities.Adapt<List<SqlHistory>>();
        }

        public async Task<IReadOnlyList<SqlHistory>> GetByUserIdAsync(long userId, int limit = 50, CancellationToken cancellationToken = default)
        {
            var entities = await sql.Select<SqlHistoryEntity>()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.ExecutionTime)
                .Limit(limit)
                .ToListAsync(cancellationToken);
            return entities.Adapt<List<SqlHistory>>();
        }
    }
}
```

- [ ] **Step 5: 验证编译**

Run: `dotnet build DbView.Infrastructure/DbView.Infrastructure.csproj`
Expected: Build succeeded

- [ ] **Step 6: 提交代码**

```bash
git add DbView.Infrastructure/Entities/ DbView.Infrastructure/Reposities/
git commit -m "feat: add infrastructure entities and repositories"
```

### Task 3: 应用层服务

**Files:**
- Create: `DbView.Application/Services/IConnectionAppService.cs`
- Create: `DbView.Application/Services/ConnectionAppService.cs`
- Create: `DbView.Application/Services/ITableAppService.cs`
- Create: `DbView.Application/Services/TableAppService.cs`
- Create: `DbView.Application/Services/ISqlAppService.cs`
- Create: `DbView.Application/Services/SqlAppService.cs`
- Modify: `DbView.Application/DbView.Application.csproj`

- [ ] **Step 1: 创建连接应用服务接口**

```csharp
// DbView.Application/Services/IConnectionAppService.cs
using DbView.Core.Models;

namespace DbView.Application.Services
{
    public interface IConnectionAppService
    {
        Task<IReadOnlyList<Connection>> GetConnectionsByUserIdAsync(long userId, CancellationToken cancellationToken = default);
        Task<Connection?> GetConnectionByIdAsync(long id, CancellationToken cancellationToken = default);
        Task<Connection> CreateConnectionAsync(Connection connection, CancellationToken cancellationToken = default);
        Task<Connection> UpdateConnectionAsync(Connection connection, CancellationToken cancellationToken = default);
        Task DeleteConnectionAsync(long id, CancellationToken cancellationToken = default);
        Task<bool> TestConnectionAsync(Connection connection, CancellationToken cancellationToken = default);
    }
}
```

- [ ] **Step 2: 创建连接应用服务实现**

```csharp
// DbView.Application/Services/ConnectionAppService.cs
using DbView.Core.Models;
using DbView.Infrastructure;

namespace DbView.Application.Services
{
    public class ConnectionAppService : IConnectionAppService
    {
        private readonly ConnectionRepository _connectionRepository;

        public ConnectionAppService(ConnectionRepository connectionRepository)
        {
            _connectionRepository = connectionRepository;
        }

        public async Task<IReadOnlyList<Connection>> GetConnectionsByUserIdAsync(long userId, CancellationToken cancellationToken = default)
        {
            return await _connectionRepository.GetByUserIdAsync(userId, cancellationToken);
        }

        public async Task<Connection?> GetConnectionByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            return await _connectionRepository.GetByIdAsync(id, cancellationToken);
        }

        public async Task<Connection> CreateConnectionAsync(Connection connection, CancellationToken cancellationToken = default)
        {
            connection.CreatedAt = DateTime.Now;
            connection.UpdatedAt = DateTime.Now;
            await _connectionRepository.AddAsync(connection, cancellationToken);
            return connection;
        }

        public async Task<Connection> UpdateConnectionAsync(Connection connection, CancellationToken cancellationToken = default)
        {
            connection.UpdatedAt = DateTime.Now;
            await _connectionRepository.UpdateAsync(connection, cancellationToken);
            return connection;
        }

        public async Task DeleteConnectionAsync(long id, CancellationToken cancellationToken = default)
        {
            await _connectionRepository.DeleteByIdAsync(id, cancellationToken);
        }

        public async Task<bool> TestConnectionAsync(Connection connection, CancellationToken cancellationToken = default)
        {
            try
            {
                // 这里将实现实际的数据库连接测试
                // 暂时返回true
                await Task.CompletedTask;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
```

- [ ] **Step 3: 创建表应用服务接口**

```csharp
// DbView.Application/Services/ITableAppService.cs
using DbView.Core.Models;

namespace DbView.Application.Services
{
    public interface ITableAppService
    {
        Task<IReadOnlyList<TableInfo>> GetTablesAsync(long connectionId, CancellationToken cancellationToken = default);
        Task<TableInfo?> GetTableStructureAsync(long connectionId, string tableName, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ColumnInfo>> GetTableColumnsAsync(long connectionId, string tableName, CancellationToken cancellationToken = default);
        Task<object> GetTableDataAsync(long connectionId, string tableName, int page, int pageSize, CancellationToken cancellationToken = default);
    }
}
```

- [ ] **Step 4: 创建表应用服务实现**

```csharp
// DbView.Application/Services/TableAppService.cs
using DbView.Core.Models;
using DbView.Infrastructure;

namespace DbView.Application.Services
{
    public class TableAppService : ITableAppService
    {
        private readonly ConnectionRepository _connectionRepository;

        public TableAppService(ConnectionRepository connectionRepository)
        {
            _connectionRepository = connectionRepository;
        }

        public async Task<IReadOnlyList<TableInfo>> GetTablesAsync(long connectionId, CancellationToken cancellationToken = default)
        {
            // 这里将实现获取数据库表列表的逻辑
            // 暂时返回空列表
            await Task.CompletedTask;
            return new List<TableInfo>();
        }

        public async Task<TableInfo?> GetTableStructureAsync(long connectionId, string tableName, CancellationToken cancellationToken = default)
        {
            // 这里将实现获取表结构的逻辑
            // 暂时返回null
            await Task.CompletedTask;
            return null;
        }

        public async Task<IReadOnlyList<ColumnInfo>> GetTableColumnsAsync(long connectionId, string tableName, CancellationToken cancellationToken = default)
        {
            // 这里将实现获取表列信息的逻辑
            // 暂时返回空列表
            await Task.CompletedTask;
            return new List<ColumnInfo>();
        }

        public async Task<object> GetTableDataAsync(long connectionId, string tableName, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            // 这里将实现获取表数据的逻辑
            // 暂时返回空对象
            await Task.CompletedTask;
            return new { Items = new List<object>(), Total = 0 };
        }
    }
}
```

- [ ] **Step 5: 创建SQL应用服务接口**

```csharp
// DbView.Application/Services/ISqlAppService.cs
using DbView.Core.Models;

namespace DbView.Application.Services
{
    public interface ISqlAppService
    {
        Task<object> ExecuteSqlAsync(long connectionId, string sql, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SqlHistory>> GetSqlHistoryAsync(long connectionId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SqlHistory>> GetUserSqlHistoryAsync(long userId, CancellationToken cancellationToken = default);
    }
}
```

- [ ] **Step 6: 创建SQL应用服务实现**

```csharp
// DbView.Application/Services/SqlAppService.cs
using DbView.Core.Models;
using DbView.Infrastructure;

namespace DbView.Application.Services
{
    public class SqlAppService : ISqlAppService
    {
        private readonly SqlHistoryRepository _sqlHistoryRepository;

        public SqlAppService(SqlHistoryRepository sqlHistoryRepository)
        {
            _sqlHistoryRepository = sqlHistoryRepository;
        }

        public async Task<object> ExecuteSqlAsync(long connectionId, string sql, CancellationToken cancellationToken = default)
        {
            // 这里将实现执行SQL的逻辑
            // 暂时返回模拟结果
            await Task.CompletedTask;
            return new 
            { 
                Success = true, 
                Message = "SQL executed successfully",
                RowsAffected = 0,
                Data = new List<object>()
            };
        }

        public async Task<IReadOnlyList<SqlHistory>> GetSqlHistoryAsync(long connectionId, CancellationToken cancellationToken = default)
        {
            return await _sqlHistoryRepository.GetByConnectionIdAsync(connectionId, cancellationToken: cancellationToken);
        }

        public async Task<IReadOnlyList<SqlHistory>> GetUserSqlHistoryAsync(long userId, CancellationToken cancellationToken = default)
        {
            return await _sqlHistoryRepository.GetByUserIdAsync(userId, cancellationToken: cancellationToken);
        }
    }
}
```

- [ ] **Step 7: 注册服务到依赖注入**

```csharp
// DbView.Application/ServiceExtensions.cs
using Microsoft.Extensions.DependencyInjection;
using DbView.Application.Services;

namespace DbView.Application
{
    public static class ServiceExtensions
    {
        public static IServiceCollection RegisterServicesFromDbViewApplication(this IServiceCollection services)
        {
            services.AddScoped<IConnectionAppService, ConnectionAppService>();
            services.AddScoped<ITableAppService, TableAppService>();
            services.AddScoped<ISqlAppService, SqlAppService>();
            return services;
        }
    }
}
```

- [ ] **Step 8: 验证编译**

Run: `dotnet build DbView.Application/DbView.Application.csproj`
Expected: Build succeeded

- [ ] **Step 9: 提交代码**

```bash
git add DbView.Application/Services/
git commit -m "feat: add application services for connection, table, and SQL management"
```

### Task 4: 基础设施层数据库服务

**Files:**
- Create: `DbView.Infrastructure/Services/DatabaseService.cs`
- Create: `DbView.Infrastructure/Services/ConnectionTestService.cs`
- Modify: `DbView.Infrastructure/ServiceExtensions.cs`

- [ ] **Step 1: 创建数据库服务**

```csharp
// DbView.Infrastructure/Services/DatabaseService.cs
using DbView.Core.Models;
using System.Data;
using System.Data.Common;

namespace DbView.Infrastructure.Services
{
    public class DatabaseService
    {
        public async Task<IReadOnlyList<TableInfo>> GetTablesAsync(Connection connection, CancellationToken cancellationToken = default)
        {
            var tables = new List<TableInfo>();
            
            using var conn = CreateConnection(connection);
            await conn.OpenAsync(cancellationToken);
            
            var command = conn.CreateCommand();
            command.CommandText = GetTablesQuery(connection.DbType);
            
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                tables.Add(new TableInfo
                {
                    TableName = reader.GetString(0),
                    SchemaName = reader.IsDBNull(1) ? "public" : reader.GetString(1),
                    Comment = reader.IsDBNull(2) ? string.Empty : reader.GetString(2)
                });
            }
            
            return tables;
        }

        public async Task<IReadOnlyList<ColumnInfo>> GetTableColumnsAsync(Connection connection, string tableName, CancellationToken cancellationToken = default)
        {
            var columns = new List<ColumnInfo>();
            
            using var conn = CreateConnection(connection);
            await conn.OpenAsync(cancellationToken);
            
            var command = conn.CreateCommand();
            command.CommandText = GetColumnsQuery(connection.DbType, tableName);
            
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(new ColumnInfo
                {
                    ColumnName = reader.GetString(0),
                    DataType = reader.GetString(1),
                    IsNullable = reader.GetBoolean(2),
                    IsPrimaryKey = reader.GetBoolean(3),
                    IsAutoIncrement = reader.GetBoolean(4),
                    DefaultValue = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    Comment = reader.IsDBNull(6) ? string.Empty : reader.GetString(6)
                });
            }
            
            return columns;
        }

        public async Task<object> GetTableDataAsync(Connection connection, string tableName, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            using var conn = CreateConnection(connection);
            await conn.OpenAsync(cancellationToken);
            
            // 获取总数
            var countCommand = conn.CreateCommand();
            countCommand.CommandText = $"SELECT COUNT(*) FROM {tableName}";
            var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
            
            // 获取数据
            var command = conn.CreateCommand();
            command.CommandText = $"SELECT * FROM {tableName} LIMIT {pageSize} OFFSET {(page - 1) * pageSize}";
            
            var dataTable = new DataTable();
            using var adapter = CreateAdapter(connection.DbType, command);
            adapter.Fill(dataTable);
            
            return new
            {
                Items = dataTable.AsEnumerable().Select(row => row.ItemArray).ToList(),
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<object> ExecuteSqlAsync(Connection connection, string sql, CancellationToken cancellationToken = default)
        {
            using var conn = CreateConnection(connection);
            await conn.OpenAsync(cancellationToken);
            
            var command = conn.CreateCommand();
            command.CommandText = sql;
            
            var startTime = DateTime.Now;
            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
            var executionTime = DateTime.Now - startTime;
            
            return new
            {
                Success = true,
                Message = $"SQL executed successfully in {executionTime.TotalMilliseconds:F2}ms",
                RowsAffected = rowsAffected
            };
        }

        private DbConnection CreateConnection(Connection connection)
        {
            return connection.DbType.ToLower() switch
            {
                "postgresql" => new Npgsql.NpgsqlConnection(GetConnectionString(connection)),
                "mysql" => new MySqlConnector.MySqlConnection(GetConnectionString(connection)),
                "sqlite" => new Microsoft.Data.Sqlite.SqliteConnection(GetConnectionString(connection)),
                "sqlserver" => new System.Data.SqlClient.SqlConnection(GetConnectionString(connection)),
                _ => throw new NotSupportedException($"Database type {connection.DbType} is not supported")
            };
        }

        private DbDataAdapter CreateAdapter(string dbType, DbCommand command)
        {
            return dbType.ToLower() switch
            {
                "postgresql" => new Npgsql.NpgsqlDataAdapter((Npgsql.NpgsqlCommand)command),
                "mysql" => new MySqlConnector.MySqlCommandAdapter((MySqlConnector.MySqlCommand)command),
                "sqlite" => new Microsoft.Data.Sqlite.SqliteDataAdapter((Microsoft.Data.Sqlite.SqliteCommand)command),
                "sqlserver" => new System.Data.SqlClient.SqlDataAdapter((System.Data.SqlClient.SqlCommand)command),
                _ => throw new NotSupportedException($"Database type {dbType} is not supported")
            };
        }

        private string GetConnectionString(Connection connection)
        {
            return connection.DbType.ToLower() switch
            {
                "postgresql" => $"Host={connection.Host};Port={connection.Port};Database={connection.DatabaseName};Username={connection.Username};Password={connection.Password}",
                "mysql" => $"Server={connection.Host};Port={connection.Port};Database={connection.DatabaseName};User={connection.Username};Password={connection.Password}",
                "sqlite" => $"Data Source={connection.DatabaseName}",
                "sqlserver" => $"Server={connection.Host},{connection.Port};Database={connection.DatabaseName};User Id={connection.Username};Password={connection.Password}",
                _ => throw new NotSupportedException($"Database type {connection.DbType} is not supported")
            };
        }

        private string GetTablesQuery(string dbType)
        {
            return dbType.ToLower() switch
            {
                "postgresql" => @"
                    SELECT 
                        t.table_name,
                        t.table_schema,
                        obj_description((t.table_schema || '.' || t.table_name)::regclass) as table_comment
                    FROM information_schema.tables t
                    WHERE t.table_type = 'BASE TABLE'
                    AND t.table_schema NOT IN ('pg_catalog', 'information_schema')
                    ORDER BY t.table_name",
                "mysql" => @"
                    SELECT 
                        TABLE_NAME,
                        TABLE_SCHEMA,
                        TABLE_COMMENT
                    FROM information_schema.TABLES
                    WHERE TABLE_TYPE = 'BASE TABLE'
                    AND TABLE_SCHEMA NOT IN ('mysql', 'information_schema', 'performance_schema', 'sys')
                    ORDER BY TABLE_NAME",
                "sqlite" => @"
                    SELECT 
                        name,
                        'main',
                        ''
                    FROM sqlite_master
                    WHERE type = 'table'
                    AND name NOT LIKE 'sqlite_%'
                    ORDER BY name",
                "sqlserver" => @"
                    SELECT 
                        t.TABLE_NAME,
                        t.TABLE_SCHEMA,
                        ep.value as table_comment
                    FROM INFORMATION_SCHEMA.TABLES t
                    LEFT JOIN sys.extended_properties ep
                        ON ep.major_id = OBJECT_ID(t.TABLE_SCHEMA + '.' + t.TABLE_NAME)
                        AND ep.minor_id = 0
                    WHERE t.TABLE_TYPE = 'BASE TABLE'
                    ORDER BY t.TABLE_NAME",
                _ => throw new NotSupportedException($"Database type {dbType} is not supported")
            };
        }

        private string GetColumnsQuery(string dbType, string tableName)
        {
            return dbType.ToLower() switch
            {
                "postgresql" => $@"
                    SELECT 
                        c.column_name,
                        c.data_type,
                        CASE WHEN c.is_nullable = 'YES' THEN true ELSE false END as is_nullable,
                        CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END as is_primary_key,
                        CASE WHEN c.column_default LIKE 'nextval%' THEN true ELSE false END as is_auto_increment,
                        c.column_default,
                        col_description('{tableName}'::regclass, c.ordinal_position) as column_comment
                    FROM information_schema.columns c
                    LEFT JOIN (
                        SELECT ku.column_name
                        FROM information_schema.table_constraints tc
                        JOIN information_schema.key_column_usage ku
                            ON tc.constraint_name = ku.constraint_name
                        WHERE tc.constraint_type = 'PRIMARY KEY'
                        AND tc.table_name = '{tableName}'
                    ) pk ON c.column_name = pk.column_name
                    WHERE c.table_name = '{tableName}'
                    ORDER BY c.ordinal_position",
                "mysql" => $@"
                    SELECT 
                        COLUMN_NAME,
                        DATA_TYPE,
                        CASE WHEN IS_NULLABLE = 'YES' THEN 1 ELSE 0 END as is_nullable,
                        CASE WHEN COLUMN_KEY = 'PRI' THEN 1 ELSE 0 END as is_primary_key,
                        CASE WHEN EXTRA = 'auto_increment' THEN 1 ELSE 0 END as is_auto_increment,
                        COLUMN_DEFAULT,
                        COLUMN_COMMENT
                    FROM information_schema.COLUMNS
                    WHERE TABLE_NAME = '{tableName}'
                    ORDER BY ORDINAL_POSITION",
                "sqlite" => $@"
                    SELECT 
                        name,
                        type,
                        CASE WHEN "notnull" = 0 THEN 1 ELSE 0 END as is_nullable,
                        CASE WHEN pk = 1 THEN 1 ELSE 0 END as is_primary_key,
                        CASE WHEN type LIKE '%AUTOINCREMENT%' THEN 1 ELSE 0 END as is_auto_increment,
                        dflt_value,
                        '' as column_comment
                    FROM pragma_table_info('{tableName}')
                    ORDER BY cid",
                "sqlserver" => $@"
                    SELECT 
                        c.COLUMN_NAME,
                        c.DATA_TYPE,
                        CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END as is_nullable,
                        CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END as is_primary_key,
                        CASE WHEN COLUMNPROPERTY(OBJECT_ID('{tableName}'), c.COLUMN_NAME, 'IsIdentity') = 1 THEN 1 ELSE 0 END as is_auto_increment,
                        c.COLUMN_DEFAULT,
                        ep.value as column_comment
                    FROM INFORMATION_SCHEMA.COLUMNS c
                    LEFT JOIN (
                        SELECT ku.COLUMN_NAME
                        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                        JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                            ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                        WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                        AND tc.TABLE_NAME = '{tableName}'
                    ) pk ON c.COLUMN_NAME = pk.COLUMN_NAME
                    LEFT JOIN sys.extended_properties ep
                        ON ep.major_id = OBJECT_ID('{tableName}')
                        AND ep.minor_id = c.ORDINAL_POSITION
                    WHERE c.TABLE_NAME = '{tableName}'
                    ORDER BY c.ORDINAL_POSITION",
                _ => throw new NotSupportedException($"Database type {dbType} is not supported")
            };
        }
    }
}
```

- [ ] **Step 2: 创建连接测试服务**

```csharp
// DbView.Infrastructure/Services/ConnectionTestService.cs
using DbView.Core.Models;

namespace DbView.Infrastructure.Services
{
    public class ConnectionTestService
    {
        private readonly DatabaseService _databaseService;

        public ConnectionTestService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<(bool Success, string Message)> TestConnectionAsync(Connection connection, CancellationToken cancellationToken = default)
        {
            try
            {
                var tables = await _databaseService.GetTablesAsync(connection, cancellationToken);
                return (true, $"Connection successful. Found {tables.Count} tables.");
            }
            catch (Exception ex)
            {
                return (false, $"Connection failed: {ex.Message}");
            }
        }
    }
}
```

- [ ] **Step 3: 注册服务到依赖注入**

```csharp
// DbView.Infrastructure/ServiceExtensions.cs
using Microsoft.Extensions.DependencyInjection;
using DbView.Infrastructure.Services;

namespace DbView.Infrastructure
{
    public static class ServiceExtensions
    {
        public static IServiceCollection RegisterServicesFromDbViewInfrastructure(this IServiceCollection services)
        {
            services.AddScoped<DatabaseService>();
            services.AddScoped<ConnectionTestService>();
            return services;
        }
    }
}
```

- [ ] **Step 4: 添加必要的NuGet包**

```bash
cd DbView.Infrastructure
dotnet add package Npgsql
dotnet add package MySqlConnector
dotnet add package Microsoft.Data.Sqlite
dotnet add package System.Data.SqlClient
```

- [ ] **Step 5: 验证编译**

Run: `dotnet build DbView.Infrastructure/DbView.Infrastructure.csproj`
Expected: Build succeeded

- [ ] **Step 6: 提交代码**

```bash
git add DbView.Infrastructure/Services/
git commit -m "feat: add database service and connection test service"
```

### Task 5: WebApi层连接管理功能

**Files:**
- Create: `DbView.WebApi/Features/Connection/List/Endpoint.cs`
- Create: `DbView.WebApi/Features/Connection/List/Models.cs`
- Create: `DbView.WebApi/Features/Connection/Create/Endpoint.cs`
- Create: `DbView.WebApi/Features/Connection/Create/Models.cs`
- Create: `DbView.WebApi/Features/Connection/Get/Endpoint.cs`
- Create: `DbView.WebApi/Features/Connection/Get/Models.cs`
- Create: `DbView.WebApi/Features/Connection/Update/Endpoint.cs`
- Create: `DbView.WebApi/Features/Connection/Update/Models.cs`
- Create: `DbView.WebApi/Features/Connection/Delete/Endpoint.cs`
- Create: `DbView.WebApi/Features/Connection/Delete/Models.cs`
- Create: `DbView.WebApi/Features/Connection/Test/Endpoint.cs`
- Create: `DbView.WebApi/Features/Connection/Test/Models.cs`

- [ ] **Step 1: 创建获取连接列表端点**

```csharp
// DbView.WebApi/Features/Connection/List/Endpoint.cs
using FastEndpoints;
using DbView.Application.Services;
using DbView.Core.Models;

namespace DbView.WebApi.Features.Connection.List
{
    internal sealed class GetConnectionListEndpoint : EndpointWithoutRequest<GetConnectionListResponse>
    {
        private readonly IConnectionAppService _connectionAppService;

        public GetConnectionListEndpoint(IConnectionAppService connectionAppService)
        {
            _connectionAppService = connectionAppService;
        }

        public override void Configure()
        {
            Get("/connections");
            AllowAnonymous();
        }

        public override async Task HandleAsync(CancellationToken c)
        {
            // 这里应该从JWT token获取用户ID，暂时使用1
            var userId = 1L;
            var connections = await _connectionAppService.GetConnectionsByUserIdAsync(userId, c);
            
            Response = new GetConnectionListResponse
            {
                Items = connections.Select(x => new ConnectionDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Host = x.Host,
                    Port = x.Port,
                    DatabaseName = x.DatabaseName,
                    DbType = x.DbType,
                    CreatedAt = x.CreatedAt
                }).ToList(),
                Total = connections.Count
            };
        }
    }
}
```

```csharp
// DbView.WebApi/Features/Connection/List/Models.cs
namespace DbView.WebApi.Features.Connection.List
{
    public class GetConnectionListResponse
    {
        public List<ConnectionDto> Items { get; set; } = new();
        public int Total { get; set; }
    }

    public class ConnectionDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string DatabaseName { get; set; } = string.Empty;
        public string DbType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
```

- [ ] **Step 2: 创建创建连接端点**

```csharp
// DbView.WebApi/Features/Connection/Create/Endpoint.cs
using FastEndpoints;
using DbView.Application.Services;
using DbView.Core.Models;

namespace DbView.WebApi.Features.Connection.Create
{
    internal sealed class CreateConnectionEndpoint : Endpoint<CreateConnectionRequest, CreateConnectionResponse>
    {
        private readonly IConnectionAppService _connectionAppService;

        public CreateConnectionEndpoint(IConnectionAppService connectionAppService)
        {
            _connectionAppService = connectionAppService;
        }

        public override void Configure()
        {
            Post("/connections");
            AllowAnonymous();
            Validator<CreateConnectionValidator>();
        }

        public override async Task HandleAsync(CreateConnectionRequest r, CancellationToken c)
        {
            var userId = 1L;
            var connection = new Connection
            {
                Name = r.Name,
                Host = r.Host,
                Port = r.Port,
                DatabaseName = r.DatabaseName,
                Username = r.Username,
                Password = r.Password,
                DbType = r.DbType,
                UserId = userId
            };

            var created = await _connectionAppService.CreateConnectionAsync(connection, c);

            Response = new CreateConnectionResponse
            {
                Id = created.Id,
                Message = "Connection created successfully"
            };
        }
    }
}
```

```csharp
// DbView.WebApi/Features/Connection/Create/Models.cs
using FastEndpoints;
using FluentValidation;

namespace DbView.WebApi.Features.Connection.Create
{
    public class CreateConnectionRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string DatabaseName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string DbType { get; set; } = string.Empty;
    }

    public class CreateConnectionResponse
    {
        public long Id { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class CreateConnectionValidator : Validator<CreateConnectionRequest>
    {
        public CreateConnectionValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("连接名称不能为空")
                .MaximumLength(100).WithMessage("连接名称最多100个字符");

            RuleFor(x => x.Host)
                .NotEmpty().WithMessage("主机地址不能为空");

            RuleFor(x => x.Port)
                .InclusiveBetween(1, 65535).WithMessage("端口必须在1-65535之间");

            RuleFor(x => x.DatabaseName)
                .NotEmpty().WithMessage("数据库名称不能为空");

            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("用户名不能为空");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("密码不能为空");

            RuleFor(x => x.DbType)
                .NotEmpty().WithMessage("数据库类型不能为空")
                .Must(x => new[] { "postgresql", "mysql", "sqlite", "sqlserver" }.Contains(x.ToLower()))
                .WithMessage("不支持的数据库类型");
        }
    }
}
```

- [ ] **Step 3: 创建获取连接详情端点**

```csharp
// DbView.WebApi/Features/Connection/Get/Endpoint.cs
using FastEndpoints;
using DbView.Application.Services;
using DbView.Core.Models;

namespace DbView.WebApi.Features.Connection.Get
{
    internal sealed class GetConnectionEndpoint : Endpoint<GetConnectionRequest, GetConnectionResponse>
    {
        private readonly IConnectionAppService _connectionAppService;

        public GetConnectionEndpoint(IConnectionAppService connectionAppService)
        {
            _connectionAppService = connectionAppService;
        }

        public override void Configure()
        {
            Get("/connections/{Id}");
            AllowAnonymous();
        }

        public override async Task HandleAsync(GetConnectionRequest r, CancellationToken c)
        {
            var connection = await _connectionAppService.GetConnectionByIdAsync(r.Id, c);
            
            if (connection == null)
            {
                await SendNotFoundAsync(c);
                return;
            }

            Response = new GetConnectionResponse
            {
                Id = connection.Id,
                Name = connection.Name,
                Host = connection.Host,
                Port = connection.Port,
                DatabaseName = connection.DatabaseName,
                Username = connection.Username,
                DbType = connection.DbType,
                CreatedAt = connection.CreatedAt,
                UpdatedAt = connection.UpdatedAt
            };
        }
    }
}
```

```csharp
// DbView.WebApi/Features/Connection/Get/Models.cs
namespace DbView.WebApi.Features.Connection.Get
{
    public class GetConnectionRequest
    {
        public long Id { get; set; }
    }

    public class GetConnectionResponse
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string DatabaseName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string DbType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
```

- [ ] **Step 4: 创建更新连接端点**

```csharp
// DbView.WebApi/Features/Connection/Update/Endpoint.cs
using FastEndpoints;
using DbView.Application.Services;
using DbView.Core.Models;

namespace DbView.WebApi.Features.Connection.Update
{
    internal sealed class UpdateConnectionEndpoint : Endpoint<UpdateConnectionRequest, UpdateConnectionResponse>
    {
        private readonly IConnectionAppService _connectionAppService;

        public UpdateConnectionEndpoint(IConnectionAppService connectionAppService)
        {
            _connectionAppService = connectionAppService;
        }

        public override void Configure()
        {
            Put("/connections/{Id}");
            AllowAnonymous();
            Validator<UpdateConnectionValidator>();
        }

        public override async Task HandleAsync(UpdateConnectionRequest r, CancellationToken c)
        {
            var existing = await _connectionAppService.GetConnectionByIdAsync(r.Id, c);
            
            if (existing == null)
            {
                await SendNotFoundAsync(c);
                return;
            }

            var connection = new Connection
            {
                Id = r.Id,
                Name = r.Name,
                Host = r.Host,
                Port = r.Port,
                DatabaseName = r.DatabaseName,
                Username = r.Username,
                Password = r.Password,
                DbType = r.DbType,
                UserId = existing.UserId,
                CreatedAt = existing.CreatedAt
            };

            await _connectionAppService.UpdateConnectionAsync(connection, c);

            Response = new UpdateConnectionResponse
            {
                Message = "Connection updated successfully"
            };
        }
    }
}
```

```csharp
// DbView.WebApi/Features/Connection/Update/Models.cs
using FastEndpoints;
using FluentValidation;

namespace DbView.WebApi.Features.Connection.Update
{
    public class UpdateConnectionRequest
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string DatabaseName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string DbType { get; set; } = string.Empty;
    }

    public class UpdateConnectionResponse
    {
        public string Message { get; set; } = string.Empty;
    }

    public class UpdateConnectionValidator : Validator<UpdateConnectionRequest>
    {
        public UpdateConnectionValidator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0).WithMessage("连接ID无效");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("连接名称不能为空")
                .MaximumLength(100).WithMessage("连接名称最多100个字符");

            RuleFor(x => x.Host)
                .NotEmpty().WithMessage("主机地址不能为空");

            RuleFor(x => x.Port)
                .InclusiveBetween(1, 65535).WithMessage("端口必须在1-65535之间");

            RuleFor(x => x.DatabaseName)
                .NotEmpty().WithMessage("数据库名称不能为空");

            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("用户名不能为空");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("密码不能为空");

            RuleFor(x => x.DbType)
                .NotEmpty().WithMessage("数据库类型不能为空")
                .Must(x => new[] { "postgresql", "mysql", "sqlite", "sqlserver" }.Contains(x.ToLower()))
                .WithMessage("不支持的数据库类型");
        }
    }
}
```

- [ ] **Step 5: 创建删除连接端点**

```csharp
// DbView.WebApi/Features/Connection/Delete/Endpoint.cs
using FastEndpoints;
using DbView.Application.Services;

namespace DbView.WebApi.Features.Connection.Delete
{
    internal sealed class DeleteConnectionEndpoint : Endpoint<DeleteConnectionRequest, DeleteConnectionResponse>
    {
        private readonly IConnectionAppService _connectionAppService;

        public DeleteConnectionEndpoint(IConnectionAppService connectionAppService)
        {
            _connectionAppService = connectionAppService;
        }

        public override void Configure()
        {
            Delete("/connections/{Id}");
            AllowAnonymous();
        }

        public override async Task HandleAsync(DeleteConnectionRequest r, CancellationToken c)
        {
            var existing = await _connectionAppService.GetConnectionByIdAsync(r.Id, c);
            
            if (existing == null)
            {
                await SendNotFoundAsync(c);
                return;
            }

            await _connectionAppService.DeleteConnectionAsync(r.Id, c);

            Response = new DeleteConnectionResponse
            {
                Message = "Connection deleted successfully"
            };
        }
    }
}
```

```csharp
// DbView.WebApi/Features/Connection/Delete/Models.cs
namespace DbView.WebApi.Features.Connection.Delete
{
    public class DeleteConnectionRequest
    {
        public long Id { get; set; }
    }

    public class DeleteConnectionResponse
    {
        public string Message { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 6: 创建测试连接端点**

```csharp
// DbView.WebApi/Features/Connection/Test/Endpoint.cs
using FastEndpoints;
using DbView.Application.Services;
using DbView.Core.Models;

namespace DbView.WebApi.Features.Connection.Test
{
    internal sealed class TestConnectionEndpoint : Endpoint<TestConnectionRequest, TestConnectionResponse>
    {
        private readonly IConnectionAppService _connectionAppService;

        public TestConnectionEndpoint(IConnectionAppService connectionAppService)
        {
            _connectionAppService = connectionAppService;
        }

        public override void Configure()
        {
            Post("/connections/test");
            AllowAnonymous();
            Validator<TestConnectionValidator>();
        }

        public override async Task HandleAsync(TestConnectionRequest r, CancellationToken c)
        {
            var connection = new Connection
            {
                Name = "Test",
                Host = r.Host,
                Port = r.Port,
                DatabaseName = r.DatabaseName,
                Username = r.Username,
                Password = r.Password,
                DbType = r.DbType
            };

            var success = await _connectionAppService.TestConnectionAsync(connection, c);

            Response = new TestConnectionResponse
            {
                Success = success,
                Message = success ? "Connection successful" : "Connection failed"
            };
        }
    }
}
```

```csharp
// DbView.WebApi/Features/Connection/Test/Models.cs
using FastEndpoints;
using FluentValidation;

namespace DbView.WebApi.Features.Connection.Test
{
    public class TestConnectionRequest
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string DatabaseName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string DbType { get; set; } = string.Empty;
    }

    public class TestConnectionResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class TestConnectionValidator : Validator<TestConnectionRequest>
    {
        public TestConnectionValidator()
        {
            RuleFor(x => x.Host)
                .NotEmpty().WithMessage("主机地址不能为空");

            RuleFor(x => x.Port)
                .InclusiveBetween(1, 65535).WithMessage("端口必须在1-65535之间");

            RuleFor(x => x.DatabaseName)
                .NotEmpty().WithMessage("数据库名称不能为空");

            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("用户名不能为空");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("密码不能为空");

            RuleFor(x => x.DbType)
                .NotEmpty().WithMessage("数据库类型不能为空")
                .Must(x => new[] { "postgresql", "mysql", "sqlite", "sqlserver" }.Contains(x.ToLower()))
                .WithMessage("不支持的数据库类型");
        }
    }
}
```

- [ ] **Step 7: 验证编译**

Run: `dotnet build DbView.WebApi/DbView.WebApi.csproj`
Expected: Build succeeded

- [ ] **Step 8: 提交代码**

```bash
git add DbView.WebApi/Features/Connection/
git commit -m "feat: add connection management endpoints"
```

### Task 6: 前端项目初始化

**Files:**
- Create: `frontend/package.json`
- Create: `frontend/vite.config.ts`
- Create: `frontend/tsconfig.json`
- Create: `frontend/src/main.ts`
- Create: `frontend/src/App.vue`
- Create: `frontend/src/router/index.ts`
- Create: `frontend/src/stores/index.ts`

- [ ] **Step 1: 初始化Vue3项目**

```bash
cd F:\work\DbView\frontend
npm create vue@latest . -- --typescript --router --pinia
```

- [ ] **Step 2: 安装TDesign依赖**

```bash
npm install tdesign-vue-next
```

- [ ] **Step 3: 安装其他依赖**

```bash
npm install axios @codemirror/lang-sql @codemirror/theme-one-dark codemirror vue-codemirror
```

- [ ] **Step 4: 配置Vite**

```typescript
// frontend/vite.config.ts
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true
      }
    }
  }
})
```

- [ ] **Step 5: 配置路由**

```typescript
// frontend/src/router/index.ts
import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/login',
      name: 'login',
      component: () => import('../views/Login.vue')
    },
    {
      path: '/',
      name: 'dashboard',
      component: () => import('../views/Dashboard.vue'),
      meta: { requiresAuth: true }
    }
  ]
})

router.beforeEach((to, from, next) => {
  const token = localStorage.getItem('token')
  if (to.meta.requiresAuth && !token) {
    next('/login')
  } else {
    next()
  }
})

export default router
```

- [ ] **Step 6: 创建HTTP请求工具**

```typescript
// frontend/src/utils/request.ts
import axios from 'axios'

const request = axios.create({
  baseURL: '/api',
  timeout: 10000
})

request.interceptors.request.use(
  config => {
    const token = localStorage.getItem('token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  error => {
    return Promise.reject(error)
  }
)

request.interceptors.response.use(
  response => {
    return response.data
  },
  error => {
    if (error.response?.status === 401) {
      localStorage.removeItem('token')
      window.location.href = '/login'
    }
    return Promise.reject(error)
  }
)

export default request
```

- [ ] **Step 7: 验证项目结构**

Run: `ls frontend/src/`
Expected: 看到创建的文件和目录

- [ ] **Step 8: 提交代码**

```bash
cd F:\work\DbView\frontend
git add .
git commit -m "feat: initialize Vue3 frontend project with TDesign"
```

### Task 7: 前端主界面布局

**Files:**
- Create: `frontend/src/layouts/MainLayout.vue`
- Create: `frontend/src/components/DatabaseTree.vue`
- Create: `frontend/src/components/SqlEditor.vue`
- Create: `frontend/src/components/DataTable.vue`

- [ ] **Step 1: 创建主布局组件**

```vue
<!-- frontend/src/layouts/MainLayout.vue -->
<template>
  <div class="main-layout">
    <!-- 左侧活动栏 -->
    <div class="activity-bar">
      <div class="activity-icon active" @click="toggleSidebar">
        <database-outlined />
      </div>
      <div class="activity-icon">
        <search-outlined />
      </div>
      <div class="activity-icon">
        <setting-outlined />
      </div>
    </div>

    <!-- 左侧侧边栏 -->
    <div class="sidebar" v-show="showSidebar">
      <div class="sidebar-header">
        <span>数据库资源管理器</span>
        <button class="refresh-btn" @click="refreshTree">
          <reload-outlined />
        </button>
      </div>
      <database-tree />
      <div class="sidebar-footer">
        <t-button theme="primary" block @click="showAddConnection">+ 新建连接</t-button>
      </div>
    </div>

    <!-- 主内容区 -->
    <div class="main-content">
      <!-- 顶部标签栏 -->
      <div class="tab-bar">
        <div
          v-for="tab in tabs"
          :key="tab.id"
          class="tab"
          :class="{ active: activeTab === tab.id }"
          @click="activeTab = tab.id"
        >
          <span>{{ tab.title }}</span>
          <span class="close-btn" @click.stop="closeTab(tab.id)">×</span>
        </div>
      </div>

      <!-- 编辑器区域 -->
      <div class="editor-area">
        <router-view />
      </div>

      <!-- 底部面板 -->
      <div class="bottom-panel">
        <div class="panel-tabs">
          <div class="panel-tab active">查询结果</div>
          <div class="panel-tab">消息</div>
          <div class="panel-tab">错误</div>
        </div>
        <div class="panel-content">
          <data-table v-if="queryResults" :data="queryResults" />
        </div>
      </div>

      <!-- 状态栏 -->
      <div class="status-bar">
        <span>{{ connectionStatus }}</span>
        <span>{{ encodingInfo }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { DatabaseOutlined, SearchOutlined, SettingOutlined, ReloadOutlined } from 'tdesign-icons-vue-next'

const showSidebar = ref(true)
const activeTab = ref('home')
const tabs = ref([
  { id: 'home', title: '首页' }
])
const queryResults = ref(null)
const connectionStatus = ref('就绪')
const encodingInfo = ref('PostgreSQL | UTF-8')

const toggleSidebar = () => {
  showSidebar.value = !showSidebar.value
}

const refreshTree = () => {
  // 刷新数据库树
}

const showAddConnection = () => {
  // 显示添加连接对话框
}

const closeTab = (id: string) => {
  tabs.value = tabs.value.filter(tab => tab.id !== id)
  if (activeTab.value === id && tabs.value.length > 0) {
    activeTab.value = tabs.value[0].id
  }
}
</script>

<style scoped>
.main-layout {
  display: flex;
  height: 100vh;
  background: #1e1e1e;
  color: #d4d4d4;
}

.activity-bar {
  width: 48px;
  background: #333;
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 10px 0;
}

.activity-icon {
  width: 40px;
  height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 15px;
  cursor: pointer;
  border-radius: 4px;
}

.activity-icon:hover {
  background: #3c3c3c;
}

.activity-icon.active {
  background: #007acc;
}

.sidebar {
  width: 250px;
  background: #252526;
  border-right: 1px solid #3c3c3c;
  display: flex;
  flex-direction: column;
}

.sidebar-header {
  padding: 10px 15px;
  border-bottom: 1px solid #3c3c3c;
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-weight: bold;
  font-size: 12px;
  text-transform: uppercase;
}

.refresh-btn {
  background: none;
  border: none;
  color: #d4d4d4;
  cursor: pointer;
}

.sidebar-footer {
  margin-top: auto;
  padding: 10px 15px;
  border-top: 1px solid #3c3c3c;
}

.main-content {
  flex: 1;
  display: flex;
  flex-direction: column;
}

.tab-bar {
  background: #252526;
  border-bottom: 1px solid #3c3c3c;
  display: flex;
}

.tab {
  padding: 8px 16px;
  border-right: 1px solid #3c3c3c;
  display: flex;
  align-items: center;
  cursor: pointer;
}

.tab.active {
  background: #1e1e1e;
}

.close-btn {
  margin-left: 8px;
}

.editor-area {
  flex: 1;
  background: #1e1e1e;
}

.bottom-panel {
  height: 150px;
  background: #1e1e1e;
  border-top: 1px solid #3c3c3c;
}

.panel-tabs {
  background: #252526;
  border-bottom: 1px solid #3c3c3c;
  display: flex;
  padding: 0 10px;
}

.panel-tab {
  padding: 6px 12px;
  font-size: 12px;
  cursor: pointer;
}

.panel-tab.active {
  background: #1e1e1e;
  border-right: 1px solid #3c3c3c;
}

.panel-content {
  padding: 10px;
  overflow: auto;
}

.status-bar {
  background: #007acc;
  padding: 4px 10px;
  font-size: 12px;
  display: flex;
  justify-content: space-between;
}
</style>
```

- [ ] **Step 2: 创建数据库树组件**

```vue
<!-- frontend/src/components/DatabaseTree.vue -->
<template>
  <div class="database-tree">
    <div v-for="connection in connections" :key="connection.id" class="connection-node">
      <div class="node-header" @click="toggleConnection(connection.id)">
        <span class="arrow" :class="{ expanded: expandedConnections.includes(connection.id) }">▶</span>
        <span class="icon">🖥️</span>
        <span class="name">{{ connection.name }}</span>
        <span class="type">{{ connection.dbType }}</span>
      </div>
      <div v-if="expandedConnections.includes(connection.id)" class="node-children">
        <div class="database-node" @click="toggleDatabase(connection.id)">
          <span class="arrow" :class="{ expanded: expandedDatabases.includes(connection.id) }">▶</span>
          <span class="icon">📁</span>
          <span class="name">{{ connection.databaseName }}</span>
        </div>
        <div v-if="expandedDatabases.includes(connection.id)" class="table-list">
          <div
            v-for="table in tables[connection.id]"
            :key="table.tableName"
            class="table-node"
            @click="selectTable(connection.id, table.tableName)"
          >
            <span class="icon">📋</span>
            <span class="name">{{ table.tableName }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { connectionApi } from '../api/connection'
import { tableApi } from '../api/table'

interface Connection {
  id: number
  name: string
  host: string
  port: number
  databaseName: string
  dbType: string
}

interface Table {
  tableName: string
  schemaName: string
}

const connections = ref<Connection[]>([])
const expandedConnections = ref<number[]>([])
const expandedDatabases = ref<number[]>([])
const tables = ref<Record<number, Table[]>>({})

onMounted(async () => {
  await loadConnections()
})

const loadConnections = async () => {
  try {
    const response = await connectionApi.getConnections()
    connections.value = response.items
  } catch (error) {
    console.error('Failed to load connections:', error)
  }
}

const toggleConnection = async (connectionId: number) => {
  if (expandedConnections.value.includes(connectionId)) {
    expandedConnections.value = expandedConnections.value.filter(id => id !== connectionId)
  } else {
    expandedConnections.value.push(connectionId)
    await loadTables(connectionId)
  }
}

const toggleDatabase = (connectionId: number) => {
  if (expandedDatabases.value.includes(connectionId)) {
    expandedDatabases.value = expandedDatabases.value.filter(id => id !== connectionId)
  } else {
    expandedDatabases.value.push(connectionId)
  }
}

const loadTables = async (connectionId: number) => {
  try {
    const response = await tableApi.getTables(connectionId)
    tables.value[connectionId] = response.items
  } catch (error) {
    console.error('Failed to load tables:', error)
  }
}

const selectTable = (connectionId: number, tableName: string) => {
  // 选中表，触发事件
  console.log('Selected table:', connectionId, tableName)
}
</script>

<style scoped>
.database-tree {
  padding: 10px 15px;
}

.node-header, .database-node, .table-node {
  display: flex;
  align-items: center;
  padding: 5px;
  cursor: pointer;
  border-radius: 4px;
}

.node-header:hover, .database-node:hover, .table-node:hover {
  background: #37373d;
}

.arrow {
  margin-right: 8px;
  transition: transform 0.2s;
}

.arrow.expanded {
  transform: rotate(90deg);
}

.icon {
  margin-right: 8px;
}

.name {
  flex: 1;
}

.type {
  font-size: 12px;
  color: #808080;
}

.node-children {
  margin-left: 20px;
  margin-top: 5px;
}

.table-list {
  margin-left: 20px;
  margin-top: 3px;
}
</style>
```

- [ ] **Step 3: 创建SQL编辑器组件**

```vue
<!-- frontend/src/components/SqlEditor.vue -->
<template>
  <div class="sql-editor">
    <div class="toolbar">
      <t-button theme="primary" @click="executeSql">运行</t-button>
      <t-button @click="formatSql">格式化</t-button>
      <t-button @click="exportResults">导出</t-button>
      <div class="spacer"></div>
      <span class="info">表: {{ tableName }} | 行数: {{ rowCount }}</span>
    </div>
    <div class="editor-container">
      <codemirror
        v-model="sql"
        :extensions="extensions"
        :style="{ height: '100%' }"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { Codemirror } from 'vue-codemirror'
import { sql } from '@codemirror/lang-sql'
import { oneDark } from '@codemirror/theme-one-dark'

const props = defineProps<{
  initialSql?: string
  tableName?: string
}>()

const emit = defineEmits<{
  (e: 'execute', sql: string): void
}>()

const sql = ref(props.initialSql || '')
const rowCount = ref(0)

const extensions = computed(() => [sql(), oneDark])

const executeSql = () => {
  emit('execute', sql.value)
}

const formatSql = () => {
  // 实现SQL格式化
}

const exportResults = () => {
  // 实现导出功能
}
</script>

<style scoped>
.sql-editor {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.toolbar {
  padding: 8px 15px;
  border-bottom: 1px solid #3c3c3c;
  display: flex;
  gap: 10px;
  align-items: center;
}

.spacer {
  flex: 1;
}

.info {
  font-size: 12px;
  color: #808080;
}

.editor-container {
  flex: 1;
  overflow: hidden;
}
</style>
```

- [ ] **Step 4: 创建数据表格组件**

```vue
<!-- frontend/src/components/DataTable.vue -->
<template>
  <div class="data-table">
    <t-table
      :data="data"
      :columns="columns"
      :pagination="pagination"
      @page-change="onPageChange"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'

const props = defineProps<{
  data: any[]
  columns?: any[]
}>()

const pagination = ref({
  current: 1,
  pageSize: 10,
  total: 0
})

const defaultColumns = computed(() => {
  if (props.data.length === 0) return []
  return Object.keys(props.data[0]).map(key => ({
    title: key,
    colKey: key
  }))
})

const columns = computed(() => props.columns || defaultColumns.value)

const onPageChange = (page: number) => {
  pagination.value.current = page
}
</script>

<style scoped>
.data-table {
  overflow: auto;
}
</style>
```

- [ ] **Step 5: 验证组件编译**

Run: `cd frontend && npm run build`
Expected: Build succeeded

- [ ] **Step 6: 提交代码**

```bash
cd F:\work\DbView\frontend
git add src/layouts/ src/components/
git commit -m "feat: add main layout and core components"
```

### Task 8: 前端页面和API

**Files:**
- Create: `frontend/src/views/Login.vue`
- Create: `frontend/src/views/Dashboard.vue`
- Create: `frontend/src/api/connection.ts`
- Create: `frontend/src/api/table.ts`
- Create: `frontend/src/api/sql.ts`
- Create: `frontend/src/api/user.ts`
- Create: `frontend/src/stores/connection.ts`
- Create: `frontend/src/stores/user.ts`

- [ ] **Step 1: 创建登录页面**

```vue
<!-- frontend/src/views/Login.vue -->
<template>
  <div class="login-page">
    <div class="login-card">
      <h2>DbView 登录</h2>
      <t-form ref="form" :data="formData" :rules="rules" @submit="onSubmit">
        <t-form-item label="用户名" name="username">
          <t-input v-model="formData.username" placeholder="请输入用户名"></t-input>
        </t-form-item>
        <t-form-item label="密码" name="password">
          <t-input v-model="formData.password" type="password" placeholder="请输入密码"></t-input>
        </t-form-item>
        <t-form-item>
          <t-button theme="primary" type="submit" block>登录</t-button>
        </t-form-item>
      </t-form>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { MessagePlugin } from 'tdesign-vue-next'
import { userApi } from '../api/user'

const router = useRouter()
const form = ref()

const formData = reactive({
  username: '',
  password: ''
})

const rules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }]
}

const onSubmit = async ({ validateResult }) => {
  if (validateResult === true) {
    try {
      const response = await userApi.login(formData.username, formData.password)
      localStorage.setItem('token', response.accessToken)
      MessagePlugin.success('登录成功')
      router.push('/')
    } catch (error) {
      MessagePlugin.error('登录失败')
    }
  }
}
</script>

<style scoped>
.login-page {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 100vh;
  background: #f5f5f5;
}

.login-card {
  width: 400px;
  padding: 40px;
  background: white;
  border-radius: 8px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

h2 {
  text-align: center;
  margin-bottom: 30px;
}
</style>
```

- [ ] **Step 2: 创建主界面**

```vue
<!-- frontend/src/views/Dashboard.vue -->
<template>
  <main-layout>
    <router-view />
  </main-layout>
</template>

<script setup lang="ts">
import MainLayout from '../layouts/MainLayout.vue'
</script>
```

- [ ] **Step 3: 创建连接API**

```typescript
// frontend/src/api/connection.ts
import request from '../utils/request'

export const connectionApi = {
  getConnections() {
    return request.get('/connections')
  },

  getConnection(id: number) {
    return request.get(`/connections/${id}`)
  },

  createConnection(data: any) {
    return request.post('/connections', data)
  },

  updateConnection(id: number, data: any) {
    return request.put(`/connections/${id}`, data)
  },

  deleteConnection(id: number) {
    return request.delete(`/connections/${id}`)
  },

  testConnection(data: any) {
    return request.post('/connections/test', data)
  }
}
```

- [ ] **Step 4: 创建表API**

```typescript
// frontend/src/api/table.ts
import request from '../utils/request'

export const tableApi = {
  getTables(connectionId: number) {
    return request.get(`/connections/${connectionId}/tables`)
  },

  getTableStructure(connectionId: number, tableName: string) {
    return request.get(`/connections/${connectionId}/tables/${tableName}`)
  },

  getTableData(connectionId: number, tableName: string, page: number, pageSize: number) {
    return request.get(`/connections/${connectionId}/tables/${tableName}/data`, {
      params: { page, pageSize }
    })
  },

  createTable(connectionId: number, data: any) {
    return request.post(`/connections/${connectionId}/tables`, data)
  },

  updateTable(connectionId: number, tableName: string, data: any) {
    return request.put(`/connections/${connectionId}/tables/${tableName}`, data)
  },

  deleteTable(connectionId: number, tableName: string) {
    return request.delete(`/connections/${connectionId}/tables/${tableName}`)
  }
}
```

- [ ] **Step 5: 创建SQL API**

```typescript
// frontend/src/api/sql.ts
import request from '../utils/request'

export const sqlApi = {
  executeSql(connectionId: number, sql: string) {
    return request.post(`/connections/${connectionId}/sql/execute`, { sql })
  },

  getSqlHistory(connectionId: number) {
    return request.get(`/connections/${connectionId}/sql/history`)
  },

  saveSql(connectionId: number, sql: string) {
    return request.post(`/connections/${connectionId}/sql/save`, { sql })
  }
}
```

- [ ] **Step 6: 创建用户API**

```typescript
// frontend/src/api/user.ts
import request from '../utils/request'

export const userApi = {
  login(username: string, password: string) {
    return request.post('/auth/login', { username, password })
  },

  logout() {
    return request.post('/auth/logout')
  },

  getUsers() {
    return request.get('/users')
  },

  updateUser(id: number, data: any) {
    return request.put(`/users/${id}`, data)
  }
}
```

- [ ] **Step 7: 创建连接状态管理**

```typescript
// frontend/src/stores/connection.ts
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { connectionApi } from '../api/connection'

export const useConnectionStore = defineStore('connection', () => {
  const connections = ref<any[]>([])
  const currentConnection = ref<any>(null)
  const loading = ref(false)

  const loadConnections = async () => {
    loading.value = true
    try {
      const response = await connectionApi.getConnections()
      connections.value = response.items
    } finally {
      loading.value = false
    }
  }

  const selectConnection = (connection: any) => {
    currentConnection.value = connection
  }

  return {
    connections,
    currentConnection,
    loading,
    loadConnections,
    selectConnection
  }
})
```

- [ ] **Step 8: 创建用户状态管理**

```typescript
// frontend/src/stores/user.ts
import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useUserStore = defineStore('user', () => {
  const token = ref(localStorage.getItem('token') || '')
  const user = ref(null)

  const setToken = (newToken: string) => {
    token.value = newToken
    localStorage.setItem('token', newToken)
  }

  const clearToken = () => {
    token.value = ''
    localStorage.removeItem('token')
  }

  return {
    token,
    user,
    setToken,
    clearToken
  }
})
```

- [ ] **Step 9: 验证编译**

Run: `cd frontend && npm run build`
Expected: Build succeeded

- [ ] **Step 10: 提交代码**

```bash
cd F:\work\DbView\frontend
git add src/views/ src/api/ src/stores/
git commit -m "feat: add login page, dashboard, and API services"
```

### Task 9: 集成测试和优化

**Files:**
- Modify: `DbView.WebApi/Program.cs`
- Modify: `frontend/src/App.vue`
- Create: `frontend/src/main.ts`

- [ ] **Step 1: 更新后端启动配置**

```csharp
// DbView.WebApi/Program.cs
using FastEndpoints;
using FastEndpoints.Security;
using FastEndpoints.Swagger;
using System.Text.Json;
using DbView.Application;
using DbView.Core;
using DbView.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 添加CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add services to the container.
builder.Services.AddControllers();

// 1. 添加 FastEndpoints
builder.Services.RegisterFastEndpoint(builder.Configuration);

// 2. 添加 MediatR (应用层)
builder.Services.AddMediator();
// 3. 注册服务
builder.Services.AddEntityMapper();

// 4. 添加安全策略

// 5. 添加基础设施服务
builder.Services.RegisterServicesFromDbViewInfrastructure();

// 6. 添加数据库上下文
builder.Services.AddDatabase(autoSync: true);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

//app.UseHttpsRedirection();
//app.UseDefaultFiles();
//app.UseStaticFiles();
app.UseCors();
app.UseAuthentication().UseAuthorization();

// 使用 FastEndpoints
app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "api";
    c.Versioning.PrependToRoute = true;
    c.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    //统一响应格式
    c.Endpoints.Configurator = ep =>
    {
        //ep.AllowAnonymous(); //如果所有接口都不需要授权时启用
        // 关键：禁用自动发送响应
        ep.DontAutoSendResponse();
        //关键：优先执行全局处理
        ep.PostProcessor<GlobalResponseProcessor>( FastEndpoints.Order.Before);
    };
    c.Errors.ResponseBuilder = (failures, ctx, statusCode) => {
        return null;
    };
});

app.MapControllers();
app.UseSwaggerGen();
if (app.Environment.IsDevelopment())
{
    app.MapGet("/", async context =>
    {
        context.Response.Redirect("/swagger"); // 重定向
        await Task.CompletedTask;
    });
}

app.Run();
```

- [ ] **Step 2: 更新前端主入口**

```typescript
// frontend/src/main.ts
import { createApp } from 'vue'
import { createPinia } from 'pinia'
import TDesign from 'tdesign-vue-next'
import 'tdesign-vue-next/es/style/index.css'

import App from './App.vue'
import router from './router'

const app = createApp(App)

app.use(createPinia())
app.use(router)
app.use(TDesign)

app.mount('#app')
```

- [ ] **Step 3: 更新App.vue**

```vue
<!-- frontend/src/App.vue -->
<template>
  <router-view />
</template>

<script setup lang="ts">
</script>

<style>
body {
  margin: 0;
  padding: 0;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
}
</style>
```

- [ ] **Step 4: 更新appsettings.json**

```json
// DbView.WebApi/appsettings.json
{
  "isDebug": true,
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5000"
      },
      "Https": {
        "Url": "https://localhost:5001"
      }
    }
  },
  "CorsPolicy": [ "http://localhost:3000" ],
  "Database": "postgresql",
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5432;Database=dbview;Username=postgres;Password=123456"
  },
  "RedisServer": {
    "Server": "127.0.0.1,defaultDatabase=db0,password=1"
  },
  "Jwt": {
    "Issuer": "dbview.cn",
    "Audience": "dbview.cn",
    "SecurityKey": "5c32565e162046edbb3b0401f3955feb",
    "Expire": 60
  }
}
```

- [ ] **Step 5: 验证后端编译**

Run: `dotnet build DbView.WebApi/DbView.WebApi.csproj`
Expected: Build succeeded

- [ ] **Step 6: 验证前端编译**

Run: `cd frontend && npm run build`
Expected: Build succeeded

- [ ] **Step 7: 提交代码**

```bash
git add .
git commit -m "feat: integrate backend and frontend, update configurations"
```

---

## 完成检查

- [ ] 后端API可以正常运行
- [ ] 前端界面可以正常显示
- [ ] 数据库连接可以正常测试
- [ ] 表列表可以正常显示
- [ ] SQL编辑器可以正常执行
- [ ] 用户可以正常登录

---

## 执行说明

这个实现计划包含了DbView数据库管理工具的完整实现。按照任务顺序执行，每个任务都包含具体的代码和验证步骤。

**执行选项：**

1. **Subagent-Driven（推荐）** - 我会为每个任务派遣一个独立的子代理，任务之间进行审查，快速迭代

2. **Inline Execution** - 在当前会话中执行任务，使用批处理和检查点

请选择执行方式。