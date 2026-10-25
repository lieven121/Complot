import type { BedDto, PlantingDto } from '@/shared/api'
import { amountLabel, IN_BED, phaseAt } from './map'

/** What to look for: all varieties of a category, or one variety. */
export type CropQuery = { categoryId: string } | { varietyId: string }

export interface CropHit {
  bed: BedDto
  /** "3×", "50%", joined when the bed holds the crop more than once ("2× · 3×"). */
  amounts: string[]
}

/** Beds that hold the crop on the date, sorted by bed code. */
export function findCrop(
  query: CropQuery,
  beds: BedDto[],
  plantings: PlantingDto[],
  isoDate: string,
  categoryOf: (varietyId: string) => string | undefined,
): CropHit[] {
  const matches = (p: PlantingDto) =>
    'varietyId' in query ? p.varietyId === query.varietyId : categoryOf(p.varietyId) === query.categoryId
  const hits = new Map<string, CropHit>()
  for (const p of plantings) {
    const phase = phaseAt(p, isoDate)
    const bed = beds.find((b) => b.id === p.bedId)
    if (!bed || !phase || !IN_BED.has(phase) || !matches(p)) continue
    const hit = hits.get(bed.id) ?? { bed, amounts: [] }
    const amount = amountLabel(p)
    if (amount) hit.amounts.push(amount)
    hits.set(bed.id, hit)
  }
  return [...hits.values()].sort((a, b) => a.bed.code.localeCompare(b.bed.code, undefined, { numeric: true }))
}
