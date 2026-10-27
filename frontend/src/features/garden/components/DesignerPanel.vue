<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted } from 'vue'
import Button from 'primevue/button'
import DatePicker from 'primevue/datepicker'
import InputNumber from '@/shared/ui/NumberInput.vue'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import { useI18n } from 'vue-i18n'
import type {
  BedDto,
  BedKind,
  FeatureDto,
  MarkerKind,
  PathDto,
  PlotDto,
  PollinationDto,
  ShapeDto,
  TreeDto,
  YearlyActionDto,
} from '@/shared/api'
import { fromIsoDate, toIsoDate } from '@/shared/dates'
import { useFormat } from '@/shared/composables/useFormat'
import { useNotify } from '@/shared/composables/useNotify'
import ColorInput from '@/shared/ui/ColorInput.vue'
import FormField from '@/shared/ui/FormField.vue'
import PollinationInput from '@/shared/ui/PollinationInput.vue'
import { useCatalogLookups } from '@/features/catalog/queries'
import { useSaveLayout } from '../queries'
import type { ItemKind, useDesigner } from '../useDesigner'

/** Edits the layout copy held by the designer; saves the whole layout at once. */
const props = defineProps<{
  designer: ReturnType<typeof useDesigner>
  siteId: string
  center: () => { x: number; y: number }
  /** Beds with plantings: their kind can no longer change. */
  bedsInUse: ReadonlySet<string>
}>()
const emit = defineEmits<{ cancel: []; saved: [] }>()

const { t } = useI18n()
const { month } = useFormat()
const notify = useNotify()
const saveLayout = useSaveLayout()
const d = props.designer
const { varieties } = useCatalogLookups()

onMounted(() => document.addEventListener('keydown', d.handleKey))
onBeforeUnmount(() => document.removeEventListener('keydown', d.handleKey))

const kinds: ItemKind[] = ['bed', 'path', 'tree', 'feature', 'plot']
const options = <T extends string>(values: T[], prefix: string) => values.map((value) => ({ value, label: t(`${prefix}.${value}`) }))
const shapeKinds = computed(() => options<ShapeDto['kind']>(['rect', 'ellipse', 'keyhole'], 'design.shape'))
const plotShapes = computed(() => shapeKinds.value.filter((s) => s.value !== 'keyhole'))
const plotTypes = computed(() => options<PlotDto['type']>(['open', 'greenhouse', 'orchard'], 'design.plotType'))
const materials = computed(() => options<PathDto['material']>(['woodChips', 'grass', 'pavers', 'gravel'], 'design.material'))
const featureTypes = computed(() => options<FeatureDto['type']>(['compost', 'water', 'pond', 'hedge', 'shed'], 'design.featureType'))
const treeKinds = computed(() => options<TreeDto['kind']>(['tree', 'shrub'], 'map.tree.kind'))
const bedKinds = computed(() => options<BedKind>(['strips', 'spots'], 'design.bedKind'))
const suns = computed(() => options<BedDto['sun']>(['full', 'half', 'shade'], 'sun'))
const months = computed(() => Array.from({ length: 12 }, (_, i) => ({ value: i + 1, label: month(i + 1) })))

const kind = computed(() => d.selection.value?.kind)
/** Any map item, seen through the fields the form edits (plots and features share `type`, so no intersection type). */
interface EditableItem {
  id: string
  name?: string
  code?: string
  shape?: ShapeDto
  plotId?: string
  kind?: TreeDto['kind'] | BedKind | MarkerKind
  label?: string
  color?: string
  thickness?: number
  sun?: BedDto['sun']
  soil?: string
  type?: PlotDto['type'] | FeatureDto['type']
  material?: PathDto['material']
  x?: number
  y?: number
  crownDiameter?: number
  cultivar?: string
  plantedOn?: string
  actions?: YearlyActionDto[]
  pollination?: PollinationDto
}
const item = computed(() => d.selected.value as EditableItem | undefined)

/** Set a field of the selected item through the designer, so it can be undone. */
const set = (field: string, value: unknown) => d.edit(field, () => Object.assign(item.value!, { [field]: value }))
const setShape = (field: keyof ShapeDto, value: unknown) =>
  d.edit(`shape.${field}`, () => (item.value!.shape = { ...item.value!.shape!, [field]: value }))
/** A line marker is edited by its two ends; moving one end keeps the other in place. */
const lineEnd = (axis: 'x' | 'y') => {
  const s = item.value!.shape!
  return axis === 'x' ? s.x + s.width : s.y + s.height
}
const setLineStart = (axis: 'x' | 'y', value: number) => {
  const end = lineEnd(axis)
  d.edit(`line.start.${axis}`, () => {
    const s = item.value!.shape!
    item.value!.shape = axis === 'x' ? { ...s, x: value, width: end - value } : { ...s, y: value, height: end - value }
  })
}
const setLineEnd = (axis: 'x' | 'y', value: number) =>
  d.edit(`line.end.${axis}`, () => {
    const s = item.value!.shape!
    item.value!.shape = axis === 'x' ? { ...s, width: value - s.x } : { ...s, height: value - s.y }
  })
const isLine = computed(() => kind.value === 'marker' && item.value?.kind === 'line')
const area = computed(() => {
  const s = item.value?.shape
  if (!s) return 0
  return s.kind === 'ellipse'
    ? (Math.PI * s.width * s.height) / 4
    : s.kind === 'keyhole'
      ? Math.PI * (Math.min(s.width, s.height) / 2) ** 2 * 0.85
      : s.width * s.height
})
const plantedOn = computed({
  get: () => fromIsoDate(item.value?.plantedOn),
  set: (date) => set('plantedOn', toIsoDate(date)),
})
const actions = computed(() => item.value?.actions ?? [])
const setAction = (index: number, field: keyof YearlyActionDto, value: unknown) =>
  d.edit(`action${index}.${field}`, () => (item.value!.actions![index] = { ...item.value!.actions![index]!, [field]: value }))
const addAction = () =>
  d.edit(`actions+${actions.value.length}`, () => item.value!.actions!.push({ title: t('design.pruning'), month: 2, estimatedMinutes: 60 }))
const pollinationGroups = computed(() => [
  ...new Set(
    [...(d.draft.value?.trees ?? []).map((x) => x.pollination), ...(varieties.data.value ?? []).map((v) => v.pollination)].flatMap((p) =>
      p?.group.trim() ? [p.group.trim()] : [],
    ),
  ),
])
const removeAction = (index: number) => d.edit(`actions-${index}`, () => item.value!.actions!.splice(index, 1))

const add = (kind: ItemKind, variant?: BedKind | MarkerKind) =>
  d.add(
    kind,
    props.center(),
    { path: t('design.newPath'), tree: t('design.newTree'), plot: t('design.newPlot'), feature: t('design.newFeature') },
    variant,
  )

const groups = computed(() => {
  const layout = d.draft.value
  if (!layout) return []
  return [
    { kind: 'plot' as const, items: layout.plots.map((p) => ({ id: p.id, label: p.name })) },
    { kind: 'bed' as const, items: layout.beds.map((b) => ({ id: b.id, label: b.code })) },
    { kind: 'path' as const, items: layout.paths.map((p) => ({ id: p.id, label: p.name })) },
    { kind: 'tree' as const, items: layout.trees.map((x) => ({ id: x.id, label: x.name })) },
    { kind: 'feature' as const, items: layout.features.map((f) => ({ id: f.id, label: f.name })) },
    {
      kind: 'marker' as const,
      items: layout.markers.map((m, i) => ({ id: m.id, label: m.label || `${t(`design.markerKind.${m.kind}`)} ${i + 1}` })),
    },
  ]
})

async function save() {
  try {
    await saveLayout.mutateAsync({ siteId: props.siteId, layout: d.draft.value! })
    notify.success('design.saved')
    emit('saved')
  } catch (error) {
    notify.error(error)
  }
}
</script>

<template>
  <section :aria-label="t('design.title')">
    <div class="panel-head">
      <div>
        <h2>✏️ {{ t('design.title') }}</h2>
        <div class="muted small">{{ t('design.help') }}</div>
      </div>
    </div>
    <div class="row">
      <Button
        icon="pi pi-undo"
        :label="t('design.undo')"
        size="small"
        severity="secondary"
        outlined
        :disabled="!d.canUndo.value"
        v-tooltip.bottom="'Ctrl+Z'"
        @click="d.undo"
      />
      <Button
        icon="pi pi-refresh"
        :label="t('design.redo')"
        size="small"
        severity="secondary"
        outlined
        :disabled="!d.canRedo.value"
        v-tooltip.bottom="'Ctrl+Y'"
        @click="d.redo"
      />
      <span class="grow" />
      <Button
        :label="d.dirty.value ? t('common.cancel') : t('design.stop')"
        size="small"
        severity="secondary"
        outlined
        @click="emit('cancel')"
      />
      <Button
        :label="t('design.save')"
        icon="pi pi-check"
        size="small"
        :disabled="!d.dirty.value"
        :loading="saveLayout.isPending.value"
        @click="save"
      />
    </div>

    <h3>{{ t('design.add') }}</h3>
    <div class="row">
      <Button
        v-for="k in kinds"
        :key="k"
        :label="t(`design.kind.${k}`)"
        icon="pi pi-plus"
        size="small"
        severity="secondary"
        outlined
        @click="add(k)"
      />
      <Button :label="t('design.addSpotsBed')" icon="pi pi-plus" size="small" severity="secondary" outlined @click="add('bed', 'spots')" />
      <Button
        v-for="m in ['area', 'line'] as const"
        :key="m"
        :label="t(`design.markerKind.${m}`)"
        icon="pi pi-plus"
        size="small"
        severity="secondary"
        outlined
        @click="add('marker', m)"
      />
    </div>
    <p class="muted small">{{ t('design.markerHelp') }}</p>

    <form v-if="item && kind" class="form-grid editor" @submit.prevent>
      <div class="full row">
        <b>{{ kind === 'marker' ? t(`design.markerKind.${item.kind}`) : t(`design.kind.${kind}`) }}</b>
        <span class="grow" />
        <Button icon="pi pi-trash" :label="t('common.delete')" size="small" severity="danger" text @click="d.remove" />
      </div>

      <FormField v-if="kind === 'bed'" :label="t('design.code')" for="d-code">
        <InputText id="d-code" :model-value="item.code" maxlength="12" @update:model-value="set('code', $event)" />
      </FormField>
      <template v-else-if="kind === 'marker'">
        <FormField :label="t('design.label')" for="d-label">
          <InputText id="d-label" :model-value="item.label" maxlength="100" @update:model-value="set('label', $event)" />
        </FormField>
        <FormField :label="t('design.color')">
          <ColorInput :model-value="item.color!" @update:model-value="set('color', $event)" />
        </FormField>
      </template>
      <FormField v-else :label="t('common.name')" for="d-name">
        <InputText id="d-name" :model-value="item.name" @update:model-value="set('name', $event)" />
      </FormField>

      <template v-if="isLine && item.shape">
        <FormField v-for="axis in ['x', 'y'] as const" :key="`s${axis}`" :label="t(`design.lineStart.${axis}`)" :for="`d-s${axis}`">
          <InputNumber
            :input-id="`d-s${axis}`"
            :model-value="item.shape[axis]"
            :max-fraction-digits="2"
            :step="0.1"
            suffix=" m"
            @update:model-value="setLineStart(axis, $event ?? 0)"
          />
        </FormField>
        <FormField v-for="axis in ['x', 'y'] as const" :key="`e${axis}`" :label="t(`design.lineEnd.${axis}`)" :for="`d-e${axis}`">
          <InputNumber
            :input-id="`d-e${axis}`"
            :model-value="lineEnd(axis)"
            :max-fraction-digits="2"
            :step="0.1"
            suffix=" m"
            @update:model-value="setLineEnd(axis, $event ?? 0)"
          />
        </FormField>
        <FormField :label="t('design.thickness')" for="d-thickness">
          <InputNumber
            input-id="d-thickness"
            :model-value="item.thickness"
            :min="0.02"
            :max="5"
            :max-fraction-digits="2"
            :step="0.05"
            suffix=" m"
            @update:model-value="set('thickness', $event ?? 0.2)"
          />
        </FormField>
      </template>

      <template v-else-if="kind === 'tree'">
        <FormField :label="t('design.x')" for="d-x"
          ><InputNumber
            input-id="d-x"
            :model-value="item.x"
            :max-fraction-digits="2"
            :step="0.1"
            @update:model-value="set('x', $event ?? 0)"
        /></FormField>
        <FormField :label="t('design.y')" for="d-y"
          ><InputNumber
            input-id="d-y"
            :model-value="item.y"
            :max-fraction-digits="2"
            :step="0.1"
            @update:model-value="set('y', $event ?? 0)"
        /></FormField>
        <FormField :label="t('design.crown')" for="d-crown">
          <InputNumber
            input-id="d-crown"
            :model-value="item.crownDiameter"
            :min="0.1"
            :max-fraction-digits="2"
            :step="0.1"
            suffix=" m"
            @update:model-value="set('crownDiameter', $event ?? 1)"
          />
        </FormField>
        <FormField :label="t('design.type')"
          ><Select
            :model-value="item.kind"
            :options="treeKinds"
            option-label="label"
            option-value="value"
            @update:model-value="set('kind', $event)"
        /></FormField>
        <FormField :label="t('design.cultivar')" for="d-cultivar"
          ><InputText id="d-cultivar" :model-value="item.cultivar" @update:model-value="set('cultivar', $event)"
        /></FormField>
        <FormField :label="t('map.tree.planted')" for="d-planted"
          ><DatePicker v-model="plantedOn" input-id="d-planted" show-icon date-format="dd/mm/yy"
        /></FormField>
        <PollinationInput
          class="full"
          :model-value="item.pollination"
          :groups="pollinationGroups"
          @update:model-value="set('pollination', $event)"
        />
        <div class="full stack">
          <b class="small">{{ t('design.actions') }}</b>
          <div v-for="(a, i) in actions" :key="i" class="action">
            <InputText :model-value="a.title" :aria-label="t('design.action')" @update:model-value="setAction(i, 'title', $event)" />
            <Select
              :model-value="a.month"
              :options="months"
              option-label="label"
              option-value="value"
              :aria-label="t('common.month')"
              @update:model-value="setAction(i, 'month', $event)"
            />
            <InputNumber
              :model-value="a.estimatedMinutes"
              :min="1"
              :suffix="` ${t('common.minutesUnit')}`"
              :aria-label="t('design.minutes')"
              @update:model-value="setAction(i, 'estimatedMinutes', $event ?? 30)"
            />
            <Button icon="pi pi-times" text severity="secondary" :aria-label="t('common.remove')" @click="removeAction(i)" />
          </div>
          <div><Button :label="t('design.addAction')" icon="pi pi-plus" text size="small" @click="addAction" /></div>
        </div>
      </template>

      <template v-else-if="item.shape">
        <FormField :label="t('design.x')" for="d-x"
          ><InputNumber
            input-id="d-x"
            :model-value="item.shape.x"
            :max-fraction-digits="2"
            :step="0.1"
            suffix=" m"
            @update:model-value="setShape('x', $event ?? 0)"
        /></FormField>
        <FormField :label="t('design.y')" for="d-y"
          ><InputNumber
            input-id="d-y"
            :model-value="item.shape.y"
            :max-fraction-digits="2"
            :step="0.1"
            suffix=" m"
            @update:model-value="setShape('y', $event ?? 0)"
        /></FormField>
        <FormField :label="t('design.width')" for="d-w"
          ><InputNumber
            input-id="d-w"
            :model-value="item.shape.width"
            :min="0.1"
            :max-fraction-digits="2"
            :step="0.05"
            suffix=" m"
            @update:model-value="setShape('width', $event ?? 0.1)"
        /></FormField>
        <FormField :label="t('design.length')" for="d-h"
          ><InputNumber
            input-id="d-h"
            :model-value="item.shape.height"
            :min="0.1"
            :max-fraction-digits="2"
            :step="0.05"
            suffix=" m"
            @update:model-value="setShape('height', $event ?? 0.1)"
        /></FormField>
        <p class="full small muted">
          {{ t('design.area') }}: <b>{{ area.toLocaleString($i18n.locale, { maximumFractionDigits: 2 }) }} m²</b>
        </p>
        <FormField v-if="kind === 'bed' || kind === 'plot' || kind === 'marker'" :label="t('design.shapeLabel')">
          <Select
            :model-value="item.shape.kind"
            :options="kind === 'bed' ? shapeKinds : plotShapes"
            option-label="label"
            option-value="value"
            @update:model-value="setShape('kind', $event)"
          />
        </FormField>
        <template v-if="kind === 'bed'">
          <FormField
            :label="t('design.bedKind.label')"
            :hint="t(bedsInUse.has(item.id) ? 'design.bedKind.inUse' : `design.bedKind.${item.kind}Help`)"
            full
          >
            <Select
              :model-value="item.kind"
              :options="bedKinds"
              option-label="label"
              option-value="value"
              :disabled="bedsInUse.has(item.id)"
              @update:model-value="set('kind', $event)"
            />
          </FormField>
          <FormField :label="t('design.plot')">
            <Select
              :model-value="item.plotId"
              :options="d.draft.value?.plots ?? []"
              option-label="name"
              option-value="id"
              @update:model-value="set('plotId', $event)"
            />
          </FormField>
          <FormField :label="t('design.sun')"
            ><Select
              :model-value="item.sun"
              :options="suns"
              option-label="label"
              option-value="value"
              @update:model-value="set('sun', $event)"
          /></FormField>
          <FormField :label="t('design.soil')" for="d-soil" full
            ><InputText id="d-soil" :model-value="item.soil" @update:model-value="set('soil', $event)"
          /></FormField>
        </template>
        <FormField v-if="kind === 'plot'" :label="t('design.type')"
          ><Select
            :model-value="item.type"
            :options="plotTypes"
            option-label="label"
            option-value="value"
            @update:model-value="set('type', $event)"
        /></FormField>
        <FormField v-if="kind === 'path'" :label="t('design.materialLabel')"
          ><Select
            :model-value="item.material"
            :options="materials"
            option-label="label"
            option-value="value"
            @update:model-value="set('material', $event)"
        /></FormField>
        <FormField v-if="kind === 'feature'" :label="t('design.type')"
          ><Select
            :model-value="item.type"
            :options="featureTypes"
            option-label="label"
            option-value="value"
            @update:model-value="set('type', $event)"
        /></FormField>
      </template>
    </form>

    <h3>{{ t('design.all') }}</h3>
    <div v-for="g in groups" :key="g.kind" class="group">
      <div class="small muted">
        <b>{{ t(`design.kind.${g.kind}`) }}</b>
      </div>
      <div class="row">
        <Button
          v-for="i in g.items"
          :key="i.id"
          :label="i.label"
          size="small"
          :severity="d.selection.value?.id === i.id ? 'contrast' : 'secondary'"
          :outlined="d.selection.value?.id !== i.id"
          @click="d.select(i.id)"
        />
      </div>
    </div>
  </section>
</template>

<style scoped>
.grow {
  flex: 1;
}
.editor {
  margin-top: 14px;
  background: var(--c-panel-2);
  border: 1px solid var(--c-line);
  border-radius: 12px;
  padding: 12px;
  grid-template-columns: 1fr 1fr;
}
.action {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 90px 90px auto;
  gap: 5px;
  align-items: center;
}
.group {
  margin-bottom: 8px;
}
.group .row {
  gap: 4px;
  margin-top: 4px;
}
</style>
