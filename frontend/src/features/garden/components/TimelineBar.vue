<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import Button from 'primevue/button'
import { useI18n } from 'vue-i18n'
import { fromIsoDate, toIsoDate } from '@/shared/dates'
import { useFormat } from '@/shared/composables/useFormat'
import { addDays } from '../map'

/** Time scrubber over the map period: seasons, today marker, drag, season jumps, play and arrow keys. */
const props = defineProps<{ from: string; to: string; today: string }>()
const date = defineModel<string>({ required: true })

const { t, locale } = useI18n()
const { month } = useFormat()

const start = computed(() => fromIsoDate(props.from)!.getTime())
const span = computed(() => fromIsoDate(props.to)!.getTime() - start.value)
const fraction = (iso: string) => (fromIsoDate(iso)!.getTime() - start.value) / span.value
const clamp = (iso: string) => (iso < props.from ? props.from : iso > props.to ? props.to : iso)
const set = (iso: string) => (date.value = clamp(iso))

type Season = 'winter' | 'spring' | 'summer' | 'autumn'
const season = (m: number): Season => (m === 12 || m <= 2 ? 'winter' : m <= 5 ? 'spring' : m <= 8 ? 'summer' : 'autumn')

const years = computed(() => {
  const first = Number(props.from.slice(0, 4))
  const last = Number(props.to.slice(0, 4))
  return Array.from({ length: last - first + 1 }, (_, i) => first + i)
})
const months = computed(() =>
  years.value.flatMap((y) =>
    Array.from({ length: 12 }, (_, i) => {
      const from = fraction(`${y}-${String(i + 1).padStart(2, '0')}-01`)
      const to = i === 11 ? fraction(`${y + 1}-01-01`) : fraction(`${y}-${String(i + 2).padStart(2, '0')}-01`)
      return { key: `${y}-${i}`, left: from * 100, width: (Math.min(to, 1) - from) * 100, season: season(i + 1), label: month(i + 1) }
    }),
  ),
)

const label = computed(() => {
  const d = fromIsoDate(date.value)!
  const text = new Intl.DateTimeFormat(locale.value, { weekday: 'short', day: 'numeric', month: 'long', year: 'numeric' }).format(d)
  return `${text} · ${t(`season.${season(d.getMonth() + 1)}`)}`
})
const mode = computed(() => (date.value === props.today ? 'today' : date.value < props.today ? 'past' : 'plan'))

/** Season boundaries: 1 March, 1 June, 1 September, 1 December. */
const seasonStarts = computed(() =>
  [years.value[0]! - 1, ...years.value].flatMap((y) => ['03', '06', '09', '12'].map((m) => `${y}-${m}-01`)),
)
const previousSeason = () => set([...seasonStarts.value].reverse().find((s) => s < addDays(date.value, -1)) ?? props.from)
const nextSeason = () => set(seasonStarts.value.find((s) => s > date.value) ?? props.to)

const track = ref<HTMLElement>()
let dragging = false
const fromPointer = (e: PointerEvent) => {
  const rect = track.value!.getBoundingClientRect()
  const f = Math.min(1, Math.max(0, (e.clientX - rect.left) / rect.width))
  set(toIsoDate(new Date(start.value + f * span.value))!)
}
const onDown = (e: PointerEvent) => {
  dragging = true
  track.value!.setPointerCapture(e.pointerId)
  fromPointer(e)
}
const onMove = (e: PointerEvent) => dragging && fromPointer(e)
const onKey = (e: KeyboardEvent) => {
  const step = e.shiftKey ? 30 : 7
  if (e.key === 'ArrowRight') set(addDays(date.value, step))
  else if (e.key === 'ArrowLeft') set(addDays(date.value, -step))
  else return
  e.preventDefault()
}

const playing = ref<number>()
const togglePlay = () => {
  if (playing.value) {
    clearInterval(playing.value)
    playing.value = undefined
    return
  }
  if (date.value >= props.to) set(props.from)
  playing.value = window.setInterval(() => {
    if (date.value >= props.to) togglePlay()
    else set(addDays(date.value, 7))
  }, 140)
}
onBeforeUnmount(() => playing.value && clearInterval(playing.value))
</script>

<template>
  <div class="timeline">
    <div class="controls">
      <Button :label="t('map.timeline.previousSeason')" size="small" severity="secondary" outlined @click="previousSeason" />
      <Button :label="t('map.timeline.today')" size="small" severity="secondary" outlined @click="set(today)" />
      <Button :label="t('map.timeline.nextSeason')" size="small" severity="secondary" outlined @click="nextSeason" />
      <Button
        :label="playing ? t('map.timeline.pause') : t('map.timeline.play')"
        :icon="playing ? 'pi pi-pause' : 'pi pi-play'"
        size="small"
        :severity="playing ? 'contrast' : 'secondary'"
        :outlined="!playing"
        @click="togglePlay"
      />
      <div class="date">
        <span>{{ label }}</span>
        <span class="badge" :class="`mode-${mode}`">{{ t(`map.timeline.mode.${mode}`) }}</span>
      </div>
    </div>
    <div
      ref="track"
      class="track"
      role="slider"
      tabindex="0"
      :aria-label="t('map.timeline.label')"
      :aria-valuetext="label"
      :aria-valuemin="0"
      :aria-valuemax="100"
      :aria-valuenow="Math.round(fraction(date) * 100)"
      @pointerdown="onDown"
      @pointermove="onMove"
      @pointerup="dragging = false"
      @keydown="onKey"
    >
      <div v-for="y in years" :key="y" class="year" :style="{ left: `${fraction(`${y}-01-01`) * 100}%` }">{{ y }}</div>
      <div v-for="m in months" :key="m.key" class="month" :class="m.season" :style="{ left: `${m.left}%`, width: `${m.width}%` }">
        {{ m.label.charAt(0).toUpperCase() }}
      </div>
      <div class="today" :style="{ left: `${fraction(today) * 100}%` }">
        <span>{{ t('map.timeline.todayMarker') }}</span>
      </div>
      <div class="handle" :style="{ left: `${fraction(date) * 100}%` }"><span /></div>
    </div>
  </div>
</template>

<style scoped>
.timeline {
  background: var(--c-panel);
  border-bottom: 1px solid var(--c-line);
  padding: 10px 16px 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.controls {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
}
.date {
  margin-left: auto;
  display: flex;
  align-items: center;
  gap: 10px;
  font-weight: 700;
  font-size: 15px;
}
.badge {
  font-size: 11px;
  padding: 2px 9px;
  border-radius: 999px;
  font-weight: 700;
  text-transform: uppercase;
}
.mode-past {
  background: #ebe6da;
  color: var(--c-muted);
}
.mode-today {
  background: var(--c-accent-soft);
  color: var(--c-accent);
}
.mode-plan {
  background: var(--c-info-soft);
  color: var(--c-info);
}
.track {
  position: relative;
  height: 48px;
  user-select: none;
  touch-action: none;
  cursor: pointer;
  outline-offset: 3px;
}
.year {
  position: absolute;
  top: 0;
  height: 48px;
  font-size: 11px;
  font-weight: 700;
  color: var(--c-muted);
  padding-left: 5px;
  border-left: 2px solid #b9a37c;
}
.month {
  position: absolute;
  top: 17px;
  height: 31px;
  font-size: 10px;
  color: rgba(0, 0, 0, 0.55);
  display: flex;
  align-items: center;
  justify-content: center;
  border-right: 1px solid rgba(255, 255, 255, 0.7);
  overflow: hidden;
}
.winter {
  background: #dde6ee;
}
.spring {
  background: #dfedd4;
}
.summer {
  background: #f2e5bd;
}
.autumn {
  background: #eed6bf;
}
.today {
  position: absolute;
  top: 14px;
  bottom: 0;
  width: 2px;
  margin-left: -1px;
  background: var(--c-warn);
  opacity: 0.7;
}
.today span {
  position: absolute;
  top: -14px;
  left: 3px;
  font-size: 9px;
  color: var(--c-warn);
  font-weight: 700;
  white-space: nowrap;
}
.handle {
  position: absolute;
  top: 12px;
  bottom: -5px;
  width: 0;
  pointer-events: none;
}
.handle::before {
  content: '';
  position: absolute;
  left: -1.5px;
  top: 0;
  bottom: 0;
  width: 3px;
  background: var(--c-ink);
  border-radius: 2px;
}
.handle span {
  position: absolute;
  bottom: -3px;
  left: -8px;
  width: 16px;
  height: 16px;
  border-radius: 50%;
  background: var(--c-ink);
  border: 3px solid #fff;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.3);
}
@media (max-width: 900px) {
  .date {
    margin-left: 0;
    width: 100%;
    font-size: 14px;
  }
}
</style>
