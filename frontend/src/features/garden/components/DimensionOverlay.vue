<script setup lang="ts">
import { computed } from 'vue'
import type { ShapeDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import ShapeSvg from './ShapeSvg.vue'

/** Dimension lines and coordinates of the item selected in the designer (map units are meters). */
const props = defineProps<{ item: { shape?: ShapeDto; x?: number; y?: number; crownDiameter?: number } }>()
const { decimal } = useFormat()
const GAP = 0.35
const TICK = 0.12
const shape = computed(() => props.item.shape)
</script>

<template>
  <g class="dims">
    <template v-if="shape">
      <ShapeSvg :shape="shape" class="outline" />
      <line :x1="shape.x" :y1="shape.y - GAP" :x2="shape.x + shape.width" :y2="shape.y - GAP" />
      <line :x1="shape.x" :y1="shape.y - GAP - TICK" :x2="shape.x" :y2="shape.y - GAP + TICK" />
      <line :x1="shape.x + shape.width" :y1="shape.y - GAP - TICK" :x2="shape.x + shape.width" :y2="shape.y - GAP + TICK" />
      <text :x="shape.x + shape.width / 2" :y="shape.y - GAP - 0.1">{{ decimal(shape.width) }} m</text>
      <line :x1="shape.x - GAP" :y1="shape.y" :x2="shape.x - GAP" :y2="shape.y + shape.height" />
      <line :x1="shape.x - GAP - TICK" :y1="shape.y" :x2="shape.x - GAP + TICK" :y2="shape.y" />
      <line :x1="shape.x - GAP - TICK" :y1="shape.y + shape.height" :x2="shape.x - GAP + TICK" :y2="shape.y + shape.height" />
      <text :transform="`translate(${shape.x - GAP - 0.1} ${shape.y + shape.height / 2}) rotate(-90)`">{{ decimal(shape.height) }} m</text>
      <text class="coords" :x="shape.x" :y="shape.y + shape.height + 0.35">({{ decimal(shape.x) }}; {{ decimal(shape.y) }})</text>
    </template>
    <template v-else-if="item.crownDiameter">
      <circle :cx="item.x" :cy="item.y" :r="item.crownDiameter / 2" class="outline" />
      <line :x1="item.x! - item.crownDiameter / 2" :y1="item.y" :x2="item.x! + item.crownDiameter / 2" :y2="item.y" />
      <text :x="item.x" :y="item.y! - 0.12">⌀ {{ decimal(item.crownDiameter) }} m</text>
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
.coords {
  font-size: 0.22px;
  text-anchor: start;
}
</style>
