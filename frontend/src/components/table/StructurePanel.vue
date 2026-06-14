<template>
  <div class="structure-panel">
    <div class="panel-toolbar">
      <t-button theme="primary" @click="onAddColumn">添加字段</t-button>
    </div>

    <t-table
      :data="columns"
      :columns="tableColumns"
    >
      <template #operation="{ row }">
        <t-button theme="primary" size="small" @click="onEditColumn(row)">编辑</t-button>
        <t-button theme="danger" size="small" @click="onDeleteColumn(row)">删除</t-button>
      </template>
    </t-table>

    <t-dialog
      v-model:visible="dialogVisible"
      :header="isEdit ? '编辑字段' : '添加字段'"
      :width="500"
      @confirm="onConfirm"
      @cancel="onCancel"
    >
      <t-form ref="formRef" :data="formData" :rules="rules">
        <t-form-item label="字段名" name="name">
          <t-input v-model="formData.name" :disabled="isEdit" />
        </t-form-item>
        <t-form-item label="类型" name="type">
          <t-select v-model="formData.type">
            <t-option value="integer" label="integer" />
            <t-option value="varchar" label="varchar" />
            <t-option value="text" label="text" />
            <t-option value="boolean" label="boolean" />
            <t-option value="timestamp" label="timestamp" />
          </t-select>
        </t-form-item>
        <t-form-item label="可空" name="nullable">
          <t-switch v-model="formData.nullable" />
        </t-form-item>
      </t-form>
    </t-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, watch, onMounted } from 'vue'
import { MessagePlugin } from 'tdesign-vue-next'
import { structureApi } from '../../api/structure'

const props = defineProps<{
  connectionId: number
  tableName: string
}>()

const columns = ref<any[]>([])
const dialogVisible = ref(false)
const isEdit = ref(false)
const formRef = ref()
const formData = reactive({
  name: '',
  type: 'varchar',
  nullable: true
})

const rules = {
  name: [{ required: true, message: '请输入字段名', trigger: 'blur' }],
  type: [{ required: true, message: '请选择类型', trigger: 'change' }]
}

const tableColumns = [
  { title: '字段名', colKey: 'name' },
  { title: '类型', colKey: 'type' },
  { title: '可空', colKey: 'nullable' },
  { title: '操作', colKey: 'operation', width: 160, fixed: 'right' as const }
]

const loadColumns = async () => {
  try {
    const result: any = await structureApi.getColumns(props.connectionId, props.tableName)
    columns.value = result.columns || result || []
  } catch (error) {
    MessagePlugin.error('加载字段失败')
    console.error(error)
  }
}

const onAddColumn = () => {
  isEdit.value = false
  formData.name = ''
  formData.type = 'varchar'
  formData.nullable = true
  dialogVisible.value = true
}

const onEditColumn = (row: any) => {
  isEdit.value = true
  Object.assign(formData, row)
  dialogVisible.value = true
}

const onDeleteColumn = async (row: any) => {
  try {
    await structureApi.deleteColumn(props.connectionId, props.tableName, row.name)
    MessagePlugin.success('删除成功')
    loadColumns()
  } catch (error) {
    MessagePlugin.error('删除失败')
  }
}

const onConfirm = async () => {
  try {
    await formRef.value?.validate()
    if (isEdit.value) {
      await structureApi.updateColumn(props.connectionId, props.tableName, formData.name, formData)
    } else {
      await structureApi.addColumn(props.connectionId, props.tableName, formData)
    }
    MessagePlugin.success('保存成功')
    dialogVisible.value = false
    loadColumns()
  } catch (error) {
    if (error !== false) {
      MessagePlugin.error('保存失败')
    }
  }
}

const onCancel = () => {
  dialogVisible.value = false
}

watch(
  () => [props.connectionId, props.tableName],
  () => {
    loadColumns()
  }
)

onMounted(() => {
  if (props.connectionId && props.tableName) {
    loadColumns()
  }
})
</script>

<style scoped>
.structure-panel {
  padding: 16px;
}

.panel-toolbar {
  margin-bottom: 16px;
}
</style>
