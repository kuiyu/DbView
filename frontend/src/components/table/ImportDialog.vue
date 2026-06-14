<template>
  <t-dialog
    v-model:visible="visible"
    header="导入数据"
    :width="600"
    @confirm="onConfirm"
    @cancel="onCancel"
  >
    <t-upload
      v-model="fileList"
      :auto-upload="false"
      accept=".csv,.json"
      :max="1"
      @change="onFileChange"
    >
      <template #tip>
        <div class="t-upload__tip">支持 CSV 和 JSON 格式</div>
      </template>
    </t-upload>

    <div v-if="previewData.length" class="preview">
      <h4>预览数据（前5行）</h4>
      <t-table :data="previewData" :columns="previewColumns" :max-height="200" />
    </div>
  </t-dialog>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { MessagePlugin } from 'tdesign-vue-next'

const emit = defineEmits<{
  (e: 'import', file: File): void
}>()

const visible = ref(false)
const fileList = ref<any[]>([])
const previewData = ref<any[]>([])
const previewColumns = ref<any[]>([])
let rawFile: File | null = null

const open = () => {
  visible.value = true
  fileList.value = []
  previewData.value = []
  previewColumns.value = []
  rawFile = null
}

const onFileChange = (file: any) => {
  if (!file || !file.raw) return
  rawFile = file.raw

  const reader = new FileReader()
  reader.onload = (e) => {
    const content = e.target?.result as string
    if (file.name.endsWith('.csv')) {
      const lines = content.split('\n').filter((l) => l.trim())
      if (lines.length === 0) return
      const headers = lines[0].split(',').map((h) => h.trim())
      previewColumns.value = headers.map((h) => ({ title: h, colKey: h }))
      previewData.value = lines.slice(1, 6).map((line) => {
        const values = line.split(',').map((v) => v.trim())
        return headers.reduce((obj, h, i) => ({ ...obj, [h]: values[i] || '' }), {} as any)
      })
    } else if (file.name.endsWith('.json')) {
      try {
        const data = JSON.parse(content)
        const rows = Array.isArray(data) ? data : [data]
        previewData.value = rows.slice(0, 5)
        if (previewData.value.length > 0) {
          previewColumns.value = Object.keys(previewData.value[0]).map((k) => ({
            title: k,
            colKey: k
          }))
        }
      } catch {
        MessagePlugin.error('JSON 解析失败')
      }
    }
  }
  reader.readAsText(file.raw)
}

const onConfirm = () => {
  if (!rawFile) {
    MessagePlugin.warning('请先选择文件')
    return
  }
  emit('import', rawFile)
  MessagePlugin.success('导入成功')
  visible.value = false
}

const onCancel = () => {
  visible.value = false
}

defineExpose({ open })
</script>

<style scoped>
.preview {
  margin-top: 16px;
}

.preview h4 {
  margin-bottom: 8px;
}
</style>
