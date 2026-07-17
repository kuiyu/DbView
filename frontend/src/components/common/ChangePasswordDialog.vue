<template>
  <t-dialog
    v-model:visible="visible"
    header="修改密码"
    :width="420"
    @confirm="onConfirm"
    @cancel="onCancel"
    :confirm-btn="{ content: '确定', loading: loading }"
  >
    <t-form ref="form" :data="formData" :rules="rules" label-width="80px">
      <t-form-item label="旧密码" name="oldPassword">
        <t-input v-model="formData.oldPassword" type="password" placeholder="请输入当前密码" />
      </t-form-item>
      <t-form-item label="新密码" name="newPassword">
        <t-input v-model="formData.newPassword" type="password" placeholder="请输入新密码" />
      </t-form-item>
      <t-form-item label="确认密码" name="confirmPassword">
        <t-input v-model="formData.confirmPassword" type="password" placeholder="请再次输入新密码" />
      </t-form-item>
    </t-form>
  </t-dialog>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { MessagePlugin } from 'tdesign-vue-next'
import { userApi } from '../../api/user'

const visible = ref(false)
const loading = ref(false)
const form = ref()

const formData = reactive({
  oldPassword: '',
  newPassword: '',
  confirmPassword: ''
})

const rules = {
  oldPassword: [{ required: true, message: '请输入当前密码', trigger: 'blur' }],
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    { min: 6, message: '新密码至少 6 位', trigger: 'blur' }
  ],
  confirmPassword: [
    { required: true, message: '请再次输入新密码', trigger: 'blur' },
    {
      validator: (val: string) => val === formData.newPassword,
      message: '两次输入的密码不一致',
      trigger: 'blur'
    }
  ]
}

const open = () => {
  visible.value = true
  formData.oldPassword = ''
  formData.newPassword = ''
  formData.confirmPassword = ''
}

const onConfirm = async () => {
  const valid = await form.value?.validate()
  if (valid !== true) return

  loading.value = true
  try {
    await userApi.changePassword({
      oldPassword: formData.oldPassword,
      newPassword: formData.newPassword
    })
    MessagePlugin.success('密码修改成功')
    visible.value = false
  } catch (err: any) {
    MessagePlugin.error(err.response?.data?.msg || '密码修改失败')
  } finally {
    loading.value = false
  }
}

const onCancel = () => {
  visible.value = false
}

defineExpose({ open })
</script>
