import { defineStore } from 'pinia'
import { ref } from 'vue'
import { connectionApi } from '../api/connection'

export const useConnectionStore = defineStore('connection', () => {
  const connections = ref<any[]>([])
  const currentConnection = ref<any>(null)
  const loading = ref(false)

  const loadConnections = async () => {
    loading.value = true
    try {
      const response = await connectionApi.getConnections()
      connections.value = (response as any).items
    } finally {
      loading.value = false
    }
  }

  const selectConnection = (connection: any) => {
    currentConnection.value = connection
  }

  return {
    connections,
    currentConnection,
    loading,
    loadConnections,
    selectConnection
  }
})
