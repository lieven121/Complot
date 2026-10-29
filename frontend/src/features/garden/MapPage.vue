<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Button from 'primevue/button'
import Select from 'primevue/select'
import { useConfirm } from 'primevue/useconfirm'
import { useI18n } from 'vue-i18n'
import type { TaskDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import { useNotify } from '@/shared/composables/useNotify'
import { useCatalogLookups } from '@/features/catalog/queries'
import { useTasks } from '@/features/tasks/queries'
import BedPanel from './components/BedPanel.vue'
import DemandPanel from './components/DemandPanel.vue'
import DesignerPanel from './components/DesignerPanel.vue'
import DimensionOverlay from './components/DimensionOverlay.vue'
import GardenMap, { type Crop } from './components/GardenMap.vue'
import MapLegend from './components/MapLegend.vue'
import NoteLog from './components/NoteLog.vue'
import OverviewPanel from './components/OverviewPanel.vue'
import TimelineBar from './components/TimelineBar.vue'
import TreePanel from './components/TreePanel.vue'
import { type CropQuery, findCrop } from './cropFinder'
import { harvestSummary } from './notes'
import { addDays } from '@/shared/dates'
import { IN_BED, phaseAt, spacingOf, toUpdateInput } from './map'
import { mapPeriod, useLayout, useNotes, usePlantings, useSiteChecks, useSites, useUpdatePlanting } from './queries'
import { type Point, useSpotPicker } from './spotPicker'
import { canEditSite } from '@/shared/session'
import { useDesigner } from './useDesigner'

const { t } = useI18n()
const { decimal } = useFormat()
const route = useRoute()
const router = useRouter()

const period = mapPeriod()
const date = ref(period.today)
const sites = useSites()

/** The site lives in the URL (?site=…), defaulting to the first one. */
const siteId = computed({
  get: () => (route.query.site as string | undefined) ?? sites.data.value?.[0]?.id,
  set: (id) => router.replace({ query: { ...route.query, site: id, item: undefined } }),
})
/** Selected bed or tree, also in the URL so a link can open it. */
const selectedId = computed({
  get: () => route.query.item as string | undefined,
  set: (id) => router.replace({ query: { ...route.query, item: id } }),
})

const layout = useLayout(siteId)
const plantings = usePlantings(siteId, period)
const checks = useSiteChecks(siteId)
const notes = useNotes(computed(() => ({ siteId: siteId.value })))
const tasks = useTasks(computed(() => ({ siteId: siteId.value })))
const { categories, varieties, varietyById, categoryById } = useCatalogLookups()

const site = computed(() => sites.data.value?.find((s) => s.id === siteId.value))
const readonly = computed(() => !canEditSite(site.value))
const selectedBed = computed(() => layout.data.value?.beds.find((b) => b.id === selectedId.value))
const selectedTree = computed(() => layout.data.value?.trees.find((x) => x.id === selectedId.value))

const cropOf = (varietyId: string): Crop | undefined => {
  const variety = varietyById.value.get(varietyId)
  const category = variety ? categoryById.value.get(variety.categoryId) : undefined
  return variety && category ? { icon: category.icon, color: category.color, name: variety.name, spacing: spacingOf(variety) } : undefined
}

const whereOf = (task: TaskDto) =>
  layout.data.value?.beds.find((b) => b.id === task.bedId)?.code ?? layout.data.value?.trees.find((x) => x.id === task.treeId)?.name

/** Beds with a warning for a crop that is in the bed on the selected date. */
const warningBedIds = computed(() => {
  const active = new Set(
    (plantings.data.value ?? [])
      .filter((p) => {
        const phase = phaseAt(p, date.value)
        return phase && IN_BED.has(phase)
      })
      .map((p) => p.id),
  )
  return new Set((checks.data.value ?? []).filter((w) => w.level === 'warning' && active.has(w.plantingId)).map((w) => w.bedId))
})
/** Disease or pest reported in the 45 days before the selected date. */
const alertIds = computed(() => {
  const since = addDays(date.value, -45)
  return new Set(
    (notes.data.value ?? [])
      .filter((n) => (n.tag === 'disease' || n.tag === 'pest') && n.date > since && n.date <= date.value)
      .flatMap((n) => [n.bedId, n.treeId].filter((id): id is string => !!id)),
  )
})
const cropsOnMap = computed(() => {
  const crops = new Map<string, Crop>()
  for (const p of plantings.data.value ?? []) {
    const phase = phaseAt(p, date.value)
    const variety = varietyById.value.get(p.varietyId)
    const category = variety && categoryById.value.get(variety.categoryId)
    if (phase && IN_BED.has(phase) && category) crops.set(category.id, { icon: category.icon, color: category.color, name: category.name })
  }
  return [...crops.values()]
})
const harvested = computed(() =>
  harvestSummary(
    (notes.data.value ?? []).filter((n) => n.date.slice(0, 4) === date.value.slice(0, 4)),
    decimal,
  ),
)

/** Crops of the selected bed, to link a log entry to one. */
const bedCrops = computed(() =>
  forBed(plantings.data.value, selectedBed.value?.id ?? '').map((p) => ({
    id: p.id,
    name: `${cropOf(p.varietyId)?.name ?? ''} (${p.sowOn.slice(0, 4)})`,
  })),
)

const forBed = <T extends { bedId?: string }>(items: T[] | undefined, id: string) => (items ?? []).filter((x) => x.bedId === id)

/** Only beds and trees open a panel; clicking anything else (or empty space) shows the overview. */
const select = (id: string | undefined) => {
  const l = layout.data.value
  selectedId.value = id && (l?.beds.some((b) => b.id === id) || l?.trees.some((x) => x.id === id)) ? id : undefined
}

const map = ref<InstanceType<typeof GardenMap>>()

// Spots: the planting form picks a place by a tap on the map; a spot of the selected bed can be dragged elsewhere.
const notify = useNotify()
const picker = useSpotPicker()
const updatePlanting = useUpdatePlanting()
async function moveSpot(plantingId: string, point: Point) {
  // The spot being edited moves with the form; it is saved with it.
  if (picker.draft.value?.plantingId === plantingId) return void (picker.point.value = point)
  const p = plantings.data.value?.find((x) => x.id === plantingId)
  if (!p) return
  try {
    await updatePlanting.mutateAsync({ id: p.id, input: { ...toUpdateInput(p), spotX: point.x, spotY: point.y } })
  } catch (error) {
    notify.error(error)
  }
}

// Crop finder: "where is my tomato?" — a whole category or one variety, on the selected date.
const findValue = ref<string>()
const findOptions = computed(() =>
  (categories.data.value ?? []).map((c) => ({
    label: `${c.icon} ${c.name}`,
    items: [
      { value: `c:${c.id}`, label: t('map.find.allOf', { name: c.name }) },
      ...(varieties.data.value ?? [])
        .filter((v) => v.categoryId === c.id)
        .map((v) => ({ value: `v:${v.id}`, label: [v.name, v.cultivar].filter(Boolean).join(' — ') })),
    ],
  })),
)
const findQuery = computed((): CropQuery | undefined => {
  const [kind, id] = findValue.value?.split(':') ?? []
  return !id ? undefined : kind === 'c' ? { categoryId: id } : { varietyId: id }
})
const found = computed(() =>
  findQuery.value && layout.data.value
    ? findCrop(
        findQuery.value,
        layout.data.value.beds,
        plantings.data.value ?? [],
        date.value,
        (id) => varietyById.value.get(id)?.categoryId,
      )
    : undefined,
)
const foundBedIds = computed(() => (found.value ? new Set(found.value.map((h) => h.bed.id)) : undefined))

// Plants needed in a period: the panel lists them and highlights their beds (the crop finder takes precedence).
const showDemand = ref(false)
const demandBedIds = ref<Set<string>>()
const highlightBedIds = computed(() => foundBedIds.value ?? (showDemand.value ? demandBedIds.value : undefined))

// Design mode: the map shows the designer's working copy; nothing is saved until "Save".
const confirm = useConfirm()
const designer = useDesigner()
const designing = ref(false)
const shownLayout = computed(() => (designing.value ? designer.draft.value : layout.data.value))
function startDesign() {
  if (!layout.data.value) return
  designer.start(layout.data.value)
  designing.value = true
}
function stopDesign() {
  if (!designer.dirty.value) {
    designing.value = false
    return
  }
  confirm.require({
    header: t('design.discardTitle'),
    message: t('design.discard'),
    acceptProps: { label: t('design.discardConfirm'), severity: 'danger' },
    rejectProps: { label: t('common.cancel'), severity: 'secondary', outlined: true },
    accept: () => (designing.value = false),
  })
}
const center = () => map.value?.center() ?? { x: 0, y: 0 }

/** Measuring tape on the map (in both modes): clicks set its points. */
const measuring = ref(false)
</script>

<template>
  <div class="map-page">
    <TimelineBar v-model="date" :from="period.from" :to="period.to" :today="period.today" />
    <div class="content">
      <div class="canvas">
        <GardenMap
          v-if="shownLayout"
          ref="map"
          :layout="shownLayout"
          :plantings="plantings.data.value ?? []"
          :date="date"
          :today="period.today"
          :crop-of="cropOf"
          :selected-id="designing ? designer.selection.value?.id : selectedId"
          :warning-bed-ids="warningBedIds"
          :alert-ids="alertIds"
          :design-mode="designing"
          :pick-bed-id="designing ? undefined : picker.bedId.value"
          :pick-point="picker.point.value"
          :pick-draft="picker.draft.value"
          :spots-editable="!designing && !readonly"
          :highlight-bed-ids="designing ? undefined : highlightBedIds"
          @select="designing ? designer.select($event) : select($event)"
          @pick="picker.point.value = $event"
          @move-spot="moveSpot"
          :guides="designing ? designer.guides.value : undefined"
          :measuring="measuring"
          @move-start="designer.moveStart"
          @move="designer.move"
          @move-end="designer.moveEnd"
          @vertex-start="designer.vertexStart"
          @vertex="designer.moveVertex"
          @vertex-end="designer.moveEnd"
          @remove-vertex="designer.removeVertex"
        >
          <template #overlay>
            <DimensionOverlay v-if="designing && designer.selected.value" :item="designer.selected.value" />
          </template>
        </GardenMap>
        <div class="top-left">
          <div class="toolbar">
            <span class="compass" aria-hidden="true">N ↑</span>
            <Select
              v-model="siteId"
              :options="sites.data.value ?? []"
              option-label="name"
              option-value="id"
              size="small"
              :aria-label="t('map.site')"
              :disabled="designing"
            />
            <Button
              v-if="!designing && !readonly"
              :label="t('design.start')"
              icon="pi pi-pencil"
              size="small"
              severity="secondary"
              :disabled="!layout.data.value"
              @click="startDesign"
            />
            <template v-else-if="designing">
              <span class="designing">✏️ {{ t('design.active') }}</span>
              <Button :label="t('design.stop')" icon="pi pi-times" size="small" severity="secondary" outlined @click="stopDesign" />
            </template>
            <Button
              v-if="!designing"
              :label="t('map.demand.open')"
              icon="pi pi-shopping-bag"
              size="small"
              :severity="showDemand ? 'contrast' : 'secondary'"
              :aria-pressed="showDemand"
              @click="showDemand = !showDemand"
            />
            <Select
              v-if="!designing"
              v-model="findValue"
              :options="findOptions"
              option-group-label="label"
              option-group-children="items"
              option-label="label"
              option-value="value"
              filter
              show-clear
              size="small"
              class="finder"
              :placeholder="t('map.find.placeholder')"
              :aria-label="t('map.find.placeholder')"
            />
          </div>
          <div v-if="found && !designing" class="found">
            <span v-if="!found.length" class="muted small">{{ t('map.find.none') }}</span>
            <Button
              v-for="hit in found"
              :key="hit.bed.id"
              size="small"
              :severity="selectedId === hit.bed.id ? 'contrast' : 'secondary'"
              :outlined="selectedId !== hit.bed.id"
              @click="selectedId = hit.bed.id"
            >
              <b>{{ hit.bed.code }}</b>
              <span v-if="hit.amounts.length" class="small">{{ hit.amounts.join(' · ') }}</span>
            </Button>
          </div>
        </div>
        <div class="zoom">
          <Button icon="pi pi-plus" severity="secondary" :aria-label="t('map.zoomIn')" @click="map?.zoom(1.3)" />
          <Button icon="pi pi-minus" severity="secondary" :aria-label="t('map.zoomOut')" @click="map?.zoom(1 / 1.3)" />
          <Button icon="pi pi-expand" severity="secondary" :aria-label="t('map.fit')" @click="map?.fit()" />
          <Button
            icon="pi pi-arrows-h"
            :severity="measuring ? 'contrast' : 'secondary'"
            :aria-label="t('map.measure.label')"
            :aria-pressed="measuring"
            v-tooltip.left="t('map.measure.label')"
            @click="measuring = !measuring"
          />
        </div>
        <div v-if="measuring" class="measure-hint small" role="status">📏 {{ t('map.measure.help') }}</div>
        <MapLegend :crops="cropsOnMap" />
      </div>

      <aside class="panel">
        <DesignerPanel
          v-if="designing && siteId"
          :designer="designer"
          :site-id="siteId"
          :center="center"
          :beds-in-use="new Set((plantings.data.value ?? []).map((p) => p.bedId))"
          @cancel="stopDesign"
          @saved="designing = false"
        />
        <BedPanel
          v-else-if="selectedBed"
          :key="selectedBed.id"
          :bed="selectedBed"
          :plot-name="layout.data.value?.plots.find((p) => p.id === selectedBed!.plotId)?.name"
          :plantings="forBed(plantings.data.value, selectedBed.id)"
          :warnings="forBed(checks.data.value, selectedBed.id)"
          :tasks="forBed(tasks.data.value, selectedBed.id)"
          :period="period"
          :date="date"
          :crop-of="cropOf"
          :where-of="whereOf"
          :readonly="readonly"
          @close="selectedId = undefined"
          @date="date = $event"
        >
          <template #log>
            <NoteLog :site-id="siteId!" :bed-id="selectedBed.id" :plantings="bedCrops" :date="date" :readonly="readonly" />
          </template>
        </BedPanel>
        <TreePanel
          v-else-if="selectedTree"
          :key="selectedTree.id"
          :tree="selectedTree"
          :tasks="(tasks.data.value ?? []).filter((x) => x.treeId === selectedTree!.id)"
          :today="period.today"
          :where-of="whereOf"
          :readonly="readonly"
          @close="selectedId = undefined"
        >
          <template #log>
            <NoteLog :site-id="siteId!" :tree-id="selectedTree.id" :date="date" :readonly="readonly" />
          </template>
        </TreePanel>
        <DemandPanel
          v-else-if="showDemand && siteId && layout.data.value"
          :site-id="siteId"
          :beds="layout.data.value.beds"
          :date="date"
          :crop-of="cropOf"
          @close="showDemand = false"
          @select="selectedId = $event"
          @highlight="demandBedIds = $event"
        />
        <OverviewPanel
          v-else-if="layout.data.value && site"
          :site-name="site.name"
          :layout="layout.data.value"
          :plantings="plantings.data.value ?? []"
          :warnings="checks.data.value ?? []"
          :tasks="tasks.data.value ?? []"
          :date="date"
          :today="period.today"
          :crop-of="cropOf"
          :where-of="whereOf"
          :harvested="harvested"
          :readonly="readonly"
          @select="selectedId = $event"
        />
      </aside>
    </div>
  </div>
</template>

<style scoped>
.map-page {
  display: flex;
  flex-direction: column;
  height: 100%;
}
.content {
  flex: 1;
  min-height: 0;
  display: grid;
  grid-template-columns: minmax(0, 1fr) 420px;
}
.canvas {
  position: relative;
  min-height: 0;
}
.top-left {
  position: absolute;
  top: 12px;
  left: 12px;
  right: 64px;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 6px;
  pointer-events: none;
}
.top-left > * {
  pointer-events: auto;
}
.toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  align-items: center;
}
.finder {
  width: 220px;
}
.found {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  align-items: center;
  background: var(--c-panel);
  border-radius: 10px;
  padding: 6px 8px;
  box-shadow: var(--shadow);
}
.found .p-button {
  gap: 6px;
}
.designing {
  background: var(--c-ink);
  color: #fff;
  border-radius: 8px;
  padding: 5px 10px;
  font-size: 12px;
  font-weight: 700;
}
.compass {
  background: var(--c-panel);
  border-radius: 8px;
  padding: 5px 9px;
  font-weight: 700;
  font-size: 12px;
  box-shadow: var(--shadow);
}
.measure-hint {
  position: absolute;
  top: 12px;
  right: 60px;
  background: var(--c-panel);
  border-radius: 8px;
  padding: 5px 10px;
  box-shadow: var(--shadow);
  white-space: nowrap;
}
.zoom {
  position: absolute;
  top: 12px;
  right: 12px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.panel {
  border-left: 1px solid var(--c-line);
  background: var(--c-panel);
  overflow: auto;
  padding: 16px 18px 30px;
}
@media (max-width: 900px) {
  .map-page {
    height: auto;
    min-height: 100%;
  }
  .content {
    grid-template-columns: minmax(0, 1fr);
    grid-template-rows: 58vh auto;
  }
  .panel {
    border-left: 0;
    border-top: 1px solid var(--c-line);
    overflow: visible;
  }
}
</style>
