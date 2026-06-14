import request from '../utils/request'

export const sqlApi = {
  executeSql(connectionId: number, sql: string) {
    return request.post(`/connections/${connectionId}/sql/execute`, { sql })
  },

  getSqlHistory(connectionId: number) {
    return request.get(`/connections/${connectionId}/sql/history`)
  },

  saveSql(connectionId: number, sql: string) {
    return request.post(`/connections/${connectionId}/sql/save`, { sql })
  }
}
