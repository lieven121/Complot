import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { MonthWindowDto } from '@/shared/api'

/** Locale-aware formatting of the values the API sends (months, dates, money, quantities). */
export function useFormat() {
  const { locale, n } = useI18n()

  const monthNames = computed(() => {
    const format = new Intl.DateTimeFormat(locale.value, { month: 'short' })
    return Array.from({ length: 12 }, (_, i) => format.format(new Date(2020, i, 1)).replace('.', ''))
  })
  const monthLongNames = computed(() => {
    const format = new Intl.DateTimeFormat(locale.value, { month: 'long' })
    return Array.from({ length: 12 }, (_, i) => format.format(new Date(2020, i, 1)))
  })

  const month = (m: number) => monthNames.value[m - 1] ?? '?'
  const monthLong = (m: number) => monthLongNames.value[m - 1] ?? '?'

  const monthWindow = (w?: MonthWindowDto | null) => {
    if (!w) return '—'
    return w.from === w.to ? month(w.from) : `${month(w.from)}–${month(w.to)}`
  }

  /** ISO date (yyyy-mm-dd) → "23 okt. 2026". */
  const date = (iso?: string | null) => {
    if (!iso) return '—'
    const [y, m, d] = iso.slice(0, 10).split('-').map(Number)
    return new Intl.DateTimeFormat(locale.value, { day: 'numeric', month: 'short', year: 'numeric' }).format(new Date(y!, m! - 1, d!))
  }

  const currency = (value: number) => n(value, 'currency')
  const decimal = (value: number) => n(value, 'decimal')

  return { monthNames, month, monthLong, monthWindow, date, currency, decimal }
}

/** Whether a month (1–12) lies inside a window that may wrap the new year — mirrors MonthWindow.Contains (backend). */
export function inMonthWindow(month: number, w?: MonthWindowDto | null): boolean {
  if (!w) return false
  return w.from <= w.to ? month >= w.from && month <= w.to : month >= w.from || month <= w.to
}
