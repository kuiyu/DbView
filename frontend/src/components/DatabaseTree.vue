<template>
  <div class="database-tree">
    <div v-for="connection in connections" :key="connection.id" class="connection-node">
      <div class="node-header" @click="toggleConnection(connection.id)">
        <span class="arrow" :class="{ expanded: expandedConnections.includes(connection.id) }">▶</span>
        <span class="icon">🖥️</span>
        <span class="name">{{ connection.name }}</span>
        <span class="type">{{ connection.dbType }}</span>
      </div>
      <div v-if="expandedConnections.includes(connection.id)" class="node-children">
        <div class="database-node" @click="toggleDatabase(connection.id)">
          <span class="arrow" :class="{ expanded: expandedDatabases.includes(connection.id) }">▶</span>
          <span class="icon">📁</span>
          <span class="name">{{ connection.databaseName }}</span>
        </div>
        <div v-if="expandedDatabases.includes(connection.id)" class="table-list">
          <div
            v-for="table in tables[connection.id]"
            :key="table.tableName"
            class="table-node"
            @click="selectTable(connection.id, table.tableName)"
          >
            <span class="icon">📋</span>
            <span class="name">{{ table.tableName }}</span>
          </div>
        </div>
      </div>
    </div>
    <div v-if="connections.length === 0" class="empty-tree">
      暂无数据库连接
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { connectionApi } from '../api/connection'
import { tableApi } from '../api/table'

interface Connection {
  id: number
  name: string
  host: string
  port: number
  databaseName: string
  dbType: string
}

interface Table {
  tableName: string
  schemaName: string
}

const emit = defineEmits<{
  (e: 'select-table', data: { connectionId: number; tableName: string }): void
}>()

const connections = ref<Connection[]>([])
const expandedConnections = ref<number[]>([])
const expandedDatabases = ref<number[]>([])
const tables = ref<Record<number, Table[]>>({})

onMounted(async () => {
  await loadConnections()
})

const loadConnections = async () => {
  try {
    const response = await connectionApi.getConnections() as any
    connections.value = response.items || response || []
  } catch (error) {
    console.error('Failed to load connections:', error)
  }
}

const toggleConnection = async (connectionId: number) => {
  if (expandedConnections.value.includes(connectionId)) {
    expandedConnections.value = expandedConnections.value.filter(id => id !== connectionId)
  } else {
    expandedConnections.value.push(connectionId)
    await loadTables(connectionId)
  }
}

const toggleDatabase = (connectionId: number) => {
  if (expandedDatabases.value.includes(connectionId)) {
    expandedDatabases.value = expandedDatabases.value.filter(id => id !== connectionId)
  } else {
    expandedDatabases.value.push(connectionId)
  }
}

const loadTables = async (connectionId: number) => {
  try {
    const response = await tableApi.getTables(connectionId) as any
    tables.value[connectionId] = response.items || response || []
  } catch (error) {
    console.error('Failed to load tables:', error)
  }
}

const selectTable = (connectionId: number, tableName: string) => {
  emit('select-table', { connectionId, tableName })
}

defineExpose({ loadConnections })
</script>

<style scoped>
.database-tree {
  padding: 10px 15px;
}

.node-header, .database-node, .table-node {
  display: flex;
  align-items: center;
  padding: 5px;
  cursor: pointer;
  border-radius: 4px;
}

.node-header:hover, .database-node:hover, .table-node:hover {
  background: #37373d;
}

.arrow {
  margin-right: 8px;
  transition: transform 0.2s;
}

.arrow.expanded {
  transform: rotate(90deg);
}

.icon {
  margin-right: 8px;
}

.name {
  flex: 1;
}

.type {
  font-size: 12px;
  color: #808080;
}

.node-children {
  margin-left: 20px;
  margin-top: 5px;
}

.table-list {
  margin-left: 20px;
  margin-top: 3px;
}

.empty-tree {
  text-align: center;
  padding: 20px;
  color: #808080;
}
</style>
