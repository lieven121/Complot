import type { TaskDto } from '@/shared/api'
import { addDays, fromIsoDate } from '@/shared/dates'

export const TASK_GROUPS = ['overdue', 'today', 'thisWeek', 'nextWeek', 'later', 'done'] as const
export type TaskGroup = (typeof TASK_GROUPS)[number]

/** Sunday of the week of `iso` (weeks start on Monday, as in Belgium). */
export function endOfWeek(iso: string): string {
  const weekday = (fromIsoDate(iso)!.getDay() + 6) % 7 // Monday = 0
  return addDays(iso, 6 - weekday)
}

export function groupOf(task: TaskDto, today: string): TaskGroup {
  if (task.done) return 'done'
  if (task.date < today) return 'overdue'
  if (task.date === today) return 'today'
  const sunday = endOfWeek(today)
  if (task.date <= sunday) return 'thisWeek'
  if (task.date <= addDays(sunday, 7)) return 'nextWeek'
  return 'later'
}

/** Tasks per group in display order, empty groups left out; done tasks most recent first. */
export function groupTasks(tasks: TaskDto[], today: string): { group: TaskGroup; tasks: TaskDto[] }[] {
  const byGroup = new Map<TaskGroup, TaskDto[]>()
  for (const task of [...tasks].sort((a, b) => a.date.localeCompare(b.date))) {
    const group = groupOf(task, today)
    byGroup.set(group, [...(byGroup.get(group) ?? []), task])
  }
  byGroup.get('done')?.reverse()
  return TASK_GROUPS.filter((g) => byGroup.has(g)).map((group) => ({ group, tasks: byGroup.get(group)! }))
}

/** Planned minutes per assignee (undefined = nobody) for the open tasks up to and including `until`. */
export function workload(tasks: TaskDto[], until: string): Map<string | undefined, number> {
  const minutes = new Map<string | undefined, number>()
  for (const task of tasks) {
    if (!task.done && task.date <= until) minutes.set(task.assigneeId, (minutes.get(task.assigneeId) ?? 0) + task.estimateMinutes)
  }
  return minutes
}
