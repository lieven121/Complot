import { describe, expect, it } from 'vite-plus/test'
import type { TaskDto } from '@/shared/api'
import { endOfWeek, groupTasks, workload } from './grouping'

const task = (key: string, date: string, extra: Partial<TaskDto> = {}): TaskDto =>
  ({ key, kind: 'custom', date, baseMinutes: 30, estimateMinutes: 30, done: false, ...extra }) as TaskDto

// 2026-10-21 is a Wednesday.
const today = '2026-10-21'

describe('task grouping', () => {
  it('ends the week on Sunday', () => {
    expect(endOfWeek(today)).toBe('2026-10-25')
    expect(endOfWeek('2026-10-25')).toBe('2026-10-25')
    expect(endOfWeek('2026-10-26')).toBe('2026-11-01')
  })

  it('groups by due date and keeps done tasks apart', () => {
    const groups = groupTasks(
      [
        task('later', '2026-11-03'),
        task('next', '2026-10-27'),
        task('old', '2026-10-01'),
        task('week', '2026-10-25'),
        task('now', today),
        task('done1', '2026-10-02', { done: true }),
        task('done2', '2026-10-20', { done: true }),
      ],
      today,
    )
    expect(groups.map((g) => [g.group, g.tasks.map((x) => x.key)])).toEqual([
      ['overdue', ['old']],
      ['today', ['now']],
      ['thisWeek', ['week']],
      ['nextWeek', ['next']],
      ['later', ['later']],
      ['done', ['done2', 'done1']],
    ])
  })

  it('sums the open work per person up to a date', () => {
    const load = workload(
      [
        task('a', today, { assigneeId: 'p1', estimateMinutes: 20 }),
        task('b', '2026-10-24', { assigneeId: 'p1', estimateMinutes: 45 }),
        task('c', '2026-10-24', { estimateMinutes: 15 }),
        task('d', '2026-10-24', { assigneeId: 'p1', done: true }),
        task('e', '2026-11-30', { assigneeId: 'p1' }),
      ],
      '2026-10-27',
    )
    expect([...load]).toEqual([
      ['p1', 65],
      [undefined, 15],
    ])
  })
})
