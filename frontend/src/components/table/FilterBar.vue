<template>
  <div class="filter-bar">
    <div class="filter-row" v-for="(filter, index) in filters" :key="index">
      <t-select
        v-model="filter.columnName"
        placeholder="选择列"
        :style="{ width: '180px' }"
        clearable
        size="small"
      >
        <t-option v-for="col in columns" :key="col" :label="col" :value="col" />
      </t-select>

      <t-select
        v-model="filter.operator"
        placeholder="运算符"
        :style="{ width: '130px' }"
        size="small"
      >
        <t-option label="等于" value="eq" />
        <t-option label="不等于" value="neq" />
        <t-option label="大于" value="gt" />
        <t-option label="大于等于" value="gte" />
        <t-option label="小于" value="lt" />
        <t-option label="小于等于" value="lte" />
        <t-option label="包含" value="like" />
        <t-option label="不包含" value="notlike" />
        <t-option label="为空" value="isnull" />
        <t-option label="不为空" value="isnotnull" />
        <t-option label="为空字符串" value="isempty" />
        <t-option label="不为空字符串" value="isnotempty" />
      </t-select>

      <t-input
        v-if="!['isnull', 'isnotnull', 'isempty', 'isnotempty'].includes(filter.operator)"
        v-model="filter.value"
        placeholder="值"
        :style="{ width: '180px' }"
        size="small"
        clearable
      />

      <t-button
        theme="danger"
        size="small"
        variant="text"
        @click="removeFilter(index)"
      >
        删除
      </t-button>
    </div>

    <div class="filter-actions">
      <t-button theme="primary" size="small" variant="outline" @click="addFilter">
        + 添加条件
      </t-button>
      <t-button size="small" @click="onSearch" :loading="loading">查询</t-button>
      <t-button size="small" variant="outline" @click="onReset">重置</t-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'

interface FilterItem {
  columnName: string
  operator: string
  value: string
}

const props = defineProps<{
  columns: string[]
  loading?: boolean
}>()

const emit = defineEmits<{
  (e: 'search', filters: FilterItem[]): void
  (e: 'reset'): void
}>()

const filters = ref<FilterItem[]>([])

const addFilter = () => {
  filters.value.push({ columnName: '', operator: 'eq', value: '' })
}

const removeFilter = (index: number) => {
  filters.value.splice(index, 1)
  if (filters.value.length === 0) {
    emit('reset')
  }
}

const onSearch = () => {
  const validFilters = filters.value.filter(f => f.columnName && f.operator)
  emit('search', validFilters)
}

const onReset = () => {
  filters.value = []
  emit('reset')
}
</script>

<style scoped>
.filter-bar {
  padding: 12px 16px;
  border-bottom: 1px solid #e8e8e8;
  background: #fafafa;
}

.filter-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}

.filter-row:last-child {
  margin-bottom: 0;
}

.filter-actions {
  display: flex;
  gap: 8px;
  margin-top: 8px;
}
</style>
