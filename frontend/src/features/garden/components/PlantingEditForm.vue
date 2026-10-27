<script setup lang="ts">
import { computed, onBeforeUnmount, reactive, watchEffect } from 'vue'
import Button from 'primevue/button'
import DatePicker from 'primevue/datepicker'
import SelectButton from 'primevue/selectbutton'
import Textarea from 'primevue/textarea'
import { useI18n } from 'vue-i18n'
import type { BedDto, PlantingDto } from '@/shared/api'
import { fromIsoDate, toIsoDate } from '@/shared/dates'
import { useNotify } from '@/shared/composables/useNotify'
import FormField from '@/shared/ui/FormField.vue'
import NumberInput from '@/shared/ui/NumberInput.vue'
import { toUpdateInput } from '../map'
import { useUpdatePlanting } from '../queries'
import { useSpotPicker } from '../spotPicker'
import SpotPatternInput from './SpotPatternInput.vue'

/**
 * Adjust a planting by hand: the actual dates, the share or number of plants, notes. In a spots bed also the
 * pattern and the place (tap or drag on the map), previewed live on the map until saved.
 */
const props = defineProps<{ bed: BedDto; planting: PlantingDto; name: string }>()
const emit = defineEmits<{ done: [] }>()

const { t } = useI18n()
const notify = useNotify()
const update = useUpdatePlanting()

const form = reactive(toUpdateInput(props.planting))
const spots = computed(() => props.bed.kind === 'spots')
const shares = [1, 0.7, 0.5, 0.3].map((value) => ({ value, label: `${value * 100}%` }))
if (!shares.some((s) => s.value === form.share)) shares.push({ value: form.share, label: `${Math.round(form.share * 100)}%` })

const picker = useSpotPicker()
if (spots.value) picker.start(props.bed.id, { x: form.spotX ?? props.bed.shape.width / 2, y: form.spotY ?? props.bed.shape.height / 2 })
onBeforeUnmount(picker.stop)
/** Fewer plants than the block is wide: narrow the block. */
const spotColumns = computed(() => (form.spotColumns ? Math.min(form.spotColumns, form.count ?? 1) : undefined))
watchEffect(() => {
  if (spots.value)
    picker.draft.value = {
      plantingId: props.planting.id,
      count: form.count ?? 1,
      columns: spotColumns.value,
      vertical: form.spotVertical ?? false,
    }
})

type DateKey = 'sowOn' | 'plantOutOn' | 'harvestFrom' | 'harvestTo' | 'clearOn'
const dateModel = (key: DateKey) =>
  computed({
    get: () => fromIsoDate(form[key]),
    set: (d) => {
      if (key === 'sowOn') form.sowOn = toIsoDate(d) ?? form.sowOn
      else form[key] = toIsoDate(d)
    },
  })
const dates = {
  sowOn: dateModel('sowOn'),
  plantOutOn: dateModel('plantOutOn'),
  harvestFrom: dateModel('harvestFrom'),
  harvestTo: dateModel('harvestTo'),
  clearOn: dateModel('clearOn'),
}

async function save() {
  try {
    const spot = spots.value && { spotX: picker.point.value?.x, spotY: picker.point.value?.y, spotColumns: spotColumns.value }
    await update.mutateAsync({ id: props.planting.id, input: { ...form, ...spot, notes: form.notes?.trim() || undefined } })
    notify.success('map.edit.saved', { name: props.name })
    emit('done')
  } catch (error) {
    notify.error(error)
  }
}
</script>

<template>
  <form class="plan" @submit.prevent="save">
    <h3>{{ t('map.edit.title', { name }) }}</h3>
    <div class="row two">
      <FormField :label="t('map.plan.sowOn')" for="e-sow">
        <DatePicker v-model="dates.sowOn.value" fluid input-id="e-sow" show-icon date-format="dd/mm/yy" />
      </FormField>
      <FormField :label="t('map.plan.plantOut')" for="e-out">
        <DatePicker v-model="dates.plantOutOn.value" fluid input-id="e-out" show-icon show-clear date-format="dd/mm/yy" />
      </FormField>
    </div>
    <div class="row two">
      <FormField :label="t('map.edit.harvestFrom')" for="e-hfrom">
        <DatePicker v-model="dates.harvestFrom.value" fluid input-id="e-hfrom" show-icon show-clear date-format="dd/mm/yy" />
      </FormField>
      <FormField :label="t('map.edit.harvestTo')" for="e-hto">
        <DatePicker v-model="dates.harvestTo.value" fluid input-id="e-hto" show-icon show-clear date-format="dd/mm/yy" />
      </FormField>
    </div>
    <div class="row two">
      <FormField :label="t('map.plan.free')" for="e-clear">
        <DatePicker v-model="dates.clearOn.value" fluid input-id="e-clear" show-icon show-clear date-format="dd/mm/yy" />
      </FormField>
      <FormField v-if="spots" :label="t('map.plan.count')" for="e-count">
        <NumberInput v-model="form.count" fluid input-id="e-count" :min="1" :max="999" show-buttons />
      </FormField>
      <FormField v-else :label="t('map.plan.share')">
        <SelectButton v-model="form.share" :options="shares" option-label="label" option-value="value" :allow-empty="false" />
      </FormField>
    </div>
    <FormField :label="t('map.edit.notes')" for="e-notes">
      <Textarea id="e-notes" v-model="form.notes" rows="2" auto-resize />
    </FormField>
    <SpotPatternInput v-if="spots" v-model:columns="form.spotColumns" v-model:vertical="form.spotVertical" :count="form.count ?? 1" />
    <span v-if="spots" class="muted small">📍 {{ t('map.edit.moveSpot') }}</span>

    <div class="row">
      <Button
        type="submit"
        :label="t('common.save')"
        icon="pi pi-check"
        :disabled="spots && !form.count"
        :loading="update.isPending.value"
      />
      <Button :label="t('common.cancel')" severity="secondary" outlined @click="emit('done')" />
    </div>
  </form>
</template>

<style scoped>
.plan {
  display: flex;
  flex-direction: column;
  gap: 10px;
  background: var(--c-panel-2);
  border: 1px solid var(--c-line);
  border-radius: 12px;
  padding: 12px;
}
.plan h3 {
  margin: 0;
}
.two > * {
  flex: 1;
  min-width: 140px;
}
</style>
