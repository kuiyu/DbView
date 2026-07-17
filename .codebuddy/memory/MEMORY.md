# DbView 项目长期记忆

## 架构概览
- 后端：.NET（FastEndpoints + FreeSql + Mapster），分层：Core（领域/实体/仓储接口）、Infrastructure（EF 风格仓储/实体/服务）、Application（应用服务）、WebApi（FastEndpoints 端点，按 Feature 分目录）。
- 前端：Vue3 + TDesign Vue Next + Pinia + vue-router + axios（utils/request.ts 统一封装，响应拦截返回 `response.data`，非 2xx 走 catch）。
- 鉴权：JWT（JwtHelper）。端点默认需要登录，调用 `AllowAnonymous()` 才放行。
- 响应包装：GlobalResponseProcessor 把 `Response` 包成 `ApiResponse { success, code, msg, result }`；错误或业务失败用 `HttpContext.Response.SendAsync(ApiResponse.Fail(msg), 状态码, ...)`。

## 关键约定（2026-07-17 确认）
- 用户存储：原仅 `appsettings.json` 的 `AppUsers` 配置段。已改为**数据库存储**（`dbview_user` 表，UserEntity 增加 UserName/Password/Role），登录时若库中无该用户且配置匹配则自动 seed 入库，使“改密码”可持久化。
- 表数据增改端点 `InsertData`/`UpdateData` 早已存在；InsertData 的 SQL 生成已加固（转义表名/列名，按类型处理数字/NULL/布尔）。
- 数据库类型支持：postgresql / mysql / sqlite / sqlserver / oracle；标识符引用规则见 DatabaseService/InsertData 中的 `GetQuotedTableName`/`GetQuotedColumnName`。

## 注意事项
- WebApi 项目目标框架为 net10.0，Core/Infrastructure 为 net8.0（可引用）。
- 构建命令（PowerShell）：`Set-Location d:\work\DbView\backend\src; dotnet build DbView.WebApi/DbView.WebApi.csproj -v q --nologo`。当前 0 错误，仅有 XML 文档/可空性警告。
- 前端未安装 node_modules，无法本地 type-check；改动需靠代码评审。
