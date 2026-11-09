<template>
  <t-dialog
    :visible="visible"
    header="备份管理"
    :width="900"
    @close="onClose"
    :footer="false"
  >
    <div class="bm-toolbar">
      <t-button theme="primary" size="small" :loading="creating" @click="onCreate">立即备份</t-button>
      <t-button size="small" :loading="loading" @click="load">刷新</t-button>
      <span class="bm-tip">
        恢复会用备份内容覆盖目标库中的同名表；系统会在恢复前自动创建一份「恢复前快照」作为回退保障。
      </span>
    </div>

    <t-alert v-if="restoring" theme="info" class="bm-alert" message="正在恢复，请勿关闭窗口……" />

    <t-table
      :data="rows"
      :columns="columns"
      row-key="id"
      size="small"
      :pagination="{ pageSize: 10 }"
      :loading="loading"
      stripe
      bordered
    >
      <template #name="{ row }">
        <div class="bm-name">
          <span>{{ row.name }}</span>
          <t-tag v-if="row.trigger === 'auto'" theme="primary" size="small" variant="light">自动</t-tag>
          <t-tag v-else-if="row.trigger === 'pre_restore'" theme="warning" size="small" variant="light">快照</t-tag>
          <t-tag v-else theme="default" size="small" variant="light">手动</t-tag>
          <t-tag v-if="row.compressed" theme="success" size="small" variant="light">压缩</t-tag>
          <t-tag v-if="row.encrypted" theme="error" size="small" variant="light">加密</t-tag>
        </div>
        <div v-if="row.warnings && row.warnings.length" class="bm-warn">
          {{ row.warnings.length }} 条告警：{{ row.warnings[0] }}
        </div>
      </template>
      <template #size="{ row }">{{ formatSize(row.fileSize) }}</template>
      <template #objects="{ row }">
        <span v-if="objectCount(row) > 0">{{ objectCount(row) }} 个</span>
        <span v-else style="color: #999">-</span>
      </template>
      <template #createdAt="{ row }">{{ formatTime(row.createdAt) }}</template>
      <template #operation="{ row }">
        <t-space size="small">
          <t-button theme="primary" size="small" :disabled="restoring" @click="onRestore(row)">恢复</t-button>
          <t-button size="small" @click="onDownload(row)">下载</t-button>
          <t-button theme="danger" size="small" :disabled="restoring" @click="onDelete(row)">删除</t-button>
        </t-space>
      </template>
    </t-table>
  </t-dialog>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { MessagePlugin, Dialog } from 'tdesign-vue-next'
import { backupApi } from '../../api/backup'

const props = defineProps<{
  connectionId: number
}>()

const visible = ref(false)
const loading = ref(false)
const creating = ref(false)
const restoring = ref(false)
const rows = ref<any[]>([])

const columns = [
  { title: '名称', colKey: 'name', width: 280 },
  { title: '大小', colKey: 'size', width: 100 },
  { title: '数据库对象', colKey: 'objects', width: 100 },
  { title: '创建时间', colKey: 'createdAt', width: 170 },
  { title: '操作', colKey: 'operation', width: 200, fixed: 'right' as const }
]

const open = () => {
  visible.value = true
  load()
}

const onClose = () => {
  visible.value = false
}

const objectCount = (row: any): number => {
  const counts = row.schemaObjectCounts || {}
  return Object.values(counts).reduce((a: number, b: any) => a + (Number(b) || 0), 0)
}

const formatSize = (bytes: number): string => {
  if (!bytes) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  let value = bytes
  let i = 0
  while (value >= 1024 && i < units.length - 1) {
    value = value / 1024
    i++
  }
  return `${value.toFixed(i === 0 ? 0 : 1)} ${units[i]}`
}

const formatTime = (value: any): string => {
  if (!value) return '-'
  const d = new Date(value)
  if (isNaN(d.getTime())) return String(value)
  return d.toLocaleString()
}

const load = async () => {
  if (!props.connectionId) return
  loading.value = true
  try {
    const result: any = await backupApi.getBackups(props.connectionId)
    rows.value = (result.items || result.result?.items || []) as any[]
  } catch (error) {
    console.error(error)
    MessagePlugin.error('加载备份列表失败')
  } finally {
    loading.value = false
  }
}

const onCreate = async () => {
  creating.value = true
  try {
    await backupApi.createBackup(props.connectionId)
    MessagePlugin.success('备份成功')
    load()
  } catch (error: any) {
    MessagePlugin.error(error?.response?.data?.message || '备份失败')
  } finally {
    creating.value = false
  }
}

const onRestore = async (row: any) => {
  const confirmed = await Dialog.confirm({
    header: '确认恢复',
    body: `将用备份「${row.name}」覆盖目标库中的同名表（现有数据会被替换）。\n恢复前会自动创建一份快照作为回退保障。是否继续？`,
    theme: 'warning',
    confirmBtn: { content: '确认恢复', theme: 'danger' },
    cancelBtn: '取消'
  })
  if (!confirmed) return

  restoring.value = true
  try {
    const result: any = await backupApi.restoreBackup(props.connectionId, row.id)
    const body = result.result || result
    if (body.success === false) {
      MessagePlugin.error(body.message || '恢复失败')
      return
    }
    const mismatches = body.rowCountMismatches || []
    if (mismatches.length > 0) {
      MessagePlugin.warning(`恢复完成，但有 ${mismatches.length} 张表行数与备份不一致：${mismatches.join('；')}`)
    } else {
      MessagePlugin.success(body.message || '恢复成功')
    }
    load()
  } catch (error: any) {
    MessagePlugin.error(error?.response?.data?.message || '恢复失败')
  } finally {
    restoring.value = false
  }
}

const onDelete = async (row: any) => {
  const confirmed = await Dialog.confirm({
    header: '确认删除',
    body: `确定删除备份「${row.name}」吗？此操作不可撤销。`,
    theme: 'danger',
    confirmBtn: { content: '删除', theme: 'danger' },
    cancelBtn: '取消'
  })
  if (!confirmed) return

  try {
    await backupApi.deleteBackup(props.connectionId, row.id)
    MessagePlugin.success('删除成功')
    load()
  } catch (error) {
    MessagePlugin.error('删除失败')
  }
}

const onDownload = async (row: any) => {
  try {
    // 必须走 axios（带 token），直接 window.open 会因缺少 Authorization 头而 401
    const blob: any = await backupApi.downloadBackup(props.connectionId, row.id)
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = row.fileName || `${row.name}.sql`
    a.click()
    URL.revokeObjectURL(url)
  } catch (error) {
    MessagePlugin.error('下载失败')
  }
}

defineExpose({ open })
</script>

<style scoped>
.bm-toolbar {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}
.bm-tip {
  font-size: 12px;
  color: #888;
  margin-left: 4px;
}
.bm-alert {
  margin-bottom: 8px;
}
.bm-name {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
}
.bm-warn {
  font-size: 12px;
  color: #d54941;
  margin-top: 2px;
}
</style>
