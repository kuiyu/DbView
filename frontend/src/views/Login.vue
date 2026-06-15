<template>
  <div class="login-container">
    <t-card class="login-card" :bordered="true" :theme="'normal'">
      <template #header>
        <div class="login-title">DbView 数据库管理工具</div>
      </template>
      <t-form ref="form" :data="formData" :rules="rules" @submit="onSubmit" label-width="0">
        <t-form-item name="username">
          <t-input v-model="formData.username" placeholder="请输入用户名" clearable>
            <template #prefixIcon><user-icon /></template>
          </t-input>
        </t-form-item>
        <t-form-item name="password">
          <t-input v-model="formData.password" type="password" placeholder="请输入密码" clearable>
            <template #prefixIcon><lock-on-icon /></template>
          </t-input>
        </t-form-item>
        <t-form-item>
          <t-button theme="primary" type="submit" block :loading="loading">登录</t-button>
        </t-form-item>
      </t-form>
    </t-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { UserIcon, LockOnIcon } from 'tdesign-icons-vue-next'
import { MessagePlugin } from 'tdesign-vue-next'
import { userApi } from '../api/user'
import { useUserStore } from '../stores/user'

const router = useRouter()
const loading = ref(false)
const userStore = useUserStore()

const formData = reactive({
  username: '',
  password: ''
})

const rules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }]
}

const onSubmit = async ({ validateResult }: { validateResult: any }) => {
  if (validateResult !== true) return
  loading.value = true
  try {
    const res: any = await userApi.login(formData.username, formData.password)
	console.log('login res '+JSON.stringify(res))
	let ret=res.result
    userStore.setToken(ret.token || ret.accessToken)
    MessagePlugin.success('登录成功')
    router.push('/')
  } catch (err: any) {
    MessagePlugin.error(err.response?.data?.message || '登录失败')
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.login-container {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 100vh;
  background-color: #f5f5f5;
}

.login-card {
  width: 400px;
}

.login-title {
  text-align: center;
  font-size: 20px;
  font-weight: bold;
}
</style>
