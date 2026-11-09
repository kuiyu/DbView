<template>
  <t-dialog
    :visible="visible"
    :header="`向量检索 - ${tableName}`"
    :width="860"
    @close="onClose"
    @confirm="onSearch"
    :confirm-btn="{ content: '检索', loading: loading }"
    :cancel-btn="null"
  >
    <div class="vs-form">
      <t-form :data="form" label-width="96px" @submit.prevent>
        <t-form-item label="向量列">
          <t-select v-model="form.column" :options="columnOptions" placeholder="选择向量列" />
        </t-form-item>

        <t-form-item label="距离算法">
          <t-select v-model="form.operator" :options="operatorOptions" />
        </t-form-item>

        <t-form-item label="查询向量">
          <t-textarea
            v-model="form.vector"
            placeholder="例如：[0.1, 0.2, 0.3] 或直接填逗号分隔的数字 0.1,0.2,0.3"
            :autosize="{ minRows: 2, maxRows: 6 }"
          />
        </t-form-item>

        <t-form-item label="附加条件">
          <t-input
            v-model="form.where"
            placeholder="可选，例如：status = 'active'，将拼接到 WHERE 之后"
          />
        </t-form-item>

        <t-form-item label="返回条数">
          <t-input-number v-model="form.limit" :min="1" :max="1000" />
        </t-form-item>

        <t-form-item label="显示距离">
          <t-switch v-model="form.showDistance" />
          <span class="vs-tip">在结果末尾追加一列“similarity”显示距离值</span>
        </t-form-item>
      </t-form>

      <t-alert v-if="errorMsg" theme="error" :message="errorMsg" class="vs-alert" />

      <div v-if="resultColumns.length" class="vs-result">
        <div class="vs-result-title">检索结果（{{ resultRows.length }} 行）</div>
        <t-table
          :data="resultRows"
          :columns="resultColumns"
          row-key="__idx"
          size="small"
          :pagination="false"
          max-height="360"
          stripe
          bordered
        >
          <template #default="{ row, col }">
            <div class="cell-content" :title="formatCell(row[col.colKey])">
              {{ formatCell(row[col.colKey]) }}
            </div>
          </template>
        </t-table>
      </div>
    </div>
  </t-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { MessagePlugin } from 'tdesign-vue-next'
import { sqlApi } from '../../api/sql'

const props = defineProps<{
  connectionId: number
  tableName: string
  vectorColumns: string[]
}>()

const visible = ref(false)
const loading = ref(false)
const errorMsg = ref('')
const resultColumns = ref<any[]>([])
const resultRows = ref<any[]>([])

const operatorOptions = [
  { label: '余弦距离 (<=>)', value: '<=>' },
  { label: '欧氏距离 (<->)', value: '<->' },
  { label: '内积 (<#>)', value: '<#>' }
]

const form = reactive({
  column: '',
  operator: '<=>',
  vector: '',
  where: '',
  limit: 20,
  showDistance: true
})

const columnOptions = computed(() =>
  props.vectorColumns.map((c) => ({ label: c, value: c }))
)

watch(
  () => visible.value,
  (v) => {
    if (v && !form.column && props.vectorColumns.length) {
      form.column = props.vectorColumns[0]
    }
    if (v) {
      errorMsg.value = ''
      resultColumns.value = []
      resultRows.value = []
    }
  }
)

const open = () => {
  visible.value = true
}

const onClose = () => {
  visible.value = false
}

const quoteIdent = (name: string) => `"${name.replace(/"/g, '""')}"`

const normalizeVector = (input: string): string => {
  let s = (input || '').trim()
  if (!s) throw new Error('请输入查询向量')
  if (!s.startsWith('[')) {
    // 允许逗号/空格分隔的数字
    const nums = s
      .split(/[\s,]+/)
      .map((x) => x.trim())
      .filter((x) => x.length > 0)
    if (nums.length === 0) throw new Error('查询向量格式不正确')
    s = `[${nums.join(',')}]`
  }
  // 转义单引号，拼接到 SQL 字符串字面量
  return s.replace(/'/g, "''")
}

const buildSql = (): string => {
  const table = quoteIdent(props.tableName)
  const col = quoteIdent(form.column)
  const vec = `'${normalizeVector(form.vector)}'`
  const distanceExpr = `${col} ${form.operator} ${vec}`
  const selectList = form.showDistance ? `*, (${distanceExpr}) AS similarity` : '*'
  let sql = `SELECT ${selectList} FROM ${table} ORDER BY ${distanceExpr} LIMIT ${form.limit}`
  if (form.where.trim()) {
    sql += ` WHERE ${form.where.trim()}`
  }
  return sql
}

const formatCell = (value: any): string => {
  if (value === null || value === undefined) return ''
  if (typeof value === 'object') {
    try {
      return JSON.stringify(value)
    } catch {
      return '[Object]'
    }
  }
  return String(value)
}

const onSearch = async () => {
  errorMsg.value = ''
  resultColumns.value = []
  resultRows.value = []
  loading.value = true
  try {
    let sql: string
    try {
      sql = buildSql()
    } catch (e: any) {
      errorMsg.value = e.message || '参数有误'
      return
    }

    const resp: any = await sqlApi.executeSql(props.connectionId, sql)
    const body = resp.result || resp
    if (body.success === false) {
      errorMsg.value = body.detail || body.message || '检索失败'
      return
    }

    const columns: string[] = body.columns || []
    const data: any[][] = body.data || []
    resultColumns.value = columns.map((c) => ({
      title: c,
      colKey: c,
      ellipsis: true,
      minWidth: 90,
      width: 160
    }))
    resultRows.value = data.map((row, idx) => {
      const obj: Record<string, any> = { __idx: idx }
      columns.forEach((c, i) => {
        obj[c] = row[i]
      })
      return obj
    })
    if (resultRows.value.length === 0) {
      MessagePlugin.info('未检索到相似数据')
    }
  } catch (e: any) {
    errorMsg.value = e?.response?.data?.detail || e?.response?.data?.message || e?.message || '检索失败'
  } finally {
    loading.value = false
  }
}

defineExpose({ open })
</script>

<style scoped>
.vs-form {
  padding-top: 8px;
}
.vs-tip {
  margin-left: 8px;
  font-size: 12px;
  color: #888;
}
.vs-alert {
  margin: 12px 0;
}
.vs-result {
  margin-top: 12px;
  border-top: 1px solid #e8e8e8;
  padding-top: 12px;
}
.vs-result-title {
  font-size: 13px;
  font-weight: 600;
  margin-bottom: 8px;
}
.cell-content {
  max-height: 80px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  line-height: 1.5;
}
</style>
