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
  PointDto,
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
import { lineLength, treeCare } from '../map'
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
const { varieties, varietyById } = useCatalogLookups()

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
  points?: PointDto[]
  width?: number
  varietyId?: string
  outline?: TreeDto['outline']
  crownWidth?: number
  crownDepth?: number
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
/** A line (path, wall, fence) is edited by its points. */
const points = computed(() => item.value?.points)
const setPoint = (index: number, axis: 'x' | 'y', value: number) =>
  d.edit(`point${index}.${axis}`, () => (item.value!.points![index] = { ...item.value!.points![index]!, [axis]: value }))
/** A new point continues the line by its last part (or 1 m east). */
const addPoint = () =>
  d.edit(`points+${points.value!.length}`, () => {
    const p = item.value!.points!
    const [a, b] = [p.at(-2)!, p.at(-1)!]
    const step = Math.hypot(b.x - a.x, b.y - a.y) > 0 ? { x: b.x - a.x, y: b.y - a.y } : { x: 1, y: 0 }
    p.push({ x: Math.round((b.x + step.x) * 100) / 100, y: Math.round((b.y + step.y) * 100) / 100 })
  })
const removePoint = (index: number) => d.edit(`points-${index}`, () => item.value!.points!.splice(index, 1))
const isLine = computed(() => !!points.value)
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

// A tree of a catalog variety follows its care and pollination; perennials are offered first.
const treeVarieties = computed(() =>
  [...(varieties.data.value ?? [])]
    .sort((a, b) => Number(b.life === 'perennial') - Number(a.life === 'perennial') || a.name.localeCompare(b.name))
    .map((v) => ({ value: v.id, label: [v.name, v.cultivar].filter(Boolean).join(' – ') })),
)
const treeVariety = computed(() => (item.value?.varietyId ? varietyById.value.get(item.value.varietyId) : undefined))
const inheritedActions = computed(() => treeCare({ actions: actions.value }, treeVariety.value).filter((c) => c.inherited))
/** Choosing a variety names a tree that has no name of its own yet. */
const setVariety = (id: string | undefined) =>
  d.edit('varietyId', () => {
    const tree = item.value!
    const variety = id ? varietyById.value.get(id) : undefined
    tree.varietyId = id ?? undefined
    if (variety && (!tree.name?.trim() || tree.name === t('design.newTree'))) tree.name = variety.name
  })
const treeOutlines = computed(() => options<TreeDto['outline']>(['ellipse', 'rect'], 'design.treeOutline'))
/** A round crown has one diameter. */
const setDiameter = (value: number) => d.edit('crownDiameter', () => Object.assign(item.value!, { crownWidth: value, crownDepth: value }))
const setOutline = (outline: TreeDto['outline']) =>
  d.edit('outline', () => {
    const tree = item.value!
    tree.outline = outline
    if (outline === 'ellipse') tree.crownDepth = tree.crownWidth
  })
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
        <Button
          icon="pi pi-clone"
          size="small"
          severity="secondary"
          text
          :aria-label="t('design.duplicate')"
          v-tooltip.bottom="`${t('design.duplicate')} (Ctrl+D)`"
          @click="d.duplicate"
        />
        <Button
          icon="pi pi-angle-double-up"
          size="small"
          severity="secondary"
          text
          :aria-label="t('design.toFront')"
          v-tooltip.bottom="`${t('design.toFront')} (Ctrl+])`"
          @click="d.arrange('front')"
        />
        <Button
          icon="pi pi-angle-double-down"
          size="small"
          severity="secondary"
          text
          :aria-label="t('design.toBack')"
          v-tooltip.bottom="`${t('design.toBack')} (Ctrl+[)`"
          @click="d.arrange('back')"
        />
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

      <template v-if="isLine && points">
        <FormField v-if="kind === 'path'" :label="t('design.materialLabel')"
          ><Select
            :model-value="item.material"
            :options="materials"
            option-label="label"
            option-value="value"
            @update:model-value="set('material', $event)"
        /></FormField>
        <FormField v-if="kind === 'path'" :label="t('design.width')" for="d-pw">
          <InputNumber
            input-id="d-pw"
            :model-value="item.width"
            :min="0.1"
            :max="20"
            :max-fraction-digits="2"
            :step="0.05"
            suffix=" m"
            @update:model-value="set('width', $event ?? 0.8)"
          />
        </FormField>
        <FormField v-else :label="t('design.thickness')" for="d-thickness">
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
        <p class="full small muted">
          {{ t('design.lineLength') }}: <b>{{ lineLength(points).toLocaleString($i18n.locale, { maximumFractionDigits: 2 }) }} m</b> ·
          {{ t('design.pointsHelp') }}
        </p>
        <div class="full stack">
          <b class="small">{{ t('design.points') }}</b>
          <div v-for="(p, i) in points" :key="i" class="point">
            <span class="small muted">{{ i + 1 }}</span>
            <InputNumber
              :model-value="p.x"
              :max-fraction-digits="2"
              :step="0.1"
              suffix=" m"
              :aria-label="`${t('design.x')} ${i + 1}`"
              @update:model-value="setPoint(i, 'x', $event ?? 0)"
            />
            <InputNumber
              :model-value="p.y"
              :max-fraction-digits="2"
              :step="0.1"
              suffix=" m"
              :aria-label="`${t('design.y')} ${i + 1}`"
              @update:model-value="setPoint(i, 'y', $event ?? 0)"
            />
            <Button
              icon="pi pi-times"
              text
              severity="secondary"
              :aria-label="t('common.remove')"
              :disabled="points.length <= 2"
              @click="removePoint(i)"
            />
          </div>
          <div><Button :label="t('design.addPoint')" icon="pi pi-plus" text size="small" @click="addPoint" /></div>
        </div>
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
        <FormField :label="t('design.variety')" :hint="t('design.varietyHelp')" full>
          <Select
            :model-value="item.varietyId"
            :options="treeVarieties"
            option-label="label"
            option-value="value"
            filter
            show-clear
            :placeholder="t('design.noVariety')"
            @update:model-value="setVariety($event)"
          />
        </FormField>
        <FormField :label="t('design.shapeLabel')">
          <Select
            :model-value="item.outline"
            :options="treeOutlines"
            option-label="label"
            option-value="value"
            @update:model-value="setOutline($event)"
          />
        </FormField>
        <FormField v-if="item.outline !== 'rect'" :label="t('design.crown')" for="d-crown">
          <InputNumber
            input-id="d-crown"
            :model-value="item.crownWidth"
            :min="0.1"
            :max-fraction-digits="2"
            :step="0.1"
            suffix=" m"
            @update:model-value="setDiameter($event ?? 1)"
          />
        </FormField>
        <template v-else>
          <FormField :label="t('design.width')" for="d-cw">
            <InputNumber
              input-id="d-cw"
              :model-value="item.crownWidth"
              :min="0.1"
              :max-fraction-digits="2"
              :step="0.1"
              suffix=" m"
              @update:model-value="set('crownWidth', $event ?? 1)"
            />
          </FormField>
          <FormField :label="t('design.length')" for="d-cd">
            <InputNumber
              input-id="d-cd"
              :model-value="item.crownDepth"
              :min="0.1"
              :max-fraction-digits="2"
              :step="0.1"
              suffix=" m"
              @update:model-value="set('crownDepth', $event ?? 1)"
            />
          </FormField>
        </template>
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
        <p v-if="treeVariety?.pollination" class="full small muted">
          {{ t('map.tree.pollination') }}:
          {{
            t('pollination.summary', {
              need: t(`pollination.need.${treeVariety.pollination.need}`),
              group: treeVariety.pollination.group,
              range: treeVariety.pollination.rangeMeters,
            })
          }}
          · {{ t('map.tree.fromVariety') }}
        </p>
        <PollinationInput
          v-else
          class="full"
          :model-value="item.pollination"
          :groups="pollinationGroups"
          @update:model-value="set('pollination', $event)"
        />
        <div class="full stack">
          <b class="small">{{ t('design.actions') }}</b>
          <div v-for="(c, i) in inheritedActions" :key="`v${i}`" class="small inherited">
            ✂️ {{ c.action.title }} · {{ month(c.action.month) }} · {{ c.action.estimatedMinutes }} {{ t('common.minutesUnit') }}
            <span class="muted">· {{ t('map.tree.fromVariety') }}</span>
          </div>
          <p v-if="treeVariety" class="small muted">{{ t('design.ownActionsHelp') }}</p>
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
.point {
  display: grid;
  grid-template-columns: 16px minmax(0, 1fr) minmax(0, 1fr) auto;
  gap: 5px;
  align-items: center;
}
.inherited {
  padding: 4px 0;
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
