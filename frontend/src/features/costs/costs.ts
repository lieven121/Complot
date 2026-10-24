import type { CostCategoryDto, CostPeriodDto, InvoiceInput, InvoiceLineDto } from '@/shared/api'

export type Grouping = 'year' | 'quarter' | 'month'

/** "2026", "K2 2026" or "mrt 26": the label under a bar; `quarter` and `monthShort` come from i18n. */
export function periodLabel(
  p: CostPeriodDto,
  grouping: Grouping,
  quarter: (q: number, y: number) => string,
  monthShort: (m: number) => string,
) {
  if (grouping === 'year') return String(p.year)
  if (grouping === 'quarter') return quarter(p.index, p.year)
  return `${monthShort(p.index)} ${String(p.year).slice(2)}`
}

const cents = (amount: number) => Math.round(amount * 100)

/** How much of the total the lines cover; `difference` > 0 is still to assign, < 0 is too much. */
export function allocation(lines: Pick<InvoiceLineDto, 'amount'>[], total: number) {
  const allocated = lines.reduce((sum, l) => sum + cents(l.amount || 0), 0) / 100
  const difference = (cents(total || 0) - cents(allocated)) / 100
  return { allocated, difference, balanced: total > 0 && difference === 0 }
}

/** Puts what is not assigned yet on the last line (or a new line when there is none). */
export function assignRest(lines: InvoiceLineDto[], total: number, fallbackCategory: string, description: string): InvoiceLineDto[] {
  const { difference } = allocation(lines, total)
  if (difference === 0) return lines
  const last = lines.at(-1)
  return last
    ? [...lines.slice(0, -1), { ...last, amount: Math.round((last.amount + difference) * 100) / 100 }]
    : [{ description, categoryId: fallbackCategory, amount: difference }]
}

/** What an AI assistant should return for an invoice, as described in the prompt. */
interface AiInvoice {
  date?: unknown
  supplier?: unknown
  number?: unknown
  total?: unknown
  lines?: { description?: unknown; category?: unknown; amount?: unknown }[]
}

export interface ParsedInvoice {
  input: Partial<InvoiceInput>
  /** i18n keys (with params) of things to check, e.g. an unknown category. */
  warnings: { key: string; params?: Record<string, unknown> }[]
}

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/

/** Text from the AI answer; anything that is not a string or number counts as empty. */
const asText = (value: unknown) => (typeof value === 'string' || typeof value === 'number' ? String(value).trim() : '')

/**
 * Reads the JSON an AI assistant made from an invoice into form values. Categories are matched by name (ignoring case);
 * unknown ones fall back to the first category and are reported. Throws on text that is not JSON.
 */
export function parseAiInvoice(text: string, categories: CostCategoryDto[]): ParsedInvoice {
  const start = text.indexOf('{')
  const end = text.lastIndexOf('}')
  const data = JSON.parse(start >= 0 && end > start ? text.slice(start, end + 1) : text) as AiInvoice
  const warnings: ParsedInvoice['warnings'] = []
  const byName = new Map(categories.map((c) => [c.name.toLowerCase(), c.id]))
  const fallback = categories[0]?.id ?? ''

  const lines = (data.lines ?? []).map((l) => {
    const name = asText(l.category)
    const categoryId = byName.get(name.toLowerCase())
    if (!categoryId) warnings.push({ key: 'costs.ai.unknownCategory', params: { name } })
    return { description: asText(l.description), categoryId: categoryId ?? fallback, amount: Number(l.amount) || 0 }
  })
  const total = Number(data.total) || lines.reduce((s, l) => s + l.amount, 0)
  if (!allocation(lines, total).balanced) warnings.push({ key: 'costs.ai.notBalanced' })

  return {
    input: {
      date: typeof data.date === 'string' && ISO_DATE.test(data.date) ? data.date : undefined,
      supplier: typeof data.supplier === 'string' ? data.supplier.trim() : undefined,
      number: typeof data.number === 'string' && data.number.trim() ? data.number.trim() : undefined,
      total,
      lines,
    },
    warnings,
  }
}

/** Example answer, shown with the prompt so people know what to expect. */
export const aiExample = (categories: CostCategoryDto[]) =>
  JSON.stringify(
    {
      date: '2026-03-15',
      supplier: 'Tuinbouwshop',
      number: 'AG2026-201',
      total: 82.2,
      lines: [
        { description: 'Tunnelbogen + folie', category: categories[0]?.name ?? '…', amount: 24 },
        { description: 'Klimgaas 2 panelen', category: categories[1]?.name ?? categories[0]?.name ?? '…', amount: 58.2 },
      ],
    },
    null,
    2,
  )
