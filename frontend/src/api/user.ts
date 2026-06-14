import request from '../utils/request'

export const userApi = {
  login(username: string, password: string) {
    return request.post('/auth/login', { username, password })
  },

  logout() {
    return request.post('/auth/logout')
  },

  getUsers() {
    return request.get('/users')
  },

  updateUser(id: number, data: any) {
    return request.put(`/users/${id}`, data)
  }
}
