<script setup lang="ts">
import { computed } from 'vue'
import type { TreeDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import { alongLine, crownShape, type Geometry, lineLength, polyline } from '../map'
import ShapeSvg from './ShapeSvg.vue'

/** Dimension lines and coordinates of the item selected in the designer (map units are meters). */
const props = defineProps<{ item: Geometry }>()
const { decimal } = useFormat()
const GAP = 0.35
const TICK = 0.12
const isTree = computed(() => !props.item.shape && !props.item.points && props.item.crownWidth !== undefined)
const shape = computed(() => props.item.shape ?? (isTree.value ? crownShape(props.item as TreeDto) : undefined))
/** Where the coordinates point: a tree's trunk, else the top-left corner. */
const origin = computed(() => (isTree.value ? { x: props.item.x!, y: props.item.y! } : { x: shape.value?.x ?? 0, y: shape.value?.y ?? 0 }))
/** A line (path, wall, fence): the length of each part and in total, and its ends. */
const line = computed(() => {
  const points = props.item.points
  if (!points?.length) return undefined
  const parts = points.slice(1).map((b, i) => ({ points: [points[i]!, b], length: lineLength([points[i]!, b]) }))
  return { points, parts, length: lineLength(points), first: points[0]!, last: points.at(-1)! }
})
</script>

<template>
  <g class="dims">
    <template v-if="line">
      <path :d="polyline(line.points)" class="outline" />
      <template v-if="line.parts.length > 1">
        <text v-for="(part, i) in line.parts" :key="i" class="part" :transform="alongLine(part.points, 0.3)"
          >{{ decimal(part.length) }} m</text
        >
      </template>
      <text class="total" :x="line.last.x + 0.2" :y="line.last.y - 0.2">Σ {{ decimal(line.length) }} m</text>
      <text class="coords" :x="line.first.x" :y="line.first.y + 0.4">({{ decimal(line.first.x) }}; {{ decimal(line.first.y) }})</text>
      <text class="coords" :x="line.last.x" :y="line.last.y + 0.4">({{ decimal(line.last.x) }}; {{ decimal(line.last.y) }})</text>
    </template>
    <template v-else-if="shape">
      <ShapeSvg :shape="shape" class="outline" />
      <line :x1="shape.x" :y1="shape.y - GAP" :x2="shape.x + shape.width" :y2="shape.y - GAP" />
      <line :x1="shape.x" :y1="shape.y - GAP - TICK" :x2="shape.x" :y2="shape.y - GAP + TICK" />
      <line :x1="shape.x + shape.width" :y1="shape.y - GAP - TICK" :x2="shape.x + shape.width" :y2="shape.y - GAP + TICK" />
      <text :x="shape.x + shape.width / 2" :y="shape.y - GAP - 0.1">{{ decimal(shape.width) }} m</text>
      <line :x1="shape.x - GAP" :y1="shape.y" :x2="shape.x - GAP" :y2="shape.y + shape.height" />
      <line :x1="shape.x - GAP - TICK" :y1="shape.y" :x2="shape.x - GAP + TICK" :y2="shape.y" />
      <line :x1="shape.x - GAP - TICK" :y1="shape.y + shape.height" :x2="shape.x - GAP + TICK" :y2="shape.y + shape.height" />
      <text :transform="`translate(${shape.x - GAP - 0.1} ${shape.y + shape.height / 2}) rotate(-90)`">{{ decimal(shape.height) }} m</text>
      <text class="coords" :x="shape.x" :y="shape.y + shape.height + 0.35">({{ decimal(origin.x) }}; {{ decimal(origin.y) }})</text>
    </template>
  </g>
</template>

<style scoped>
.dims {
  pointer-events: none;
}
.outline {
  fill: none;
  stroke: #3f7d4e;
  stroke-width: 0.08;
  stroke-dasharray: 0.2 0.1;
}
line {
  stroke: #3f7d4e;
  stroke-width: 0.04;
}
text {
  font-size: 0.26px;
  fill: #3f7d4e;
  font-weight: 800;
  text-anchor: middle;
  paint-order: stroke;
  stroke: #fff;
  stroke-width: 0.07px;
}
.part {
  font-size: 0.22px;
}
.total,
.coords {
  font-size: 0.22px;
  text-anchor: start;
}
</style>
