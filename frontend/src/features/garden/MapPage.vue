<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Button from 'primevue/button'
import Select from 'primevue/select'
import { useConfirm } from 'primevue/useconfirm'
import { useI18n } from 'vue-i18n'
import type { TaskDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import { useCatalogLookups } from '@/features/catalog/queries'
import { useTasks } from '@/features/tasks/queries'
import BedPanel from './components/BedPanel.vue'
import DesignerPanel from './components/DesignerPanel.vue'
import DimensionOverlay from './components/DimensionOverlay.vue'
import GardenMap, { type Crop } from './components/GardenMap.vue'
import MapLegend from './components/MapLegend.vue'
import OverviewPanel from './components/OverviewPanel.vue'
import TimelineBar from './components/TimelineBar.vue'
import TreePanel from './components/TreePanel.vue'
import { harvestSummary } from './harvest'
import { addDays, IN_BED, phaseAt } from './map'
import { mapPeriod, useLayout, useNotes, usePlantings, useSiteChecks, useSites } from './queries'
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
const { varietyById, categoryById } = useCatalogLookups()

const site = computed(() => sites.data.value?.find((s) => s.id === siteId.value))
const selectedBed = computed(() => layout.data.value?.beds.find((b) => b.id === selectedId.value))
const selectedTree = computed(() => layout.data.value?.trees.find((x) => x.id === selectedId.value))

const cropOf = (varietyId: string): Crop | undefined => {
  const variety = varietyById.value.get(varietyId)
  const category = variety ? categoryById.value.get(variety.categoryId) : undefined
  return variety && category ? { icon: category.icon, color: category.color, name: variety.name } : undefined
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

const forBed = <T extends { bedId?: string }>(items: T[] | undefined, id: string) => (items ?? []).filter((x) => x.bedId === id)

/** Only beds and trees open a panel; clicking anything else (or empty space) shows the overview. */
const select = (id: string | undefined) => {
  const l = layout.data.value
  selectedId.value = id && (l?.beds.some((b) => b.id === id) || l?.trees.some((x) => x.id === id)) ? id : undefined
}

const map = ref<InstanceType<typeof GardenMap>>()

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
          @select="designing ? designer.select($event) : select($event)"
          @move-start="designer.moveStart"
          @move="designer.move"
          @move-end="designer.moveEnd"
        >
          <template #overlay>
            <DimensionOverlay v-if="designing && designer.selected.value" :item="designer.selected.value" />
          </template>
        </GardenMap>
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
            v-if="!designing"
            :label="t('design.start')"
            icon="pi pi-pencil"
            size="small"
            severity="secondary"
            :disabled="!layout.data.value"
            @click="startDesign"
          />
          <span v-else class="designing">✏️ {{ t('design.active') }}</span>
        </div>
        <div class="zoom">
          <Button icon="pi pi-plus" severity="secondary" :aria-label="t('map.zoomIn')" @click="map?.zoom(1.3)" />
          <Button icon="pi pi-minus" severity="secondary" :aria-label="t('map.zoomOut')" @click="map?.zoom(1 / 1.3)" />
          <Button icon="pi pi-expand" severity="secondary" :aria-label="t('map.fit')" @click="map?.fit()" />
        </div>
        <MapLegend :crops="cropsOnMap" />
      </div>

      <aside class="panel">
        <DesignerPanel
          v-if="designing && siteId"
          :designer="designer"
          :site-id="siteId"
          :center="center"
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
          @close="selectedId = undefined"
          @date="date = $event"
        />
        <TreePanel
          v-else-if="selectedTree"
          :key="selectedTree.id"
          :tree="selectedTree"
          :tasks="(tasks.data.value ?? []).filter((x) => x.treeId === selectedTree!.id)"
          :today="period.today"
          :where-of="whereOf"
          @close="selectedId = undefined"
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
.toolbar {
  position: absolute;
  top: 12px;
  left: 12px;
  display: flex;
  gap: 6px;
  align-items: center;
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
