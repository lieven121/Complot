import type { BusyDto, TaskDto } from '@/shared/api'
import { addDays, fromIsoDate } from '@/shared/dates'

/** The visible day: 07:00–21:00, in 15-minute steps. */
export const DAY_START = 7 * 60
export const DAY_END = 21 * 60
export const SNAP = 15
export const PX_PER_HOUR = 48

/** Scheduling window around the shown week: overdue work up to 3 weeks back, upcoming up to 2 weeks ahead. */
const LOOK_BACK = 21
const LOOK_AHEAD = 14

const pad = (n: number) => String(n).padStart(2, '0')

/** Monday of the week of `iso`. */
export function mondayOf(iso: string): string {
  const weekday = (fromIsoDate(iso)!.getDay() + 6) % 7
  return addDays(iso, -weekday)
}

export const weekDays = (monday: string) => Array.from({ length: 7 }, (_, i) => addDays(monday, i))

/** ISO 8601 week number (weeks start on Monday; week 1 holds the first Thursday). */
export function isoWeek(iso: string): number {
  const [y, m, d] = iso.split('-').map(Number)
  const date = new Date(Date.UTC(y!, m! - 1, d!))
  // The Thursday of this week decides the year; count weeks from 1 January of that year.
  date.setUTCDate(date.getUTCDate() + 4 - (date.getUTCDay() || 7))
  const yearStart = Date.UTC(date.getUTCFullYear(), 0, 1)
  return Math.ceil(((date.getTime() - yearStart) / 86_400_000 + 1) / 7)
}

/** "2026-11-14T09:15:00": the API's local wall-clock date-time. */
export const localDateTime = (day: string, minutes: number) => `${day}T${pad(Math.floor(minutes / 60))}:${pad(minutes % 60)}:00`
export const dayOf = (dateTime: string) => dateTime.slice(0, 10)
export const minutesOf = (dateTime: string) => Number(dateTime.slice(11, 13)) * 60 + Number(dateTime.slice(14, 16))

/** Start (minutes after midnight) for a drop `offsetY` pixels below the top of a day column. */
export function slotAt(offsetY: number, durationMinutes: number, pxPerHour = PX_PER_HOUR): number {
  const minutes = DAY_START + Math.floor((offsetY / pxPerHour) * (60 / SNAP)) * SNAP
  return Math.min(Math.max(minutes, DAY_START), DAY_END - Math.min(durationMinutes, 60))
}

/** Position of a block in a day column. */
export function blockBox(startMinutes: number, durationMinutes: number, pxPerHour = PX_PER_HOUR) {
  return { top: ((startMinutes - DAY_START) / 60) * pxPerHour, height: Math.max(20, (durationMinutes / 60) * pxPerHour - 2) }
}

/** Shown for everyone ('all') or one person: their tasks plus the unassigned ones. */
const forWho = (task: TaskDto, who: string) => who === 'all' || !task.assigneeId || task.assigneeId === who

/** Open tasks without a slot, due around the shown week; overdue first. */
export function toSchedule(tasks: TaskDto[], monday: string, who: string): TaskDto[] {
  const from = addDays(monday, -LOOK_BACK)
  const to = addDays(monday, 7 + LOOK_AHEAD)
  return tasks
    .filter((t) => !t.done && !t.scheduledStart && t.date >= from && t.date < to && forWho(t, who))
    .sort((a, b) => a.date.localeCompare(b.date))
}

/** Tasks with a slot in the shown week; for one person only theirs. */
export function scheduledIn(tasks: TaskDto[], monday: string, who: string): TaskDto[] {
  const end = addDays(monday, 7)
  return tasks.filter((t) => {
    const day = t.scheduledStart ? dayOf(t.scheduledStart) : undefined
    return day !== undefined && day >= monday && day < end && (who === 'all' || t.assigneeId === who)
  })
}

/** Busy time that overlaps [start, start + minutes) on `day`. */
export function clashes(busy: BusyDto[], day: string, start: number, minutes: number): boolean {
  return busy.some((b) => {
    if (dayOf(b.start) !== day) return false
    const end = dayOf(b.end) === day ? minutesOf(b.end) : 24 * 60
    return minutesOf(b.start) < start + minutes && start < end
  })
}
