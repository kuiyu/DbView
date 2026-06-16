import request from '../utils/request'

export const backupApi = {
  // 获取备份列表
  getBackups(connectionId: number) {
    return request.get(`/connections/${connectionId}/backups`)
  },

  // 创建备份
  createBackup(connectionId: number, backupName?: string) {
    return request.post(`/connections/${connectionId}/backups`, { name: backupName })
  },

  // 恢复备份
  restoreBackup(connectionId: number, backupId: number) {
    return request.post(`/connections/${connectionId}/backups/${backupId}/restore`)
  },

  // 删除备份
  deleteBackup(connectionId: number, backupId: number) {
    return request.delete(`/connections/${connectionId}/backups/${backupId}`)
  },

  // 下载备份文件
  downloadBackup(connectionId: number, backupId: number) {
    return request.get(`/connections/${connectionId}/backups/${backupId}/download`, {
      responseType: 'blob'
    })
  },

  // 获取自动备份配置
  getBackupConfig(connectionId: number) {
    return request.get(`/connections/${connectionId}/backup-config`)
  },

  // 更新自动备份配置
  updateBackupConfig(connectionId: number, config: { enabled: boolean; intervalHours: number; maxBackups: number }) {
    return request.put(`/connections/${connectionId}/backup-config`, config)
  }
}
