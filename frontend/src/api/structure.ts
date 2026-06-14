import request from '../utils/request'

export const structureApi = {
  getColumns(connectionId: number, tableName: string) {
    return request.get(`/connections/${connectionId}/tables/${tableName}`)
  },

  addColumn(connectionId: number, tableName: string, column: any) {
    return request.post(`/connections/${connectionId}/tables/${tableName}/columns`, column)
  },

  updateColumn(connectionId: number, tableName: string, columnName: string, column: any) {
    return request.put(`/connections/${connectionId}/tables/${tableName}/columns/${columnName}`, column)
  },

  deleteColumn(connectionId: number, tableName: string, columnName: string) {
    return request.delete(`/connections/${connectionId}/tables/${tableName}/columns/${columnName}`)
  }
}
