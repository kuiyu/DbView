<template>
  <t-dialog
    v-model:visible="visible"
    :header="header"
    :width="400"
    @confirm="onConfirm"
    @cancel="onCancel"
  >
    <p>{{ message }}</p>
  </t-dialog>
</template>

<script setup lang="ts">
import { ref } from 'vue'

const visible = ref(false)
const header = ref('确认')
const message = ref('')
let resolveCallback: ((value: boolean) => void) | null = null

const open = (msg: string, title?: string) => {
  visible.value = true
  message.value = msg
  if (title) header.value = title
  return new Promise<boolean>((resolve) => {
    resolveCallback = resolve
  })
}

const onConfirm = () => {
  visible.value = false
  resolveCallback?.(true)
}

const onCancel = () => {
  visible.value = false
  resolveCallback?.(false)
}

defineExpose({ open })
</script>
