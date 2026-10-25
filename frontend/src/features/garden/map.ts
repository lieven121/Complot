import type { BedDto, PlantingDto, PlantingPhase, ShapeDto, SiteLayoutDto } from '@/shared/api'

/** Drawing only: the planted area and neighbour rules are calculated by the API. */
export function keyholePath(s: ShapeDto): string {
  const r = Math.min(s.width, s.height) / 2
  const cx = s.x + s.width / 2
  const cy = s.y + s.height / 2
  const gap = Math.min(0.3, r * 0.3)
  const bottom = cy + Math.sqrt(r * r - gap * gap)
  return `M${cx - gap} ${bottom} A${r} ${r} 0 1 1 ${cx + gap} ${bottom} L${cx + gap} ${cy + 0.1} L${cx - gap} ${cy + 0.1} Z`
}

export interface Box {
  x: number
  y: number
  w: number
  h: number
}

/** Everything drawn on the site, with a margin for labels. */
export function layoutBounds(layout: SiteLayoutDto): Box {
  const boxes = [...layout.plots, ...layout.beds, ...layout.paths, ...layout.features].map((i) => i.shape)
  const xs = [...boxes.map((b) => b.x), ...layout.trees.map((t) => t.x - t.crownDiameter / 2)]
  const ys = [...boxes.map((b) => b.y), ...layout.trees.map((t) => t.y - t.crownDiameter / 2)]
  const xe = [...boxes.map((b) => b.x + b.width), ...layout.trees.map((t) => t.x + t.crownDiameter / 2)]
  const ye = [...boxes.map((b) => b.y + b.height), ...layout.trees.map((t) => t.y + t.crownDiameter / 2 + 0.5)]
  if (!xs.length) return { x: -1, y: -1, w: 12, h: 10 }
  const x = Math.min(...xs) - 0.8
  const y = Math.min(...ys) - 1
  return { x, y, w: Math.max(...xe) - x + 0.8, h: Math.max(...ye) - y + 0.8 }
}

/** The phase a planting is in on a date: a lookup in the segments the API calculated. */
export function phaseAt(planting: PlantingDto, isoDate: string): PlantingPhase | undefined {
  return planting.phases.find((s) => s.from <= isoDate && isoDate <= s.to)?.phase
}

/** Phases in which the crop is physically in the bed (not still indoors). */
export const IN_BED: ReadonlySet<PlantingPhase> = new Set(['young', 'growing', 'harvest', 'clearing', 'dormant'])

/** Fill opacity per phase: the fuller the colour, the further along the crop. */
export const PHASE_OPACITY: Record<PlantingPhase, number> = {
  indoor: 0,
  young: 0.33,
  growing: 0.62,
  harvest: 0.95,
  clearing: 0.3,
  dormant: 0.42,
}

/** Smallest drawn spot radius (m), so a single small plant stays visible and clickable. */
const MIN_SPOT_RADIUS = 0.08

/** Radius of a circle with the planted area of a spot (m²; the API calculates it from count and spacing). */
export const spotRadius = (usedArea: number) => Math.max(MIN_SPOT_RADIUS, Math.sqrt(usedArea / Math.PI))

export interface Spot {
  planting: PlantingDto
  phase: PlantingPhase
  /** Centre in map coordinates (m). */
  x: number
  y: number
  r: number
}

/** Spots of a spots bed that are in the bed on the date. */
export function spotsOf(bed: BedDto, plantings: PlantingDto[], isoDate: string): Spot[] {
  return plantings.flatMap((planting) => {
    const phase = planting.bedId === bed.id ? phaseAt(planting, isoDate) : undefined
    if (!phase || !IN_BED.has(phase)) return []
    const x = bed.shape.x + (planting.spotX ?? bed.shape.width / 2)
    const y = bed.shape.y + (planting.spotY ?? bed.shape.height / 2)
    return [{ planting, phase, x, y, r: spotRadius(planting.usedArea) }]
  })
}

/** How much of a crop a planting holds, for lists: "3×" in a spots bed, "50%" of a strips bed. */
export const amountLabel = (planting: PlantingDto) =>
  planting.count != null ? `${planting.count}×` : planting.share < 1 ? `${Math.round(planting.share * 100)}%` : ''
