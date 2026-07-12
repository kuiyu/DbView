<template>
  <div class="data-table">
    <filter-bar :columns="columnNames" :loading="loading" @search="onFilterSearch" @reset="onFilterReset" />
    <div class="table-toolbar">
      <t-button theme="primary" @click="onAdd">新增</t-button>
      <t-button theme="danger" :disabled="!selectedRowKeys.length" @click="onBatchDelete">
        批量删除 ({{ selectedRowKeys.length }})
      </t-button>
      <t-button @click="onImport">导入</t-button>
      <t-button @click="onExport">导出</t-button>
      <t-button @click="onRefresh" :loading="loading">刷新</t-button>
    </div>

    <div class="table-container">
      <t-table
        :data="data"
        :columns="tableColumns"
        row-key="rowIndex"
        :selected-row-keys="selectedRowKeys"
        :pagination="pagination"
        :stripe="true"
        :bordered="true"
        :select-all="true"
        :loading="loading"
        @select-change="onSelectChange"
        @page-change="onPageChange"
        @select-all-change="onSelectAllChange"
      >
        <template #operation="{ row }">
			<t-space>
				<t-button theme="primary" size="small" @click="onEdit(row)">编辑</t-button>
				<t-button theme="danger" size="small" @click="onDelete(row)">删除</t-button>
			</t-space>
          
        </template>
        <template #default="{ row, col }">
          <div class="cell-content" :title="formatCellValue(row[col.colKey])">
            <template v-if="isDateColumn(col.colKey)">
              {{ formatDateValue(row[col.colKey]) }}
            </template>
            <template v-else-if="isBooleanColumn(col.colKey)">
              {{ row[col.colKey] ? 'true' : 'false' }}
            </template>
            <template v-else>
              {{ formatCellValue(row[col.colKey]) }}
            </template>
          </div>
        </template>
      </t-table>
    </div>

    <EditDialog ref="editDialog" @save="onEditSave" />
    <ImportDialog ref="importDialog" @import="onImportSave" />
    <ConfirmDialog ref="confirmDialog" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { MessagePlugin } from 'tdesign-vue-next'
import { tableApi, type QueryFilter } from '../../api/table'
import { structureApi } from '../../api/structure'
import EditDialog from './EditDialog.vue'
import ImportDialog from './ImportDialog.vue'
import ConfirmDialog from '../common/ConfirmDialog.vue'
import FilterBar from './FilterBar.vue'

const props = defineProps<{
  connectionId: number,
  tableName: string
}>()

const emit = defineEmits<{
  (e: 'refresh'): void
}>()

const rawData = ref<any[]>([])
const columnNames = ref<string[]>([])
const columnTypes = ref<Record<string, string>>({})
const primaryKeys = ref<Set<string>>(new Set())
const selectedRowKeys = ref<number[]>([])
const selectedRows = ref<any[]>([])
const pagination = ref({ current: 1, pageSize: 20, total: 0 })
const loading = ref(false)
const editDialog = ref<InstanceType<typeof EditDialog>>()
const importDialog = ref<InstanceType<typeof ImportDialog>>()
const confirmDialog = ref<InstanceType<typeof ConfirmDialog>>()
const activeFilters = ref<QueryFilter[]>([])
const isFiltering = ref(false)

const dateTypeKeywords = ['date', 'time', 'timestamp', 'datetime']
const booleanTypeKeywords = ['bool', 'boolean']

const isDateColumn = (colKey: string) => {
  const type = columnTypes.value[colKey]?.toLowerCase() || ''
  return dateTypeKeywords.some(kw => type.includes(kw))
}

const isBooleanColumn = (colKey: string) => {
  const type = columnTypes.value[colKey]?.toLowerCase() || ''
  return booleanTypeKeywords.some(kw => type.includes(kw))
}

const formatDateValue = (value: any): string => {
  if (value === null || value === undefined) return ''
  if (typeof value === 'string') {
    if (value.startsWith('0001-01-01') || value.startsWith('0001/01/01')) return ''
    if (value.includes('T')) {
      return value.split('T')[0]
    }
    return value
  }
  if (value instanceof Date) {
    const year = value.getFullYear()
    if (year < 1900) return ''
    return value.toLocaleDateString()
  }
  return String(value)
}

const data = computed(() => {
  if (!rawData.value.length || !columnNames.value.length) return []
  return rawData.value.map((row, rowIndex) => {
    const obj: Record<string, any> = {
      rowIndex: rowIndex,
      id: row[0] ?? rowIndex
    }
    columnNames.value.forEach((col, idx) => {
      obj[col] = row[idx]
    })
    return obj
  })
})

const tableColumns = computed(() => {
  const cols = columnNames.value.map((col) => ({
    title: col,
    colKey: col,
    width: col.toLowerCase() === 'id' ? 120 : 150,
    cell: 'default'
  }))
  return [
    { colKey: 'row-select', type: 'multiple', width: 50 },
    ...cols,
    { title: '操作', colKey: 'operation', width: 120, fixed: 'right' as const }
  ]
})

const formatCellValue = (value: any): string => {
	
  if (value === null || value === undefined) {
    return ''
  }
  if (typeof value === 'object') {
    try {
      return JSON.stringify(value)
    } catch {
      return '[Object]'
    }
  }
  if (typeof value === 'boolean') {
    return value ? 'true' : 'false'
  }
  if (value instanceof Date) {
    return value.toLocaleString()
  }
  return String(value)
}

const loadData = async () => {
  loading.value = true
  try {
    let result: any
    if (isFiltering.value && activeFilters.value.length > 0) {
      result = await tableApi.queryTableData(
        props.connectionId,
        props.tableName,
        activeFilters.value,
        '',
        'asc',
        pagination.value.current,
        pagination.value.pageSize
      )
    } else {
      result = await tableApi.getTableData(
        props.connectionId,
        props.tableName,
        pagination.value.current,
        pagination.value.pageSize
      )
    }
    // 兼容后端GlobalResponseProcessor包装的响应格式 {success, code, msg, result: {...}}
    // 以及未包装的原始响应格式 {items: [...], total: N}
    const data = result.result || result
    rawData.value = data.items || []
    pagination.value.total = data.total || 0
    selectedRowKeys.value = []
    selectedRows.value = []
    
    await loadColumnInfo()
  } catch (error) {
    MessagePlugin.error('加载数据失败')
    console.error(error)
  } finally {
    loading.value = false
  }
}

const loadColumnInfo = async () => {
  try {
    const result: any = await structureApi.getColumns(props.connectionId, props.tableName)
    const structure = Array.isArray(result) ? result : (result.result || [])

    if (structure && structure.length > 0) {
      columnNames.value = structure.map((col: any) => col.columnName || col.name || col)
      columnTypes.value = {}
      primaryKeys.value = new Set()
      structure.forEach((col: any) => {
        const name = col.columnName || col.name || col
        columnTypes.value[name] = col.dataType || col.type || ''
        if (col.isPrimaryKey || col.is_pk || col.primaryKey) {
          primaryKeys.value.add(name)
        }
      })
    } else if (rawData.value.length > 0 && Array.isArray(rawData.value[0])) {
      columnNames.value = rawData.value[0].map((_: any, idx: number) => `column_${idx + 1}`)
      primaryKeys.value = new Set([columnNames.value[0]])
    }
  } catch (error) {
    console.error('加载表结构失败:', error)
    if (rawData.value.length > 0 && Array.isArray(rawData.value[0])) {
      columnNames.value = rawData.value[0].map((_: any, idx: number) => `column_${idx + 1}`)
      primaryKeys.value = new Set([columnNames.value[0]])
    }
  }
}

const getColumnDefs = () => {
  return columnNames.value.map(col => ({
    colKey: col,
    title: col,
    dataType: columnTypes.value[col] || '',
    isPrimaryKey: primaryKeys.value.has(col)
  }))
}

const onAdd = () => {
  editDialog.value?.open(undefined, getColumnDefs())
}

const onEdit = (row: any) => {
  const rowData: Record<string, any> = {}
  columnNames.value.forEach(col => {
    rowData[col] = row[col]
  })
  editDialog.value?.open(rowData, getColumnDefs())
}

const onDelete = async (row: any) => {
  try {
    const primaryKey = row[columnNames.value[0]]
    if (primaryKey === undefined || primaryKey === null) {
      MessagePlugin.error('无法获取主键值')
      return
    }
    const confirmResult = await confirmDialog.value?.open('确定要删除这条数据吗？此操作无法撤销。', '删除确认')

    if (!confirmResult) {
      return
    }

    await tableApi.deleteTableData(props.connectionId, props.tableName, primaryKey)
    MessagePlugin.success('删除成功')
    loadData()
  } catch (error) {
    MessagePlugin.error('删除失败')
    console.error(error)
  }
}

const onBatchDelete = async () => {
  if (!selectedRowKeys.value.length) {
    MessagePlugin.warning('请先选择要删除的数据')
    return
  }
  try {
    const primaryKeys = selectedRows.value.map(row => row[columnNames.value[0]]).filter(k => k !== undefined)
    if (primaryKeys.length === 0) {
      MessagePlugin.error('无法获取主键值')
      return
    }
    const confirmResult = await confirmDialog.value?.open(
      `确定要删除选中的 ${selectedRowKeys.value.length} 条数据吗？此操作无法撤销。`,
      '批量删除确认'
    )

    if (!confirmResult) {
      return
    }

    await tableApi.batchDelete(props.connectionId, props.tableName, primaryKeys)
    selectedRowKeys.value = []
    selectedRows.value = []
    MessagePlugin.success('批量删除成功')
    loadData()
  } catch (error) {
    MessagePlugin.error('批量删除失败')
    console.error(error)
  }
}

const onSelectChange = (keys: number[], options: any[]) => {
  selectedRowKeys.value = keys
  selectedRows.value = options || []
}

const onSelectAllChange = (selected: boolean, options: any[]) => {
  if (selected) {
    selectedRowKeys.value = options.map((o: any) => o.rowIndex)
    selectedRows.value = options
  } else {
    selectedRowKeys.value = []
    selectedRows.value = []
  }
}

const onPageChange = (pageInfo: { current: number; previous: number | undefined; pageSize: number }) => {
  console.log('onPageChange:', pageInfo)
  pagination.value.current = pageInfo.current
  pagination.value.pageSize = pageInfo.pageSize
  selectedRowKeys.value = []
  selectedRows.value = []
  loadData()
}

const onImport = () => {
  importDialog.value?.open()
}

const onExport = () => {
  if (!data.value.length) {
    MessagePlugin.warning('没有数据可导出')
    return
  }
  const csv = [columnNames.value.join(',')]
  data.value.forEach((row) => {
    csv.push(columnNames.value.map(col => {
      const val = row[col]
      if (val === null || val === undefined) return ''
      if (typeof val === 'string' && val.includes(',')) return `"${val}"`
      return String(val)
    }).join(','))
  })
  const blob = new Blob([csv.join('\n')], { type: 'text/csv' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `${props.tableName}.csv`
  a.click()
  URL.revokeObjectURL(url)
  MessagePlugin.success('导出成功')
}

const onRefresh = () => {
  loadData()
  emit('refresh')
}

const onFilterSearch = (filters: QueryFilter[]) => {
  activeFilters.value = filters
  isFiltering.value = filters.length > 0
  pagination.value.current = 1
  loadData()
}

const onFilterReset = () => {
  activeFilters.value = []
  isFiltering.value = false
  pagination.value.current = 1
  loadData()
}

const onEditSave = async (formData: Record<string, any>, isEdit: boolean) => {
  try {
    if (isEdit) {
      const pkName = columnNames.value[0]
      const pkValue = formData[pkName]
      const updateData = { ...formData }
      delete updateData[pkName]
      await tableApi.updateData(props.connectionId, props.tableName, updateData, { [pkName]: pkValue })
    } else {
      await tableApi.insertData(props.connectionId, props.tableName, formData)
    }
    MessagePlugin.success(isEdit ? '更新成功' : '新增成功')
    loadData()
  } catch (error) {
    MessagePlugin.error(isEdit ? '更新失败' : '新增失败')
    console.error(error)
  }
}

const onImportSave = async (file: File) => {
  try {
    await tableApi.importData(props.connectionId, props.tableName, file)
    MessagePlugin.success('导入成功')
    loadData()
  } catch (error) {
    MessagePlugin.error('导入失败')
    console.error(error)
  }
}

watch(
  () => [props.connectionId, props.tableName],
  () => {
    pagination.value.current = 1
    selectedRowKeys.value = []
    selectedRows.value = []
    loadData()
  }
)

onMounted(() => {
  if (props.connectionId && props.tableName) {
    loadData()
  }
})
</script>

<style scoped>
.data-table {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.table-toolbar {
  padding: 12px 16px;
  display: flex;
  gap: 8px;
  flex-shrink: 0;
  border-bottom: 1px solid #e8e8e8;
}

.table-container {
  flex: 1;
  overflow: auto;
}

::deep(.t-table) {
  height: 100%;
}

.cell-content {
  max-height: 80px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  line-height: 1.5;
}

::deep(.t-table__pagination) {
  flex-shrink: 0;
  padding: 8px 16px;
  border-top: 1px solid #e8e8e8;
}
</style>
