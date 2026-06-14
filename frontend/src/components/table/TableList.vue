<template>
  <div class="table-list">
    <div class="table-list-header">
      <span class="title">{{ connection?.name || '数据库' }}</span>
      <t-button theme="default" size="small" @click="$emit('refresh')">
        <template #icon><refresh-icon /></template>
      </t-button>
    </div>
    <div class="table-search">
      <t-input v-model="searchKeyword" placeholder="搜索表..." prefix-icon="search" clearable />
    </div>
    <div class="table-items">
      <div v-for="table in filteredTables" :key="table.tableName"
        class="table-item" :class="{ active: selectedTable === table.tableName }"
        @click="$emit('select-table', table.tableName)">
        <span class="table-icon">📋</span>
        <span class="table-name">{{ table.tableName }}</span>
        <span v-if="table.schemaName" class="table-schema">{{ table.schemaName }}</span>
      </div>
      <div v-if="filteredTables.length === 0" class="empty-tables">
        {{ loading ? '加载中...' : '暂无表' }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { RefreshIcon } from 'tdesign-icons-vue-next'
import { tableApi } from '../../api/table'

interface Table {
  tableName: string
  schemaName?: string
}

const props = defineProps<{
  connection: any
  selectedTable?: string
}>()

const emit = defineEmits<{
  (e: 'select-table', tableName: string): void
  (e: 'refresh'): void
}>()

const tables = ref<Table[]>([])
const searchKeyword = ref('')
const loading = ref(false)

const filteredTables = computed(() => {
  if (!searchKeyword.value) return tables.value
  const keyword = searchKeyword.value.toLowerCase()
  return tables.value.filter(t => t.tableName.toLowerCase().includes(keyword))
})

const loadTables = async () => {
  if (!props.connection?.id) return
  loading.value = true
  try {
    const response: any = await tableApi.getTables(props.connection.id)
    tables.value = response.items || response || []
  } catch (error) {
    console.error('Failed to load tables:', error)
    tables.value = []
  } finally {
    loading.value = false
  }
}

watch(() => props.connection, () => {
  tables.value = []
  loadTables()
}, { immediate: true })

onMounted(() => {
  if (props.connection?.id) {
    loadTables()
  }
})
</script>

<style scoped>
.table-list {
  height: 100%;
  display: flex;
  flex-direction: column;
}

.table-list-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px;
  border-bottom: 1px solid #e8e8e8;
}

.table-list-header .title {
  font-weight: 500;
  color: #333;
}

.table-search {
  padding: 8px 12px;
  border-bottom: 1px solid #e8e8e8;
}

.table-items {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}

.table-item {
  display: flex;
  align-items: center;
  padding: 8px 12px;
  margin-bottom: 4px;
  border-radius: 4px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.table-item:hover {
  background-color: #f5f5f5;
}

.table-item.active {
  background-color: #e8f4ff;
}

.table-icon {
  margin-right: 10px;
}

.table-name {
  flex: 1;
  color: #333;
}

.table-schema {
  font-size: 12px;
  color: #999;
  margin-left: 8px;
}

.empty-tables {
  text-align: center;
  padding: 20px;
  color: #999;
}
</style>
