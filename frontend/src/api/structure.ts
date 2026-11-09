import request from '../utils/request'

export const structureApi = {
  getColumns(connectionId: number, tableName: string, schema?: string) {
    // PostgreSQL 多 schema 场景必须带上 schema，否则同名表会取到别的 schema 的列定义
    const query = schema ? `?schema=${encodeURIComponent(schema)}` : ''
    return request.get(`/connections/${connectionId}/tables/${encodeURIComponent(tableName)}${query}`)
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
