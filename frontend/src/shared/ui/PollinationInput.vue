<script setup lang="ts">
import { computed, useId } from 'vue'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import { useI18n } from 'vue-i18n'
import type { PollinationDto, PollinationNeed } from '@/shared/api'
import FormField from './FormField.vue'
import NumberInput from './NumberInput.vue'

/**
 * Pollination of a variety or tree: what it needs, its group (plants of one group pollinate each other) and the
 * range. No need chosen = not tracked. `groups` are suggestions (the groups already in use).
 */
const model = defineModel<PollinationDto | undefined>()
defineProps<{ groups?: string[]; error?: string }>()

const { t } = useI18n()
const id = useId()
const DEFAULT_RANGE = 20

const needs = computed(() =>
  (['selfFertile', 'partner', 'female', 'male'] as PollinationNeed[]).map((value) => ({ value, label: t(`pollination.need.${value}`) })),
)
const setNeed = (need: PollinationNeed | null | undefined) =>
  (model.value = need ? { need, group: model.value?.group ?? '', rangeMeters: model.value?.rangeMeters ?? DEFAULT_RANGE } : undefined)
const set = (patch: Partial<PollinationDto>) => model.value && (model.value = { ...model.value, ...patch })
</script>

<template>
  <div class="pollination">
    <FormField :label="t('pollination.title')" :for="`${id}-need`" :error="error" :hint="model ? undefined : t('pollination.hint')">
      <Select
        :model-value="model?.need"
        :input-id="`${id}-need`"
        :options="needs"
        option-label="label"
        option-value="value"
        :placeholder="t('pollination.none')"
        show-clear
        fluid
        @update:model-value="setNeed"
      />
    </FormField>
    <template v-if="model">
      <FormField :label="t('pollination.group')" :for="`${id}-group`" :hint="t('pollination.groupHint')">
        <InputText
          :id="`${id}-group`"
          :model-value="model.group"
          :list="`${id}-groups`"
          :invalid="!model.group.trim()"
          fluid
          @update:model-value="set({ group: $event ?? '' })"
        />
        <datalist :id="`${id}-groups`">
          <option v-for="g in groups" :key="g" :value="g" />
        </datalist>
      </FormField>
      <FormField :label="t('pollination.range')" :for="`${id}-range`">
        <NumberInput
          :model-value="model.rangeMeters"
          :input-id="`${id}-range`"
          :min="1"
          :max="1000"
          suffix=" m"
          fluid
          @update:model-value="set({ rangeMeters: $event ?? DEFAULT_RANGE })"
        />
      </FormField>
    </template>
  </div>
</template>

<style scoped>
.pollination {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 10px;
}
</style>
