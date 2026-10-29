import { describe, expect, it } from 'vite-plus/test'
import type { BedDto, PlantingDto } from '@/shared/api'
import { findCrop } from './cropFinder'
import { amountLabel, layoutBounds, spotRadius, spotShape, spotsOf } from './map'
import { useDesigner } from './useDesigner'

const bed = (id: string, code: string, kind: BedDto['kind'] = 'spots'): BedDto => ({
  id,
  plotId: 'p1',
  code,
  kind,
  shape: { kind: 'rect', x: 10, y: 5, width: 1, height: 1 },
  sun: 'full',
  soil: undefined,
  notes: undefined,
  area: 1,
})

const planting = (bedId: string, varietyId: string, extra: Partial<PlantingDto> = {}): PlantingDto => ({
  id: crypto.randomUUID(),
  bedId,
  varietyId,
  share: 1,
  count: undefined,
  spotX: undefined,
  spotY: undefined,
  spotColumns: undefined,
  spotVertical: false,
  sowOn: '2026-04-01',
  plantOutOn: undefined,
  harvestFrom: undefined,
  harvestTo: undefined,
  clearOn: '2026-09-01',
  notes: undefined,
  plantCount: undefined,
  usedArea: 0.09,
  phases: [{ phase: 'growing', from: '2026-04-01', to: '2026-09-01' }],
  ...extra,
})

describe('spots', () => {
  it('sizes a spot by its planted area, with a visible minimum', () => {
    expect(spotRadius(Math.PI * 0.25)).toBeCloseTo(0.5)
    expect(spotRadius(0)).toBe(0.08)
  })

  it('places spots relative to the bed, only while in the bed', () => {
    const m1 = bed('b1', 'M1')
    const spots = spotsOf(m1, [planting('b1', 'tomato', { count: 2, spotX: 0.3, spotY: 0.6 })], '2026-06-01')
    expect(spots).toHaveLength(1)
    expect(spots[0]).toMatchObject({ x: 10.3, y: 5.6 })
    expect(spotsOf(m1, [planting('b1', 'tomato')], '2026-12-01')).toEqual([])
  })

  it('lays out a block in rows of plants, turned when vertical', () => {
    const spacing = { along: 0.15, across: 0.3 }
    const leeks = spotShape({ count: 10, columns: 2 }, spacing)
    expect(leeks).toMatchObject({ round: false, w: 0.75, h: 0.6 })
    expect(leeks.plants[0]).toEqual({ x: -0.3, y: -0.15 })
    expect(spotShape({ count: 10, columns: 2, vertical: true }, spacing)).toMatchObject({ w: 0.6, h: 0.75 })

    const lettuceRow = spotShape({ count: 5, columns: 1 }, { along: 0.3, across: 0.3 })
    expect(new Set(lettuceRow.plants.map((p) => p.y))).toEqual(new Set([0]))
    expect(lettuceRow.w).toBeCloseTo(1.5)
  })

  it('spirals a round cluster from the centre', () => {
    const cluster = spotShape({ count: 7 }, { along: 0.3, across: 0.3 })
    expect(cluster.round).toBe(true)
    expect(cluster.plants).toHaveLength(7)
    expect(Math.max(...cluster.plants.map((p) => Math.hypot(p.x, p.y)))).toBeLessThanOrEqual(cluster.r)
    expect(spotShape({ count: 1 }, { along: 0.3, across: 0.3 }).plants).toEqual([{ x: 0, y: 0 }])
  })

  it('labels amounts as a count or a share', () => {
    expect(amountLabel(planting('b1', 'x', { count: 3 }))).toBe('3×')
    expect(amountLabel(planting('b1', 'x', { share: 0.5 }))).toBe('50%')
    expect(amountLabel(planting('b1', 'x'))).toBe('')
  })
})

describe('crop finder', () => {
  const beds = [bed('b2', 'M10'), bed('b1', 'M2'), bed('b3', 'A1', 'strips')]
  const plantings = [
    planting('b2', 'tomato', { count: 2 }),
    planting('b2', 'cherry', { count: 1 }),
    planting('b1', 'basil', { count: 3 }),
    planting('b3', 'cherry', { share: 0.5 }),
  ]
  const categoryOf = (id: string) => (id === 'basil' ? 'herbs' : 'tomatoes')

  it('finds a category across beds, sorted by code', () => {
    const hits = findCrop({ categoryId: 'tomatoes' }, beds, plantings, '2026-06-01', categoryOf)
    expect(hits.map((h) => [h.bed.code, h.amounts])).toEqual([
      ['A1', ['50%']],
      ['M10', ['2×', '1×']],
    ])
  })

  it('finds one variety, and nothing outside its season', () => {
    expect(findCrop({ varietyId: 'basil' }, beds, plantings, '2026-06-01', categoryOf).map((h) => h.bed.code)).toEqual(['M2'])
    expect(findCrop({ varietyId: 'basil' }, beds, plantings, '2027-01-01', categoryOf)).toEqual([])
  })
})

describe('designer', () => {
  it('adds a small spots bed with its own code series', () => {
    const d = useDesigner()
    d.start({
      plots: [{ id: 'p1', name: 'P', type: 'open', shape: { kind: 'rect', x: 0, y: 0, width: 5, height: 5 } }],
      beds: [],
      paths: [],
      features: [],
      trees: [],
      markers: [],
    })
    const labels = { path: '', tree: '', plot: '', feature: '' }
    d.add('bed', { x: 1, y: 1 }, labels, 'spots')
    d.add('bed', { x: 2, y: 2 }, labels, 'spots')
    expect(d.draft.value!.beds.map((b) => [b.code, b.kind, b.shape.width])).toEqual([
      ['M1', 'spots', 1],
      ['M2', 'spots', 1],
    ])
  })
})

describe('markers', () => {
  it('adds a wall line that moves as a whole and counts in the map bounds', () => {
    const d = useDesigner()
    d.start({ plots: [], beds: [], paths: [], features: [], trees: [], markers: [] })
    d.add('marker', { x: 2, y: 3 }, { path: '', tree: '', plot: '', feature: '' }, 'line')
    const wall = d.draft.value!.markers[0]!
    expect(wall).toMatchObject({
      kind: 'line',
      points: [
        { x: 2, y: 3 },
        { x: 5, y: 3 },
      ],
    })
    d.moveStart(wall.id)
    d.move(wall.id, 1, 1)
    d.moveEnd()
    expect(d.draft.value!.markers[0]!.points).toEqual([
      { x: 3, y: 4 },
      { x: 6, y: 4 },
    ])
    expect(layoutBounds(d.draft.value!).w).toBeGreaterThan(3)
  })
})
