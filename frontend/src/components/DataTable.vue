<template>
  <div class="data-table">
    <t-table
      :data="data"
      :columns="columns"
      :pagination="pagination"
      @page-change="onPageChange"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'

const props = defineProps<{
  data: any[]
  columns?: any[]
}>()

const pagination = ref({
  current: 1,
  pageSize: 10,
  total: 0
})

const defaultColumns = computed(() => {
  if (props.data.length === 0) return []
  return Object.keys(props.data[0]).map(key => ({
    title: key,
    colKey: key
  }))
})

const columns = computed(() => props.columns || defaultColumns.value)

const onPageChange = (page: number) => {
  pagination.value.current = page
}
</script>

<style scoped>
.data-table {
  overflow: auto;
}
</style>
