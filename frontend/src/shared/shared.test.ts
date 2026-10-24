import { describe, expect, it } from 'vite-plus/test'
import { inMonthWindow } from './composables/useFormat'
import { fromIsoDate, toIsoDate } from './dates'

describe('inMonthWindow', () => {
  it.each([
    [{ from: 3, to: 6 }, 3, true],
    [{ from: 3, to: 6 }, 7, false],
    [{ from: 10, to: 2 }, 12, true],
    [{ from: 10, to: 2 }, 1, true],
    [{ from: 10, to: 2 }, 6, false],
  ])('%o contains month %i: %s', (window, month, expected) => {
    expect(inMonthWindow(month, window)).toBe(expected)
  })

  it('is false without a window', () => expect(inMonthWindow(5, undefined)).toBe(false))
})

describe('ISO dates', () => {
  it('round-trip in local time', () => {
    expect(toIsoDate(fromIsoDate('2026-10-24'))).toBe('2026-10-24')
    expect(toIsoDate(new Date(2026, 0, 5))).toBe('2026-01-05')
  })

  it('handle empty values', () => {
    expect(fromIsoDate(undefined)).toBeNull()
    expect(toIsoDate(null)).toBeUndefined()
  })
})
