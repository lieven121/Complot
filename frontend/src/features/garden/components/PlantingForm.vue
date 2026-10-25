<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import DatePicker from 'primevue/datepicker'
import Select from 'primevue/select'
import SelectButton from 'primevue/selectbutton'
import { useI18n } from 'vue-i18n'
import type { BedDto, PlanPlantingInput, PlantingPreviewDto } from '@/shared/api'
import { fromIsoDate, toIsoDate } from '@/shared/dates'
import { useFormat } from '@/shared/composables/useFormat'
import { useNotify } from '@/shared/composables/useNotify'
import FormField from '@/shared/ui/FormField.vue'
import NumberInput from '@/shared/ui/NumberInput.vue'
import { useCatalogLookups } from '@/features/catalog/queries'
import { useCreatePlanting, usePreviewPlanting } from '../queries'
import { useSpotPicker } from '../spotPicker'
import WarningList from './WarningList.vue'

/**
 * Plan a crop in a bed: the API calculates the dates and runs the checks before anything is saved. In a spots bed
 * the crop gets a number of plants and a place, picked by tapping the bed on the map.
 */
const props = defineProps<{ bed: BedDto; date: string }>()
const emit = defineEmits<{ done: [] }>()

const { t } = useI18n()
const { date: formatDate, monthWindow } = useFormat()
const notify = useNotify()
const { categories, varieties, varietyById } = useCatalogLookups()
const preview = usePreviewPlanting()
const create = useCreatePlanting()

const varietyId = ref<string>()
const sowOn = ref(props.date)
const share = ref(1)
const direct = ref(false)
const count = ref<number | null | undefined>(1)
const result = ref<PlantingPreviewDto>()

const spots = computed(() => props.bed.kind === 'spots')
const picker = useSpotPicker()
if (spots.value) picker.start(props.bed.id, { x: props.bed.shape.width / 2, y: props.bed.shape.height / 2 })
onBeforeUnmount(picker.stop)

const groups = computed(() =>
  (categories.data.value ?? []).map((c) => ({
    label: `${c.icon} ${c.name}`,
    items: (varieties.data.value ?? [])
      .filter((v) => v.categoryId === c.id)
      .map((v) => ({ id: v.id, label: [v.name, v.cultivar].filter(Boolean).join(' — ') })),
  })),
)
const variety = computed(() => (varietyId.value ? varietyById.value.get(varietyId.value) : undefined))
const sowDate = computed({ get: () => fromIsoDate(sowOn.value), set: (d) => (sowOn.value = toIsoDate(d) ?? sowOn.value) })
const shares = [1, 0.7, 0.5, 0.3].map((value) => ({ value, label: `${value * 100}%` }))

const input = computed<PlanPlantingInput | undefined>(() =>
  varietyId.value && (!spots.value || count.value)
    ? {
        bedId: props.bed.id,
        varietyId: varietyId.value,
        share: spots.value ? 1 : share.value,
        sowOn: sowOn.value,
        direct: direct.value,
        notes: undefined,
        ...(spots.value && { count: count.value ?? undefined, spotX: picker.point.value?.x, spotY: picker.point.value?.y }),
      }
    : undefined,
)

let timer: number | undefined
watch(
  input,
  (value) => {
    clearTimeout(timer)
    result.value = undefined
    if (!value) return
    timer = window.setTimeout(async () => {
      try {
        result.value = await preview.mutateAsync(value)
      } catch (error) {
        notify.error(error)
      }
    }, 250)
  },
  { deep: true },
)

async function add() {
  if (!input.value) return
  try {
    await create.mutateAsync(input.value)
    notify.success('map.plan.added', { name: variety.value?.name ?? '', bed: props.bed.code })
    emit('done')
  } catch (error) {
    notify.error(error)
  }
}
</script>

<template>
  <form class="plan" @submit.prevent="add">
    <h3>{{ t('map.plan.title', { bed: bed.code }) }}</h3>
    <FormField :label="t('map.plan.variety')" for="p-variety">
      <Select
        v-model="varietyId"
        input-id="p-variety"
        :options="groups"
        option-group-label="label"
        option-group-children="items"
        option-label="label"
        option-value="id"
        filter
        :placeholder="t('map.plan.chooseVariety')"
      />
    </FormField>
    <div class="row two">
      <FormField :label="t('map.plan.sowOn')" for="p-date">
        <DatePicker v-model="sowDate" input-id="p-date" show-icon date-format="dd/mm/yy" />
      </FormField>
      <FormField v-if="spots" :label="t('map.plan.count')" for="p-count">
        <NumberInput v-model="count" input-id="p-count" :min="1" :max="999" show-buttons />
      </FormField>
      <FormField v-else :label="t('map.plan.share')">
        <SelectButton v-model="share" :options="shares" option-label="label" option-value="value" :allow-empty="false" />
      </FormField>
    </div>
    <span v-if="spots" class="muted small">📍 {{ t('map.plan.pickSpot') }}</span>
    <label v-if="variety?.sowIndoor" class="row small">
      <Checkbox v-model="direct" binary input-id="p-direct" />
      <span>{{ t('map.plan.direct') }}</span>
    </label>

    <div v-if="result" class="result">
      <dl>
        <template v-if="result.planting.plantOutOn">
          <dt>🏠 {{ t('map.plan.sowIndoor') }}</dt>
          <dd>{{ formatDate(result.planting.sowOn) }}</dd>
          <dt>🪴 {{ t('map.plan.plantOut') }}</dt>
          <dd>{{ formatDate(result.planting.plantOutOn) }}</dd>
        </template>
        <template v-else>
          <dt>🌱 {{ variety?.life === 'perennial' ? t('map.plan.plant') : t('map.plan.sow') }}</dt>
          <dd>{{ formatDate(result.planting.sowOn) }}</dd>
        </template>
        <template v-if="result.planting.harvestFrom">
          <dt>🧺 {{ t('map.plan.harvest') }}</dt>
          <dd>{{ formatDate(result.planting.harvestFrom) }} → {{ formatDate(result.planting.harvestTo) }}</dd>
          <dt>🧹 {{ t('map.plan.free') }}</dt>
          <dd>{{ formatDate(result.planting.clearOn) }}</dd>
        </template>
        <template v-else-if="variety">
          <dt>🧺 {{ t('map.plan.harvest') }}</dt>
          <dd>{{ t('map.plan.everyYear', { months: monthWindow(variety.harvest) }) }}</dd>
        </template>
        <template v-if="result.planting.plantCount">
          <dt>🔢 {{ t('map.plan.plants') }}</dt>
          <dd>≈ {{ result.planting.plantCount }}</dd>
        </template>
        <template v-if="variety?.daysToFirstHarvest">
          <dt>⏱ {{ t('map.plan.growth') }}</dt>
          <dd>{{ t('common.days', { n: variety.daysToFirstHarvest }) }}</dd>
        </template>
      </dl>
      <WarningList :warnings="result.warnings" :empty-text="t('map.plan.noConflicts')" />
    </div>
    <span v-else-if="preview.isPending.value" class="muted small">{{ t('map.plan.calculating') }}</span>

    <div class="row">
      <Button type="submit" :label="t('map.plan.add')" icon="pi pi-plus" :disabled="!result" :loading="create.isPending.value" />
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
.result {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
dl {
  display: grid;
  grid-template-columns: auto 1fr;
  gap: 3px 10px;
  margin: 0;
  font-size: 13px;
}
dt {
  color: var(--c-muted);
}
dd {
  margin: 0;
  font-weight: 600;
}
</style>
