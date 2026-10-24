import { describe, expect, it } from 'vite-plus/test'
import type { CostCategoryDto } from '@/shared/api'
import { allocation, assignRest, parseAiInvoice, periodLabel } from './costs'

const categories: CostCategoryDto[] = [
  { id: 'seed', name: 'Zaden & plantgoed', color: '#3f7d4e' },
  { id: 'other', name: 'Overig / verzending', color: '#9a958a' },
]

describe('cost helpers', () => {
  it('labels periods', () => {
    const quarter = (q: number, y: number) => `K${q} ${y}`
    const month = (m: number) => ['jan', 'feb', 'mrt'][m - 1]!
    expect(periodLabel({ year: 2026, index: 0, total: 0, byCategory: {} }, 'year', quarter, month)).toBe('2026')
    expect(periodLabel({ year: 2026, index: 2, total: 0, byCategory: {} }, 'quarter', quarter, month)).toBe('K2 2026')
    expect(periodLabel({ year: 2026, index: 3, total: 0, byCategory: {} }, 'month', quarter, month)).toBe('mrt 26')
  })

  it('checks that the lines add up, in cents', () => {
    expect(allocation([{ amount: 0.1 }, { amount: 0.2 }], 0.3)).toEqual({ allocated: 0.3, difference: 0, balanced: true })
    expect(allocation([{ amount: 30 }], 50)).toEqual({ allocated: 30, difference: 20, balanced: false })
    expect(allocation([], 0).balanced).toBe(false)
  })

  it('assigns the rest to the last line or a new one', () => {
    const lines = [{ description: 'Zaden', categoryId: 'seed', amount: 32.4 }]
    expect(assignRest(lines, 38.4, 'other', 'Rest')[0]!.amount).toBe(38.4)
    expect(assignRest([], 6, 'other', 'Rest')).toEqual([{ description: 'Rest', categoryId: 'other', amount: 6 }])
  })
})

describe('AI invoice JSON', () => {
  it('fills the form and matches categories by name', () => {
    const text =
      'Hier is het resultaat:\n```json\n{"date":"2026-01-20","supplier":"De Bolster","number":"DB-26-0198","total":46.85,"lines":[{"description":"Zaden","category":"zaden & plantgoed","amount":40.85},{"description":"Verzending","category":"Overig / verzending","amount":6}]}\n```'
    const { input, warnings } = parseAiInvoice(text, categories)
    expect(input).toMatchObject({ date: '2026-01-20', supplier: 'De Bolster', number: 'DB-26-0198', total: 46.85 })
    expect(input.lines?.map((l) => l.categoryId)).toEqual(['seed', 'other'])
    expect(warnings).toEqual([])
  })

  it('reports unknown categories and totals that do not add up', () => {
    const { input, warnings } = parseAiInvoice(
      '{"total": 10, "lines": [{"description": "x", "category": "Mest", "amount": 7}]}',
      categories,
    )
    expect(input.lines?.[0]?.categoryId).toBe('seed')
    expect(warnings.map((w) => w.key)).toEqual(['costs.ai.unknownCategory', 'costs.ai.notBalanced'])
  })

  it('rejects text that is not JSON', () => {
    expect(() => parseAiInvoice('geen json', categories)).toThrow()
  })
})
