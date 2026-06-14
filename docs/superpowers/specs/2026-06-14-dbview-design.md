# DbView 数据库管理工具设计文档

## 项目概述
DbView是一个类似pgAdmin和阿里DMS的数据库管理工具，支持多数据库（PostgreSQL、MySQL、SQLite、SQL Server）的表管理、SQL执行、用户管理等功能。

## 技术栈
- **后端**：ASP.NET Core + FreeSql + DDD架构
- **前端**：Vue3 + TDesign + TypeScript
- **认证**：JWT Token
- **数据库**：支持多数据库（PostgreSQL、MySQL、SQLite、SQL Server）

## 架构设计

### 后端架构（DDD）
```
DbView.WebApi/           # WebApi层 - 接口和路由
DbView.Application/      # 应用层 - 业务逻辑协调
DbView.Core/             # 领域层 - 核心业务规则
DbView.Infrastructure/   # 基础设施层 - 数据访问和外部服务
```

### 前端架构
```
src/
├── api/                 # API调用
├── components/          # 通用组件
├── layouts/             # 布局组件
├── views/               # 页面视图
│   ├── connection/      # 连接管理
│   ├── table/           # 表管理
│   ├── sql/             # SQL编辑器
│   └── user/            # 用户管理
├── stores/              # 状态管理
└── utils/               # 工具函数
```

## 功能模块

### 1. 连接管理
- 数据库连接配置（主机、端口、用户名、密码、数据库名）
- 连接测试和保存
- 连接列表管理
- 连接状态监控

### 2. 表管理
- 查看数据库表列表
- 查看表结构（字段、类型、约束）
- 查看表数据（分页、排序、筛选）
- 创建表
- 修改表结构
- 删除表
- 导入/导出表数据

### 3. SQL编辑器
- SQL语法高亮
- 自动补全
- 执行SQL语句
- 查看执行结果
- 保存常用SQL
- SQL历史记录

### 4. 用户管理
- 用户登录/登出
- 用户信息管理
- 角色权限管理（管理员、普通用户）

### 5. 数据库工具
- 数据库备份/恢复
- 数据库同步
- 数据导入/导出

## 数据库设计

### 连接配置表 (db_connections)
```sql
CREATE TABLE db_connections (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    host VARCHAR(255) NOT NULL,
    port INTEGER NOT NULL,
    database_name VARCHAR(100) NOT NULL,
    username VARCHAR(100) NOT NULL,
    password VARCHAR(255) NOT NULL,
    db_type VARCHAR(50) NOT NULL, -- postgresql, mysql, sqlite, sqlserver
    user_id INTEGER REFERENCES users(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

### SQL历史记录表 (sql_history)
```sql
CREATE TABLE sql_history (
    id SERIAL PRIMARY KEY,
    connection_id INTEGER REFERENCES db_connections(id),
    user_id INTEGER REFERENCES users(id),
    sql_text TEXT NOT NULL,
    execution_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    status VARCHAR(20), -- success, error
    result_rows INTEGER,
    error_message TEXT
);
```

### 用户表 (users)
```sql
CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(100) UNIQUE NOT NULL,
    password VARCHAR(255) NOT NULL,
    email VARCHAR(255),
    role VARCHAR(50) DEFAULT 'user',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

## API设计

### 连接管理API
- `GET /api/connections` - 获取连接列表
- `POST /api/connections` - 创建连接
- `GET /api/connections/{id}` - 获取连接详情
- `PUT /api/connections/{id}` - 更新连接
- `DELETE /api/connections/{id}` - 删除连接
- `POST /api/connections/{id}/test` - 测试连接

### 表管理API
- `GET /api/connections/{id}/tables` - 获取表列表
- `GET /api/connections/{id}/tables/{tableName}` - 获取表结构
- `GET /api/connections/{id}/tables/{tableName}/data` - 获取表数据
- `POST /api/connections/{id}/tables` - 创建表
- `PUT /api/connections/{id}/tables/{tableName}` - 修改表
- `DELETE /api/connections/{id}/tables/{tableName}` - 删除表

### SQL执行API
- `POST /api/connections/{id}/sql/execute` - 执行SQL
- `GET /api/connections/{id}/sql/history` - 获取SQL历史
- `POST /api/connections/{id}/sql/save` - 保存SQL

### 用户API
- `POST /api/auth/login` - 用户登录
- `POST /api/auth/logout` - 用户登出
- `GET /api/users` - 获取用户列表
- `PUT /api/users/{id}` - 更新用户信息

## 前端界面设计

### 主界面布局（IDE风格）
- **左侧活动栏**：图标导航（资源管理器、搜索、设置等）
- **左侧侧边栏**：数据库资源管理器，显示连接和表结构
- **顶部标签栏**：多个文件/查询标签
- **中间编辑器**：SQL编辑器或表数据查看器
- **底部面板**：查询结果、消息、错误
- **状态栏**：显示连接状态和编码信息

### 主要页面
1. **登录页面**：用户名密码登录
2. **主界面**：IDE风格的数据库管理界面
3. **连接管理页面**：配置和管理数据库连接
4. **表结构页面**：查看和编辑表结构
5. **表数据页面**：查看和编辑表数据
6. **SQL编辑器页面**：编写和执行SQL

## 开发计划

### 第一阶段：基础架构（1-2天）
1. 后端基础架构搭建
2. 前端项目初始化
3. 用户认证模块

### 第二阶段：核心功能（3-5天）
1. 连接管理模块
2. 表管理模块（查看表结构、表数据）
3. 基本CRUD操作

### 第三阶段：高级功能（3-5天）
1. SQL编辑器
2. 数据导入/导出
3. 用户权限管理

### 第四阶段：优化完善（2-3天）
1. 界面优化
2. 性能优化
3. 错误处理和日志

## 设计原则
1. **模块化**：按功能模块划分，便于维护和扩展
2. **用户体验**：参考成熟产品（DMS、pgAdmin），提供熟悉的操作体验
3. **安全性**：密码加密存储，SQL注入防护
4. **性能**：分页查询，连接池管理
5. **可扩展性**：支持多种数据库类型，易于添加新功能