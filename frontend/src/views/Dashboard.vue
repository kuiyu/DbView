<template>
  <main-layout @add-connection="openConnectionDialog" :show-top-bar="true">
    <template #sidebar>
      <div class="connection-panel">
        <div class="panel-header">
          <span class="panel-title">数据库连接</span>
          <t-button theme="primary" size="small" variant="text" @click="openConnectionDialog">
            <template #icon><add-icon /></template>
          </t-button>
        </div>
        <div class="connection-list">
          <div v-if="connections.length === 0" class="empty-connections">
            <div class="empty-icon">📁</div>
            <div class="empty-text">暂无数据库连接</div>
            <t-button theme="primary" size="small" @click="openConnectionDialog">+ 新建连接</t-button>
          </div>
          <div
            v-for="connection in connections"
            :key="connection.id"
            class="connection-node"
          >
            <!-- 连接头部 - 可点击展开/折叠 -->
            <div
              class="connection-header"
              :class="{ active: currentConnection?.id === connection.id, expanded: expandedConnections.includes(connection.id) }"
              @click="toggleConnection(connection)"
            >
              <span class="expand-icon" :class="{ expanded: expandedConnections.includes(connection.id) }">▶</span>
              <span class="connection-icon">🖥️</span>
              <span class="connection-name">{{ connection.name }}</span>
              <t-dropdown :options="menuOptions" @click="(data: any) => { if (data.value === 'delete') handleDeleteConnection(connection) }">
                <t-button theme="default" size="small" variant="text" @click.stop class="more-btn">⋯</t-button>
              </t-dropdown>
            </div>
            <!-- 展开的表列表 -->
            <div v-if="expandedConnections.includes(connection.id)" class="connection-tables">
              <div v-if="loadingTables && currentConnection?.id === connection.id" class="loading-text">
                加载中...
              </div>
              <div v-else-if="tables.length === 0" class="no-tables">暂无表</div>
              <div
                v-for="table in tables"
                :key="table.tableName"
                class="table-item"
                :class="{ active: currentTable === table.tableName }"
              >
                <span class="table-icon">📋</span>
                <span class="table-name" @click="onTableSelectByName(table.tableName)">{{ table.tableName }}</span>
                <t-button theme="danger" size="small" variant="text" class="delete-table-btn" @click.stop="onDeleteTable(table.tableName)">删除</t-button>
              </div>
              <!-- 自动备份配置 -->
              <div v-if="currentConnection?.id === connection.id" class="backup-config-section">
                <div class="backup-config-header">
                  <span class="backup-config-title">自动备份</span>
                  <t-switch v-model="backupConfig.enabled" @change="onBackupConfigChange" />
                </div>
                <div v-if="backupConfig.enabled" class="backup-config-body">
                  <div class="config-row">
                    <span class="config-label">备份间隔</span>
                    <t-input-number v-model="backupConfig.intervalHours" :min="1" :max="720" :step="1" theme="normal" size="small" @change="onBackupConfigChange">
                      <template #suffix>小时</template>
                    </t-input-number>
                  </div>
                  <div class="config-row">
                    <span class="config-label">保留份数</span>
                    <t-input-number v-model="backupConfig.maxBackups" :min="1" :max="100" :step="1" theme="normal" size="small" @change="onBackupConfigChange">
                      <template #suffix>份</template>
                    </t-input-number>
                  </div>
                  <div v-if="backupConfig.lastBackupTime" class="config-row">
                    <span class="config-label">上次备份</span>
                    <span class="config-value">{{ formatTime(backupConfig.lastBackupTime) }}</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </template>

    <template #content>
      <div v-if="currentTable && currentConnection" class="data-panel">
        <div class="panel-header">
          <span class="panel-title">{{ currentConnection.name }} / {{ currentTable }}</span>
          <div class="panel-tabs">
            <t-button :theme="activeTab === 'data' ? 'primary' : 'default'" size="small" variant="text" @click="activeTab = 'data'">数据</t-button>
            <t-button :theme="activeTab === 'sql' ? 'primary' : 'default'" size="small" variant="text" @click="activeTab = 'sql'">SQL</t-button>
          </div>
          <t-button theme="default" size="small" variant="text" @click="clearTable">关闭</t-button>
        </div>
        <data-table
          v-if="activeTab === 'data'"
          :connection-id="currentConnection.id"
          :table-name="currentTable"
        />
        <sql-editor
          v-else-if="activeTab === 'sql'"
          :initial-sql="`SELECT * FROM ${currentTable}`"
          :table-name="currentTable"
          @execute="onExecuteSql"
        />
      </div>
      <div v-else-if="currentConnection" class="welcome-panel">
        <div class="welcome-content">
          <div class="welcome-icon">🗄️</div>
          <div class="welcome-title">{{ currentConnection.name }}</div>
          <div class="welcome-desc">请在左侧选择一张表查看数据</div>
        </div>
      </div>
      <div v-else class="empty-state">
        <t-empty description="请先选择数据库连接">
          <template #action>
            <t-button theme="primary" @click="openConnectionDialog">+ 新建连接</t-button>
          </template>
        </t-empty>
      </div>
    </template>
  </main-layout>

  <connection-dialog ref="connectionDialog" @save="onConnectionSave" />
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { MessagePlugin, DialogPlugin } from 'tdesign-vue-next'
import { AddIcon, RefreshIcon, SearchIcon } from 'tdesign-icons-vue-next'
import MainLayout from '../layouts/MainLayout.vue'
import ConnectionDialog from '../components/connection/ConnectionDialog.vue'
import DataTable from '../components/table/DataTable.vue'
import SqlEditor from '../components/SqlEditor.vue'
import { connectionApi } from '../api/connection'
import { tableApi } from '../api/table'
import { backupApi } from '../api/backup'
import { sqlApi } from '../api/sql'

interface Table {
  tableName: string
  schemaName?: string
}

const connectionDialog = ref()
const connections = ref<any[]>([])
const currentConnection = ref<any>(null)
const currentTable = ref<string>('')
const tables = ref<Table[]>([])
const searchKeyword = ref('')
const loadingTables = ref(false)
const expandedConnections = ref<number[]>([])
const backupConfig = ref({ enabled: false, intervalHours: 24, maxBackups: 10, lastBackupTime: null as string | null })
const activeTab = ref<'data' | 'sql'>('data')
const sqlResult = ref<any>(null)

const menuOptions = [
  { content: '备份', value: 'backup' },
  { content: '删除', value: 'delete' }
]

const toggleConnection = async (connection: any) => {
  const idx = expandedConnections.value.indexOf(connection.id)
  if (idx > -1) {
    // 折叠
    expandedConnections.value.splice(idx, 1)
    if (currentConnection.value?.id === connection.id) {
      currentConnection.value = null
      tables.value = []
    }
  } else {
    // 展开
    expandedConnections.value.push(connection.id)
    currentConnection.value = connection
    await loadTables()
  }
}

const handleDeleteConnection = async (connection: any) => {
  if (!connection?.id) {
    MessagePlugin.error('连接ID无效')
    return
  }
  try {
    await connectionApi.deleteConnection(connection.id)
    MessagePlugin.success('删除成功')
    if (currentConnection.value?.id === connection.id) {
      currentConnection.value = null
      currentTable.value = ''
      tables.value = []
    }
    loadConnections()
  } catch (error) {
    MessagePlugin.error('删除失败')
  }
}

const filteredTables = computed(() => {
  if (!searchKeyword.value) return tables.value
  const keyword = searchKeyword.value.toLowerCase()
  return tables.value.filter(t => t.tableName.toLowerCase().includes(keyword))
})

const loadConnections = async () => {
  try {
    const response: any = await connectionApi.getConnections()
    const items = response.items || response || []
    // 按id去重
    const seen = new Set()
    connections.value = items.filter((item: any) => {
      if (seen.has(item.id)) return false
      seen.add(item.id)
      return true
    })
  } catch (error) {
    console.error('Failed to load connections:', error)
    MessagePlugin.error('加载连接列表失败')
  }
}

const loadTables = async () => {
  if (!currentConnection.value?.id) {
    tables.value = []
    return
  }
  loadingTables.value = true
  try {
    const response: any = await tableApi.getTables(currentConnection.value.id)
    tables.value = response.items || response || []
    await loadBackupConfig()
  } catch (error) {
    console.error('Failed to load tables:', error)
    tables.value = []
  } finally {
    loadingTables.value = false
  }
}

const loadBackupConfig = async () => {
  if (!currentConnection.value?.id) return
  try {
    const result: any = await backupApi.getBackupConfig(currentConnection.value.id)
    const config = result.config || result
    backupConfig.value = {
      enabled: config.enabled || false,
      intervalHours: config.intervalHours || 24,
      maxBackups: config.maxBackups || 10,
      lastBackupTime: config.lastBackupTime || null
    }
  } catch (error) {
    backupConfig.value = { enabled: false, intervalHours: 24, maxBackups: 10, lastBackupTime: null }
  }
}

let backupConfigTimer: ReturnType<typeof setTimeout> | null = null

const onBackupConfigChange = () => {
  if (backupConfigTimer) clearTimeout(backupConfigTimer)
  backupConfigTimer = setTimeout(async () => {
    if (!currentConnection.value?.id) return
    try {
      await backupApi.updateBackupConfig(currentConnection.value.id, {
        enabled: backupConfig.value.enabled,
        intervalHours: backupConfig.value.intervalHours,
        maxBackups: backupConfig.value.maxBackups
      })
      MessagePlugin.success('备份配置已保存')
    } catch (error) {
      MessagePlugin.error('保存备份配置失败')
    }
  }, 500)
}

const formatTime = (time: string | null) => {
  if (!time) return '-'
  return new Date(time).toLocaleString()
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
  loadTables()
}

const handleMenu = async (data: any, connection: any) => {
  if (data.value === 'delete') {
    try {
      await connectionApi.deleteConnection(connection.id)
      MessagePlugin.success('删除成功')
      if (currentConnection.value?.id === connection.id) {
        currentConnection.value = null
        currentTable.value = ''
        tables.value = []
      }
      loadConnections()
    } catch (error) {
      MessagePlugin.error('删除失败')
    }
  } else if (data.value === 'backup') {
    try {
      const backupName = `${connection.name}_${new Date().toISOString().slice(0, 10)}`
      await backupApi.createBackup(connection.id, backupName)
      MessagePlugin.success('备份成功')
    } catch (error) {
      MessagePlugin.error('备份失败')
      console.error(error)
    }
  }
}

const onTableSelectByName = (tableName: string) => {
  currentTable.value = tableName
}

const refreshTables = () => {
  loadTables()
}

const clearTable = () => {
  currentTable.value = ''
  activeTab.value = 'data'
}

const onExecuteSql = async (sql: string) => {
  if (!currentConnection.value?.id) return
  try {
    const result: any = await sqlApi.executeSql(currentConnection.value.id, sql)
    sqlResult.value = result.result || result
    MessagePlugin.success('SQL执行成功')
  } catch (error) {
    MessagePlugin.error('SQL执行失败')
    console.error(error)
  }
}

const onDeleteTable = async (tableName: string) => {
  if (!currentConnection.value?.id) return
  
  DialogPlugin.confirm({
    header: '确认删除',
    body: `确定要删除表 "${tableName}" 吗？此操作不可恢复。`,
    confirmBtn: { content: '删除', theme: 'danger' },
    cancelBtn: '取消',
    onConfirm: async () => {
      try {
        await tableApi.deleteTable(currentConnection.value.id, tableName)
        MessagePlugin.success('表删除成功')
        if (currentTable.value === tableName) {
          currentTable.value = ''
        }
        await loadTables()
      } catch (error) {
        MessagePlugin.error('删除表失败')
      }
    }
  })
}

watch(() => currentConnection.value, () => {
  loadTables()
})

onMounted(() => {
  loadConnections()
})
</script>

<style scoped>
.connection-panel {
  height: 100%;
  display: flex;
  flex-direction: column;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  border-bottom: 1px solid #e8e8e8;
}

.panel-title {
  font-weight: 500;
  color: #333;
}

.connection-list {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}

.connection-node {
  margin-bottom: 4px;
}

.connection-header {
  display: flex;
  align-items: center;
  padding: 8px 12px;
  border-radius: 6px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.connection-header:hover {
  background-color: #f5f5f5;
}

.connection-header.active {
  background-color: #e8f4ff;
}

.expand-icon {
  font-size: 10px;
  color: #999;
  margin-right: 8px;
  transition: transform 0.2s;
}

.expand-icon.expanded {
  transform: rotate(90deg);
}

.connection-icon {
  margin-right: 8px;
  font-size: 16px;
}

.connection-name {
  flex: 1;
  font-weight: 500;
  color: #333;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.more-btn {
  opacity: 0;
  transition: opacity 0.2s;
}

.connection-header:hover .more-btn {
  opacity: 1;
}

.connection-tables {
  margin-left: 28px;
  padding: 4px 0;
}

.table-item {
  display: flex;
  align-items: center;
  padding: 6px 12px;
  border-radius: 4px;
  transition: background-color 0.2s;
}

.table-item:hover {
  background-color: #f5f5f5;
}

.table-item.active {
  background-color: #e8f4ff;
}

.table-icon {
  margin-right: 8px;
  font-size: 14px;
}

.table-name {
  flex: 1;
  color: #333;
  font-size: 13px;
  cursor: pointer;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.delete-table-btn {
  opacity: 0;
  transition: opacity 0.2s;
  padding: 0 4px;
}

.table-item:hover .delete-table-btn {
  opacity: 1;
}

.loading-text, .no-tables {
  padding: 8px 12px;
  color: #999;
  font-size: 12px;
}

.empty-connections {
  padding: 20px;
}

.data-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  background: #fff;
  overflow: hidden;
}

.panel-tabs {
  display: flex;
  gap: 4px;
}

.welcome-panel {
  flex: 1;
  display: flex;
  justify-content: center;
  align-items: center;
  background: #fff;
}

.welcome-content {
  text-align: center;
}

.welcome-icon {
  font-size: 64px;
  margin-bottom: 16px;
}

.welcome-title {
  font-size: 20px;
  font-weight: 500;
  color: #333;
  margin-bottom: 8px;
}

.welcome-desc {
  color: #999;
}

.data-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  background: #fff;
  overflow: hidden;
}

.empty-state {
  height: 100%;
  display: flex;
  justify-content: center;
  align-items: center;
  background: #fff;
}

.backup-config-section {
  padding: 8px 12px;
  border-top: 1px solid #e8e8e8;
  margin-top: 4px;
}

.backup-config-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.backup-config-title {
  font-size: 12px;
  color: #666;
  font-weight: 500;
}

.backup-config-body {
  margin-top: 8px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.config-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.config-label {
  font-size: 12px;
  color: #999;
}

.config-value {
  font-size: 12px;
  color: #333;
}
</style>
