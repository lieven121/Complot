<script setup lang="ts">
import { computed, onMounted, onBeforeUnmount, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { BedDto, PlantingDto, PlantingPhase, ShapeDto, SiteLayoutDto } from '@/shared/api'
import { IN_BED, layoutBounds, phaseAt, PHASE_OPACITY, type Spot, spotsOf } from '../map'
import type { Point } from '../spotPicker'
import ShapeSvg from './ShapeSvg.vue'

export interface Crop {
  icon: string
  color: string
  name: string
}

/**
 * The site map in meters (x → east, y → south). Shows what is in each bed on `date`; in design mode items can be
 * dragged (the parent applies the move, snapping and undo history).
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
  /** Spots in the selected bed can be dragged to a new place (`moveSpot`). */
  spotsEditable?: boolean
  /** When set, other beds are dimmed (crop finder). */
  highlightBedIds?: ReadonlySet<string>
}>()
const emit = defineEmits<{
  select: [id: string | undefined]
  moveStart: [id: string]
  move: [id: string, dx: number, dy: number]
  moveEnd: [id: string]
  pick: [point: Point]
  moveSpot: [plantingId: string, point: Point]
}>()

const { t } = useI18n()
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

// Pointer: drag on empty space pans; in design mode drag on an item moves it; a click without movement selects.
// Outside design mode a spot of the selected bed can be dragged. The item is taken from pointerdown: after
// setPointerCapture, pointerup targets the <svg> itself.
type Gesture = { kind: 'pan' | 'move' | 'spot'; id?: string; spotId?: string; x: number; y: number; tx: number; ty: number; moved: boolean }
let gesture: Gesture | undefined
/** Where the dragged spot is drawn until the parent has saved it. */
const spotDrag = ref<{ id: string; x: number; y: number }>()
function onDown(e: PointerEvent) {
  const target = e.target as Element
  const id = target.closest<SVGElement>('[data-id]')?.dataset.id
  const spotId = target.closest<SVGElement>('[data-spot]')?.dataset.spot
  const kind = props.designMode && id ? 'move' : spotId && props.spotsEditable && id === props.selectedId ? 'spot' : 'pan'
  gesture = { kind, id, spotId, x: e.clientX, y: e.clientY, tx: view.tx, ty: view.ty, moved: false }
  svg.value!.setPointerCapture(e.pointerId)
}
function onMove(e: PointerEvent) {
  if (!gesture) return
  const dx = e.clientX - gesture.x
  const dy = e.clientY - gesture.y
  if (!gesture.moved && Math.abs(dx) + Math.abs(dy) > 4) {
    gesture.moved = true
    if (gesture.kind === 'move') emit('moveStart', gesture.id!)
  }
  if (!gesture.moved) return
  if (gesture.kind === 'pan') {
    view.tx = gesture.tx + dx
    view.ty = gesture.ty + dy
  } else if (gesture.kind === 'spot') {
    const bed = bedById(gesture.id)!
    const p = inBed(bed, toMap(e))
    spotDrag.value = { id: gesture.spotId!, x: bed.shape.x + p.x, y: bed.shape.y + p.y }
  } else emit('move', gesture.id!, dx / view.k, dy / view.k)
}
function onUp(e: PointerEvent) {
  const g = gesture
  gesture = undefined
  if (!g) return
  if (g.moved && g.kind === 'move') emit('moveEnd', g.id!)
  else if (g.moved && g.kind === 'spot' && spotDrag.value) {
    const bed = bedById(g.id)!
    emit('moveSpot', g.spotId!, { x: spotDrag.value.x - bed.shape.x, y: spotDrag.value.y - bed.shape.y })
  } else if (!g.moved && props.pickBedId && g.id === props.pickBedId) emit('pick', inBed(bedById(g.id)!, toMap(e)))
  else if (!g.moved) emit('select', g.id)
}
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
    const spots = spotsOf(bed, props.plantings, props.date).map((s): Spot & { crop?: Crop } => {
      const dragged = spotDrag.value?.id === s.planting.id ? spotDrag.value : undefined
      return { ...s, ...(dragged && { x: dragged.x, y: dragged.y }), crop: props.cropOf(s.planting.varietyId) }
    })
    const indoor = props.plantings.some((p) => p.bedId === bed.id && phaseAt(p, props.date) === 'indoor')
    return { bed, segments: [], spots, indoor }
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
  return { bed, segments, spots: [], indoor: active.some((x) => x.phase === 'indoor') }
}
const beds = computed(() => props.layout.beds.map(bedView))

const label = (seg: Segment) => {
  const long = Math.max(seg.w, seg.h)
  const text = long < 1.7 ? (seg.crop?.icon ?? '') : `${seg.crop?.icon ?? ''} ${seg.crop?.name ?? ''}`
  const cx = seg.x + seg.w / 2
  const cy = seg.y + seg.h / 2
  return { text, cx, cy, rotate: seg.h > seg.w * 1.3 ? `rotate(-90 ${cx} ${cy})` : undefined }
}

/** A line's label sits in the middle, along the line but never upside down. */
const lineLabel = (s: ShapeDto) => {
  let angle = (Math.atan2(s.height, s.width) * 180) / Math.PI
  if (angle > 90) angle -= 180
  if (angle < -90) angle += 180
  return `translate(${s.x + s.width / 2} ${s.y + s.height / 2}) rotate(${angle}) translate(0 -0.22)`
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
      @pointerdown="onDown"
      @pointermove="onMove"
      @pointerup="onUp"
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

        <g v-for="m in layout.markers" :key="m.id" :data-id="m.id" class="item marker">
          <template v-if="m.kind === 'line'">
            <line
              :x1="m.shape.x"
              :y1="m.shape.y"
              :x2="m.shape.x + m.shape.width"
              :y2="m.shape.y + m.shape.height"
              :stroke="m.color"
              :stroke-width="m.thickness"
              stroke-linecap="square"
            />
            <!-- A wider invisible line, so a thin wall is easy to grab in the designer. -->
            <line
              v-if="designMode"
              :x1="m.shape.x"
              :y1="m.shape.y"
              :x2="m.shape.x + m.shape.width"
              :y2="m.shape.y + m.shape.height"
              stroke="transparent"
              :stroke-width="Math.max(m.thickness, 0.4)"
            />
            <text v-if="m.label" class="marker-label" :transform="lineLabel(m.shape)">{{ m.label }}</text>
          </template>
          <template v-else>
            <ShapeSvg :shape="m.shape" :fill="m.color" fill-opacity=".7" :stroke="m.color" stroke-width=".04" />
            <text v-if="m.label" class="marker-label" :x="m.shape.x + m.shape.width / 2" :y="m.shape.y + m.shape.height / 2">
              {{ m.label }}
            </text>
          </template>
        </g>

        <g v-for="p in layout.paths" :key="p.id" :data-id="p.id" class="item">
          <rect :x="p.shape.x" :y="p.shape.y" :width="p.shape.width" :height="p.shape.height" :fill="pathFill[p.material]" opacity=".9" />
          <text
            class="path-label"
            :transform="`translate(${p.shape.x + p.shape.width / 2} ${p.shape.y + p.shape.height / 2})${p.shape.height > p.shape.width ? ' rotate(-90)' : ''}`"
          >
            {{ p.name }}
          </text>
        </g>

        <g v-for="f in layout.features" :key="f.id" :data-id="f.id" class="item">
          <ShapeSvg
            :shape="f.type === 'pond' ? { ...f.shape, kind: 'ellipse' } : f.shape"
            :fill="featureStyle[f.type].fill"
            :stroke="featureStyle[f.type].stroke"
            stroke-width=".05"
          />
          <text class="feature-label" :x="f.shape.x + f.shape.width / 2" :y="f.shape.y + f.shape.height + 0.4">
            {{ featureStyle[f.type].icon }} {{ f.name }}
          </text>
        </g>

        <g v-for="pl in layout.plots" :key="pl.id" :data-id="pl.id" class="item">
          <ShapeSvg :shape="pl.shape" :rx="0.3" :class="`plot-${pl.type}`" />
          <text class="plot-label" :x="pl.shape.x + 0.1" :y="pl.shape.y - 0.16">{{ plotIcon[pl.type] }} {{ pl.name }}</text>
        </g>

        <g
          v-for="v in beds"
          :key="v.bed.id"
          :data-id="v.bed.id"
          class="item bed"
          :class="{ dimmed: highlightBedIds && !highlightBedIds.has(v.bed.id), picking: pickBedId === v.bed.id }"
          :aria-label="v.bed.code"
        >
          <clipPath :id="`clip-${v.bed.id}`"><ShapeSvg :shape="v.bed.shape" /></clipPath>
          <ShapeSvg :shape="v.bed.shape" fill="url(#soil)" />
          <g :clip-path="`url(#clip-${v.bed.id})`">
            <template v-for="seg in v.segments" :key="seg.planting.id">
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
              v-for="spot in v.spots"
              :key="spot.planting.id"
              :data-spot="spot.planting.id"
              class="spot"
              :class="{ draggable: spotsEditable && selectedId === v.bed.id }"
            >
              <circle
                :cx="spot.x"
                :cy="spot.y"
                :r="spot.r"
                :fill="spot.crop?.color ?? '#888'"
                :fill-opacity="Math.max(0.45, PHASE_OPACITY[spot.phase])"
                stroke="#2a2a24"
                :stroke-width="future ? 0.03 : 0.015"
                :stroke-dasharray="future ? '.08 .05' : undefined"
              />
              <circle v-if="spot.phase === 'harvest'" :cx="spot.x" :cy="spot.y" :r="spot.r" fill="url(#dots)" />
              <circle v-if="spot.phase === 'clearing'" :cx="spot.x" :cy="spot.y" :r="spot.r" fill="url(#hatch)" />
              <text class="spot-icon" :x="spot.x" :y="spot.y" :font-size="Math.min(0.3, spot.r * 1.3)">{{ spot.crop?.icon }}</text>
              <text v-if="(spot.planting.count ?? 1) > 1" class="spot-count" :x="spot.x + spot.r * 0.7" :y="spot.y + spot.r * 0.85">
                {{ spot.planting.count }}
              </text>
            </g>
            <circle
              v-if="pickBedId === v.bed.id && pickPoint"
              class="pick"
              :cx="v.bed.shape.x + pickPoint.x"
              :cy="v.bed.shape.y + pickPoint.y"
              r=".12"
            />
          </g>
          <circle
            v-if="v.bed.shape.kind === 'keyhole'"
            :cx="v.bed.shape.x + v.bed.shape.width / 2"
            :cy="v.bed.shape.y + v.bed.shape.height / 2"
            r=".3"
            fill="#6b4e31"
          />
          <ShapeSvg :shape="v.bed.shape" class="outline" :class="{ selected: selectedId === v.bed.id }" />
          <text
            class="bed-code"
            :x="v.bed.shape.x + (v.bed.shape.kind === 'rect' ? 0.07 : v.bed.shape.width / 2)"
            :y="v.bed.shape.y + 0.27"
            :text-anchor="v.bed.shape.kind === 'rect' ? 'start' : 'middle'"
          >
            {{ v.bed.code }}
          </text>
          <text
            v-if="v.indoor"
            :x="v.bed.shape.x + v.bed.shape.width - 0.08"
            :y="v.bed.shape.y + v.bed.shape.height - 0.1"
            text-anchor="end"
            font-size=".34"
          >
            🏠
          </text>
          <text v-if="alertIds?.has(v.bed.id)" :x="v.bed.shape.x + 0.06" :y="v.bed.shape.y + v.bed.shape.height - 0.1" font-size=".34">
            🦠
          </text>
          <g v-if="!designMode && warningBedIds?.has(v.bed.id)">
            <circle :cx="v.bed.shape.x + v.bed.shape.width - 0.2" :cy="v.bed.shape.y + 0.2" r=".15" fill="#b9502c" />
            <text
              :x="v.bed.shape.x + v.bed.shape.width - 0.2"
              :y="v.bed.shape.y + 0.27"
              text-anchor="middle"
              font-size=".22"
              font-weight="800"
              fill="#fff"
            >
              !
            </text>
          </g>
        </g>

        <g v-for="tree in layout.trees" :key="tree.id" :data-id="tree.id" class="item tree">
          <circle
            :cx="tree.x"
            :cy="tree.y"
            :r="tree.crownDiameter / 2"
            :fill="tree.kind === 'shrub' ? '#7f9d59' : '#5f8f4a'"
            fill-opacity=".42"
            :stroke="selectedId === tree.id ? '#2a2a24' : '#4d7339'"
            :stroke-width="selectedId === tree.id ? 0.12 : 0.05"
            :stroke-dasharray="tree.kind === 'shrub' ? '.2 .12' : undefined"
          />
          <circle :cx="tree.x" :cy="tree.y" r=".14" fill="#6b4a2b" />
          <text class="feature-label" :x="tree.x" :y="tree.y + tree.crownDiameter / 2 + 0.38">
            {{ tree.kind === 'shrub' ? '🌿' : '🌳' }} {{ tree.name }}
          </text>
          <text v-if="alertIds?.has(tree.id)" :x="tree.x + tree.crownDiameter * 0.35" :y="tree.y - tree.crownDiameter * 0.3" font-size=".4">
            🦠
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
.spot-icon {
  text-anchor: middle;
  dominant-baseline: central;
  pointer-events: none;
}
.spot-count {
  font-size: 0.16px;
  font-weight: 800;
  text-anchor: middle;
  fill: #2a2a24;
  paint-order: stroke;
  stroke: #fff;
  stroke-width: 0.05px;
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
