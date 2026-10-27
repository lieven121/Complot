import type { BedDto, PlantingDto, PlantingPhase, ShapeDto, SiteLayoutDto, UpdatePlantingInput } from '@/shared/api'
import type { Point } from './spotPicker'

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

/** A box with a positive size: a line marker's shape runs from (x, y) by (width, height), either way. */
export const normalized = (s: ShapeDto): ShapeDto => ({
  ...s,
  x: Math.min(s.x, s.x + s.width),
  y: Math.min(s.y, s.y + s.height),
  width: Math.abs(s.width),
  height: Math.abs(s.height),
})

/** Everything drawn on the site, with a margin for labels. */
export function layoutBounds(layout: SiteLayoutDto): Box {
  const boxes = [...layout.plots, ...layout.beds, ...layout.paths, ...layout.features, ...layout.markers].map((i) => normalized(i.shape))
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

/** Distance between plants (m): along a row and between rows. */
export interface PlantSpacing {
  along: number
  across: number
}

/** The variety's spacing, or 30 × 30 cm when it has none (as the API does for the planted area). */
export const spacingOf = (variety?: { spacingInRowCm?: number; spacingBetweenRowsCm?: number }): PlantSpacing =>
  variety?.spacingInRowCm && variety.spacingBetweenRowsCm
    ? { along: variety.spacingInRowCm / 100, across: variety.spacingBetweenRowsCm / 100 }
    : { along: 0.3, across: 0.3 }

/** How the plants of a spot are laid out: a round cluster, or a block `columns` plants wide. */
export interface SpotPattern {
  count: number
  columns?: number
  vertical?: boolean
}

/** Footprint of a spot around its centre (m) and the plant positions in it (relative to the centre). */
export interface SpotShape {
  round: boolean
  /** Radius of a round cluster. */
  r: number
  /** Size of a block. */
  w: number
  h: number
  plants: Point[]
}

const GOLDEN_ANGLE = Math.PI * (3 - Math.sqrt(5))

/**
 * A block has rows of ⌈count / columns⌉ plants (east–west, or north–south when vertical), `columns` rows side by
 * side: 10 leeks 2 wide is 2 rows of 5. A round cluster spirals out from the centre, one plant's area each.
 */
export function spotShape({ count, columns, vertical }: SpotPattern, spacing: PlantSpacing): SpotShape {
  const n = Math.max(1, count)
  if (!columns) {
    const s = Math.sqrt(spacing.along * spacing.across)
    const plants = Array.from({ length: n }, (_, i) => {
      const radius = n === 1 ? 0 : s * Math.sqrt((i + 0.5) / Math.PI)
      return { x: radius * Math.cos(i * GOLDEN_ANGLE), y: radius * Math.sin(i * GOLDEN_ANGLE) }
    })
    const r = spotRadius(n * spacing.along * spacing.across)
    return { round: true, r, w: 2 * r, h: 2 * r, plants }
  }
  const rows = Math.min(columns, n)
  const perRow = Math.ceil(n / rows)
  const plants = Array.from({ length: n }, (_, i) => {
    const along = (i % perRow) - (perRow - 1) / 2
    const across = Math.floor(i / perRow) - (rows - 1) / 2
    const p = { x: along * spacing.along, y: across * spacing.across }
    return vertical ? { x: p.y, y: p.x } : p
  })
  const [w, h] = [perRow * spacing.along, rows * spacing.across]
  return { round: false, r: Math.min(w, h) / 2, w: vertical ? h : w, h: vertical ? w : h, plants }
}

export interface Spot {
  planting: PlantingDto
  phase: PlantingPhase
  /** Centre in map coordinates (m). */
  x: number
  y: number
  shape: SpotShape
}

/** Spots of a spots bed that are in the bed on the date. */
export function spotsOf(
  bed: BedDto,
  plantings: PlantingDto[],
  isoDate: string,
  spacing: (varietyId: string) => PlantSpacing = () => spacingOf(),
): Spot[] {
  return plantings.flatMap((planting) => {
    const phase = planting.bedId === bed.id ? phaseAt(planting, isoDate) : undefined
    if (!phase || !IN_BED.has(phase)) return []
    const x = bed.shape.x + (planting.spotX ?? bed.shape.width / 2)
    const y = bed.shape.y + (planting.spotY ?? bed.shape.height / 2)
    const pattern = { count: planting.count ?? 1, columns: planting.spotColumns, vertical: planting.spotVertical }
    const shape = spotShape(pattern, spacing(planting.varietyId))
    // A round spot keeps the planted area the API calculated.
    return [{ planting, phase, x, y, shape: shape.round ? { ...shape, r: spotRadius(planting.usedArea) } : shape }]
  })
}

/** How much of a crop a planting holds, for lists: "3×" in a spots bed, "50%" of a strips bed. */
export const amountLabel = (planting: PlantingDto) =>
  planting.count != null ? `${planting.count}×` : planting.share < 1 ? `${Math.round(planting.share * 100)}%` : ''

/** The planting as it stands, as input for an update (callers override the fields they change). */
export const toUpdateInput = (p: PlantingDto): UpdatePlantingInput => ({
  share: p.share,
  sowOn: p.sowOn,
  plantOutOn: p.plantOutOn,
  harvestFrom: p.harvestFrom,
  harvestTo: p.harvestTo,
  clearOn: p.clearOn,
  notes: p.notes,
  count: p.count,
  spotX: p.spotX,
  spotY: p.spotY,
  spotColumns: p.spotColumns,
  spotVertical: p.spotVertical,
})
