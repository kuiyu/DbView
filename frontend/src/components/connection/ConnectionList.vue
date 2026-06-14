<template>
  <div class="connection-list">
    <div v-for="connection in connections" :key="connection.id" class="connection-item"
      :class="{ active: selectedId === connection.id }" @click="$emit('select', connection)">
      <div class="connection-icon">🖥️</div>
      <div class="connection-info">
        <div class="connection-name">{{ connection.name }}</div>
        <div class="connection-detail">{{ connection.host }}:{{ connection.port }}/{{ connection.databaseName }}</div>
      </div>
      <t-dropdown :options="menuOptions" @click="(data: any) => handleMenu(data, connection)">
        <t-button theme="default" size="small" variant="text">⋯</t-button>
      </t-dropdown>
    </div>
    <div v-if="connections.length === 0" class="empty-connections">
      暂无连接
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  connections: any[]
  selectedId?: number
}>()

const emit = defineEmits<{
  (e: 'select', connection: any): void
  (e: 'delete', connection: any): void
  (e: 'refresh'): void
}>()

const menuOptions = [
  { content: '删除', value: 'delete' }
]

const handleMenu = (data: any, connection: any) => {
  if (data.value === 'delete') {
    emit('delete', connection)
  }
}
</script>

<style scoped>
.connection-list {
  padding: 8px;
}

.connection-item {
  display: flex;
  align-items: center;
  padding: 10px 12px;
  margin-bottom: 4px;
  border-radius: 6px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.connection-item:hover {
  background-color: #f5f5f5;
}

.connection-item.active {
  background-color: #e8f4ff;
  border-left: 3px solid #0052d9;
}

.connection-icon {
  margin-right: 12px;
  font-size: 20px;
}

.connection-info {
  flex: 1;
  overflow: hidden;
}

.connection-name {
  font-weight: 500;
  color: #333;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.connection-detail {
  font-size: 12px;
  color: #999;
  margin-top: 4px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.empty-connections {
  text-align: center;
  padding: 20px;
  color: #999;
}
</style>
