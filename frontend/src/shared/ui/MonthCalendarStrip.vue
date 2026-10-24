<script setup lang="ts">
import type { MonthWindowDto } from '@/shared/api'
import { inMonthWindow, useFormat } from '@/shared/composables/useFormat'

/** Twelve-month strip with one coloured row per window (sowing, planting out, harvest…). */
defineProps<{ rows: { label: string; window?: MonthWindowDto | null; color: string }[] }>()
const { month } = useFormat()
</script>

<template>
  <div class="cal" role="table">
    <span role="columnheader" />
    <span v-for="m in 12" :key="m" class="head" role="columnheader">{{ month(m).charAt(0).toUpperCase() }}</span>
    <template v-for="row in rows" :key="row.label">
      <span class="label" role="rowheader">{{ row.label }}</span>
      <span
        v-for="m in 12"
        :key="m"
        class="cell"
        role="cell"
        :title="`${row.label}: ${month(m)}`"
        :style="inMonthWindow(m, row.window) ? { background: row.color } : undefined"
      />
    </template>
  </div>
</template>

<style scoped>
.cal {
  display: grid;
  grid-template-columns: 130px repeat(12, 1fr);
  gap: 2px;
  font-size: 11px;
  align-items: center;
}
.head {
  text-align: center;
  color: var(--c-muted);
  font-weight: 700;
}
.cell {
  height: 18px;
  border-radius: 3px;
  background: #efeadf;
}
</style>
