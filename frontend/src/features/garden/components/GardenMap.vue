<script setup lang="ts">
import { computed, onMounted, onBeforeUnmount, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { BedDto, FeatureDto, MarkerDto, PathDto, PlantingDto, PlantingPhase, SiteLayoutDto, TreeDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import {
  alongLine,
  crownShape,
  IN_BED,
  layoutBounds,
  lineLength,
  type PlantSpacing,
  phaseAt,
  PHASE_OPACITY,
  polyline,
  snapTargets,
  type Spot,
  spacingOf,
  spotShape,
  spotsOf,
} from '../map'
import { alignBox, type Guide, type SnapOptions } from '../snapping'
import type { Point, SpotDraft } from '../spotPicker'
import ShapeSvg from './ShapeSvg.vue'
import SpotSvg from './SpotSvg.vue'

export interface Crop {
  icon: string
  color: string
  name: string
  spacing?: PlantSpacing
}

/**
 * The site map in meters (x → east, y → south). Shows what is in each bed on `date`; in design mode items and the
 * points of lines can be dragged (the parent applies the move, snapping and undo history). The middle mouse button
 * always pans. With `measuring`, clicks set the points of a measuring tape.
 */
const props = defineProps<{
  layout: SiteLayoutDto
  plantings: PlantingDto[]
  date: string
  today: string
  cropOf: (varietyId: string) => Crop | undefined
  selectedId?: string
  warningBedIds?: ReadonlySet<string>
  alertIds?: ReadonlySet<string>
  designMode?: boolean
  /** While set, a tap inside this bed reports the point (`pick`) instead of selecting. */
  pickBedId?: string
  /** The point picked so far (m from the bed's top-left corner), drawn as a ring. */
  pickPoint?: Point
  /** The spot being planned or edited at the picked point, drawn live. */
  pickDraft?: SpotDraft
  /** Spots in the selected bed can be dragged to a new place (`moveSpot`). */
  spotsEditable?: boolean
  /** When set, other beds are dimmed (crop finder). */
  highlightBedIds?: ReadonlySet<string>
  /** Lines the dragged item lines up with (design mode). */
  guides?: Guide[]
  measuring?: boolean
}>()
const emit = defineEmits<{
  select: [id: string | undefined]
  moveStart: [id: string]
  move: [id: string, dx: number, dy: number, snap: SnapOptions]
  moveEnd: [id: string]
  /** Dragging a point of the selected line starts; with `insert` a new point is added at `index` first. */
  vertexStart: [id: string, index: number, insert?: Point]
  vertex: [id: string, index: number, at: Point, snap: SnapOptions]
  vertexEnd: []
  removeVertex: [id: string, index: number]
  pick: [point: Point]
  moveSpot: [plantingId: string, point: Point]
}>()

const { t } = useI18n()
const { decimal } = useFormat()
const svg = ref<SVGSVGElement>()
const view = reactive({ k: 40, tx: 0, ty: 0 })
const bounds = computed(() => layoutBounds(props.layout))
const future = computed(() => props.date > props.today)

function fit() {
  const rect = svg.value?.getBoundingClientRect()
  if (!rect?.width) return
  const b = bounds.value
  view.k = Math.min(rect.width / b.w, rect.height / b.h) * 0.97
  view.tx = (rect.width - b.w * view.k) / 2 - b.x * view.k
  view.ty = (rect.height - b.h * view.k) / 2 - b.y * view.k
}
const zoom = (factor: number, mx?: number, my?: number) => {
  const rect = svg.value!.getBoundingClientRect()
  const x = mx ?? rect.width / 2
  const y = my ?? rect.height / 2
  const k = Math.min(320, Math.max(10, view.k * factor))
  const f = k / view.k
  view.tx = x - (x - view.tx) * f
  view.ty = y - (y - view.ty) * f
  view.k = k
}
/** World coordinates (meters) of the middle of the visible map: where new items are placed. */
const center = () => {
  const rect = svg.value!.getBoundingClientRect()
  return { x: (rect.width / 2 - view.tx) / view.k, y: (rect.height / 2 - view.ty) / view.k }
}
defineExpose({ fit, zoom, center })

const observer = new ResizeObserver(() => fit())
onMounted(() => svg.value && observer.observe(svg.value))
onBeforeUnmount(() => observer.disconnect())
watch(() => props.layout, fit, { flush: 'post' })

/** Map coordinates (m) of a pointer position. */
const toMap = (e: PointerEvent): Point => {
  const rect = svg.value!.getBoundingClientRect()
  return { x: (e.clientX - rect.left - view.tx) / view.k, y: (e.clientY - rect.top - view.ty) / view.k }
}
/** Point inside a bed (m from its top-left corner), kept within the bed and snapped to 5 cm. */
const inBed = (bed: BedDto, p: Point): Point => {
  const clamp = (v: number, max: number) => Math.round(Math.min(max, Math.max(0, v)) * 20) / 20
  return { x: clamp(p.x - bed.shape.x, bed.shape.width), y: clamp(p.y - bed.shape.y, bed.shape.height) }
}
const bedById = (id: string | undefined) => props.layout.beds.find((b) => b.id === id)

// Pointer: drag on empty space pans; in design mode drag on an item moves it, on a point of the selected line moves
// that point (or, on a midpoint, adds one); a click without movement selects. The middle button always pans and never
// selects or moves. Outside design mode a spot of the selected bed can be dragged; while measuring a click sets a
// point of the tape. The item is taken from pointerdown: after setPointerCapture, pointerup targets the <svg> itself.
type Gesture = {
  kind: 'pan' | 'move' | 'spot' | 'vertex'
  id?: string
  spotId?: string
  /** The point of the line being dragged; `insert` adds it there first. */
  vertex?: { index: number; insert?: Point }
  middle?: boolean
  x: number
  y: number
  tx: number
  ty: number
  moved: boolean
}
let gesture: Gesture | undefined
/** Where the dragged spot is drawn until the parent has saved it. */
const spotDrag = ref<{ id: string; x: number; y: number }>()
/** Lining up reaches a few pixels, whatever the zoom; Alt switches snapping off. */
const SNAP_PIXELS = 8
const snapOf = (e: PointerEvent): SnapOptions => ({ free: e.altKey, tolerance: SNAP_PIXELS / view.k })
function onDown(e: PointerEvent) {
  if (e.button === 2) return
  const target = e.target as Element
  const base = { x: e.clientX, y: e.clientY, tx: view.tx, ty: view.ty, moved: false }
  svg.value!.setPointerCapture(e.pointerId)
  if (e.button === 1) {
    e.preventDefault()
    gesture = { kind: 'pan', middle: true, ...base }
    return
  }
  const id = target.closest<SVGElement>('[data-id]')?.dataset.id
  const spotId = target.closest<SVGElement>('[data-spot]')?.dataset.spot
  const vertex = target.closest<SVGElement>('[data-vertex]')?.dataset.vertex
  const insert = target.closest<SVGElement>('[data-insert]')?.dataset.insert
  if (props.designMode && props.selectedId && (vertex ?? insert) !== undefined) {
    const index = Number(vertex ?? insert)
    gesture = { kind: 'vertex', id: props.selectedId, vertex: insert !== undefined ? { index, insert: toMap(e) } : { index }, ...base }
    return
  }
  const kind = props.measuring
    ? 'pan'
    : props.designMode && id
      ? 'move'
      : spotId && props.spotsEditable && id === props.selectedId
        ? 'spot'
        : 'pan'
  gesture = { kind, id, spotId, ...base }
}
function onMove(e: PointerEvent) {
  if (props.measuring && !gesture) hover.value = tapePoint(e)
  if (!gesture) return
  const dx = e.clientX - gesture.x
  const dy = e.clientY - gesture.y
  if (!gesture.moved && Math.abs(dx) + Math.abs(dy) > 4) {
    gesture.moved = true
    if (gesture.kind === 'move') emit('moveStart', gesture.id!)
    if (gesture.kind === 'vertex') emit('vertexStart', gesture.id!, gesture.vertex!.index, gesture.vertex!.insert)
  }
  if (!gesture.moved) return
  if (gesture.kind === 'pan') {
    view.tx = gesture.tx + dx
    view.ty = gesture.ty + dy
  } else if (gesture.kind === 'spot') {
    const bed = bedById(gesture.id)!
    const p = inBed(bed, toMap(e))
    spotDrag.value = { id: gesture.spotId!, x: bed.shape.x + p.x, y: bed.shape.y + p.y }
  } else if (gesture.kind === 'vertex') emit('vertex', gesture.id!, gesture.vertex!.index, toMap(e), snapOf(e))
  else emit('move', gesture.id!, dx / view.k, dy / view.k, snapOf(e))
}
function onUp(e: PointerEvent) {
  const g = gesture
  gesture = undefined
  if (!g || g.middle) return
  if (g.moved && g.kind === 'move') emit('moveEnd', g.id!)
  else if (g.moved && g.kind === 'vertex') emit('vertexEnd')
  else if (g.moved && g.kind === 'spot' && spotDrag.value) {
    const bed = bedById(g.id)!
    emit('moveSpot', g.spotId!, { x: spotDrag.value.x - bed.shape.x, y: spotDrag.value.y - bed.shape.y })
  } else if (g.moved || g.kind === 'vertex') return
  else if (props.measuring) tape.value = [...tape.value, tapePoint(e)]
  else if (props.pickBedId && g.id === props.pickBedId) emit('pick', inBed(bedById(g.id)!, toMap(e)))
  else emit('select', g.id)
}
/** A double click on a point of the selected line removes it. */
function onDoubleClick(e: MouseEvent) {
  const vertex = (e.target as Element).closest<SVGElement>('[data-vertex]')?.dataset.vertex
  if (props.designMode && props.selectedId && vertex !== undefined) emit('removeVertex', props.selectedId, Number(vertex))
}

// Measuring tape: each click adds a point, lined up with the items like in the designer (Alt: free); Esc clears it.
const tape = ref<Point[]>([])
const hover = ref<Point>()
function tapePoint(e: PointerEvent): Point {
  const p = toMap(e)
  const fit = alignBox({ ...p, w: 0, h: 0 }, snapTargets(props.layout), { ...snapOf(e), grid: false })
  return { x: p.x + fit.dx, y: p.y + fit.dy }
}
const tapeLine = computed(() => (hover.value && tape.value.length ? [...tape.value, hover.value] : tape.value))
const tapeSegments = computed(() =>
  tapeLine.value.slice(1).map((b, i) => ({ a: tapeLine.value[i]!, b, length: lineLength([tapeLine.value[i]!, b]) })),
)
const clearTape = () => {
  tape.value = []
  hover.value = undefined
}
const onKey = (e: KeyboardEvent) => e.key === 'Escape' && props.measuring && clearTape()
onMounted(() => document.addEventListener('keydown', onKey))
onBeforeUnmount(() => document.removeEventListener('keydown', onKey))
watch(() => props.measuring, clearTape)
// Keep the dragged spot where it was dropped until the saved planting comes back.
watch(
  () => props.plantings,
  () => (spotDrag.value = undefined),
)
const onWheel = (e: WheelEvent) => {
  const rect = svg.value!.getBoundingClientRect()
  zoom(Math.pow(1.0015, -e.deltaY), e.clientX - rect.left, e.clientY - rect.top)
}

interface Segment {
  planting: PlantingDto
  phase: PlantingPhase
  crop?: Crop
  x: number
  y: number
  w: number
  h: number
}

/**
 * Crops in a bed on the date: in a strips bed each over its share of the bed along the long side, in a spots bed as
 * a circle per spot.
 */
function bedView(bed: BedDto) {
  if (bed.kind === 'spots') {
    const spacing = (varietyId: string) => props.cropOf(varietyId)?.spacing ?? spacingOf()
    const spots = spotsOf(bed, withDraft(bed), props.date, spacing).map((s): Spot & { crop?: Crop } => {
      const dragged = spotDrag.value?.id === s.planting.id ? spotDrag.value : undefined
      return { ...s, ...(dragged && { x: dragged.x, y: dragged.y }), crop: props.cropOf(s.planting.varietyId) }
    })
    const indoor = props.plantings.some((p) => p.bedId === bed.id && phaseAt(p, props.date) === 'indoor')
    return { bed, segments: [], spots, indoor, ghost: ghostOf(bed) }
  }
  const active = props.plantings
    .filter((p) => p.bedId === bed.id)
    .map((p) => ({ planting: p, phase: phaseAt(p, props.date) }))
    .filter((x): x is { planting: PlantingDto; phase: PlantingPhase } => !!x.phase)
  const inBed = active.filter((x) => IN_BED.has(x.phase))
  const s = bed.shape
  const horizontal = s.width >= s.height
  const length = horizontal ? s.width : s.height
  const total = inBed.reduce((sum, x) => sum + x.planting.share, 0)
  const scale = total > 1 ? 1 / total : 1
  let offset = 0
  const segments: Segment[] = inBed.map((x) => {
    const len = length * x.planting.share * scale
    const seg = horizontal ? { x: s.x + offset, y: s.y, w: len, h: s.height } : { x: s.x, y: s.y + offset, w: s.width, h: len }
    offset += len
    return { ...x, crop: props.cropOf(x.planting.varietyId), ...seg }
  })
  return { bed, segments, spots: [], indoor: active.some((x) => x.phase === 'indoor'), ghost: undefined }
}

/** The plantings, with the one being edited (if any) as it is drafted. */
function withDraft(bed: BedDto): PlantingDto[] {
  const d = props.pickDraft
  if (!d?.plantingId || props.pickBedId !== bed.id || !props.pickPoint) return props.plantings
  const { x, y } = props.pickPoint
  return props.plantings.map((p) =>
    p.id === d.plantingId ? { ...p, count: d.count, spotColumns: d.columns, spotVertical: d.vertical, spotX: x, spotY: y } : p,
  )
}

/** The spot being planned, drawn at the picked point before it exists. */
function ghostOf(bed: BedDto) {
  const d = props.pickDraft
  if (!d || d.plantingId || props.pickBedId !== bed.id || !props.pickPoint) return undefined
  const crop = d.varietyId ? props.cropOf(d.varietyId) : undefined
  const shape = spotShape(d, crop?.spacing ?? spacingOf())
  return { x: bed.shape.x + props.pickPoint.x, y: bed.shape.y + props.pickPoint.y, shape, crop, count: d.count }
}
type StackEntry =
  | { kind: 'marker'; item: MarkerDto }
  | { kind: 'path'; item: PathDto }
  | { kind: 'feature'; item: FeatureDto }
  | { kind: 'bed'; item: BedDto; view: ReturnType<typeof bedView> }
  | { kind: 'tree'; item: TreeDto }
/**
 * Everything above the plots, bottom first: by layer, and on the same layer markers, paths, features, beds and trees.
 * Plots are always below (sorted among each other by layer).
 */
const stack = computed(() => {
  const l = props.layout
  const entries: StackEntry[] = [
    ...l.markers.map((item) => ({ kind: 'marker' as const, item })),
    ...l.paths.map((item) => ({ kind: 'path' as const, item })),
    ...l.features.map((item) => ({ kind: 'feature' as const, item })),
    ...l.beds.map((item) => ({ kind: 'bed' as const, item, view: bedView(item) })),
    ...l.trees.map((item) => ({ kind: 'tree' as const, item })),
  ]
  // Array.sort is stable: items on the same layer keep the order above.
  return entries.sort((a, b) => (a.item.layer ?? 0) - (b.item.layer ?? 0))
})
const plots = computed(() => [...props.layout.plots].sort((a, b) => (a.layer ?? 0) - (b.layer ?? 0)))

/** The selected line (path or wall) in the designer: its points can be dragged. */
const selectedLine = computed(() => {
  if (!props.designMode || !props.selectedId) return undefined
  const line = [...props.layout.paths, ...props.layout.markers].find((x) => x.id === props.selectedId)
  return line?.points
})
/** Handles keep their size on screen. */
const handle = computed(() => 6 / view.k)

const label = (seg: Segment) => {
  const long = Math.max(seg.w, seg.h)
  const text = long < 1.7 ? (seg.crop?.icon ?? '') : `${seg.crop?.icon ?? ''} ${seg.crop?.name ?? ''}`
  const cx = seg.x + seg.w / 2
  const cy = seg.y + seg.h / 2
  return { text, cx, cy, rotate: seg.h > seg.w * 1.3 ? `rotate(-90 ${cx} ${cy})` : undefined }
}

const plotIcon = { open: '🌾', greenhouse: '🏡', orchard: '🍎' } as const
const pathFill = { woodChips: 'url(#chips)', grass: '#a9c58a', pavers: 'url(#pavers)', gravel: '#d6d0c2' } as const
const featureStyle = {
  compost: { fill: '#6b4e31', stroke: '#4a3520', icon: '♻️' },
  water: { fill: '#bcd6e6', stroke: '#6f9ab3', icon: '💧' },
  pond: { fill: '#9cc3d6', stroke: '#7aa7bd', icon: '🐸' },
  hedge: { fill: 'url(#leaf)', stroke: '#4d7339', icon: '🌳' },
  shed: { fill: '#b9a58a', stroke: '#7d6a4d', icon: '🏚️' },
} as const
const scaleBar = computed(() => ({ x: bounds.value.x + bounds.value.w - 2.6, y: bounds.value.y + bounds.value.h - 0.3 }))
</script>

<template>
  <div class="map" :class="{ design: designMode }">
    <svg
      ref="svg"
      :aria-label="t('map.label')"
      role="img"
      :class="{ measuring }"
      @pointerdown="onDown"
      @pointermove="onMove"
      @pointerup="onUp"
      @dblclick="onDoubleClick"
      @mousedown.middle.prevent
      @wheel.prevent="onWheel"
    >
      <defs>
        <pattern id="soil" width=".3" height=".3" patternUnits="userSpaceOnUse">
          <rect width=".3" height=".3" fill="#d8c6a2" />
          <circle cx=".08" cy=".1" r=".028" fill="#b59d70" />
          <circle cx=".22" cy=".23" r=".02" fill="#c3ad84" />
        </pattern>
        <pattern id="hatch" width=".22" height=".22" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
          <line x1="0" y1="0" x2="0" y2=".22" stroke="rgba(80,60,30,.45)" stroke-width=".05" />
        </pattern>
        <pattern id="dots" width=".28" height=".28" patternUnits="userSpaceOnUse">
          <circle cx=".14" cy=".14" r=".045" fill="rgba(255,255,255,.8)" />
        </pattern>
        <pattern id="leaf" width=".5" height=".5" patternUnits="userSpaceOnUse">
          <rect width=".5" height=".5" fill="#7b9a5f" />
          <circle cx=".12" cy=".15" r=".14" fill="#6c8c52" />
          <circle cx=".38" cy=".38" r=".16" fill="#86a768" />
        </pattern>
        <pattern id="chips" width=".4" height=".4" patternUnits="userSpaceOnUse">
          <rect width=".4" height=".4" fill="#cdb48a" />
          <rect x=".05" y=".08" width=".12" height=".04" fill="#b89a6a" transform="rotate(20 .1 .1)" />
        </pattern>
        <pattern id="pavers" width=".5" height=".5" patternUnits="userSpaceOnUse">
          <rect width=".5" height=".5" fill="#cfc9bc" />
          <rect x=".02" y=".02" width=".46" height=".21" fill="#d9d4c8" />
          <rect x=".02" y=".27" width=".21" height=".21" fill="#d9d4c8" />
          <rect x=".27" y=".27" width=".21" height=".21" fill="#d9d4c8" />
        </pattern>
      </defs>
      <g :transform="`translate(${view.tx},${view.ty}) scale(${view.k})`">
        <g v-if="designMode" class="grid">
          <line
            v-for="x in Math.ceil(bounds.w)"
            :key="`x${x}`"
            :x1="Math.floor(bounds.x) + x"
            :x2="Math.floor(bounds.x) + x"
            :y1="bounds.y"
            :y2="bounds.y + bounds.h"
          />
          <line
            v-for="y in Math.ceil(bounds.h)"
            :key="`y${y}`"
            :y1="Math.floor(bounds.y) + y"
            :y2="Math.floor(bounds.y) + y"
            :x1="bounds.x"
            :x2="bounds.x + bounds.w"
          />
        </g>

        <!-- Plots are the bottom layer: everything else lies on or around them. -->
        <g v-for="pl in plots" :key="pl.id" :data-id="pl.id" class="item">
          <ShapeSvg :shape="pl.shape" :rx="0.3" :class="`plot-${pl.type}`" />
          <text class="plot-label" :x="pl.shape.x + 0.1" :y="pl.shape.y - 0.16">{{ plotIcon[pl.type] }} {{ pl.name }}</text>
        </g>

        <template v-for="e in stack" :key="e.item.id">
          <g v-if="e.kind === 'marker'" :data-id="e.item.id" class="item marker">
            <template v-if="e.item.kind === 'line' && e.item.points">
              <path
                :d="polyline(e.item.points)"
                fill="none"
                :stroke="e.item.color"
                :stroke-width="e.item.thickness"
                stroke-linecap="square"
                stroke-linejoin="miter"
              />
              <!-- A wider invisible line, so a thin wall or fence is easy to grab in the designer. -->
              <path
                v-if="designMode"
                :d="polyline(e.item.points)"
                fill="none"
                stroke="transparent"
                :stroke-width="Math.max(e.item.thickness, 0.4)"
              />
              <text v-if="e.item.label" class="marker-label" :transform="alongLine(e.item.points, 0.22)">{{ e.item.label }}</text>
            </template>
            <template v-else-if="e.item.shape">
              <ShapeSvg :shape="e.item.shape" :fill="e.item.color" fill-opacity=".7" :stroke="e.item.color" stroke-width=".04" />
              <text
                v-if="e.item.label"
                class="marker-label"
                :x="e.item.shape.x + e.item.shape.width / 2"
                :y="e.item.shape.y + e.item.shape.height / 2"
              >
                {{ e.item.label }}
              </text>
            </template>
          </g>

          <g v-else-if="e.kind === 'path'" :data-id="e.item.id" class="item">
            <path
              :d="polyline(e.item.points)"
              fill="none"
              :stroke="pathFill[e.item.material]"
              :stroke-width="e.item.width"
              stroke-linejoin="miter"
              opacity=".9"
            />
            <text class="path-label" :transform="alongLine(e.item.points)">{{ e.item.name }}</text>
          </g>

          <g v-else-if="e.kind === 'feature'" :data-id="e.item.id" class="item">
            <ShapeSvg
              :shape="e.item.type === 'pond' ? { ...e.item.shape, kind: 'ellipse' } : e.item.shape"
              :fill="featureStyle[e.item.type].fill"
              :stroke="featureStyle[e.item.type].stroke"
              stroke-width=".05"
            />
            <text class="feature-label" :x="e.item.shape.x + e.item.shape.width / 2" :y="e.item.shape.y + e.item.shape.height + 0.4">
              {{ featureStyle[e.item.type].icon }} {{ e.item.name }}
            </text>
          </g>

          <g
            v-else-if="e.kind === 'bed'"
            :data-id="e.view.bed.id"
            class="item bed"
            :class="{ dimmed: highlightBedIds && !highlightBedIds.has(e.view.bed.id), picking: pickBedId === e.view.bed.id }"
            :aria-label="e.view.bed.code"
          >
            <clipPath :id="`clip-${e.view.bed.id}`"><ShapeSvg :shape="e.view.bed.shape" /></clipPath>
            <ShapeSvg :shape="e.view.bed.shape" fill="url(#soil)" />
            <g :clip-path="`url(#clip-${e.view.bed.id})`">
              <template v-for="seg in e.view.segments" :key="seg.planting.id">
                <rect
                  :x="seg.x"
                  :y="seg.y"
                  :width="seg.w"
                  :height="seg.h"
                  :fill="seg.crop?.color ?? '#888'"
                  :fill-opacity="PHASE_OPACITY[seg.phase]"
                  :stroke="future ? '#333' : undefined"
                  :stroke-width="future ? 0.05 : undefined"
                  :stroke-dasharray="future ? '.18 .12' : undefined"
                />
                <rect v-if="seg.phase === 'harvest'" :x="seg.x" :y="seg.y" :width="seg.w" :height="seg.h" fill="url(#dots)" />
                <rect v-if="seg.phase === 'clearing'" :x="seg.x" :y="seg.y" :width="seg.w" :height="seg.h" fill="url(#hatch)" />
                <text class="crop-label" :x="label(seg).cx" :y="label(seg).cy" :transform="label(seg).rotate">{{ label(seg).text }}</text>
              </template>
              <g
                v-for="spot in e.view.spots"
                :key="spot.planting.id"
                :data-spot="spot.planting.id"
                class="spot"
                :class="{ draggable: spotsEditable && selectedId === e.view.bed.id }"
              >
                <SpotSvg
                  :x="spot.x"
                  :y="spot.y"
                  :shape="spot.shape"
                  :color="spot.crop?.color ?? '#888'"
                  :icon="spot.crop?.icon"
                  :count="spot.planting.count ?? 1"
                  :phase="spot.phase"
                  :dashed="future"
                />
              </g>
              <g v-if="e.view.ghost" class="ghost">
                <SpotSvg
                  :x="e.view.ghost.x"
                  :y="e.view.ghost.y"
                  :shape="e.view.ghost.shape"
                  :color="e.view.ghost.crop?.color ?? '#888'"
                  :icon="e.view.ghost.crop?.icon"
                  :count="e.view.ghost.count"
                  dashed
                />
              </g>
              <circle
                v-if="pickBedId === e.view.bed.id && pickPoint"
                class="pick"
                :cx="e.view.bed.shape.x + pickPoint.x"
                :cy="e.view.bed.shape.y + pickPoint.y"
                r=".12"
              />
            </g>
            <circle
              v-if="e.view.bed.shape.kind === 'keyhole'"
              :cx="e.view.bed.shape.x + e.view.bed.shape.width / 2"
              :cy="e.view.bed.shape.y + e.view.bed.shape.height / 2"
              r=".3"
              fill="#6b4e31"
            />
            <ShapeSvg :shape="e.view.bed.shape" class="outline" :class="{ selected: selectedId === e.view.bed.id }" />
            <text
              class="bed-code"
              :x="e.view.bed.shape.x + (e.view.bed.shape.kind === 'rect' ? 0.07 : e.view.bed.shape.width / 2)"
              :y="e.view.bed.shape.y + 0.27"
              :text-anchor="e.view.bed.shape.kind === 'rect' ? 'start' : 'middle'"
            >
              {{ e.view.bed.code }}
            </text>
            <text
              v-if="e.view.indoor"
              :x="e.view.bed.shape.x + e.view.bed.shape.width - 0.08"
              :y="e.view.bed.shape.y + e.view.bed.shape.height - 0.1"
              text-anchor="end"
              font-size=".34"
            >
              🏠
            </text>
            <text
              v-if="alertIds?.has(e.view.bed.id)"
              :x="e.view.bed.shape.x + 0.06"
              :y="e.view.bed.shape.y + e.view.bed.shape.height - 0.1"
              font-size=".34"
            >
              🦠
            </text>
            <g v-if="!designMode && warningBedIds?.has(e.view.bed.id)">
              <circle :cx="e.view.bed.shape.x + e.view.bed.shape.width - 0.2" :cy="e.view.bed.shape.y + 0.2" r=".15" fill="#b9502c" />
              <text
                :x="e.view.bed.shape.x + e.view.bed.shape.width - 0.2"
                :y="e.view.bed.shape.y + 0.27"
                text-anchor="middle"
                font-size=".22"
                font-weight="800"
                fill="#fff"
              >
                !
              </text>
            </g>
          </g>

          <g v-else-if="e.kind === 'tree'" :data-id="e.item.id" class="item tree">
            <ShapeSvg
              :shape="crownShape(e.item)"
              :rx="0.15"
              :fill="e.item.kind === 'shrub' ? '#7f9d59' : '#5f8f4a'"
              fill-opacity=".42"
              :stroke="selectedId === e.item.id ? '#2a2a24' : '#4d7339'"
              :stroke-width="selectedId === e.item.id ? 0.12 : 0.05"
              :stroke-dasharray="e.item.kind === 'shrub' ? '.2 .12' : undefined"
            />
            <circle :cx="e.item.x" :cy="e.item.y" r=".14" fill="#6b4a2b" />
            <text class="feature-label" :x="e.item.x" :y="e.item.y + e.item.crownDepth / 2 + 0.38">
              {{ e.item.kind === 'shrub' ? '🌿' : '🌳' }} {{ e.item.name }}
            </text>
            <text
              v-if="alertIds?.has(e.item.id)"
              :x="e.item.x + e.item.crownWidth * 0.35"
              :y="e.item.y - e.item.crownDepth * 0.3"
              font-size=".4"
            >
              🦠
            </text>
            <!-- needs a pollinator but has none within range -->
            <g v-if="!designMode && e.item.pollinators?.length === 0">
              <circle :cx="e.item.x + e.item.crownWidth * 0.35" :cy="e.item.y + e.item.crownDepth * 0.3" r=".15" fill="#b9502c" />
              <text
                :x="e.item.x + e.item.crownWidth * 0.35"
                :y="e.item.y + e.item.crownDepth * 0.3 + 0.07"
                text-anchor="middle"
                font-size=".22"
                font-weight="800"
                fill="#fff"
              >
                !
              </text>
            </g>
          </g>
        </template>

        <!-- Points of the selected line: drag to move, drag a midpoint to add one, double-click to remove. -->
        <g v-if="selectedLine" class="handles">
          <circle
            v-for="(p, i) in selectedLine.slice(1)"
            :key="`m${i}`"
            :data-insert="i + 1"
            class="insert"
            :cx="(selectedLine[i]!.x + p.x) / 2"
            :cy="(selectedLine[i]!.y + p.y) / 2"
            :r="handle * 0.75"
            :stroke-width="handle / 4"
          />
          <circle
            v-for="(p, i) in selectedLine"
            :key="`v${i}`"
            :data-vertex="i"
            class="vertex"
            :cx="p.x"
            :cy="p.y"
            :r="handle"
            :stroke-width="handle / 3"
          />
        </g>

        <g v-if="guides?.length" class="guides">
          <line
            v-for="(g, i) in guides"
            :key="i"
            :x1="g.x1"
            :y1="g.y1"
            :x2="g.x2"
            :y2="g.y2"
            :stroke-width="1.5 / view.k"
            :stroke-dasharray="`${6 / view.k} ${4 / view.k}`"
          />
        </g>

        <g v-if="measuring && tapeLine.length" class="tape">
          <path :d="polyline(tapeLine)" :stroke-width="2.5 / view.k" />
          <circle v-for="(p, i) in tape" :key="i" :cx="p.x" :cy="p.y" :r="handle * 0.7" />
          <template v-if="tapeSegments.length > 1">
            <text v-for="(seg, i) in tapeSegments" :key="`s${i}`" class="tape-part" :transform="alongLine([seg.a, seg.b], 0.12)">
              {{ decimal(seg.length) }} m
            </text>
          </template>
          <text v-if="tapeSegments.length" class="tape-total" :x="tapeLine.at(-1)!.x + 0.15" :y="tapeLine.at(-1)!.y - 0.15">
            📏 {{ decimal(lineLength(tapeLine)) }} m
          </text>
        </g>

        <slot name="overlay" :view="view" />

        <g class="scale">
          <line :x1="scaleBar.x" :y1="scaleBar.y" :x2="scaleBar.x + 2" :y2="scaleBar.y" />
          <line :x1="scaleBar.x" :y1="scaleBar.y - 0.15" :x2="scaleBar.x" :y2="scaleBar.y + 0.15" />
          <line :x1="scaleBar.x + 2" :y1="scaleBar.y - 0.15" :x2="scaleBar.x + 2" :y2="scaleBar.y + 0.15" />
          <text class="feature-label" :x="scaleBar.x + 1" :y="scaleBar.y - 0.15">2 m</text>
        </g>
      </g>
    </svg>
  </div>
</template>

<style scoped>
.map {
  position: relative;
  height: 100%;
  overflow: hidden;
  background: #e9ecdc radial-gradient(rgba(80, 90, 50, 0.12) 1px, transparent 1px) 0 0 / 22px 22px;
}
svg {
  width: 100%;
  height: 100%;
  display: block;
  cursor: grab;
  touch-action: none;
}
.design .item {
  cursor: move;
}
svg.measuring,
svg.measuring .item {
  cursor: crosshair;
}
.handles .vertex {
  fill: #fff;
  stroke: #3f7d4e;
  cursor: grab;
}
.handles .insert {
  fill: rgba(63, 125, 78, 0.35);
  stroke: #fff;
  cursor: copy;
}
.guides line {
  stroke: #d2338b;
  pointer-events: none;
}
.tape {
  pointer-events: none;
}
.tape path {
  fill: none;
  stroke: #d2338b;
}
.tape circle {
  fill: #d2338b;
}
.tape-total,
.tape-part {
  font-size: 0.3px;
  font-weight: 800;
  fill: #8a1f5a;
  paint-order: stroke;
  stroke: #fff;
  stroke-width: 0.08px;
}
.tape-part {
  font-size: 0.22px;
  text-anchor: middle;
}
.bed,
.tree {
  cursor: pointer;
}
.grid line {
  stroke: rgba(63, 125, 78, 0.18);
  stroke-width: 0.02;
}
.plot-open {
  fill: #efe5cf;
  stroke: #c9b58c;
  stroke-width: 0.05;
}
.plot-greenhouse {
  fill: #e3eef1;
  stroke: #7fa6b5;
  stroke-width: 0.07;
}
.plot-orchard {
  fill: #e7eed6;
  stroke: #a5b67f;
  stroke-width: 0.05;
}
.plot-label {
  font-size: 0.42px;
  font-weight: 700;
  fill: #5d5642;
  pointer-events: none;
}
.outline {
  fill: none;
  stroke: #8c7650;
  stroke-width: 0.04;
}
.bed:hover .outline {
  stroke: var(--c-ink);
  stroke-width: 0.08;
}
.outline.selected {
  stroke: var(--c-ink);
  stroke-width: 0.12;
}
.marker-label {
  font-size: 0.26px;
  font-weight: 650;
  text-anchor: middle;
  dominant-baseline: middle;
  fill: #4b5340;
  paint-order: stroke;
  stroke: rgba(255, 255, 255, 0.7);
  stroke-width: 0.05px;
  pointer-events: none;
}
.bed.dimmed {
  opacity: 0.3;
}
.bed.picking {
  cursor: crosshair;
}
.spot.draggable {
  cursor: move;
}
.ghost {
  pointer-events: none;
}
.pick {
  fill: none;
  stroke: var(--c-ink);
  stroke-width: 0.04;
  stroke-dasharray: 0.06 0.04;
  pointer-events: none;
}
.bed-code {
  font-size: 0.24px;
  font-weight: 800;
  fill: #5d4a2c;
  pointer-events: none;
}
.crop-label {
  font-size: 0.27px;
  text-anchor: middle;
  dominant-baseline: middle;
  font-weight: 650;
  fill: #2a2a24;
  paint-order: stroke;
  stroke: rgba(255, 255, 255, 0.75);
  stroke-width: 0.06px;
  pointer-events: none;
}
.feature-label {
  font-size: 0.3px;
  fill: #4b5340;
  font-weight: 600;
  text-anchor: middle;
  pointer-events: none;
}
.path-label {
  font-size: 0.24px;
  fill: #6b5a3a;
  font-weight: 600;
  opacity: 0.8;
  text-anchor: middle;
  dominant-baseline: middle;
  pointer-events: none;
}
.scale line {
  stroke: #444;
  stroke-width: 0.05;
}
</style>
