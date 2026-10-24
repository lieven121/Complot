import type { ToolDemandDto, ToolDto } from '@/shared/api'
import { addDays } from '@/shared/dates'

/** Maintenance counts as "due" this many days ahead; replacement a year ahead. */
const MAINTENANCE_AHEAD = 30
const REPLACE_AHEAD = 365

export const quantityOf = (tool: ToolDto) => tool.qtyGood + tool.qtyRepair + tool.qtyWorn

/** Share of good / repair / worn, in percent of the quantity (for the condition bar). */
export function conditionShares(tool: ToolDto) {
  const total = quantityOf(tool) || 1
  return { good: (tool.qtyGood / total) * 100, repair: (tool.qtyRepair / total) * 100, worn: (tool.qtyWorn / total) * 100 }
}

/** The plan needs more than there is in good condition. */
export function shortage(tool: ToolDto, demand: ToolDemandDto | undefined): number {
  return demand ? Math.max(0, demand.quantity - tool.qtyGood) : 0
}

export const maintenanceDue = (tool: ToolDto, today: string) =>
  !!tool.nextMaintenance && tool.nextMaintenance <= addDays(today, MAINTENANCE_AHEAD)
export const replacementDue = (tool: ToolDto, today: string) => !!tool.replaceBy && tool.replaceBy <= addDays(today, REPLACE_AHEAD)

/** Purchase value of everything (unit price × quantity). */
export const totalValue = (tools: ToolDto[]) => tools.reduce((sum, t) => sum + (t.unitPrice ?? 0) * quantityOf(t), 0)

/** Tools grouped by kind, kinds in alphabetical order. */
export function byKind(tools: ToolDto[]): [string, ToolDto[]][] {
  const groups = new Map<string, ToolDto[]>()
  for (const tool of tools) groups.set(tool.kind, [...(groups.get(tool.kind) ?? []), tool])
  return [...groups].sort(([a], [b]) => a.localeCompare(b))
}
