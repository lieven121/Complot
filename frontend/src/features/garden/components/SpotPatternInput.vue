<script setup lang="ts">
import { computed, useId } from 'vue'
import SelectButton from 'primevue/selectbutton'
import { useI18n } from 'vue-i18n'
import FormField from '@/shared/ui/FormField.vue'
import NumberInput from '@/shared/ui/NumberInput.vue'

/** How the plants of a spot are laid out: a round cluster, or a block `columns` plants wide in either direction. */
const columns = defineModel<number | undefined>('columns')
const vertical = defineModel<boolean>('vertical', { default: false })
const props = defineProps<{ count: number }>()

const { t } = useI18n()
const id = useId()

const shapes = computed(() => [
  { value: 'circle', label: t('map.pattern.circle'), icon: 'pi pi-circle' },
  { value: 'block', label: t('map.pattern.block'), icon: 'pi pi-th-large' },
])
const shape = computed({
  get: () => (columns.value ? 'block' : 'circle'),
  set: (value) => (columns.value = value === 'block' ? Math.min(2, props.count) : undefined),
})
const directions = computed(() => [
  { value: false, label: t('map.pattern.horizontal'), icon: 'pi pi-arrows-h' },
  { value: true, label: t('map.pattern.vertical'), icon: 'pi pi-arrows-v' },
])
const rows = computed(() => (columns.value ? Math.ceil(props.count / Math.min(columns.value, props.count)) : 0))
</script>

<template>
  <div class="pattern">
    <FormField :label="t('map.pattern.shape')">
      <SelectButton v-model="shape" :options="shapes" option-value="value" option-label="label" :allow-empty="false">
        <template #option="{ option }"><i :class="option.icon" /> {{ option.label }}</template>
      </SelectButton>
    </FormField>
    <template v-if="columns">
      <FormField :label="t('map.pattern.wide')" :for="`${id}-columns`" :hint="t('map.pattern.size', { length: rows, wide: columns })">
        <NumberInput
          :model-value="columns"
          :input-id="`${id}-columns`"
          :min="1"
          :max="Math.max(1, count)"
          show-buttons
          fluid
          @update:model-value="columns = Math.min(Math.max(1, $event ?? 1), Math.max(1, count))"
        />
      </FormField>
      <FormField :label="t('map.pattern.direction')">
        <SelectButton v-model="vertical" :options="directions" option-value="value" option-label="label" :allow-empty="false">
          <template #option="{ option }"><i :class="option.icon" :aria-label="option.label" /></template>
        </SelectButton>
      </FormField>
    </template>
  </div>
</template>

<style scoped>
.pattern {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}
.pattern > * {
  flex: 1;
  min-width: 120px;
}
</style>
