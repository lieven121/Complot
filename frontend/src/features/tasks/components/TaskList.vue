<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { TaskDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'
import { useNotify } from '@/shared/composables/useNotify'
import { useTexts } from '@/features/garden/useTexts'
import { usePersonById, useReopenTask } from '../queries'
import CompleteTaskDialog from './CompleteTaskDialog.vue'
import TaskDialog from './TaskDialog.vue'

/**
 * Tasks as large, touch-friendly rows. The round button checks a task off (asking for the actual time) or reopens
 * it; tapping the text opens the details. `whereOf` names the bed or tree.
 */
const props = defineProps<{ tasks: TaskDto[]; today: string; whereOf: (task: TaskDto) => string | undefined; emptyText?: string }>()

const { t } = useI18n()
const { date, dayMonth, duration, dateTime } = useFormat()
const texts = useTexts()
const notify = useNotify()
const personById = usePersonById()
const reopen = useReopenTask()

const completing = ref<TaskDto>()
const details = ref<TaskDto>()

async function toggle(task: TaskDto) {
  if (!task.done) {
    completing.value = task
    return
  }
  try {
    await reopen.mutateAsync(task.key)
    notify.success('tasks.reopened')
  } catch (error) {
    notify.error(error)
  }
}
const sameYear = (iso: string) => iso.slice(0, 4) === props.today.slice(0, 4)
</script>

<template>
  <ul class="tasks">
    <li v-for="task in tasks" :key="task.key" class="task" :class="{ done: task.done }">
      <button
        type="button"
        class="check"
        :class="{ on: task.done }"
        :aria-pressed="task.done"
        :aria-label="task.done ? t('tasks.reopen') : t('tasks.complete')"
        @click="toggle(task)"
      >
        <i v-if="task.done" class="pi pi-check" />
      </button>
      <button type="button" class="body" @click="details = task">
        <span class="title">{{ texts.task(task) }}</span>
        <span class="meta">
          <span :class="{ overdue: !task.done && task.date < today }"
            >📅 {{ sameYear(task.date) ? dayMonth(task.date) : date(task.date) }}</span
          >
          <span v-if="whereOf(task)">📍 {{ whereOf(task) }}</span>
          <span>⏱ {{ duration(task.estimateMinutes) }}</span>
          <span v-if="task.scheduledStart">🗓️ {{ dateTime(task.scheduledStart) }}</span>
          <span v-if="task.done && task.actualMinutes">✔ {{ duration(task.actualMinutes) }}</span>
        </span>
      </button>
      <span
        v-if="task.assigneeId && personById.get(task.assigneeId)"
        class="avatar"
        :style="{ background: personById.get(task.assigneeId)!.color }"
        :title="personById.get(task.assigneeId)!.name"
        >{{ personById.get(task.assigneeId)!.name.charAt(0) }}</span
      >
    </li>
    <li v-if="!tasks.length && emptyText" class="muted small">{{ emptyText }}</li>
  </ul>
  <CompleteTaskDialog v-if="completing" :task="completing" @close="completing = undefined" />
  <TaskDialog v-if="details" :task="details" :where="whereOf(details)" @close="details = undefined" />
</template>

<style scoped>
.tasks {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.task {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  background: var(--c-panel);
  border: 1px solid var(--c-line);
  border-radius: 12px;
}
.check {
  width: 32px;
  height: 32px;
  flex: none;
  border-radius: 50%;
  border: 2px solid #b9a37c;
  background: #fff;
  color: #fff;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}
.check:hover,
.check:focus-visible {
  border-color: var(--c-accent);
}
.check.on {
  background: var(--c-accent);
  border-color: var(--c-accent);
}
.body {
  all: unset;
  flex: 1;
  min-width: 0;
  cursor: pointer;
  display: flex;
  flex-direction: column;
}
.body:focus-visible {
  outline: 2px solid var(--c-accent);
  border-radius: 6px;
}
.title {
  font-weight: 600;
}
.done .title {
  text-decoration: line-through;
  color: var(--c-muted);
}
.meta {
  font-size: 12px;
  color: var(--c-muted);
  display: flex;
  flex-wrap: wrap;
  gap: 2px 10px;
  margin-top: 2px;
}
.overdue {
  color: var(--c-warn);
  font-weight: 700;
}
.avatar {
  width: 26px;
  height: 26px;
  flex: none;
  border-radius: 50%;
  color: #fff;
  font-size: 12px;
  font-weight: 700;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}
@media (max-width: 900px) {
  .check {
    width: 36px;
    height: 36px;
  }
}
</style>
