import { describe, expect, it } from 'vite-plus/test'
import type { ToolDto } from '@/shared/api'
import { byKind, conditionShares, maintenanceDue, replacementDue, shortage, totalValue } from './tools'

const tool = (extra: Partial<ToolDto>): ToolDto =>
  ({
    id: 'x',
    name: 'x',
    kind: 'Test',
    qtyGood: 0,
    qtyRepair: 0,
    qtyWorn: 0,
    unitPrice: undefined,
    log: [],
    bedIds: [],
    ...extra,
  }) as ToolDto

describe('tools overview', () => {
  const today = '2026-10-24'

  it('splits the quantity by condition', () => {
    expect(conditionShares(tool({ qtyGood: 18, qtyWorn: 2 }))).toEqual({ good: 90, repair: 0, worn: 10 })
    expect(conditionShares(tool({}))).toEqual({ good: 0, repair: 0, worn: 0 })
  })

  it('finds a shortage against the planned peak', () => {
    const spirals = tool({ qtyGood: 18, qtyWorn: 2 })
    expect(shortage(spirals, { toolId: 'x', quantity: 26, month: 5 })).toBe(8)
    expect(shortage(spirals, { toolId: 'x', quantity: 10, month: 5 })).toBe(0)
    expect(shortage(spirals, undefined)).toBe(0)
  })

  it('flags maintenance within a month and replacement within a year', () => {
    expect(maintenanceDue(tool({ nextMaintenance: '2026-11-10' }), today)).toBe(true)
    expect(maintenanceDue(tool({ nextMaintenance: '2027-01-10' }), today)).toBe(false)
    expect(maintenanceDue(tool({}), today)).toBe(false)
    expect(replacementDue(tool({ replaceBy: '2027-03-01' }), today)).toBe(true)
    expect(replacementDue(tool({ replaceBy: '2031-03-01' }), today)).toBe(false)
  })

  it('sums the value and groups by kind', () => {
    const tools = [
      tool({ name: 'a', kind: 'Water', qtyGood: 2, unitPrice: 45 }),
      tool({ name: 'b', kind: 'Bodem', qtyGood: 3, qtyRepair: 1, unitPrice: 0.5 }),
    ]
    expect(totalValue(tools)).toBe(92)
    expect(byKind(tools).map(([kind]) => kind)).toEqual(['Bodem', 'Water'])
  })
})
