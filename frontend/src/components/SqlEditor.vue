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
        :model-value="props.sqlText || ''"
        @update:model-value="onSqlChange"
        :extensions="extensions"
        :style="{ height: '100%' }"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { Codemirror } from 'vue-codemirror'
import { sql as sqlLang } from '@codemirror/lang-sql'
import { oneDark } from '@codemirror/theme-one-dark'

const props = defineProps<{
  sqlText?: string
  tableName?: string
}>()

const emit = defineEmits<{
  (e: 'update:sqlText', value: string): void
  (e: 'execute', value: string): void
  (e: 'export'): void
}>()

const rowCount = ref(0)

const onSqlChange = (value: string) => {
  emit('update:sqlText', value)
}

const extensions = computed(() => [sqlLang(), oneDark])

const executeSql = () => {
  emit('execute', props.sqlText || '')
}

const formatSql = () => {
  const sql = (props.sqlText || '').trim()
  if (!sql) return

  // 采用主要子句关键字（不区分大小写）在前换行并转大写，形成基础格式化
  const clauseKeywords = [
    'SELECT', 'FROM', 'WHERE', 'GROUP BY', 'ORDER BY', 'HAVING',
    'LEFT JOIN', 'RIGHT JOIN', 'INNER JOIN', 'JOIN', 'LIMIT', 'OFFSET',
    'UNION', 'INSERT INTO', 'VALUES', 'UPDATE', 'SET', 'DELETE FROM'
  ]

  let formatted = sql
  for (const kw of clauseKeywords) {
    const pattern = new RegExp('\\b' + kw.replace(' ', '\\s+') + '\\b', 'gi')
    formatted = formatted.replace(pattern, (m) => '\n' + m.toUpperCase())
  }

  formatted = formatted
    .split('\n')
    .map((line, i) => (i === 0 ? line.trim() : '  ' + line.trim()))
    .join('\n')
    .trim()

  // AND / OR 独立成行并缩进
  formatted = formatted.replace(/\s+(AND|OR)\b/gi, '\n    $1')

  emit('update:sqlText', formatted)
}

const exportResults = () => {
  emit('export')
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
