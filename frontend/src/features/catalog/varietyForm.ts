import type { VarietyDto, VarietyInput } from '@/shared/api'

/** Form values for a new variety (sensible defaults for an annual sown outdoors). */
export function newVarietyInput(categoryId = ''): VarietyInput {
  return {
    categoryId,
    familyId: undefined,
    name: '',
    cultivar: undefined,
    latinName: undefined,
    life: 'annual',
    sowDepthCm: undefined,
    spacingInRowCm: undefined,
    spacingBetweenRowsCm: undefined,
    heightCm: undefined,
    sun: ['full'],
    sowIndoor: undefined,
    weeksIndoor: undefined,
    sowOutdoor: { from: 4, to: 6 },
    plantOut: undefined,
    harvest: { from: 7, to: 9 },
    germination: undefined,
    daysToFirstHarvest: undefined,
    harvestWeeks: undefined,
    maxYearsInBed: 1,
    notes: undefined,
    actions: [],
    suppliers: [],
  }
}

/** Form values for editing an existing variety. */
export function varietyInputFrom(v: VarietyDto): VarietyInput {
  return {
    categoryId: v.categoryId,
    familyId: v.familyId,
    name: v.name,
    cultivar: v.cultivar,
    latinName: v.latinName,
    life: v.life,
    sowDepthCm: v.sowDepthCm,
    spacingInRowCm: v.spacingInRowCm,
    spacingBetweenRowsCm: v.spacingBetweenRowsCm,
    heightCm: v.heightCm,
    sun: [...v.sun],
    sowIndoor: v.sowIndoor && { ...v.sowIndoor },
    weeksIndoor: v.weeksIndoor,
    sowOutdoor: v.sowOutdoor && { ...v.sowOutdoor },
    plantOut: v.plantOut && { ...v.plantOut },
    harvest: { ...v.harvest },
    germination: v.germination && { ...v.germination },
    daysToFirstHarvest: v.daysToFirstHarvest,
    harvestWeeks: v.harvestWeeks,
    maxYearsInBed: v.maxYearsInBed,
    notes: v.notes,
    actions: v.actions.map((a) => ({ ...a })),
    suppliers: v.suppliers.map((s) => ({ supplierId: s.supplierId, price: s.price, unit: s.unit, url: s.url })),
  }
}
