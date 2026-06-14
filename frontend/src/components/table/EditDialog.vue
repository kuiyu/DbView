<template>
  <t-dialog
    v-model:visible="visible"
    :header="isEdit ? '编辑数据' : '新增数据'"
    :width="600"
    @confirm="onConfirm"
    @cancel="onCancel"
  >
    <t-form ref="form" :data="formData" :rules="rules">
      <t-form-item
        v-for="field in fields"
        :key="field.colKey"
        :label="field.title || field.colKey"
        :name="field.colKey"
      >
        <template v-if="field.isPrimaryKey && isEdit">
          <t-input
            :model-value="formData[field.colKey]"
            readonly
            disabled
          />
        </template>
        <template v-else-if="isDateField(field)">
          <t-date-picker
            v-model="formData[field.colKey]"
            :placeholder="`请选择${field.title || field.colKey}`"
            allow-clear
            @change="onDateChange(field.colKey)"
          />
        </template>
        <template v-else-if="isNumberField(field)">
          <t-input
            v-model="formData[field.colKey]"
            :placeholder="`请输入${field.title || field.colKey}`"
            type="number"
          />
        </template>
        <template v-else-if="isBooleanField(field)">
          <t-switch v-model="formData[field.colKey]" />
        </template>
        <template v-else>
          <t-input v-model="formData[field.colKey]" :placeholder="`请输入${field.title || field.colKey}`" />
        </template>
      </t-form-item>
    </t-form>
  </t-dialog>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { MessagePlugin } from 'tdesign-vue-next'

const emit = defineEmits<{
  (e: 'save', data: Record<string, any>, isEdit: boolean): void
}>()

const visible = ref(false)
const isEdit = ref(false)
const form = ref()
const fields = ref<{ colKey: string; title?: string; dataType?: string; isPrimaryKey?: boolean }[]>([])

const formData = reactive<Record<string, any>>({})

const rules = {}

const dateTypeKeywords = ['date', 'time', 'timestamp', 'datetime']
const numberTypeKeywords = ['int', 'float', 'double', 'decimal', 'number', 'numeric']
const booleanTypeKeywords = ['bool', 'boolean']

const isDateField = (field: { colKey: string; title?: string; dataType?: string }) => {
  if (!field.dataType) return false
  const type = field.dataType.toLowerCase()
  return dateTypeKeywords.some(kw => type.includes(kw))
}

const isNumberField = (field: { colKey: string; title?: string; dataType?: string }) => {
  if (!field.dataType) return false
  const type = field.dataType.toLowerCase()
  return numberTypeKeywords.some(kw => type.includes(kw))
}

const isBooleanField = (field: { colKey: string; title?: string; dataType?: string }) => {
  if (!field.dataType) return false
  const type = field.dataType.toLowerCase()
  return booleanTypeKeywords.some(kw => type.includes(kw))
}

const normalizeDateValue = (value: any): string | null => {
  if (value === null || value === undefined || value === '') return null
  if (typeof value === 'string') {
    if (value.startsWith('0001-01-01') || value.startsWith('0001/01/01')) return null
    if (value.includes('T')) {
      return value.split('T')[0]
    }
    return value
  }
  if (value instanceof Date) {
    const year = value.getFullYear()
    if (year < 1900) return null
    return value.toISOString().split('T')[0]
  }
  return null
}

const onDateChange = (fieldName: string) => {
  formData[fieldName] = normalizeDateValue(formData[fieldName])
}

const open = (row?: any, columnDefs?: { colKey: string; title?: string; dataType?: string; isPrimaryKey?: boolean }[]) => {
  visible.value = true
  isEdit.value = !!row
  fields.value = columnDefs || []

  Object.keys(formData).forEach(key => {
    delete formData[key]
  })

  if (row) {
    columnDefs?.forEach((f) => {
      if (isDateField(f)) {
        formData[f.colKey] = normalizeDateValue(row[f.colKey])
      } else {
        formData[f.colKey] = row[f.colKey] ?? ''
      }
    })
  } else if (columnDefs) {
    columnDefs.forEach((f) => {
      formData[f.colKey] = f.isPrimaryKey ? '' : ''
    })
  }
}

const onConfirm = async () => {
  await form.value?.validate()
  const submitData: Record<string, any> = {}
  Object.keys(formData).forEach(key => {
    const value = formData[key]
    if (value === '' || value === null || value === undefined) {
      submitData[key] = null
    } else {
      submitData[key] = value
    }
  })
  emit('save', submitData, isEdit.value)
  MessagePlugin.success(isEdit.value ? '更新成功' : '新增成功')
  visible.value = false
}

const onCancel = () => {
  visible.value = false
}

defineExpose({ open })
</script>