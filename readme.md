# DbView · 多数据库可视化管理平台

> 一个类 **pgAdmin** 与**阿里云 DMS** 的轻量级、Web 化数据库管理工具：在一个浏览器标签页里，集中管理多种数据库的连接、表结构、表数据、SQL 查询与定时备份。无需安装桌面客户端，打开网页即可上手。

---

## 1. 产品简介

DbView 面向**开发、测试与运维**团队，解决「多数据库、多环境、来回切换客户端」的协作痛点。它以「**连接**」为核心，把日常最高频的数据库操作收敛进同一个 Web IDE：

- 用浏览器统一管理 **PostgreSQL / MySQL / SQL Server / SQLite / Oracle** 五种数据库；
- 像 IDE 一样：左侧树形浏览库表，中间查看与编辑数据，底部查看执行结果；
- 内置 SQL 编辑器、可视化筛选排序、**手动 + 定时自动备份**；
- 自带账号体系，支持团队协作与权限扩展。

无论你是本地调库、远程排障，还是做日常数据巡检，DbView 都让「多库管理」回归一个标签页。

---

## 2. 核心特性

| 特性 | 价值点 |
| --- | --- |
| **一套界面管 5 种库** | 统一语法适配（表名/字段名自动按库类型加引号），切换数据库零学习成本。 |
| **可视化连接管理** | 新建/编辑/删除连接，支持「先测试连接、再保存」，连接信息持久化，随用随连。 |
| **表结构一目了然** | 树形展开连接 → 表列表，支持表搜索、删除表（DROP TABLE），并展示字段名、类型、可空、主键、自增、默认值、注释等完整元信息。 |
| **表数据浏览与编辑** | 分页浏览、列排序、条件筛选；支持**新增 / 编辑 / 删除（按主键）**单行数据，所见即所得。 |
| **专业 SQL 编辑器** | CodeMirror 6 语法高亮 + 暗色主题；自动识别查询/非查询语句，返回结果集或影响行数，并显示执行耗时。 |
| **数据安全备份** | 一键生成 `.sql` 结构 + 数据备份文件；可配置**定时自动备份**与保留份数，后台无人值守运行。 |
| **团队协作账号** | 账号登录（JWT）、修改密码、用户管理（增删改查），为多角色协作预留空间。 |
| **Web IDE 体验** | 左侧连接/表树 + 顶部用户栏 + 中间数据/SQL 双标签，轻量状态提示，操作流畅。 |

---

## 3. 技术架构

### 3.1 总体分层

```
┌─────────────────────────────┐
│  前端 (Vue 3 + TDesign)      │  浏览器 / SPA
│  Vite 开发服务器 :3000       │
└──────────────┬──────────────┘
               │  HTTP /api  (开发态走 Vite 代理)
┌──────────────▼──────────────┐
│  后端 API (ASP.NET Core)     │  Kestrel :5000
│  FastEndpoints (REST)        │
│  ├─ WebApi       表现/路由层  │
│  ├─ Application  应用服务层    │
│  ├─ Infrastructure 数据/服务层 │
│  └─ Core        领域模型/抽象  │
└──────────────┬──────────────┘
               │  ADO.NET / FreeSql 驱动
┌──────────────▼──────────────┐
│  PostgreSQL / MySQL /        │
│  SQL Server / SQLite / Oracle│
└─────────────────────────────┘
```

- **后端采用 DDD 分层**：`Core`（领域模型、仓储抽象、备份抽象）→ `Infrastructure`（实体、仓储、数据库连接与备份服务）→ `Application`（应用服务协调）→ `WebApi`（FastEndpoints 端点与路由），职责清晰、易扩展。
- **前端采用组件化 + Pinia + Vue Router**：连接/表/SQL/用户按模块拆分，状态用 Pinia 统一管理。
- **生产态**：前端 `npm run build` 产物由后端通过 `MapFallbackToFile("index.html")` 托管，前后端同域部署，无需额外网关或反向代理配置。

### 3.2 技术栈

| 层 | 技术 |
| --- | --- |
| 后端框架 | ASP.NET Core（WebApi 目标框架 **.NET 10**，Core/Infrastructure 目标 **.NET 8**） |
| API 框架 | [FastEndpoints](https://fast-endpoints.com/) 7.2.0（REST 风格端点，路由前缀 `/api`） |
| ORM / 驱动 | FreeSql 各数据库 Provider（PostgreSQL / MySQL / SQLite / SQL Server）+ `Oracle.ManagedDataAccess.Core` |
| 对象映射 | Mapster |
| 认证 | JWT（HS256，登录颁发） |
| 前端框架 | Vue 3.5 + TypeScript + Vite 6 |
| UI 组件库 | TDesign Vue Next（`tdesign-vue-next` + 图标） |
| 状态管理 | Pinia |
| SQL 编辑器 | CodeMirror 6（`@codemirror/lang-sql` + `one-dark` 主题） |
| HTTP 客户端 | Axios |

---

## 4. 支持的数据库

连接对话框中可选类型及默认端口：

| 类型 | 标识 (dbType) | 默认端口 | 连接串特点 |
| --- | --- | --- | --- |
| PostgreSQL | `postgresql` | 5432 | Host/Port/Database/Username/Password |
| MySQL | `mysql` | 3306 | Server/Port/Database/User/Password |
| SQL Server | `sqlserver` | 1433 | Server,Port/Database/User Id/Password |
| SQLite | `sqlite` | — | 仅 Database（文件路径） |
| Oracle | `oracle` | 1521 | 使用 `SERVICE_NAME` 的 TNS 描述串 |

不同数据库的表名、字段名引用方式已自动适配（`"name"` / `` `name` `` / `[name]`），使用者无需关心差异。

---

## 5. 功能模块

### 5.1 连接管理
- 列表、详情、新建、编辑、删除。
- **「测试连接 → 成功后才能保存」**的交互约束，避免无效配置。
- 连接配置持久化到后端元数据库（FreeSql 实体 `db_connections`），一次配置、长期复用。

### 5.2 表管理
- **表列表**：按连接展开，支持搜索、删除表（DROP TABLE，二次确认）。
- **表结构**：读取并展示字段完整元信息（列名、类型、可空、主键、自增、默认值、注释）。
- **表数据**：
  - 分页浏览（基于主键或默认排序的 `LIMIT/OFFSET`）；
  - 条件筛选（等值/不等/范围/模糊/LIKE/空值判断等）；
  - 列排序（升/降序）；
  - 行级 **新增 / 编辑 / 删除**（删除依赖表存在主键）。

### 5.3 SQL 执行
- SQL 编辑器：语法高亮、暗色主题，预填 `SELECT * FROM <表>`。
- 自动判断语句类型：
  - `SELECT/SHOW/DESCRIBE/EXPLAIN` → 返回列头 + 结果集；
  - 其它 DML/DDL → 返回影响行数与耗时；
  - 执行失败返回错误信息，便于即时修正。
- 「数据 / SQL」双标签：选中表后可一键切到针对该表的 SQL 视图，交互顺滑。

### 5.4 备份
- **手动备份**：根据连接生成 `.sql`（含建表语句与 `INSERT` 数据），存放于后端 `backups/` 目录，可随时取用。
- **自动备份**：后台托管服务（`BackupSchedulerService`）每分钟轮询启用的备份计划，到时自动备份并清理超出保留份数的历史文件，实现无人值守。
- **备份配置**：每个连接可独立开关、设置间隔（小时）与保留份数，灵活适配不同重要级别的库。

### 5.5 用户管理
- 登录（JWT 签发）、退出登录、修改密码（登录用户自助）。
- 用户增删改查（后端完整端点），为管理员分配团队成员提供基础。

---

## 6. 项目结构

```
DbView/
├── backend/                      # 后端解决方案
│   └── src/
│       ├── DbView.sln
│       ├── DbView.Core/          # 领域层：模型、仓储接口、备份抽象、用户值对象
│       ├── DbView.Infrastructure/# 基础设施层：实体、通用仓储、DatabaseService、BackupService
│       ├── DbView.Application/   # 应用层：连接/表/SQL/用户 应用服务、DTO、映射
│       └── DbView.WebApi/        # 表现层：FastEndpoints 端点（按 Connection/Table/Sql/User/Backup 分目录）
├── frontend/                     # 前端工程（Vue3 + Vite + TDesign）
│   └── src/
│       ├── api/                  # 后端接口封装（connection/table/sql/structure/backup/user）
│       ├── components/           # 组件（connection/ table/ common/ 通用布局与编辑器）
│       ├── layouts/MainLayout.vue# 主框架（侧边栏 + 顶部用户栏 + 内容区）
│       ├── views/                # 页面（Login、Dashboard）
│       ├── stores/               # Pinia 状态（connection、user）
│       ├── router/               # 路由（登录 / 仪表盘，登录守卫）
│       └── utils/request.ts      # Axios 实例（注入 JWT、401 跳转登录）
├── docs/superpowers/             # 设计文档与实现计划（design / plan）
├── test1.db / test2.db           # SQLite 示例库（开发自测用）
├── sql1.json / sql2.json         # 示例建表 SQL 片段
└── readme.md                     # 本文件
```

---

## 7. 环境要求与启动

### 7.1 环境
- **后端**：.NET 10 SDK（同时兼容 .NET 8 运行库）；目标数据库对应的原生/托管驱动随 NuGet 一并还原。
- **前端**：Node.js ≥ 18（推荐 22，项目 `@tsconfig/node22`），npm。
- **数据库**：被管理的目标数据库实例（或本地 SQLite 文件）。

### 7.2 后端启动
```bash
cd backend/src
dotnet restore
dotnet run --project DbView.WebApi      # 默认 http://localhost:5000
```
- 接口统一前缀：`/api`
- 开发态已开启 Swagger，便于接口联调与自测。

### 7.3 前端启动（开发态）
```bash
cd frontend
npm install
npm run dev        # Vite 开发服务器默认 http://localhost:3000
```
- 开发态通过 Vite 代理将 `/api` 转发到 `http://localhost:5000/api`（见 `.env.development`）。
- 登录后访问仪表盘，新建连接 → 展开 → 选表即可使用。

### 7.4 生产构建（前后端一体）
```bash
cd frontend
npm run build      # 产物输出到 dist/
# 将 dist/ 交由后端托管（MapFallbackToFile），或反向代理到后端 /api
```
- 生产态 API 基址为 `/api`（见 `.env.production`），与后端同域，部署简单。

---

## 8. API 概览

> 统一前缀 `/api`，路由形如 `/api/v1/...`（FastEndpoints 版本前置）。以下为已实现并可调用的端点：

### 连接 (Connection)
| 方法 | 路径 | 说明 |
| --- | --- | --- |
| GET | `/connections` | 连接列表 |
| POST | `/connections` | 新建连接 |
| GET | `/connections/{id}` | 连接详情 |
| PUT | `/connections/{id}` | 更新连接 |
| DELETE | `/connections/{id}` | 删除连接 |
| POST | `/connections/test` | 测试连接 |

### 表 (Table)
| 方法 | 路径 | 说明 |
| --- | --- | --- |
| GET | `/connections/{id}/tables` | 表列表 |
| GET | `/connections/{id}/tables/{table}` | 表结构（字段元信息） |
| GET | `/connections/{id}/tables/{table}/data` | 表数据（分页） |
| POST | `/connections/{id}/tables/{table}/query` | 表数据（筛选 + 排序 + 分页） |
| POST | `/connections/{id}/tables/{table}/data` | 新增数据行 |
| PUT | `/connections/{id}/tables/{table}/data` | 更新数据行 |
| DELETE | `/connections/{id}/tables/{table}/data/{pk}` | 删除数据行（按主键） |
| DELETE | `/connections/{id}/tables/{table}` | 删除表（DROP TABLE） |

### SQL
| 方法 | 路径 | 说明 |
| --- | --- | --- |
| POST | `/connections/{id}/sql/execute` | 执行 SQL（查询/非查询） |

### 用户 (User / Auth)
| 方法 | 路径 | 说明 |
| --- | --- | --- |
| POST | `/auth/login` | 登录（返回 JWT） |
| POST | `/auth/logout` | 退出登录 |
| POST | `/auth/change-password` | 修改密码 |
| GET | `/users` | 用户列表 |
| PUT | `/users/{id}` | 更新用户 |
| POST/DELETE/GET | `/users` 相关 | 用户创建 / 删除 / 详情 |

### 备份 (Backup)
| 方法 | 路径 | 说明 |
| --- | --- | --- |
| POST | `/connections/{id}/backups` | 手动创建备份 |
| GET | `/connections/{id}/backups` | 备份列表 |
| GET | `/connections/{id}/backup-config` | 读取自动备份配置 |
| PUT | `/connections/{id}/backup-config` | 更新自动备份配置 |
| （后台） | `BackupSchedulerService` | 按配置定时自动备份 + 清理 |

---

## 9. 产品路线图

DbView 持续演进，后续规划中的能力包括：

- **表结构可视化编辑**：在界面上直接建表、加/减字段、改字段类型，无需手写 DDL。
- **SQL 历史与收藏**：自动记录执行历史，支持收藏常用语句，提升重复工作效率。
- **结果导出**：表数据与 SQL 结果一键导出为 CSV / Excel，方便分析与分享。
- **备份增强**：备份恢复与下载、异地/对象存储归档，筑牢数据安全底线。
- **团队权限与审计**：细粒度角色（管理员 / 只读用户）、操作审计日志，适配企业级协作。
- **CI/CD 流水线**：补充构建与测试自动化，保障持续交付质量。

---

## 10. 相关文档

- `docs/superpowers/specs/2026-06-14-dbview-design.md` —— 总体设计文档
- `docs/superpowers/specs/2026-06-14-dms-features-design.md` —— DMS 风格功能设计
- `docs/superpowers/plans/` —— 分阶段实现计划

---

*DbView · 让多数据库管理回归一个浏览器标签页。*
