import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { MonthWindowDto } from '@/shared/api'

/** Locale-aware formatting of the values the API sends (months, dates, money, quantities). */
export function useFormat() {
  const { locale, n, t } = useI18n()

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

  /** ISO date → "24 okt." (without the year). */
  const dayMonth = (iso: string) => {
    const [y, m, d] = iso.slice(0, 10).split('-').map(Number)
    return new Intl.DateTimeFormat(locale.value, { day: 'numeric', month: 'short' }).format(new Date(y!, m! - 1, d!))
  }

  const currency = (value: number) => n(value, 'currency')
  const decimal = (value: number) => n(value, 'decimal')

  /** 45 → "45 min", 80 → "1u20" (nl) / "1h20" (en). */
  const duration = (minutes: number) =>
    minutes < 60
      ? t('common.duration.minutes', { m: minutes })
      : t('common.duration.hours', { h: Math.floor(minutes / 60), m: String(minutes % 60).padStart(2, '0') })

  /** "za 24 okt. 10:00" for a calendar slot. */
  const dateTime = (iso: string) =>
    new Intl.DateTimeFormat(locale.value, { weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }).format(
      new Date(iso),
    )

  return { monthNames, month, monthLong, monthWindow, date, dayMonth, dateTime, currency, decimal, duration }
}

/** Whether a month (1–12) lies inside a window that may wrap the new year — mirrors MonthWindow.Contains (backend). */
export function inMonthWindow(month: number, w?: MonthWindowDto | null): boolean {
  if (!w) return false
  return w.from <= w.to ? month >= w.from && month <= w.to : month >= w.from || month <= w.to
}
