import request from '../utils/request'

export const connectionApi = {
  getConnections() {
    return request.get('/connections')
  },

  getConnection(id: number) {
    return request.get(`/connections/${id}`)
  },

  createConnection(data: any) {
    return request.post('/connections', data)
  },

  updateConnection(id: number, data: any) {
    return request.put(`/connections/${id}`, data)
  },

  deleteConnection(id: number) {
    return request.delete(`/connections/${id}`)
  },

  testConnection(data: any) {
    return request.post('/connections/test', data)
  }
}
