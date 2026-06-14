import request from '../utils/request'

export const tableApi = {
  getTables(connectionId: number) {
    return request.get(`/connections/${connectionId}/tables`)
  },

  getTableStructure(connectionId: number, tableName: string) {
    return request.get(`/connections/${connectionId}/tables/${tableName}`)
  },

  getTableData(connectionId: number, tableName: string, page: number, pageSize: number) {
    return request.get(`/connections/${connectionId}/tables/${tableName}/data`, {
      params: { page, pageSize }
    })
  },

  createTable(connectionId: number, data: any) {
    return request.post(`/connections/${connectionId}/tables`, data)
  },

  updateTable(connectionId: number, tableName: string, data: any) {
    return request.put(`/connections/${connectionId}/tables/${tableName}`, data)
  },

  deleteTable(connectionId: number, tableName: string) {
    return request.delete(`/connections/${connectionId}/tables/${tableName}`)
  },

  insertData(connectionId: number, tableName: string, data: any) {
    return request.post(`/connections/${connectionId}/tables/${tableName}/data`, data)
  },

  importData(connectionId: number, tableName: string, file: File) {
    const formData = new FormData()
    formData.append('file', file)
    return request.post(`/connections/${connectionId}/tables/${tableName}/import`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })
  },

  batchDelete(connectionId: number, tableName: string, ids: any[]) {
    return request.delete(`/connections/${connectionId}/tables/${tableName}/data`, {
      data: { ids }
    })
  },

  deleteTableData(connectionId: number, tableName: string, primaryKey: any) {
    return request.delete(`/connections/${connectionId}/tables/${tableName}/data/${primaryKey}`)
  }
}
