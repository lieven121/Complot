<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { PlantingDto, PlantingPhase } from '@/shared/api'
import { fromIsoDate, toIsoDate } from '@/shared/dates'
import { useFormat } from '@/shared/composables/useFormat'
import type { Crop } from './GardenMap.vue'

/** Multi-year bar chart of a bed's plantings by phase; clicking moves the timeline to that date. */
const props = defineProps<{
  plantings: PlantingDto[]
  from: string
  to: string
  today: string
  date: string
  cropOf: (varietyId: string) => Crop | undefined
  warnedIds?: ReadonlySet<string>
}>()
const emit = defineEmits<{ date: [iso: string] }>()

const { t } = useI18n()
const { month, date: formatDate } = useFormat()

const W = 380
const LEFT = 104
const RIGHT = W - 6
const ROW = 22
const TOP = 30
const height = computed(() => TOP + Math.max(props.plantings.length, 1) * ROW + 6)

const start = computed(() => fromIsoDate(props.from)!.getTime())
const span = computed(() => fromIsoDate(props.to)!.getTime() - start.value)
const x = (iso: string) => LEFT + Math.min(1, Math.max(0, (fromIsoDate(iso)!.getTime() - start.value) / span.value)) * (RIGHT - LEFT)

const years = computed(() => {
  const first = Number(props.from.slice(0, 4))
  return Array.from({ length: Number(props.to.slice(0, 4)) - first + 1 }, (_, i) => first + i)
})

const style: Record<PlantingPhase, (color: string) => Record<string, string | number>> = {
  indoor: (c) => ({ fill: '#fff', stroke: c, 'stroke-dasharray': '2 1.5' }),
  young: (c) => ({ fill: c, 'fill-opacity': 0.45 }),
  growing: (c) => ({ fill: c, 'fill-opacity': 0.45 }),
  dormant: (c) => ({ fill: c, 'fill-opacity': 0.25 }),
  harvest: (c) => ({ fill: c }),
  clearing: () => ({ fill: 'url(#gantt-hatch)' }),
}

const rows = computed(() =>
  props.plantings.map((p, i) => {
    const crop = props.cropOf(p.varietyId)
    const name = crop?.name ?? '?'
    return {
      id: p.id,
      y: TOP + i * ROW + 3,
      label: `${crop?.icon ?? ''} ${name.length > 13 ? `${name.slice(0, 12)}…` : name}${props.warnedIds?.has(p.id) ? ' ⚠' : ''}`,
      bars: p.phases.map((s) => ({
        key: `${s.phase}-${s.from}`,
        x: x(s.from),
        w: Math.max(0.5, x(s.to) - x(s.from)),
        attrs: style[s.phase](crop?.color ?? '#888'),
        title: `${t(`phase.${s.phase}`)}: ${formatDate(s.from)} → ${formatDate(s.to)}`,
      })),
    }
  }),
)

function onClick(e: MouseEvent) {
  const svg = e.currentTarget as SVGSVGElement
  const rect = svg.getBoundingClientRect()
  const px = ((e.clientX - rect.left) / rect.width) * W
  const f = (px - LEFT) / (RIGHT - LEFT)
  if (f >= 0 && f <= 1) emit('date', toIsoDate(new Date(start.value + f * span.value))!)
}
</script>

<template>
  <svg :viewBox="`0 0 ${W} ${height}`" class="gantt" role="img" :aria-label="t('map.bed.timeline')" @click="onClick">
    <defs>
      <pattern id="gantt-hatch" width="4" height="4" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
        <line x1="0" y1="0" x2="0" y2="4" stroke="#9a8a6a" stroke-width="1.4" />
      </pattern>
    </defs>
    <g v-for="y in years" :key="y">
      <rect
        :x="x(`${y}-01-01`)"
        :y="TOP - 12"
        :width="x(`${y}-12-31`) - x(`${y}-01-01`)"
        :height="height - TOP + 12"
        :fill="y % 2 ? '#f3eee2' : '#faf6ec'"
      />
      <text :x="x(`${y}-01-01`) + 3" y="12" font-weight="700">{{ y }}</text>
      <g v-for="m in 12" :key="m">
        <line
          :x1="x(`${y}-${String(m).padStart(2, '0')}-01`)"
          :x2="x(`${y}-${String(m).padStart(2, '0')}-01`)"
          :y1="TOP - 12"
          :y2="height"
          stroke="#e4ddcc"
          stroke-width=".6"
        />
        <text v-if="(m - 1) % 3 === 0" :x="x(`${y}-${String(m).padStart(2, '0')}-01`) + 1" :y="TOP - 3" class="month">
          {{ month(m).charAt(0).toUpperCase() }}
        </text>
      </g>
    </g>
    <g v-for="row in rows" :key="row.id">
      <text x="4" :y="row.y + ROW - 9">{{ row.label }}</text>
      <rect v-for="bar in row.bars" :key="bar.key" :x="bar.x" :y="row.y" :width="bar.w" :height="ROW - 6" rx="2" v-bind="bar.attrs">
        <title>{{ bar.title }}</title>
      </rect>
    </g>
    <line :x1="x(today)" :x2="x(today)" :y1="TOP - 12" :y2="height" stroke="#b9502c" stroke-dasharray="2 2" />
    <line :x1="x(date)" :x2="x(date)" :y1="TOP - 14" :y2="height" stroke="#2a2a24" stroke-width="1.6" />
  </svg>
</template>

<style scoped>
.gantt {
  width: 100%;
  display: block;
  cursor: crosshair;
  background: var(--c-panel-2);
  border: 1px solid var(--c-line);
  border-radius: 10px;
}
text {
  font-size: 10px;
  fill: #3b392f;
}
.month {
  font-size: 7px;
  fill: #8a846f;
}
</style>
