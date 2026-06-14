<template>
  <div class="main-layout">
    <div class="sidebar">
      <slot name="sidebar"></slot>
    </div>

    <div class="main-content">
      <div class="top-bar" v-if="showTopBar">
        <div class="top-bar-left">
          <slot name="top-left"></slot>
        </div>
        <div class="top-bar-right">
          <div class="user-info">
            <t-avatar size="small">{{ currentUser?.username?.charAt(0) || 'U' }}</t-avatar>
            <span class="username">{{ currentUser?.username || '用户' }}</span>
            <t-dropdown :options="userMenuOptions" @click="onUserMenuClick">
              <t-button theme="default" size="small" variant="text" class="user-menu-btn">
                <template #icon><more-icon /></template>
              </t-button>
            </t-dropdown>
          </div>
        </div>
      </div>
      <div class="content-area">
        <slot name="content">
          <router-view />
        </slot>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { MessagePlugin } from 'tdesign-vue-next'
import { MoreIcon } from 'tdesign-icons-vue-next'
import { useUserStore } from '../stores/user'

const router = useRouter()
const userStore = useUserStore()

defineProps<{
  showTopBar?: boolean
}>()

const emit = defineEmits<{
  (e: 'add-connection'): void
}>()

const currentUser = ref({
  username: ''
})

const userMenuOptions = [
  { content: '个人设置', value: 'settings' },
  { content: '退出登录', value: 'logout' }
]

onMounted(() => {
  const token = localStorage.getItem('token')
  if (token) {
    try {
      const payload = JSON.parse(atob(token.split('.')[1]))
      currentUser.value.username = payload.username || payload.name || ''
    } catch {
      currentUser.value.username = ''
    }
  }
})

const onUserMenuClick = (data: any) => {
  if (data.value === 'logout') {
    userStore.clearToken()
    MessagePlugin.success('已退出登录')
    router.push('/login')
  } else if (data.value === 'settings') {
    MessagePlugin.info('个人设置功能开发中')
  }
}

const showAddConnection = () => {
  emit('add-connection')
}

defineExpose({ showAddConnection })
</script>

<style scoped>
.main-layout {
  display: flex;
  height: 100vh;
  background: #f5f5f5;
}

.sidebar {
  width: 280px;
  background: #fff;
  border-right: 1px solid #e8e8e8;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.main-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.top-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 16px;
  background: #fff;
  border-bottom: 1px solid #e8e8e8;
  flex-shrink: 0;
}

.top-bar-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.top-bar-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.user-info {
  display: flex;
  align-items: center;
  gap: 8px;
}

.username {
  font-size: 14px;
  color: #333;
}

.user-menu-btn {
  opacity: 0;
  transition: opacity 0.2s;
}

.user-info:hover .user-menu-btn {
  opacity: 1;
}

.content-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
</style>