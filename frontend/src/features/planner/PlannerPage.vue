<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Button from 'primevue/button'
import { useToast } from 'primevue/usetoast'
import { useI18n } from 'vue-i18n'
import type { TaskDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import { useNotify } from '@/shared/composables/useNotify'
import { useQueryParam } from '@/shared/composables/useQueryParam'
import { addDays, todayIso } from '@/shared/dates'
import { canEditSite, useSession } from '@/shared/session'
import { useSites } from '@/features/garden/queries'
import { useTexts } from '@/features/garden/useTexts'
import TaskDialog from '@/features/tasks/components/TaskDialog.vue'
import { usePeople, usePersonById, useSetTaskState, useTasks } from '@/features/tasks/queries'
import CalendarPanel from './components/CalendarPanel.vue'
import WeekGrid from './components/WeekGrid.vue'
import { clashes, isoWeek, localDateTime, mondayOf, scheduledIn, slotAt, toSchedule, weekDays } from './planner'
import { useBusy, useCalendars, usePlannerSettings } from './queries'

const ALL = 'all'
const NOBODY_COLOR = '#9a958a'

const { t } = useI18n()
const { dayMonth, duration, date: formatDate } = useFormat()
const texts = useTexts()
const notify = useNotify()
const toast = useToast()
const route = useRoute()
const router = useRouter()
const today = todayIso()

const { me, isEditor } = useSession()
const settings = usePlannerSettings()
const calendars = useCalendars()
const people = usePeople()
const personById = usePersonById()
const sites = useSites()
const setState = useSetTaskState()

// Week and person live in the URL (?week=2026-11-16&who=…); by default this week and "my" calendar.
const weekParam = useQueryParam('week')
const monday = computed(() => mondayOf(weekParam.value ?? today))
const whoParam = useQueryParam('who')
const who = computed(() => whoParam.value ?? me.data.value?.personId ?? ALL)
const days = computed(() => weekDays(monday.value))
const goWeek = (offset: number) => (weekParam.value = offset === 0 ? undefined : addDays(monday.value, offset))

const tasks = useTasks(computed(() => ({ from: addDays(monday.value, -21), to: addDays(monday.value, 20) })))
const toPlan = computed(() => toSchedule(tasks.data.value ?? [], monday.value, who.value))
const planned = computed(() => scheduledIn(tasks.data.value ?? [], monday.value, who.value))

const person = computed(() => (who.value === ALL ? undefined : personById.value.get(who.value)))
const calendar = computed(() => calendars.data.value?.find((c) => c.personId === who.value))
const busy = useBusy(
  computed(() => person.value?.id),
  monday,
  computed(() => !!settings.data.value?.googleEnabled && !!calendar.value?.googleAccount),
)

// Shown on tasks of every visible site, so editing depends on each task's site.
const siteById = computed(() => new Map((sites.data.value ?? []).map((s) => [s.id, s])))
const canEdit = (task: TaskDto) => (task.siteId ? canEditSite(siteById.value.get(task.siteId)) : isEditor.value)
const colorOf = (task: TaskDto) => (task.assigneeId && personById.value.get(task.assigneeId)?.color) || NOBODY_COLOR
const titleOf = (task: TaskDto) => texts.task(task)

/** The planner spans all sites, so it names the site (bed codes are per site). */
const whereOf = (task: TaskDto) => (task.siteId ? siteById.value.get(task.siteId)?.name : undefined)

const picked = ref<string>()
const details = ref<TaskDto>()
const taskByKey = (key: string) => (tasks.data.value ?? []).find((task) => task.key === key)

async function schedule(task: TaskDto, day: string, offsetY: number) {
  if (!canEdit(task)) return
  const minutes = slotAt(offsetY, task.estimateMinutes)
  const assigneeId = who.value === ALL ? task.assigneeId : who.value
  try {
    const result = await setState.mutateAsync({
      key: task.key,
      input: {
        assigneeId,
        estimateOverride: task.estimateOverride,
        scheduledStart: localDateTime(day, minutes),
        calendarTitle: titleOf(task),
      },
    })
    const name = assigneeId ? personById.value.get(assigneeId)?.name : undefined
    const when = `${dayMonth(day)} ${result.scheduledStart!.slice(11, 16)}`
    if (person.value && clashes(busy.data.value ?? [], day, minutes, task.estimateMinutes))
      toast.add({ severity: 'warn', summary: t('planner.overlap', { name: person.value.name }), life: 5000 })
    else notify.success(name ? 'planner.scheduledFor' : 'planner.scheduled', { task: titleOf(task), when, name })
    if (result.calendarError) notify.errorCode(result.calendarError)
  } catch (error) {
    notify.error(error)
  }
}

async function unschedule(task: TaskDto) {
  if (!canEdit(task) || !task.scheduledStart) return
  try {
    await setState.mutateAsync({
      key: task.key,
      input: { assigneeId: task.assigneeId, estimateOverride: task.estimateOverride, scheduledStart: undefined },
    })
    notify.success('planner.unscheduled')
  } catch (error) {
    notify.error(error)
  }
}

/** A drag ends on a day (with its key) or a picked task is placed by tapping a day (empty key). */
function onDrop(key: string, day: string, offsetY: number) {
  const task = taskByKey(key || picked.value || '')
  picked.value = undefined
  if (task) void schedule(task, day, offsetY)
}

function onListDrop(event: DragEvent) {
  const task = taskByKey(event.dataTransfer?.getData('text/plain') ?? '')
  if (task) void unschedule(task)
}

const onDragStart = (event: DragEvent, task: TaskDto) => {
  event.dataTransfer?.setData('text/plain', task.key)
  if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move'
}
const pick = (task: TaskDto) => {
  if (canEdit(task)) picked.value = picked.value === task.key ? undefined : task.key
}

const onKey = (event: KeyboardEvent) => {
  if (event.key === 'Escape') picked.value = undefined
}

// Back from Google's consent screen: ?calendar=connected|denied|failed.
onMounted(() => {
  document.addEventListener('keydown', onKey)
  const result = route.query.calendar
  if (typeof result === 'string') {
    if (result === 'connected') notify.success('planner.calendar.connected')
    else
      toast.add({
        severity: result === 'denied' ? 'info' : 'error',
        summary: t(`planner.calendar.${result === 'denied' ? 'denied' : 'failed'}`),
        life: 5000,
      })
    void router.replace({ query: { ...route.query, calendar: undefined } })
  }
})
onBeforeUnmount(() => document.removeEventListener('keydown', onKey))

const weekTitle = computed(() =>
  t('planner.week', { n: isoWeek(monday.value), from: dayMonth(monday.value), to: dayMonth(addDays(monday.value, 6)) }),
)
const pickedTask = computed(() => (picked.value ? taskByKey(picked.value) : undefined))
</script>

<template>
  <div class="page planner-page">
    <div class="page-head">
      <h1>{{ t('nav.planner') }}</h1>
      <div class="weeknav">
        <Button icon="pi pi-chevron-left" text rounded severity="secondary" :aria-label="t('planner.previous')" @click="goWeek(-7)" />
        <Button :label="t('planner.thisWeek')" size="small" severity="secondary" outlined @click="goWeek(0)" />
        <Button icon="pi pi-chevron-right" text rounded severity="secondary" :aria-label="t('planner.next')" @click="goWeek(7)" />
        <b>{{ weekTitle }}</b>
      </div>
      <div class="who" role="group" :aria-label="t('planner.calendarOf')">
        <button
          v-for="p in people.data.value ?? []"
          :key="p.id"
          type="button"
          class="chip"
          :class="{ on: who === p.id }"
          :style="{ '--c': p.color }"
          @click="whoParam = p.id"
        >
          <span class="dot" />{{ p.name }}
        </button>
        <button type="button" class="chip" :class="{ on: who === ALL }" @click="whoParam = ALL">{{ t('planner.everyone') }}</button>
      </div>
    </div>

    <p class="muted small hint">
      <b v-if="pickedTask" class="picking">{{ t('planner.picked', { task: titleOf(pickedTask) }) }}</b>
      <template v-else>{{ t('planner.help') }}</template>
    </p>

    <div class="layout">
      <section class="todo" @dragover.prevent @drop.prevent="onListDrop">
        <h3>{{ t('planner.toSchedule') }} ({{ toPlan.length }})</h3>
        <div class="cards">
          <button
            v-for="task in toPlan"
            :key="task.key"
            type="button"
            class="card-task"
            :class="{ picked: picked === task.key, locked: !canEdit(task) }"
            :style="{ '--c': colorOf(task) }"
            :draggable="canEdit(task)"
            @dragstart="onDragStart($event, task)"
            @click="pick(task)"
          >
            <b>{{ titleOf(task) }}</b>
            <span class="meta">
              <span :class="{ overdue: task.date < today }"
                >📅
                {{
                  t('planner.due', { date: task.date.slice(0, 4) === today.slice(0, 4) ? dayMonth(task.date) : formatDate(task.date) })
                }}</span
              >
              <span>⏱ {{ duration(task.estimateMinutes) }}</span>
              <span v-if="whereOf(task)">📍 {{ whereOf(task) }}</span>
              <span v-if="task.assigneeId && personById.get(task.assigneeId)">👤 {{ personById.get(task.assigneeId)!.name }}</span>
            </span>
          </button>
          <p v-if="tasks.isSuccess.value && !toPlan.length" class="muted small">{{ t('planner.nothingToSchedule') }}</p>
        </div>
      </section>

      <WeekGrid
        :days="days"
        :today="today"
        :tasks="planned"
        :busy="busy.data.value ?? []"
        :picking="!!picked"
        :color-of="colorOf"
        :title-of="titleOf"
        :can-edit="canEdit"
        @drop="onDrop"
        @open="details = $event"
      />
      <CalendarPanel
        v-if="person"
        class="calendars"
        :person="person"
        :calendar="calendar"
        :google-enabled="settings.data.value?.googleEnabled ?? false"
      />
    </div>

    <TaskDialog v-if="details" :task="details" :where="whereOf(details)" :readonly="!canEdit(details)" @close="details = undefined" />
  </div>
</template>

<style scoped>
.planner-page {
  max-width: none;
}
.weeknav {
  display: flex;
  align-items: center;
  gap: 4px;
}
.who {
  margin-left: auto;
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}
.chip {
  all: unset;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  border-radius: 999px;
  border: 1px solid var(--c-line);
  background: var(--c-panel);
  font-size: 13px;
}
.chip.on {
  border-color: var(--c-accent);
  background: var(--c-accent-soft);
  font-weight: 600;
}
.chip:focus-visible {
  outline: 2px solid var(--c-accent);
}
.dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: var(--c);
}
.hint {
  margin: 0 0 10px;
}
.picking {
  color: var(--c-accent);
}
/* Desktop: list and calendars on the left, week on the right. Phone: list, week, calendars. */
.layout {
  display: grid;
  grid-template-columns: 290px minmax(0, 1fr);
  grid-template-areas: 'todo week' 'calendars week';
  grid-template-rows: auto 1fr;
  gap: 12px 14px;
  align-items: start;
}
.todo {
  grid-area: todo;
}
.layout > :deep(.calendar) {
  grid-area: week;
}
.calendars {
  grid-area: calendars;
}
.todo h3 {
  margin-top: 0;
}
.cards {
  display: flex;
  flex-direction: column;
  gap: 6px;
  max-height: 46dvh;
  overflow: auto;
  padding-right: 2px;
}
.card-task {
  all: unset;
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 8px 10px;
  border: 1px solid var(--c-line);
  border-left: 4px solid var(--c);
  border-radius: 9px;
  background: var(--c-panel);
  cursor: grab;
  font-size: 13px;
  user-select: none;
}
.card-task.locked {
  cursor: default;
  opacity: 0.7;
}
.card-task.picked {
  outline: 2px solid var(--c-accent);
  background: var(--c-accent-soft);
}
.card-task:focus-visible {
  outline: 2px solid var(--c-accent);
}
.meta {
  font-size: 11.5px;
  color: var(--c-muted);
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}
.overdue {
  color: var(--c-warn);
  font-weight: 700;
}
@media (max-width: 1000px) {
  .layout {
    grid-template-columns: minmax(0, 1fr);
    grid-template-areas: 'todo' 'week' 'calendars';
    grid-template-rows: auto;
  }
  .who {
    margin-left: 0;
  }
  .cards {
    max-height: 30dvh;
  }
}
</style>
