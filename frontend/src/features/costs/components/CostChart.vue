<script setup lang="ts">
import { computed } from 'vue'
import type { CostCategoryDto, CostPeriodDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'

/** Stacked bars: one per period, a segment per cost category. Hover a bar for its split. */
const props = defineProps<{ periods: CostPeriodDto[]; categories: CostCategoryDto[]; labelOf: (p: CostPeriodDto) => string }>()

const { currency } = useFormat()
const W = 760
const H = 250
const LEFT = 56
const BOTTOM = 26
const TOP = 10
const plotWidth = W - LEFT - 10
const plotHeight = H - BOTTOM - TOP

/** A round axis maximum with at most five gridlines. */
const scale = computed(() => {
  const max = Math.max(10, ...props.periods.map((p) => p.total))
  const step = [10, 25, 50, 100, 200, 250, 500, 1000, 2500, 5000].find((s) => max / s <= 5) ?? 10_000
  return { step, top: Math.ceil(max / step) * step }
})
const y = (value: number) => TOP + plotHeight - (value / scale.value.top) * plotHeight
const ticks = computed(() => Array.from({ length: scale.value.top / scale.value.step + 1 }, (_, i) => i * scale.value.step))
const band = computed(() => plotWidth / Math.max(1, props.periods.length))

const bars = computed(() =>
  props.periods.map((p, i) => {
    let acc = 0
    const segments = props.categories
      .filter((c) => (p.byCategory[c.id] ?? 0) > 0)
      .map((c) => {
        const value = p.byCategory[c.id]!
        const segment = { color: c.color, y: y(acc + value), height: Math.max(0.5, y(acc) - y(acc + value) - 1) }
        acc += value
        return segment
      })
    const tooltip = [
      `${props.labelOf(p)} · ${currency(p.total)}`,
      ...props.categories.filter((c) => p.byCategory[c.id]).map((c) => `${c.name}: ${currency(p.byCategory[c.id]!)}`),
    ].join('\n')
    return { x: LEFT + i * band.value, segments, tooltip, label: props.labelOf(p) }
  }),
)
const dense = computed(() => props.periods.length > 14)
</script>

<template>
  <svg :viewBox="`0 0 ${W} ${H}`" class="chart" role="img">
    <g v-for="tick in ticks" :key="tick">
      <line :x1="LEFT" :x2="W - 10" :y1="y(tick)" :y2="y(tick)" class="grid" />
      <text :x="LEFT - 6" :y="y(tick) + 4" class="axis" text-anchor="end">{{ currency(tick) }}</text>
    </g>
    <g v-for="bar in bars" :key="bar.label" class="bar">
      <title>{{ bar.tooltip }}</title>
      <rect :x="bar.x" :y="TOP" :width="band" :height="plotHeight" fill="transparent" />
      <rect
        v-for="(s, i) in bar.segments"
        :key="i"
        :x="bar.x + band * 0.18"
        :y="s.y"
        :width="band * 0.64"
        :height="s.height"
        :fill="s.color"
        rx="1.5"
      />
      <text :x="bar.x + band / 2" :y="H - 8" class="axis" :class="{ dense }" text-anchor="middle">
        {{ dense ? bar.label.split(' ')[0] : bar.label }}
      </text>
    </g>
  </svg>
</template>

<style scoped>
.chart {
  width: 100%;
  display: block;
}
.grid {
  stroke: var(--c-line);
}
.axis {
  font-size: 10.5px;
  fill: var(--c-muted);
}
.axis.dense {
  font-size: 9px;
}
.bar:hover rect:first-of-type {
  fill: rgba(0, 0, 0, 0.04);
}
</style>
