<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import { useI18n } from 'vue-i18n'
import { useSession } from '@/shared/session'
import { useFormat } from '@/shared/composables/useFormat'
import CategoryChip from '@/shared/ui/CategoryChip.vue'
import MonthCalendarStrip from '@/shared/ui/MonthCalendarStrip.vue'
import LotEditor from './components/LotEditor.vue'
import LotStatusTag from './components/LotStatusTag.vue'
import VarietyEditor from './components/VarietyEditor.vue'
import { useCatalogLookups, useInventoryLots, useVariety } from './queries'

const props = defineProps<{ id: string }>()

const { t, locale } = useI18n()
const { isEditor } = useSession()
const { month, currency, decimal, date } = useFormat()
const variety = useVariety(() => props.id)
const lots = useInventoryLots()
const { categoryById, familyById, supplierById } = useCatalogLookups()
const editor = ref<InstanceType<typeof VarietyEditor>>()
const lotEditor = ref<InstanceType<typeof LotEditor>>()

const v = computed(() => variety.data.value)
const category = computed(() => (v.value ? categoryById.value.get(v.value.categoryId) : undefined))
const family = computed(() => (v.value?.effectiveFamilyId ? familyById.value.get(v.value.effectiveFamilyId) : undefined))
const myLots = computed(() => (lots.data.value ?? []).filter((l) => l.varietyId === props.id))
const cheapest = computed(() => Math.min(...(v.value?.suppliers.filter((s) => !s.isOwnSource).map((s) => s.price) ?? [])))

const value = (n: number | undefined, unitKey: string) => (n == null ? '—' : t(unitKey, { n: decimal(n) }))
const tiles = computed(() => {
  const x = v.value
  if (!x) return []
  return [
    [t('catalog.variety.sowDepthCm'), value(x.sowDepthCm, 'common.cm')],
    [t('catalog.variety.spacingInRowCm'), x.spacingInRowCm ? value(x.spacingInRowCm, 'common.cm') : t('catalog.broadcast')],
    [t('catalog.variety.spacingBetweenRowsCm'), value(x.spacingBetweenRowsCm, 'common.cm')],
    [t('catalog.variety.heightCm'), value(x.heightCm, 'common.cm')],
    [t('catalog.variety.sun'), x.sun.map((s) => t(`sun.${s}`)).join(', ')],
    [t('catalog.variety.germinationDays'), x.germination ? t('common.days', { n: `${x.germination.min}–${x.germination.max}` }) : '—'],
    [t('catalog.variety.daysToFirstHarvest'), value(x.daysToFirstHarvest, 'common.days')],
    [t('catalog.variety.harvestWeeks'), value(x.harvestWeeks, 'common.weeks')],
    [t('catalog.variety.weeksIndoor'), value(x.weeksIndoor, 'common.weeks')],
    [t('catalog.variety.seedsPerGram'), x.seedsPerGram ? decimal(x.seedsPerGram) : '—'],
    [t('catalog.rotationRest'), x.rotationRestYears ? t('common.years', { n: x.rotationRestYears }) : t('common.none')],
    [
      t('catalog.variety.maxYearsInBed'),
      x.life !== 'perennial'
        ? t('catalog.oneSeason')
        : x.maxYearsInBed
          ? t('common.years', { n: x.maxYearsInBed })
          : t('catalog.unlimited'),
    ],
    [
      t('pollination.title'),
      x.pollination
        ? t('pollination.summary', {
            need: t(`pollination.need.${x.pollination.need}`),
            group: x.pollination.group,
            range: decimal(x.pollination.rangeMeters),
          })
        : '—',
    ],
  ]
})
const calendar = computed(() => [
  { label: t('catalog.variety.sowIndoor'), window: v.value?.sowIndoor, color: '#9cc0e2' },
  { label: t('catalog.variety.sowOutdoor'), window: v.value?.sowOutdoor, color: '#86ba7c' },
  { label: t('catalog.variety.plantOut'), window: v.value?.plantOut, color: '#5d9d6a' },
  { label: t('catalog.variety.harvest'), window: v.value?.harvest, color: '#e4a646' },
])
</script>

<template>
  <div class="page">
    <RouterLink :to="{ name: 'plants' }" class="back">← {{ t('nav.plants') }}</RouterLink>
    <div v-if="variety.isError.value" class="card">{{ t('catalog.variety.notFound') }}</div>
    <template v-else-if="v">
      <div class="page-head">
        <div class="icon">{{ category?.icon }}</div>
        <div>
          <h1>
            {{ v.name }} <span class="muted cultivar">{{ v.cultivar }}</span>
          </h1>
          <div class="row muted">
            <i v-if="v.latinName">{{ v.latinName }}</i>
            <CategoryChip :category="category" />
            <span v-if="family">{{ family.latinName }} ({{ locale === 'en' ? family.nameEn : family.nameNl }})</span>
            <span>{{ t(`life.${v.life}`) }}</span>
          </div>
        </div>
        <div class="actions">
          <Button
            v-if="isEditor"
            :label="t('catalog.lot.new')"
            icon="pi pi-plus"
            severity="secondary"
            outlined
            @click="lotEditor?.open(undefined, v.id)"
          />
          <Button v-if="isEditor" :label="t('common.edit')" icon="pi pi-pencil" @click="editor?.open(v)" />
        </div>
      </div>

      <div class="tiles">
        <div v-for="[label, text] in tiles" :key="label" class="tile">
          <small>{{ label }}</small
          ><b>{{ text }}</b>
        </div>
      </div>

      <h3>{{ t('catalog.variety.calendar') }}</h3>
      <div class="card"><MonthCalendarStrip :rows="calendar" /></div>

      <div class="grid-2 sections">
        <div>
          <h3>{{ t('catalog.category.goodNeighbours') }}</h3>
          <div class="row">
            <CategoryChip v-for="id in category?.goodNeighbourIds ?? []" :key="id" :category="categoryById.get(id)" />
            <span v-if="!category?.goodNeighbourIds.length" class="muted">—</span>
          </div>
          <h3>{{ t('catalog.category.badNeighbours') }}</h3>
          <div class="row">
            <CategoryChip v-for="id in category?.badNeighbourIds ?? []" :key="id" :category="categoryById.get(id)" />
            <span v-if="!category?.badNeighbourIds.length" class="muted">—</span>
          </div>
          <template v-if="v.actions.length">
            <h3>{{ t('catalog.variety.actions') }}</h3>
            <div class="stack">
              <div v-for="(a, i) in v.actions" :key="i" class="item">
                ✂️ {{ a.title }}<span class="end">{{ month(a.month) }} · {{ t('common.minutes', { n: a.estimatedMinutes }) }}</span>
              </div>
            </div>
          </template>
          <template v-if="v.notes">
            <h3>{{ t('catalog.variety.notes') }}</h3>
            <p class="notes">{{ v.notes }}</p>
          </template>
        </div>
        <div>
          <h3>{{ t('catalog.suppliers') }}</h3>
          <div class="stack">
            <div v-for="s in v.suppliers" :key="s.supplierId" class="item">
              {{ s.isOwnSource ? '♻️ ' : '' }}{{ s.supplierName }}
              <span v-if="s.price === cheapest && v.suppliers.length > 1" class="cheapest">{{ t('catalog.supplier.cheapest') }}</span>
              <span class="end"
                >{{ s.unit }}<template v-if="s.pieces"> ({{ t('catalog.supplier.piecesShort', { n: decimal(s.pieces) }) }})</template> ·
                {{ s.price ? currency(s.price) : t('catalog.supplier.free') }}</span
              >
            </div>
            <span v-if="!v.suppliers.length" class="muted">—</span>
          </div>
          <h3>{{ t('catalog.tabs.inventory') }}</h3>
          <div class="stack">
            <button v-for="lot in myLots" :key="lot.id" type="button" class="item lot" @click="lotEditor?.open(lot)">
              {{ decimal(lot.quantity) }} {{ lot.unit }}
              <span class="muted">· {{ supplierById.get(lot.supplierId)?.name }}</span>
              <span class="end">{{ date(lot.bestBefore) }} <LotStatusTag :status="lot.status" /></span>
            </button>
            <span v-if="!myLots.length" class="muted">{{ t('catalog.lot.none') }}</span>
          </div>
        </div>
      </div>
      <VarietyEditor ref="editor" />
      <LotEditor ref="lotEditor" />
    </template>
  </div>
</template>

<style scoped>
.back {
  display: inline-block;
  margin-bottom: 10px;
  color: var(--c-accent);
  text-decoration: none;
}
.icon {
  font-size: 40px;
  line-height: 1;
}
.cultivar {
  font-weight: 500;
}
.tiles {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
  gap: 8px;
}
.tile {
  background: var(--c-panel);
  border: 1px solid var(--c-line);
  border-radius: 10px;
  padding: 8px 10px;
  display: flex;
  flex-direction: column;
}
.tile small {
  font-size: 10.5px;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--c-muted);
  font-weight: 700;
}
.sections {
  margin-top: 4px;
}
.cheapest {
  font-size: 11px;
  font-weight: 700;
  color: var(--c-accent);
}
.notes {
  margin: 0;
  white-space: pre-line;
}
.lot {
  font: inherit;
  text-align: left;
  cursor: pointer;
  width: 100%;
}
</style>
