/** The API exchanges dates as ISO "yyyy-mm-dd" (DateOnly). These helpers convert at the UI edge, in local time. */

const pad = (n: number) => String(n).padStart(2, '0')

export function toIsoDate(date: Date | null | undefined): string | undefined {
  if (!date) return undefined
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

export function fromIsoDate(iso: string | null | undefined): Date | null {
  if (!iso) return null
  const [y, m, d] = iso.slice(0, 10).split('-').map(Number)
  return new Date(y!, m! - 1, d!, 12)
}

export const todayIso = () => toIsoDate(new Date())!
