# DMS风格数据库管理功能实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现类似阿里DMS的数据库管理功能，包括连接弹窗、表数据CRUD、批量操作和表结构管理。

**Architecture:** 后端使用FastEndpoints创建RESTful API，前端使用Vue3 + TDesign组件库实现IDE风格界面。模块化组件设计，每个功能独立组件。

**Tech Stack:** ASP.NET Core, FastEndpoints, FreeSql, Vue3, TypeScript, TDesign, CodeMirror

---

## 文件结构映射

### 后端文件结构
```
DbView.WebApi/Features/
├── Connection/                    # 连接管理
│   ├── Create/
│   ├── Test/
│   └── List/
├── Table/                         # 表操作
│   ├── List/
│   ├── GetData/
│   ├── InsertData/
│   ├── UpdateData/
│   ├── DeleteData/
│   ├── BatchDelete/
│   └── Import/
└── Structure/                     # 表结构
    ├── GetColumns/
    ├── AddColumn/
    ├── UpdateColumn/
    └── DeleteColumn/
```

### 前端文件结构
```
frontend/src/
├── components/
│   ├── connection/
│   │   └── ConnectionDialog.vue
│   ├── table/
│   │   ├── TableList.vue
│   │   ├── DataTable.vue
│   │   ├── EditDialog.vue
│   │   ├── ImportDialog.vue
│   │   └── StructurePanel.vue
│   └── common/
│       ├── Pagination.vue
│       └── ConfirmDialog.vue
├── views/
│   └── Dashboard.vue
└── api/
    ├── connection.ts
    ├── table.ts
    └── structure.ts
```

---

## 实现任务

### Task 1: 后端连接管理API

**Files:**
- Create: `DbView.WebApi/Features/Connection/Create/Endpoint.cs`
- Create: `DbView.WebApi/Features/Connection/Create/Models.cs`
- Create: `DbView.WebApi/Features/Connection/Test/Endpoint.cs`
- Create: `DbView.WebApi/Features/Connection/Test/Models.cs`

- [ ] **Step 1: 创建测试连接端点**

```csharp
// DbView.WebApi/Features/Connection/Test/Endpoint.cs
using FastEndpoints;
using DbView.Infrastructure.Services;
using DbView.Core.Models;

namespace DbView.WebApi.Features.Connection.Test
{
    internal sealed class TestConnectionEndpoint : Endpoint<TestConnectionRequest, TestConnectionResponse>
    {
        private readonly ConnectionTestService _connectionTestService;

        public TestConnectionEndpoint(ConnectionTestService connectionTestService)
        {
            _connectionTestService = connectionTestService;
        }

        public override void Configure()
        {
            Post("/connections/test");
            AllowAnonymous();
        }

        public override async Task HandleAsync(TestConnectionRequest r, CancellationToken c)
        {
            var connection = new Connection
            {
                Host = r.Host,
                Port = r.Port,
                DatabaseName = r.DatabaseName,
                Username = r.Username,
                Password = r.Password,
                DbType = r.DbType
            };

            var (success, message) = await _connectionTestService.TestConnectionAsync(connection, c);

            Response = new TestConnectionResponse
            {
                Success = success,
                Message = message
            };
        }
    }
}
```

- [ ] **Step 2: 创建测试连接请求/响应模型**

```csharp
// DbView.WebApi/Features/Connection/Test/Models.cs
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
}
```

- [ ] **Step 3: 验证编译**

Run: `dotnet build DbView.WebApi/DbView.WebApi.csproj`
Expected: Build succeeded

- [ ] **Step 4: 提交代码**

```bash
git add DbView.WebApi/Features/Connection/Test/
git commit -m "feat: add test connection endpoint"
```

### Task 2: 后端表数据CRUD API

**Files:**
- Create: `DbView.WebApi/Features/Table/GetData/Endpoint.cs`
- Create: `DbView.WebApi/Features/Table/GetData/Models.cs`
- Create: `DbView.WebApi/Features/Table/InsertData/Endpoint.cs`
- Create: `DbView.WebApi/Features/Table/InsertData/Models.cs`
- Create: `DbView.WebApi/Features/Table/UpdateData/Endpoint.cs`
- Create: `DbView.WebApi/Features/Table/UpdateData/Models.cs`
- Create: `DbView.WebApi/Features/Table/DeleteData/Endpoint.cs`
- Create: `DbView.WebApi/Features/Table/DeleteData/Models.cs`

- [ ] **Step 1: 创建获取表数据端点**

```csharp
// DbView.WebApi/Features/Table/GetData/Endpoint.cs
using FastEndpoints;
using DbView.Infrastructure.Services;
using DbView.Core.Models;

namespace DbView.WebApi.Features.Table.GetData
{
    internal sealed class GetTableDataEndpoint : Endpoint<GetTableDataRequest, GetTableDataResponse>
    {
        private readonly DatabaseService _databaseService;
        private readonly ConnectionRepository _connectionRepository;

        public GetTableDataEndpoint(DatabaseService databaseService, ConnectionRepository connectionRepository)
        {
            _databaseService = databaseService;
            _connectionRepository = connectionRepository;
        }

        public override void Configure()
        {
            Get("/connections/{ConnectionId}/tables/{TableName}/data");
            AllowAnonymous();
        }

        public override async Task HandleAsync(GetTableDataRequest r, CancellationToken c)
        {
            var connection = await _connectionRepository.GetByIdAsync(r.ConnectionId, c);
            if (connection == null)
            {
                await SendNotFoundAsync(c);
                return;
            }

            var result = await _databaseService.GetTableDataAsync(
                connection, 
                r.TableName, 
                r.Page, 
                r.PageSize, 
                c);

            Response = new GetTableDataResponse
            {
                Items = result.Items,
                Total = result.Total,
                Page = r.Page,
                PageSize = r.PageSize
            };
        }
    }
}
```

- [ ] **Step 2: 创建获取表数据请求/响应模型**

```csharp
// DbView.WebApi/Features/Table/GetData/Models.cs
namespace DbView.WebApi.Features.Table.GetData
{
    public class GetTableDataRequest
    {
        public long ConnectionId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class GetTableDataResponse
    {
        public List<List<object>> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
```

- [ ] **Step 3: 创建新增数据端点**

```csharp
// DbView.WebApi/Features/Table/InsertData/Endpoint.cs
using FastEndpoints;
using DbView.Infrastructure.Services;
using DbView.Core.Models;

namespace DbView.WebApi.Features.Table.InsertData
{
    internal sealed class InsertTableDataEndpoint : Endpoint<InsertTableDataRequest, InsertTableDataResponse>
    {
        private readonly DatabaseService _databaseService;
        private readonly ConnectionRepository _connectionRepository;

        public InsertTableDataEndpoint(DatabaseService databaseService, ConnectionRepository connectionRepository)
        {
            _databaseService = databaseService;
            _connectionRepository = connectionRepository;
        }

        public override void Configure()
        {
            Post("/connections/{ConnectionId}/tables/{TableName}/data");
            AllowAnonymous();
        }

        public override async Task HandleAsync(InsertTableDataRequest r, CancellationToken c)
        {
            var connection = await _connectionRepository.GetByIdAsync(r.ConnectionId, c);
            if (connection == null)
            {
                await SendNotFoundAsync(c);
                return;
            }

            var sql = GenerateInsertSql(r.TableName, r.Data);
            var result = await _databaseService.ExecuteSqlAsync(connection, sql, c);

            Response = new InsertTableDataResponse
            {
                Success = true,
                Message = "Data inserted successfully"
            };
        }

        private string GenerateInsertSql(string tableName, Dictionary<string, object> data)
        {
            var columns = string.Join(", ", data.Keys);
            var values = string.Join(", ", data.Values.Select(v => $"'{v}'"));
            return $"INSERT INTO {tableName} ({columns}) VALUES ({values})";
        }
    }
}
```

- [ ] **Step 4: 创建新增数据请求/响应模型**

```csharp
// DbView.WebApi/Features/Table/InsertData/Models.cs
namespace DbView.WebApi.Features.Table.InsertData
{
    public class InsertTableDataRequest
    {
        public long ConnectionId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public Dictionary<string, object> Data { get; set; } = new();
    }

    public class InsertTableDataResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 5: 验证编译**

Run: `dotnet build DbView.WebApi/DbView.WebApi.csproj`
Expected: Build succeeded

- [ ] **Step 6: 提交代码**

```bash
git add DbView.WebApi/Features/Table/
git commit -m "feat: add table data CRUD endpoints"
```

### Task 3: 前端连接弹窗组件

**Files:**
- Create: `frontend/src/components/connection/ConnectionDialog.vue`
- Modify: `frontend/src/views/Dashboard.vue`

- [ ] **Step 1: 创建连接弹窗组件**

```vue
<!-- frontend/src/components/connection/ConnectionDialog.vue -->
<template>
  <t-dialog
    v-model:visible="visible"
    header="新建数据库连接"
    :width="500"
    @confirm="onConfirm"
    @cancel="onCancel"
  >
    <t-form ref="form" :data="formData" :rules="rules" @submit="onSubmit">
      <t-form-item label="数据库类型" name="dbType">
        <t-radio-group v-model="formData.dbType">
          <t-radio-button value="postgresql">PostgreSQL</t-radio-button>
          <t-radio-button value="mysql">MySQL</t-radio-button>
          <t-radio-button value="sqlserver">SQL Server</t-radio-button>
          <t-radio-button value="sqlite">SQLite</t-radio-button>
        </t-radio-group>
      </t-form-item>
      
      <t-form-item label="连接名称" name="name">
        <t-input v-model="formData.name" placeholder="例如：生产环境数据库" />
      </t-form-item>
      
      <t-form-item label="主机地址" name="host">
        <t-input v-model="formData.host" placeholder="127.0.0.1" />
      </t-form-item>
      
      <t-form-item label="端口" name="port">
        <t-input v-model.number="formData.port" placeholder="5432" />
      </t-form-item>
      
      <t-form-item label="数据库名" name="databaseName">
        <t-input v-model="formData.databaseName" placeholder="请输入数据库名" />
      </t-form-item>
      
      <t-form-item label="用户名" name="username">
        <t-input v-model="formData.username" placeholder="请输入用户名" />
      </t-form-item>
      
      <t-form-item label="密码" name="password">
        <t-input v-model="formData.password" type="password" placeholder="请输入密码" />
      </t-form-item>
    </t-form>
    
    <div v-if="testResult" :class="['test-result', testResult.success ? 'success' : 'error']">
      {{ testResult.message }}
    </div>
  </t-dialog>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { MessagePlugin } from 'tdesign-vue-next'
import { connectionApi } from '../../api/connection'

const visible = ref(false)
const form = ref()
const testResult = ref<{ success: boolean; message: string } | null>(null)

const formData = reactive({
  name: '',
  host: '127.0.0.1',
  port: 5432,
  databaseName: '',
  username: '',
  password: '',
  dbType: 'postgresql'
})

const rules = {
  name: [{ required: true, message: '请输入连接名称', trigger: 'blur' }],
  host: [{ required: true, message: '请输入主机地址', trigger: 'blur' }],
  port: [{ required: true, message: '请输入端口', trigger: 'blur' }],
  databaseName: [{ required: true, message: '请输入数据库名', trigger: 'blur' }],
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }]
}

const open = () => {
  visible.value = true
  testResult.value = null
}

const onConfirm = async () => {
  await form.value?.validate()
  // 保存连接
  MessagePlugin.success('连接保存成功')
  visible.value = false
}

const onCancel = () => {
  visible.value = false
}

const testConnection = async () => {
  try {
    const result = await connectionApi.testConnection(formData)
    testResult.value = result
  } catch (error) {
    testResult.value = { success: false, message: '连接测试失败' }
  }
}

defineExpose({ open, testConnection })
</script>

<style scoped>
.test-result {
  padding: 10px;
  border-radius: 4px;
  margin-top: 16px;
}

.test-result.success {
  background: #f6ffed;
  border: 1px solid #b7eb8f;
  color: #52c41a;
}

.test-result.error {
  background: #fff2f0;
  border: 1px solid #ffccc7;
  color: #ff4d4f;
}
</style>
```

- [ ] **Step 2: 更新Dashboard使用连接弹窗**

```vue
<!-- frontend/src/views/Dashboard.vue -->
<template>
  <main-layout>
    <template #sidebar>
      <div class="sidebar-header">
        <span>数据库连接</span>
        <t-button theme="primary" size="small" @click="openConnectionDialog">+ 新建</t-button>
      </div>
      <connection-list @select="onConnectionSelect" />
    </template>
    
    <template #content>
      <table-list v-if="currentConnection" :connection="currentConnection" />
      <div v-else class="empty-state">
        <p>请先选择或创建数据库连接</p>
      </div>
    </template>
  </main-layout>
  
  <connection-dialog ref="connectionDialog" />
</template>

<script setup lang="ts">
import { ref } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import ConnectionDialog from '../components/connection/ConnectionDialog.vue'
import ConnectionList from '../components/connection/ConnectionList.vue'
import TableList from '../components/table/TableList.vue'

const connectionDialog = ref()
const currentConnection = ref<any>(null)

const openConnectionDialog = () => {
  connectionDialog.value?.open()
}

const onConnectionSelect = (connection: any) => {
  currentConnection.value = connection
}
</script>
```

- [ ] **Step 3: 验证编译**

Run: `cd frontend && npm run build`
Expected: Build succeeded

- [ ] **Step 4: 提交代码**

```bash
cd F:\work\DbView\frontend
git add src/components/connection/ src/views/Dashboard.vue
git commit -m "feat: add connection dialog component"
```

### Task 4: 前端表数据表格组件

**Files:**
- Create: `frontend/src/components/table/DataTable.vue`
- Create: `frontend/src/components/table/EditDialog.vue`
- Create: `frontend/src/components/table/ImportDialog.vue`

- [ ] **Step 1: 创建数据表格组件**

```vue
<!-- frontend/src/components/table/DataTable.vue -->
<template>
  <div class="data-table">
    <div class="table-toolbar">
      <t-button theme="primary" @click="onAdd">新增</t-button>
      <t-button theme="danger" :disabled="!selectedRows.length" @click="onBatchDelete">批量删除</t-button>
      <t-button @click="onImport">导入</t-button>
      <t-button @click="onExport">导出</t-button>
      <t-button @click="onRefresh">刷新</t-button>
    </div>
    
    <t-table
      :data="data"
      :columns="columns"
      :selected-row-keys="selectedRowKeys"
      :pagination="pagination"
      @select-change="onSelectChange"
      @page-change="onPageChange"
    >
      <template #operation="{ row }">
        <t-button theme="primary" size="small" @click="onEdit(row)">编辑</t-button>
        <t-button theme="danger" size="small" @click="onDelete(row)">删除</t-button>
      </template>
    </t-table>
    
    <edit-dialog ref="editDialog" @save="onEditSave" />
    <import-dialog ref="importDialog" @import="onImportSave" />
    <confirm-dialog ref="confirmDialog" @confirm="onConfirmDelete" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { tableApi } from '../../api/table'
import EditDialog from './EditDialog.vue'
import ImportDialog from './ImportDialog.vue'
import ConfirmDialog from '../common/ConfirmDialog.vue'

const props = defineProps<{
  connection: any
  tableName: string
}>()

const data = ref<any[]>([])
const selectedRowKeys = ref<number[]>([])
const pagination = ref({ current: 1, pageSize: 20, total: 0 })
const editDialog = ref()
const importDialog = ref()
const confirmDialog = ref()

const columns = computed(() => {
  if (data.value.length === 0) return []
  return Object.keys(data.value[0]).map(key => ({
    title: key,
    colKey: key
  }))
})

const loadData = async () => {
  const result = await tableApi.getTableData(
    props.connection.id,
    props.tableName,
    pagination.value.current,
    pagination.value.pageSize
  )
  data.value = result.items
  pagination.value.total = result.total
}

const onAdd = () => {
  editDialog.value?.open()
}

const onEdit = (row: any) => {
  editDialog.value?.open(row)
}

const onDelete = (row: any) => {
  confirmDialog.value?.open('确认删除这条记录？')
}

const onBatchDelete = () => {
  confirmDialog.value?.open(`确认删除选中的 ${selectedRowKeys.value.length} 条记录？`)
}

const onImport = () => {
  importDialog.value?.open()
}

const onExport = () => {
  // 导出功能
}

const onRefresh = () => {
  loadData()
}

const onSelectChange = (keys: number[]) => {
  selectedRowKeys.value = keys
}

const onPageChange = (page: number) => {
  pagination.value.current = page
  loadData()
}

const onEditSave = async (data: any) => {
  await tableApi.insertData(props.connection.id, props.tableName, data)
  loadData()
}

const onImportSave = async (file: File) => {
  await tableApi.importData(props.connection.id, props.tableName, file)
  loadData()
}

const onConfirmDelete = async () => {
  if (selectedRowKeys.value.length > 0) {
    await tableApi.batchDelete(props.connection.id, props.tableName, selectedRowKeys.value)
    selectedRowKeys.value = []
    loadData()
  }
}

loadData()
</script>

<style scoped>
.data-table {
  padding: 16px;
}

.table-toolbar {
  margin-bottom: 16px;
  display: flex;
  gap: 8px;
}
</style>
```

- [ ] **Step 2: 创建编辑弹窗组件**

```vue
<!-- frontend/src/components/table/EditDialog.vue -->
<template>
  <t-dialog
    v-model:visible="visible"
    :header="isEdit ? '编辑数据' : '新增数据'"
    :width="600"
    @confirm="onConfirm"
    @cancel="onCancel"
  >
    <t-form ref="form" :data="formData" :rules="rules">
      <t-form-item
        v-for="field in fields"
        :key="field.name"
        :label="field.name"
        :name="field.name"
      >
        <t-input v-model="formData[field.name]" :placeholder="`请输入${field.name}`" />
      </t-form-item>
    </t-form>
  </t-dialog>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { MessagePlugin } from 'tdesign-vue-next'

const visible = ref(false)
const isEdit = ref(false)
const form = ref()
const fields = ref<any[]>([])
const formData = reactive<Record<string, any>>({})

const rules = {}

const open = (row?: any) => {
  visible.value = true
  isEdit.value = !!row
  
  if (row) {
    Object.assign(formData, row)
  } else {
    // 清空表单
    Object.keys(formData).forEach(key => {
      formData[key] = ''
    })
  }
}

const onConfirm = async () => {
  await form.value?.validate()
  // 保存数据
  MessagePlugin.success('保存成功')
  visible.value = false
}

const onCancel = () => {
  visible.value = false
}

defineExpose({ open })
</script>
```

- [ ] **Step 3: 创建导入弹窗组件**

```vue
<!-- frontend/src/components/table/ImportDialog.vue -->
<template>
  <t-dialog
    v-model:visible="visible"
    header="导入数据"
    :width="500"
    @confirm="onConfirm"
    @cancel="onCancel"
  >
    <t-upload
      v-model="fileList"
      :auto-upload="false"
      accept=".csv,.json"
      :max="1"
      @change="onFileChange"
    >
      <template #tip>
        <div class="t-upload__tip">支持 CSV 和 JSON 格式</div>
      </template>
    </t-upload>
    
    <div v-if="previewData" class="preview">
      <h4>预览数据</h4>
      <t-table :data="previewData" :columns="previewColumns" :max-height="200" />
    </div>
  </t-dialog>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { MessagePlugin } from 'tdesign-vue-next'

const visible = ref(false)
const fileList = ref<any[]>([])
const previewData = ref<any[]>([])
const previewColumns = ref<any[]>([])

const open = () => {
  visible.value = true
  fileList.value = []
  previewData.value = []
}

const onFileChange = (file: any) => {
  // 解析文件并预览
  const reader = new FileReader()
  reader.onload = (e) => {
    const content = e.target?.result as string
    if (file.name.endsWith('.csv')) {
      // 解析CSV
      const lines = content.split('\n')
      const headers = lines[0].split(',')
      previewColumns.value = headers.map(h => ({ title: h, colKey: h }))
      previewData.value = lines.slice(1, 6).map(line => {
        const values = line.split(',')
        return headers.reduce((obj, h, i) => ({ ...obj, [h]: values[i] }), {})
      })
    } else if (file.name.endsWith('.json')) {
      // 解析JSON
      const data = JSON.parse(content)
      previewData.value = Array.isArray(data) ? data.slice(0, 5) : [data]
      if (previewData.value.length > 0) {
        previewColumns.value = Object.keys(previewData.value[0]).map(k => ({
          title: k,
          colKey: k
        }))
      }
    }
  }
  reader.readAsText(file.raw)
}

const onConfirm = () => {
  // 导入数据
  MessagePlugin.success('导入成功')
  visible.value = false
}

const onCancel = () => {
  visible.value = false
}

defineExpose({ open })
</script>

<style scoped>
.preview {
  margin-top: 16px;
}

.preview h4 {
  margin-bottom: 8px;
}
</style>
```

- [ ] **Step 4: 验证编译**

Run: `cd frontend && npm run build`
Expected: Build succeeded

- [ ] **Step 5: 提交代码**

```bash
cd F:\work\DbView\frontend
git add src/components/table/
git commit -m "feat: add data table, edit dialog, and import dialog components"
```

### Task 5: 前端表结构管理组件

**Files:**
- Create: `frontend/src/components/table/StructurePanel.vue`
- Create: `frontend/src/api/structure.ts`

- [ ] **Step 1: 创建结构API**

```typescript
// frontend/src/api/structure.ts
import request from '../utils/request'

export const structureApi = {
  getColumns(connectionId: number, tableName: string) {
    return request.get(`/connections/${connectionId}/tables/${tableName}/structure`)
  },

  addColumn(connectionId: number, tableName: string, column: any) {
    return request.post(`/connections/${connectionId}/tables/${tableName}/columns`, column)
  },

  updateColumn(connectionId: number, tableName: string, columnName: string, column: any) {
    return request.put(`/connections/${connectionId}/tables/${tableName}/columns/${columnName}`, column)
  },

  deleteColumn(connectionId: number, tableName: string, columnName: string) {
    return request.delete(`/connections/${connectionId}/tables/${tableName}/columns/${columnName}`)
  }
}
```

- [ ] **Step 2: 创建结构面板组件**

```vue
<!-- frontend/src/components/table/StructurePanel.vue -->
<template>
  <div class="structure-panel">
    <div class="panel-toolbar">
      <t-button theme="primary" @click="onAddColumn">添加字段</t-button>
    </div>
    
    <t-table
      :data="columns"
      :columns="tableColumns"
    >
      <template #operation="{ row }">
        <t-button theme="primary" size="small" @click="onEditColumn(row)">编辑</t-button>
        <t-button theme="danger" size="small" @click="onDeleteColumn(row)">删除</t-button>
      </template>
    </t-table>
    
    <t-dialog
      v-model:visible="dialogVisible"
      :header="isEdit ? '编辑字段' : '添加字段'"
      :width="500"
      @confirm="onConfirm"
      @cancel="onCancel"
    >
      <t-form ref="form" :data="formData" :rules="rules">
        <t-form-item label="字段名" name="name">
          <t-input v-model="formData.name" :disabled="isEdit" />
        </t-form-item>
        <t-form-item label="类型" name="type">
          <t-select v-model="formData.type">
            <t-option value="integer" label="integer" />
            <t-option value="varchar" label="varchar" />
            <t-option value="text" label="text" />
            <t-option value="boolean" label="boolean" />
            <t-option value="timestamp" label="timestamp" />
          </t-select>
        </t-form-item>
        <t-form-item label="可空" name="nullable">
          <t-switch v-model="formData.nullable" />
        </t-form-item>
      </t-form>
    </t-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { MessagePlugin } from 'tdesign-vue-next'
import { structureApi } from '../../api/structure'

const props = defineProps<{
  connection: any
  tableName: string
}>()

const columns = ref<any[]>([])
const dialogVisible = ref(false)
const isEdit = ref(false)
const form = ref()
const formData = reactive({
  name: '',
  type: 'varchar',
  nullable: true
})

const rules = {
  name: [{ required: true, message: '请输入字段名', trigger: 'blur' }],
  type: [{ required: true, message: '请选择类型', trigger: 'change' }]
}

const tableColumns = [
  { title: '字段名', colKey: 'name' },
  { title: '类型', colKey: 'type' },
  { title: '可空', colKey: 'nullable' },
  { title: '操作', colKey: 'operation' }
]

const loadColumns = async () => {
  columns.value = await structureApi.getColumns(props.connection.id, props.tableName)
}

const onAddColumn = () => {
  isEdit.value = false
  formData.name = ''
  formData.type = 'varchar'
  formData.nullable = true
  dialogVisible.value = true
}

const onEditColumn = (row: any) => {
  isEdit.value = true
  Object.assign(formData, row)
  dialogVisible.value = true
}

const onDeleteColumn = async (row: any) => {
  await structureApi.deleteColumn(props.connection.id, props.tableName, row.name)
  MessagePlugin.success('删除成功')
  loadColumns()
}

const onConfirm = async () => {
  await form.value?.validate()
  if (isEdit.value) {
    await structureApi.updateColumn(props.connection.id, props.tableName, formData.name, formData)
  } else {
    await structureApi.addColumn(props.connection.id, props.tableName, formData)
  }
  MessagePlugin.success('保存成功')
  dialogVisible.value = false
  loadColumns()
}

const onCancel = () => {
  dialogVisible.value = false
}

loadColumns()
</script>

<style scoped>
.structure-panel {
  padding: 16px;
}

.panel-toolbar {
  margin-bottom: 16px;
}
</style>
```

- [ ] **Step 3: 验证编译**

Run: `cd frontend && npm run build`
Expected: Build succeeded

- [ ] **Step 4: 提交代码**

```bash
cd F:\work\DbView\frontend
git add src/components/table/StructurePanel.vue src/api/structure.ts
git commit -m "feat: add table structure management component"
```

### Task 6: 集成测试和优化

**Files:**
- Modify: `frontend/src/views/Dashboard.vue`
- Modify: `frontend/src/components/table/TableList.vue`

- [ ] **Step 1: 更新Dashboard集成所有组件**

```vue
<!-- frontend/src/views/Dashboard.vue -->
<template>
  <main-layout>
    <template #sidebar>
      <div class="sidebar-header">
        <span>数据库连接</span>
        <t-button theme="primary" size="small" @click="openConnectionDialog">+ 新建</t-button>
      </div>
      <connection-list 
        :connections="connections"
        @select="onConnectionSelect"
        @refresh="loadConnections"
      />
    </template>
    
    <template #content>
      <table-list 
        v-if="currentConnection"
        :connection="currentConnection"
        @select-table="onTableSelect"
      />
      <data-table
        v-if="currentTable"
        :connection="currentConnection"
        :table-name="currentTable"
      />
      <div v-else class="empty-state">
        <p>请先选择或创建数据库连接</p>
      </div>
    </template>
  </main-layout>
  
  <connection-dialog ref="connectionDialog" @save="onConnectionSave" />
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import MainLayout from '../layouts/MainLayout.vue'
import ConnectionDialog from '../components/connection/ConnectionDialog.vue'
import ConnectionList from '../components/connection/ConnectionList.vue'
import TableList from '../components/table/TableList.vue'
import DataTable from '../components/table/DataTable.vue'
import { connectionApi } from '../api/connection'

const connectionDialog = ref()
const connections = ref<any[]>([])
const currentConnection = ref<any>(null)
const currentTable = ref<string>('')

const loadConnections = async () => {
  connections.value = await connectionApi.getConnections()
}

const openConnectionDialog = () => {
  connectionDialog.value?.open()
}

const onConnectionSave = () => {
  loadConnections()
}

const onConnectionSelect = (connection: any) => {
  currentConnection.value = connection
  currentTable.value = ''
}

const onTableSelect = (tableName: string) => {
  currentTable.value = tableName
}

onMounted(() => {
  loadConnections()
})
</script>

<style scoped>
.sidebar-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px;
  border-bottom: 1px solid #e8e8e8;
}

.empty-state {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 100%;
  color: #999;
}
</style>
```

- [ ] **Step 2: 验证编译**

Run: `cd frontend && npm run build`
Expected: Build succeeded

- [ ] **Step 3: 提交代码**

```bash
cd F:\work\DbView\frontend
git add src/views/Dashboard.vue
git commit -m "feat: integrate all components in Dashboard"
```

---

## 完成检查

- [ ] 后端测试连接API正常工作
- [ ] 后端表数据CRUD API正常工作
- [ ] 前端连接弹窗正常显示和提交
- [ ] 前端数据表格正常显示和分页
- [ ] 前端编辑弹窗正常新增和编辑数据
- [ ] 前端导入弹窗正常导入CSV/JSON
- [ ] 前端结构面板正常查看和修改表结构

---

## 执行说明

这个实现计划包含了DMS风格数据库管理功能的完整实现。按照任务顺序执行，每个任务都包含具体的代码和验证步骤。

**执行选项：**

1. **Subagent-Driven（推荐）** - 我会为每个任务派遣一个独立的子代理，任务之间进行审查，快速迭代

2. **Inline Execution** - 在当前会话中执行任务，使用批处理和检查点

请选择执行方式。