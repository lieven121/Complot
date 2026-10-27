<script setup lang="ts">
import { computed } from 'vue'
import type { PlantingPhase } from '@/shared/api'
import { PHASE_OPACITY, type SpotShape } from '../map'

/**
 * One spot in a spots bed: a round cluster or a block around (x, y), with a dot per plant once there are several.
 * `dashed` draws a planned spot or the live preview of a spot being planned.
 */
const props = defineProps<{
  x: number
  y: number
  shape: SpotShape
  color: string
  icon?: string
  count: number
  phase?: PlantingPhase
  dashed?: boolean
}>()

const opacity = computed(() => (props.phase ? Math.max(0.45, PHASE_OPACITY[props.phase]) : 0.35))
const box = computed(() => ({
  x: props.x - props.shape.w / 2,
  y: props.y - props.shape.h / 2,
  width: props.shape.w,
  height: props.shape.h,
}))
const iconSize = computed(() => Math.min(0.3, props.shape.r * 1.3))
const dotRadius = computed(() => {
  const s = props.shape
  return Math.min(0.06, (s.round ? s.r / Math.sqrt(props.count) : Math.min(s.w, s.h)) * 0.18)
})
</script>

<template>
  <circle
    v-if="shape.round"
    :cx="x"
    :cy="y"
    :r="shape.r"
    :fill="color"
    :fill-opacity="opacity"
    stroke="#2a2a24"
    :stroke-width="dashed ? 0.03 : 0.015"
    :stroke-dasharray="dashed ? '.08 .05' : undefined"
  />
  <rect
    v-else
    v-bind="box"
    rx=".04"
    stroke="#2a2a24"
    :fill="color"
    :fill-opacity="opacity"
    :stroke-width="dashed ? 0.03 : 0.015"
    :stroke-dasharray="dashed ? '.08 .05' : undefined"
  />
  <template v-if="phase === 'harvest' || phase === 'clearing'">
    <circle v-if="shape.round" :cx="x" :cy="y" :r="shape.r" :fill="phase === 'harvest' ? 'url(#dots)' : 'url(#hatch)'" />
    <rect v-else v-bind="box" :fill="phase === 'harvest' ? 'url(#dots)' : 'url(#hatch)'" />
  </template>
  <template v-if="count > 1">
    <circle v-for="(p, i) in shape.plants" :key="i" :cx="x + p.x" :cy="y + p.y" :r="dotRadius" class="plant" />
  </template>
  <text class="spot-icon" :x="x" :y="y" :font-size="iconSize">{{ icon }}</text>
  <text v-if="count > 1" class="spot-count" :x="x + shape.w * 0.35" :y="y + shape.h * 0.42">{{ count }}</text>
</template>

<style scoped>
.plant {
  fill: #2a2a24;
  fill-opacity: 0.35;
  pointer-events: none;
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
</style>
