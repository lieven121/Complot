<script setup lang="ts">
import { computed } from 'vue'
import Select from 'primevue/select'
import Checkbox from 'primevue/checkbox'
import { useI18n } from 'vue-i18n'
import type { MonthWindowDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'

/** From–to month picker. When `optional`, a checkbox turns the window on/off (null = not applicable). */
const props = defineProps<{ optional?: boolean; inputId?: string; invalid?: boolean }>()
const model = defineModel<MonthWindowDto | undefined>({ required: true })

const { t } = useI18n()
const { monthLong } = useFormat()
const months = computed(() => Array.from({ length: 12 }, (_, i) => ({ value: i + 1, label: monthLong(i + 1) })))

const enabled = computed({
  get: () => model.value != null,
  set: (on: boolean) => (model.value = on ? { from: 4, to: 6 } : undefined),
})

const update = (part: 'from' | 'to', value: number) => {
  if (model.value) model.value = { ...model.value, [part]: value }
}
</script>

<template>
  <div class="month-window">
    <Checkbox v-if="props.optional" v-model="enabled" binary :input-id="inputId" :aria-label="t('common.applicable')" />
    <template v-if="model">
      <Select
        :model-value="model.from"
        :options="months"
        option-label="label"
        option-value="value"
        size="small"
        :invalid="invalid"
        :aria-label="t('common.from')"
        @update:model-value="update('from', $event)"
      />
      <span class="muted">–</span>
      <Select
        :model-value="model.to"
        :options="months"
        option-label="label"
        option-value="value"
        size="small"
        :invalid="invalid"
        :aria-label="t('common.to')"
        @update:model-value="update('to', $event)"
      />
    </template>
    <span v-else class="muted small">{{ t('common.notApplicable') }}</span>
  </div>
</template>

<style scoped>
.month-window {
  display: flex;
  align-items: center;
  gap: 6px;
  min-height: 34px;
}
.month-window :deep(.p-select) {
  min-width: 0;
  flex: 1;
}
</style>
