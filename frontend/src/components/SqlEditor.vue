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
        v-model="sqlText"
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
  initialSql?: string
  tableName?: string
}>()

const emit = defineEmits<{
  (e: 'execute', value: string): void
}>()

const sqlText = ref(props.initialSql || '')
const rowCount = ref(0)

const extensions = computed(() => [sqlLang(), oneDark])

const executeSql = () => {
  emit('execute', sqlText.value)
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
