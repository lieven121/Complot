<script lang="ts">
import { ref } from 'vue'

/** The period and the picked variety survive opening a bed from the list (the panel is unmounted meanwhile). */
const days = ref<number | undefined>(31)
const from = ref('')
const to = ref('')
const picked = ref<string>()
</script>

<script setup lang="ts">
import { computed, watch, watchEffect } from 'vue'
import Button from 'primevue/button'
import DatePicker from 'primevue/datepicker'
import SelectButton from 'primevue/selectbutton'
import { useI18n } from 'vue-i18n'
import type { BedDto, DemandStatus, PlantDemandDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import { addDays, fromIsoDate, toIsoDate } from '@/shared/dates'
import StatusTag from '@/shared/ui/StatusTag.vue'
import { usePlantDemand } from '../queries'
import type { Crop } from './GardenMap.vue'

/**
 * Plants needed for everything sown or planted in a period, per variety, against the stock. The beds of the
 * period (or of the chosen variety) are highlighted on the map.
 */
const props = defineProps<{
  siteId: string
  beds: BedDto[]
  date: string
  cropOf: (varietyId: string) => Crop | undefined
}>()
const emit = defineEmits<{ close: []; select: [bedId: string]; highlight: [bedIds: Set<string> | undefined] }>()

const { t } = useI18n()
const { decimal, currency } = useFormat()

const PRESETS = [14, 31, 92]
if (!from.value) {
  from.value = props.date
  to.value = addDays(props.date, 30)
}
const presets = computed(() => PRESETS.map((value) => ({ value, label: t(`map.demand.days.${value}`) })))
watch([days, from], ([d, f]) => d && (to.value = addDays(f, d - 1)))
const fromDate = computed({
  get: () => fromIsoDate(from.value),
  set: (d) => {
    from.value = toIsoDate(d) ?? from.value
    if (to.value < from.value) to.value = from.value
  },
})
const toDate = computed({
  get: () => fromIsoDate(to.value),
  set: (d) => {
    to.value = toIsoDate(d) ?? to.value
    days.value = undefined
  },
})

const demand = usePlantDemand(
  () => props.siteId,
  computed(() => ({ from: from.value, to: to.value })),
)
const rows = computed(() => demand.data.value ?? [])
const short = computed(() => rows.value.filter((r) => r.status === 'short').length)

/** Budget totals; varieties without a price (or with stock that cannot be counted) are left out and counted. */
const priced = computed(() => rows.value.filter((r) => r.plants > 0))
const sum = (values: (number | undefined)[]) => values.reduce<number>((total, v) => total + (v ?? 0), 0)
const budget = computed(() => ({
  without: sum(priced.value.map((r) => r.costWithoutStock)),
  with: sum(priced.value.map((r) => r.costWithStock)),
  unpriced: priced.value.filter((r) => r.costWithoutStock == null).length,
  uncountedStock: priced.value.filter((r) => r.costWithoutStock != null && r.costWithStock == null).length,
}))

// One variety picked shows only its beds; otherwise every bed of the period.
watchEffect(() => {
  const shown = rows.value.filter((r) => !picked.value || r.varietyId === picked.value)
  emit('highlight', rows.value.length ? new Set(shown.flatMap((r) => r.bedIds)) : undefined)
})

const TONE: Record<DemandStatus, 'ok' | 'danger' | 'neutral' | 'warn'> = {
  enough: 'ok',
  short: 'danger',
  unknown: 'neutral',
  noStock: 'warn',
}
const bedCode = (id: string) => props.beds.find((b) => b.id === id)?.code ?? '?'
const stockText = (r: PlantDemandDto) =>
  [
    r.stock != null ? t('map.demand.pieces', { n: decimal(r.stock) }) : undefined,
    ...r.otherStock.map((a) => `${decimal(a.quantity)} ${a.unit}`),
  ]
    .filter(Boolean)
    .join(' + ') || '—'
</script>

<template>
  <section>
    <div class="panel-head">
      <div>
        <h2>🌱 {{ t('map.demand.title') }}</h2>
        <div class="muted small">{{ t('map.demand.subtitle') }}</div>
      </div>
      <Button icon="pi pi-times" text rounded severity="secondary" class="close" :aria-label="t('common.close')" @click="emit('close')" />
    </div>

    <div class="period">
      <SelectButton v-model="days" :options="presets" option-value="value" option-label="label" size="small" />
      <div class="dates">
        <DatePicker v-model="fromDate" date-format="dd/mm/yy" show-icon fluid :aria-label="t('map.demand.from')" />
        <span class="muted">→</span>
        <DatePicker
          v-model="toDate"
          date-format="dd/mm/yy"
          :min-date="fromDate ?? undefined"
          show-icon
          fluid
          :aria-label="t('map.demand.to')"
        />
      </div>
    </div>

    <p v-if="rows.length" class="small">
      {{ t('map.demand.summary', { varieties: rows.length, plants: rows.reduce((sum, r) => sum + r.plants, 0) }) }}
      <b v-if="short" class="warn">{{ t('map.demand.shortCount', { n: short }) }}</b>
    </p>
    <p v-else-if="!demand.isPending.value" class="muted small">{{ t('map.demand.none') }}</p>

    <template v-if="priced.length">
      <div class="budget">
        <div class="card stat">
          <span class="l">{{ t('map.demand.budget.without') }}</span
          ><span class="v">{{ currency(budget.without) }}</span>
        </div>
        <div class="card stat">
          <span class="l">{{ t('map.demand.budget.with') }}</span
          ><span class="v">{{ currency(budget.with) }}</span>
        </div>
      </div>
      <p v-if="budget.unpriced || budget.uncountedStock" class="muted small">
        <span v-if="budget.unpriced">{{ t('map.demand.budget.unpriced', { n: budget.unpriced }) }} </span>
        <span v-if="budget.uncountedStock">{{ t('map.demand.budget.uncountedStock', { n: budget.uncountedStock }) }}</span>
      </p>
    </template>

    <div class="stack">
      <div
        v-for="r in rows"
        :key="r.varietyId"
        class="item row"
        :class="{ picked: picked === r.varietyId }"
        role="button"
        tabindex="0"
        :aria-pressed="picked === r.varietyId"
        @click="picked = picked === r.varietyId ? undefined : r.varietyId"
        @keydown.enter.space.prevent="picked = picked === r.varietyId ? undefined : r.varietyId"
      >
        <div class="line">
          <span class="name">{{ cropOf(r.varietyId)?.icon }} {{ cropOf(r.varietyId)?.name }}</span>
          <StatusTag :tone="TONE[r.status]">{{ t(`map.demand.status.${r.status}`) }}</StatusTag>
        </div>
        <div class="line small">
          <span>
            <b>{{ t('map.demand.plants', { n: r.plants }) }}</b>
            <span v-if="r.uncounted" class="muted"> + {{ t('map.demand.uncounted', { n: r.uncounted }) }}</span>
            <span class="muted"> · {{ t('map.demand.plantings', { n: r.plantings }) }}</span>
          </span>
          <span :class="{ warn: r.status === 'short' }">
            {{ t('map.demand.stock') }} <b>{{ stockText(r) }}</b>
            <template v-if="r.status === 'short'"> ({{ t('map.demand.missing', { n: decimal(r.plants - (r.stock ?? 0)) }) }})</template>
          </span>
        </div>
        <div v-if="r.plants" class="line small muted">
          <span v-if="r.costWithoutStock == null">{{ t('map.demand.budget.noPrice') }}</span>
          <template v-else>
            <span>{{ t('map.demand.budget.rowWithout', { amount: currency(r.costWithoutStock) }) }}</span>
            <span>{{
              r.costWithStock == null
                ? t('map.demand.budget.rowUnknown')
                : t('map.demand.budget.rowWith', { amount: currency(r.costWithStock) })
            }}</span>
          </template>
        </div>
        <div class="beds">
          <Button
            v-for="id in r.bedIds"
            :key="id"
            :label="bedCode(id)"
            size="small"
            severity="secondary"
            outlined
            @click.stop="emit('select', id)"
          />
          <RouterLink :to="{ name: 'variety', params: { id: r.varietyId } }" class="small catalog" @click.stop>
            {{ t('map.demand.toCatalog') }}
          </RouterLink>
        </div>
      </div>
    </div>
    <p v-if="rows.length" class="muted small">{{ t('map.demand.note') }}</p>
  </section>
</template>

<style scoped>
.period {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin: 12px 0;
}
.dates {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto minmax(0, 1fr);
  gap: 6px;
  align-items: center;
}
.budget {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
  margin-bottom: 8px;
}
.stat {
  display: flex;
  flex-direction: column;
  padding: 10px 12px;
}
.stat .l {
  font-size: 10.5px;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--c-muted);
  font-weight: 700;
}
.stat .v {
  font-size: 20px;
  font-weight: 750;
}
.row {
  display: flex;
  flex-direction: column;
  gap: 4px;
  cursor: pointer;
}
.row.picked {
  outline: 2px solid var(--c-accent);
}
.line {
  display: flex;
  justify-content: space-between;
  gap: 8px;
  flex-wrap: wrap;
}
.name {
  font-weight: 700;
}
.warn {
  color: var(--c-warn);
}
.beds {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  align-items: center;
}
.catalog {
  margin-left: auto;
  color: var(--c-accent);
}
</style>
