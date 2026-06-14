<template>
  <t-dialog
    v-model:visible="visible"
    header="新建数据库连接"
    :width="500"
    :confirm-btn="confirmBtnProps"
    @confirm="onConfirm"
    @cancel="onCancel"
  >
    <t-form ref="form" :data="formData" :rules="rules">
      <t-form-item label="数据库类型" name="dbType">
        <t-radio-group v-model="formData.dbType" :disabled="testing">
          <t-radio-button value="postgresql">PostgreSQL</t-radio-button>
          <t-radio-button value="mysql">MySQL</t-radio-button>
          <t-radio-button value="sqlserver">SQL Server</t-radio-button>
          <t-radio-button value="sqlite">SQLite</t-radio-button>
        </t-radio-group>
      </t-form-item>

      <t-form-item label="连接名称" name="name">
        <t-input v-model="formData.name" placeholder="例如：生产环境数据库" :disabled="testing" />
      </t-form-item>

      <t-form-item label="主机地址" name="host">
        <t-input v-model="formData.host" placeholder="127.0.0.1" :disabled="testing" />
      </t-form-item>

      <t-form-item label="端口" name="port">
        <t-input v-model.number="formData.port" placeholder="5432" :disabled="testing" />
      </t-form-item>

      <t-form-item label="数据库名" name="databaseName">
        <t-input v-model="formData.databaseName" placeholder="请输入数据库名" :disabled="testing" />
      </t-form-item>

      <t-form-item label="用户名" name="username">
        <t-input v-model="formData.username" placeholder="请输入用户名" :disabled="testing" />
      </t-form-item>

      <t-form-item label="密码" name="password">
        <t-input v-model="formData.password" type="password" placeholder="请输入密码" :disabled="testing" />
      </t-form-item>
    </t-form>

    <!-- 测试连接按钮 -->
    <div class="test-btn-area">
      <t-button theme="default" :loading="testing" @click="onTestConnection">
        <template #icon><refresh-icon /></template>
        测试连接
      </t-button>
    </div>

    <!-- 测试结果 -->
    <div v-if="testResult" :class="['test-result', testResult.success ? 'success' : 'error']">
      <span v-if="testResult.success" class="result-icon">✓</span>
      <span v-else class="result-icon">✗</span>
      {{ testResult.message }}
    </div>
  </t-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, computed } from 'vue'
import { MessagePlugin } from 'tdesign-vue-next'
import { RefreshIcon } from 'tdesign-icons-vue-next'
import { connectionApi } from '../../api/connection'

const visible = ref(false)
const form = ref()
const testing = ref(false)
const testResult = ref<{ success: boolean; message: string } | null>(null)
const testPassed = ref(false)

const formData = reactive({
  name: '',
  host: '127.0.0.1',
  port: 5432,
  databaseName: '',
  username: '',
  password: '',
  dbType: 'postgresql'
})

const rules = {
  name: [{ required: true, message: '请输入连接名称', trigger: 'blur' }],
  host: [{ required: true, message: '请输入主机地址', trigger: 'blur' }],
  port: [{ required: true, message: '请输入端口', trigger: 'blur' }],
  databaseName: [{ required: true, message: '请输入数据库名', trigger: 'blur' }],
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }]
}

const confirmBtnProps = computed(() => ({
  content: testPassed.value ? '保存连接' : '请先测试连接',
  disabled: !testPassed.value,
  theme: testPassed.value ? 'primary' : 'default'
}))

const emit = defineEmits<{
  (e: 'save'): void
}>()

const open = () => {
  visible.value = true
  testResult.value = null
  testPassed.value = false
  // 重置表单
  formData.name = ''
  formData.host = '127.0.0.1'
  formData.port = 5432
  formData.databaseName = ''
  formData.username = ''
  formData.password = ''
  formData.dbType = 'postgresql'
}

const onTestConnection = async () => {
  // 先验证表单
  try {
    await form.value?.validate()
  } catch {
    MessagePlugin.warning('请先填写完整的连接信息')
    return
  }

  testing.value = true
  testResult.value = null
  testPassed.value = false

  try {
    const result: any = await connectionApi.testConnection(formData)
    testResult.value = result
    testPassed.value = result.success === true
    if (result.success) {
      MessagePlugin.success('连接测试成功')
    }
  } catch (error) {
    testResult.value = { success: false, message: '连接测试失败，请检查连接信息' }
  } finally {
    testing.value = false
  }
}

const onConfirm = async () => {
  if (!testPassed.value) {
    MessagePlugin.warning('请先测试连接成功后再保存')
    return
  }

  try {
    await connectionApi.createConnection(formData)
    MessagePlugin.success('连接保存成功')
    emit('save')
    visible.value = false
  } catch (error) {
    MessagePlugin.error('保存连接失败')
  }
}

const onCancel = () => {
  visible.value = false
}

defineExpose({ open })
</script>

<style scoped>
.test-btn-area {
  display: flex;
  justify-content: center;
  margin-top: 16px;
  padding-top: 16px;
  border-top: 1px solid #e8e8e8;
}

.test-result {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  border-radius: 4px;
  margin-top: 12px;
  font-size: 14px;
}

.result-icon {
  font-weight: bold;
  font-size: 16px;
}

.test-result.success {
  background: #f6ffed;
  border: 1px solid #b7eb8f;
  color: #52c41a;
}

.test-result.error {
  background: #fff2f0;
  border: 1px solid #ffccc7;
  color: #ff4d4f;
}
</style>
