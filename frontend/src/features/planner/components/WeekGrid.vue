<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { BusyDto, TaskDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import { blockBox, DAY_END, DAY_START, dayOf, minutesOf, PX_PER_HOUR } from '../planner'

/**
 * Week calendar, 07:00–21:00. Tasks are dropped on a day column (drag & drop with a mouse) or placed by tapping a
 * column while a task is picked (touch). The parent turns the drop height into a time.
 */
const props = defineProps<{
  days: string[]
  today: string
  tasks: TaskDto[]
  busy: BusyDto[]
  picking: boolean
  colorOf: (task: TaskDto) => string
  titleOf: (task: TaskDto) => string
  canEdit: (task: TaskDto) => boolean
}>()
const emit = defineEmits<{ drop: [key: string, day: string, offsetY: number]; open: [task: TaskDto] }>()

const { t, locale } = useI18n()
const { dayMonth } = useFormat()
const over = ref<string>()

const hours = Array.from({ length: (DAY_END - DAY_START) / 60 }, (_, i) => DAY_START / 60 + i)
const height = ((DAY_END - DAY_START) / 60) * PX_PER_HOUR
const weekday = (day: string) => new Date(`${day}T12:00:00`).toLocaleDateString(locale.value, { weekday: 'short' })
const time = (dateTime: string) => dateTime.slice(11, 16)

const tasksOn = (day: string) => props.tasks.filter((task) => dayOf(task.scheduledStart!) === day)
const busyOn = (day: string) =>
  props.busy
    .filter((b) => dayOf(b.start) <= day && dayOf(b.end) >= day)
    .map((b) => {
      const start = dayOf(b.start) === day ? minutesOf(b.start) : 0
      const end = dayOf(b.end) === day ? minutesOf(b.end) : 24 * 60
      const from = Math.max(start, DAY_START)
      return { key: b.start, box: blockBox(from, Math.min(end, DAY_END) - from) }
    })
    .filter((b) => b.box.height > 0)
const taskBox = (task: TaskDto) => blockBox(minutesOf(task.scheduledStart!), task.estimateMinutes)

const now = ref(new Date())
const nowTop = computed(() => {
  const minutes = now.value.getHours() * 60 + now.value.getMinutes()
  return minutes >= DAY_START && minutes <= DAY_END ? ((minutes - DAY_START) / 60) * PX_PER_HOUR : undefined
})

const offsetIn = (event: MouseEvent) => event.clientY - (event.currentTarget as HTMLElement).getBoundingClientRect().top

function onDrop(event: DragEvent, day: string) {
  over.value = undefined
  const key = event.dataTransfer?.getData('text/plain')
  if (key) emit('drop', key, day, offsetIn(event))
}

function onColumnClick(event: MouseEvent, day: string) {
  if (props.picking && !(event.target as HTMLElement).closest('.event')) emit('drop', '', day, offsetIn(event))
}

const onDragStart = (event: DragEvent, task: TaskDto) => {
  event.dataTransfer?.setData('text/plain', task.key)
  if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move'
}
</script>

<template>
  <div class="calendar">
    <div class="grid" :style="{ '--px-hour': `${PX_PER_HOUR}px` }">
      <div class="head corner" />
      <div v-for="day in days" :key="day" class="head" :class="{ today: day === today }">
        {{ weekday(day) }}<small>{{ dayMonth(day) }}</small>
      </div>
      <div class="times">
        <div v-for="h in hours" :key="h" class="time">{{ String(h).padStart(2, '0') }}:00</div>
      </div>
      <div
        v-for="day in days"
        :key="day"
        class="column"
        :class="{ over: over === day, picking }"
        :style="{ height: `${height}px` }"
        :data-day="day"
        @dragover.prevent="over = day"
        @dragleave="over = over === day ? undefined : over"
        @drop.prevent="onDrop($event, day)"
        @click="onColumnClick($event, day)"
      >
        <div v-for="b in busyOn(day)" :key="b.key" class="busy" :style="{ top: `${b.box.top}px`, height: `${b.box.height}px` }">
          {{ t('planner.busy') }}
        </div>
        <button
          v-for="task in tasksOn(day)"
          :key="task.key"
          type="button"
          class="event"
          :class="{ done: task.done, locked: !canEdit(task) }"
          :style="{ '--c': colorOf(task), top: `${taskBox(task).top}px`, height: `${taskBox(task).height}px` }"
          :draggable="canEdit(task)"
          :title="titleOf(task)"
          @dragstart="onDragStart($event, task)"
          @click.stop="emit('open', task)"
        >
          <b>{{ time(task.scheduledStart!) }}</b> {{ titleOf(task) }}
          <span v-if="task.calendarError" class="flag warn" :title="t(`errors.${task.calendarError}`)">⚠</span>
          <span v-else-if="task.calendarSynced" class="flag" :title="t('planner.inGoogle')">G</span>
        </button>
        <div v-if="day === today && nowTop !== undefined" class="now" :style="{ top: `${nowTop}px` }" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.calendar {
  border: 1px solid var(--c-line);
  border-radius: var(--radius);
  background: var(--c-panel);
  overflow: auto;
  max-height: calc(100dvh - 190px);
  box-shadow: var(--shadow);
}
.grid {
  display: grid;
  grid-template-columns: 48px repeat(7, minmax(92px, 1fr));
  min-width: 720px;
}
.head {
  position: sticky;
  top: 0;
  z-index: 3;
  background: var(--c-panel-2);
  border-bottom: 1px solid var(--c-line);
  border-left: 1px solid var(--c-line);
  padding: 6px 4px;
  text-align: center;
  font-size: 12px;
  font-weight: 700;
  text-transform: capitalize;
}
.head.corner {
  border-left: 0;
}
.head small {
  display: block;
  font-weight: 500;
  color: var(--c-muted);
  text-transform: none;
}
.head.today {
  color: var(--c-accent);
  background: var(--c-accent-soft);
}
.time:first-child {
  top: 0;
}
.time {
  height: var(--px-hour);
  font-size: 10px;
  color: var(--c-muted);
  text-align: right;
  padding: 0 5px;
  position: relative;
  top: -7px;
}
.column {
  position: relative;
  border-left: 1px solid var(--c-line);
  background-image: repeating-linear-gradient(to bottom, #ebe5d6 0 1px, transparent 1px calc(var(--px-hour) / 2));
}
.column.over,
.column.picking {
  background-color: #eef5ea;
}
.column.picking {
  cursor: copy;
}
.busy {
  position: absolute;
  left: 2px;
  right: 2px;
  background: repeating-linear-gradient(45deg, #ece8de 0 6px, #e2ddd0 6px 12px);
  border-radius: 6px;
  font-size: 10.5px;
  color: var(--c-muted);
  padding: 2px 5px;
  overflow: hidden;
  pointer-events: none;
}
.event {
  all: unset;
  box-sizing: border-box;
  position: absolute;
  left: 4px;
  right: 3px;
  border-radius: 6px;
  background: var(--c);
  color: #fff;
  font-size: 11px;
  line-height: 1.25;
  padding: 3px 6px;
  overflow: hidden;
  cursor: grab;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.2);
  z-index: 2;
}
.event:focus-visible {
  outline: 2px solid var(--c-ink);
}
.event.locked {
  cursor: pointer;
}
.event.done {
  opacity: 0.55;
  text-decoration: line-through;
}
.flag {
  position: absolute;
  right: 4px;
  bottom: 2px;
  font-size: 9px;
  font-weight: 700;
  opacity: 0.85;
}
.flag.warn {
  font-size: 11px;
  opacity: 1;
}
.now {
  position: absolute;
  left: 0;
  right: 0;
  border-top: 2px solid var(--c-warn);
  z-index: 2;
  pointer-events: none;
}
</style>
