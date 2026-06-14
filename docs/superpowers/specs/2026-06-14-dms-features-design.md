# DMS风格数据库管理功能设计文档

## 功能概述
实现类似阿里DMS的数据库管理功能，包括：
1. 数据库连接弹窗（经典DMS风格）
2. 表列表和数据查看
3. 完整CRUD操作（新增、编辑、删除、批量操作）
4. 表结构管理（查看和修改）

## 界面设计

### 1. 数据库连接弹窗
- **风格**：经典DMS风格
- **功能**：
  - 数据库类型选择（PostgreSQL、MySQL、SQL Server、SQLite）
  - 连接信息表单（名称、主机、端口、数据库名、用户名、密码）
  - 测试连接按钮和状态显示
  - 保存并连接按钮

### 2. 表管理界面
- **布局**：IDE风格
  - 左侧边栏：数据库连接信息和表列表
  - 顶部工具栏：新建表、导入、导出、刷新按钮
  - 标签页：数据、结构、SQL、信息
  - 数据表格：显示表数据，支持编辑和删除
  - 分页控件

### 3. 编辑弹窗
- **风格**：表单弹窗
- **功能**：
  - 动态表单（根据表字段生成）
  - 字段验证
  - 保存和取消按钮

### 4. 导入弹窗
- **风格**：文件上传弹窗
- **功能**：
  - 支持CSV和JSON格式
  - 文件预览
  - 导入选项（覆盖/追加）

## 技术实现

### 前端组件结构
```
frontend/src/
├── components/
│   ├── connection/
│   │   └── ConnectionDialog.vue      # 连接弹窗
│   ├── table/
│   │   ├── TableList.vue             # 表列表
│   │   ├── DataTable.vue             # 数据表格
│   │   ├── EditDialog.vue            # 编辑弹窗
│   │   ├── ImportDialog.vue          # 导入弹窗
│   │   └── StructurePanel.vue        # 结构面板
│   └── common/
│       ├── Pagination.vue            # 分页组件
│       └── ConfirmDialog.vue         # 确认弹窗
├── views/
│   └── Dashboard.vue                 # 主界面
└── api/
    ├── connection.ts                 # 连接API
    ├── table.ts                      # 表操作API
    └── structure.ts                  # 结构API
```

### 后端API设计

#### 连接管理API
- `POST /api/connections` - 创建连接
- `POST /api/connections/test` - 测试连接
- `GET /api/connections` - 获取连接列表

#### 表操作API
- `GET /api/connections/{id}/tables` - 获取表列表
- `GET /api/connections/{id}/tables/{name}/data` - 获取表数据
- `POST /api/connections/{id}/tables/{name}/data` - 新增数据
- `PUT /api/connections/{id}/tables/{name}/data/{pk}` - 更新数据
- `DELETE /api/connections/{id}/tables/{name}/data/{pk}` - 删除数据
- `POST /api/connections/{id}/tables/{name}/data/batch-delete` - 批量删除
- `POST /api/connections/{id}/tables/{name}/data/import` - 导入数据

#### 表结构API
- `GET /api/connections/{id}/tables/{name}/structure` - 获取表结构
- `POST /api/connections/{id}/tables/{name}/columns` - 添加字段
- `PUT /api/connections/{id}/tables/{name}/columns/{column}` - 修改字段
- `DELETE /api/connections/{id}/tables/{name}/columns/{column}` - 删除字段

## 数据流设计

### 连接流程
1. 用户点击"新建连接"按钮
2. 弹出连接弹窗
3. 用户填写连接信息
4. 点击"测试连接"按钮
5. 后端测试连接并返回结果
6. 连接成功后，用户点击"保存并连接"
7. 前端获取表列表并显示

### CRUD操作流程
1. 用户在表列表中选择表
2. 前端请求表数据
3. 显示数据表格
4. 用户点击"新增"按钮
5. 弹出编辑弹窗（空表单）
6. 用户填写数据并保存
7. 前端发送创建请求
8. 刷新数据表格

### 批量操作流程
1. 用户在数据表格中选择多条记录
2. 点击"批量删除"按钮
3. 弹出确认对话框
4. 用户确认删除
5. 前端发送批量删除请求
6. 刷新数据表格

## 错误处理

### 连接错误
- 连接失败：显示错误信息，允许重试
- 超时：显示超时提示，建议检查网络

### 数据操作错误
- 验证失败：显示字段错误信息
- 权限不足：显示权限错误提示
- 数据冲突：显示冲突信息，建议刷新

## 测试策略

### 单元测试
- 组件测试：测试各组件的渲染和交互
- API测试：测试后端接口的正确性

### 集成测试
- 连接测试：测试数据库连接功能
- CRUD测试：测试数据操作功能

### 端到端测试
- 用户流程测试：测试完整的用户操作流程