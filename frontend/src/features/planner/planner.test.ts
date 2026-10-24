import { describe, expect, it } from 'vite-plus/test'
import type { TaskDto } from '@/shared/api'
import { blockBox, clashes, isoWeek, localDateTime, mondayOf, scheduledIn, slotAt, toSchedule } from './planner'

const task = (key: string, date: string, extra: Partial<TaskDto> = {}): TaskDto =>
  ({ key, kind: 'custom', date, baseMinutes: 30, estimateMinutes: 30, done: false, ...extra }) as TaskDto

describe('planner weeks', () => {
  it('finds the Monday and the ISO week', () => {
    expect(mondayOf('2026-10-24')).toBe('2026-10-19')
    expect(mondayOf('2026-10-19')).toBe('2026-10-19')
    expect(isoWeek('2026-10-24')).toBe(43)
    expect(isoWeek('2027-01-01')).toBe(53)
    expect(isoWeek('2027-01-04')).toBe(1)
  })
})

describe('planner slots', () => {
  it('snaps a drop to 15 minutes within the day', () => {
    expect(slotAt(0, 30)).toBe(7 * 60)
    expect(slotAt(48 * 2 + 30, 30)).toBe(9 * 60 + 30) // 2h37m below 07:00 → 09:30
    expect(slotAt(-50, 30)).toBe(7 * 60)
    expect(slotAt(10_000, 90)).toBe(20 * 60) // a long task still starts at least an hour before closing
    expect(localDateTime('2026-11-14', 9 * 60 + 15)).toBe('2026-11-14T09:15:00')
  })

  it('sizes blocks by duration', () => {
    expect(blockBox(9 * 60, 90)).toEqual({ top: 96, height: 70 })
    expect(blockBox(7 * 60, 5).height).toBe(20)
  })

  it('detects overlap with busy time on the same day', () => {
    const busy = [{ start: '2026-11-14T10:00:00', end: '2026-11-14T11:00:00' }]
    expect(clashes(busy, '2026-11-14', 9 * 60 + 30, 45)).toBe(true)
    expect(clashes(busy, '2026-11-14', 11 * 60, 30)).toBe(false)
    expect(clashes(busy, '2026-11-15', 10 * 60, 30)).toBe(false)
  })
})

describe('planner lists', () => {
  const monday = '2026-11-16'
  const tasks = [
    task('overdue', '2026-10-30'),
    task('mine', '2026-11-18', { assigneeId: 'lien' }),
    task('theirs', '2026-11-18', { assigneeId: 'tom' }),
    task('far', '2027-01-10'),
    task('done', '2026-11-17', { done: true }),
    task('planned', '2026-11-17', { scheduledStart: '2026-11-19T09:00:00', assigneeId: 'tom' }),
    task('earlyPlan', '2026-12-20', { scheduledStart: '2026-11-20T14:00:00', assigneeId: 'lien' }),
  ]

  it('lists open, unplanned work around the week for one person or everyone', () => {
    expect(toSchedule(tasks, monday, 'lien').map((t) => t.key)).toEqual(['overdue', 'mine'])
    expect(toSchedule(tasks, monday, 'all').map((t) => t.key)).toEqual(['overdue', 'mine', 'theirs'])
  })

  it('shows tasks planned in the week, even when due later', () => {
    expect(scheduledIn(tasks, monday, 'all').map((t) => t.key)).toEqual(['planned', 'earlyPlan'])
    expect(scheduledIn(tasks, monday, 'lien').map((t) => t.key)).toEqual(['earlyPlan'])
  })
})
