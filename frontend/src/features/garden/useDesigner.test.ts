import { describe, expect, it } from 'vite-plus/test'
import type { SiteLayoutDto } from '@/shared/api'
import { snap } from './snapping'
import { useDesigner } from './useDesigner'

const labels = { path: 'Pad', tree: 'Boom', plot: 'Perceel', feature: 'Element' }
const layout = (): SiteLayoutDto => ({
  plots: [{ id: 'p1', name: 'Moestuin', type: 'open', shape: { kind: 'rect', x: 0, y: 0, width: 10, height: 10 } }],
  beds: [
    {
      id: 'b1',
      plotId: 'p1',
      code: 'A1',
      kind: 'strips',
      shape: { kind: 'rect', x: 1, y: 1, width: 1.2, height: 4 },
      sun: 'full',
      soil: undefined,
      notes: undefined,
      area: 4.8,
    },
  ],
  paths: [],
  features: [],
  trees: [],
  markers: [],
})

describe('designer', () => {
  it('snaps to 10 cm', () => expect(snap(1.234)).toBe(1.2))

  it('moves with snapping and undoes the whole drag in one step', () => {
    const d = useDesigner()
    d.start(layout())
    d.moveStart('b1')
    d.move('b1', 0.43, 0.06)
    d.move('b1', 0.91, 0.12)
    d.moveEnd()
    expect(d.draft.value!.beds[0]!.shape).toMatchObject({ x: 1.9, y: 1.1 })
    d.undo()
    expect(d.draft.value!.beds[0]!.shape).toMatchObject({ x: 1, y: 1 })
    d.redo()
    expect(d.draft.value!.beds[0]!.shape.x).toBe(1.9)
  })

  it('groups typing in one field into a single undo step', () => {
    const d = useDesigner()
    d.start(layout())
    d.select('b1')
    for (const width of [2, 2.5, 2.75]) d.edit('width', () => (d.draft.value!.beds[0]!.shape.width = width))
    d.edit('code', () => (d.draft.value!.beds[0]!.code = 'B9'))
    d.undo()
    expect(d.draft.value!.beds[0]).toMatchObject({ code: 'A1', shape: { width: 2.75 } })
    d.undo()
    expect(d.draft.value!.beds[0]!.shape.width).toBe(1.2)
    expect(d.canUndo.value).toBe(false)
  })

  it('adds a bed inside the plot under the cursor with a free code, and removes it again', () => {
    const d = useDesigner()
    d.start(layout())
    d.add('bed', { x: 5.04, y: 5.02 }, labels)
    const added = d.draft.value!.beds[1]!
    expect(added).toMatchObject({ code: 'N1', plotId: 'p1', shape: { x: 5, y: 5 } })
    expect(d.selection.value).toEqual({ kind: 'bed', id: added.id })
    d.remove()
    expect(d.draft.value!.beds).toHaveLength(1)
    d.undo()
    expect(d.draft.value!.beds).toHaveLength(2)
  })

  it('keyboard shortcuts undo and redo, except inside text fields', () => {
    const d = useDesigner()
    d.start(layout())
    d.add('tree', { x: 3, y: 3 }, labels)
    const key = (init: KeyboardEventInit) => ({ ...init, preventDefault() {}, target: null }) as unknown as KeyboardEvent
    d.handleKey(key({ key: 'z', ctrlKey: true }))
    expect(d.draft.value!.trees).toHaveLength(0)
    d.handleKey(key({ key: 'y', ctrlKey: true }))
    expect(d.draft.value!.trees).toHaveLength(1)
  })
})

describe('designer lines', () => {
  it('adds a path as a line, bends it with an extra point and keeps at least two points', () => {
    const d = useDesigner()
    d.start(layout())
    d.add('path', { x: 6, y: 1 }, labels)
    const path = d.draft.value!.paths[0]!
    expect(path.points).toEqual([
      { x: 6, y: 1 },
      { x: 6, y: 5 },
    ])
    d.vertexStart(path.id, 1, { x: 6, y: 3 })
    d.moveVertex(path.id, 1, { x: 8.03, y: 3.04 })
    d.moveEnd()
    expect(d.draft.value!.paths[0]!.points).toEqual([
      { x: 6, y: 1 },
      { x: 8, y: 3 },
      { x: 6, y: 5 },
    ])
    d.removeVertex(path.id, 1)
    d.removeVertex(path.id, 1)
    expect(d.draft.value!.paths[0]!.points).toHaveLength(2)
    d.undo()
    expect(d.draft.value!.paths[0]!.points).toHaveLength(3)
  })

  it('lines a point up with the other points of its line', () => {
    const d = useDesigner()
    d.start({ ...layout(), plots: [], beds: [] })
    d.add('path', { x: 0, y: 0 }, labels)
    const id = d.draft.value!.paths[0]!.id
    d.vertexStart(id, 1)
    d.moveVertex(id, 1, { x: 0.12, y: 3.33 }, { tolerance: 0.15 })
    expect(d.draft.value!.paths[0]!.points[1]).toEqual({ x: 0, y: 3.3 })
    expect(d.guides.value).toHaveLength(1)
    d.moveEnd()
    expect(d.guides.value).toEqual([])
  })
})

describe('designer snapping', () => {
  it('lines an edge up with another item within the tolerance, or drags freely with Alt', () => {
    const d = useDesigner()
    d.start(layout())
    d.add('bed', { x: 3, y: 1 }, labels)
    const id = d.draft.value!.beds[1]!.id
    d.moveStart(id)
    // Its top edge (1.07) comes within 15 cm of the other bed's top edge (1): it lines up there.
    d.move(id, 0.43, 0.07, { tolerance: 0.15 })
    expect(d.draft.value!.beds[1]!.shape).toMatchObject({ x: 3.4, y: 1 })
    expect(d.guides.value[0]).toMatchObject({ y1: 1, y2: 1 })
    d.move(id, 0.43, 0.07, { free: true, tolerance: 0.15 })
    expect(d.draft.value!.beds[1]!.shape).toMatchObject({ x: 3.43, y: 1.07 })
  })

  it('snaps a tree by its trunk and keeps its rectangular crown', () => {
    const d = useDesigner()
    d.start(layout())
    d.add('tree', { x: 3, y: 3 }, labels)
    const tree = d.draft.value!.trees[0]!
    Object.assign(tree, { outline: 'rect', crownWidth: 4, crownDepth: 0.5 })
    d.moveStart(tree.id)
    d.move(tree.id, 1.04, 0.52)
    expect(d.draft.value!.trees[0]).toMatchObject({ x: 4, y: 3.5, crownWidth: 4, crownDepth: 0.5 })
  })
})

describe('designer clipboard and layers', () => {
  it('pastes a copy next to the original with a new id and a free bed code', () => {
    const d = useDesigner()
    d.start(layout())
    d.select('b1')
    d.copy()
    d.paste()
    d.paste()
    const beds = d.draft.value!.beds
    expect(beds.map((b) => [b.code, b.shape.x, b.shape.y])).toEqual([
      ['A1', 1, 1],
      ['A2', 1.5, 1.5],
      ['A3', 2, 2],
    ])
    expect(new Set(beds.map((b) => b.id)).size).toBe(3)
    expect(d.selection.value?.id).toBe(beds[2]!.id)
    d.undo()
    expect(d.draft.value!.beds).toHaveLength(2)
  })

  it('copies, pastes and duplicates with the keyboard', () => {
    const d = useDesigner()
    d.start(layout())
    d.add('tree', { x: 3, y: 3 }, labels)
    const key = (init: KeyboardEventInit) => ({ ...init, preventDefault() {}, target: null }) as unknown as KeyboardEvent
    d.handleKey(key({ key: 'd', ctrlKey: true }))
    d.handleKey(key({ key: 'c', ctrlKey: true }))
    d.handleKey(key({ key: 'v', ctrlKey: true }))
    expect(d.draft.value!.trees.map((t) => [t.x, t.y])).toEqual([
      [3, 3],
      [3.5, 3.5],
      [4, 4],
    ])
    d.handleKey(key({ key: 'Delete' }))
    expect(d.draft.value!.trees).toHaveLength(2)
  })

  it('brings an item to the front or back of everything but the plots', () => {
    const d = useDesigner()
    d.start(layout())
    d.add('path', { x: 1, y: 1 }, labels)
    d.add('feature', { x: 2, y: 2 }, labels)
    d.select('b1')
    d.arrange('back')
    expect(d.draft.value!.beds[0]!.layer).toBe(-1)
    d.arrange('back')
    expect(d.canUndo.value && d.draft.value!.beds[0]!.layer).toBe(-1)
    d.arrange('front')
    expect(d.draft.value!.beds[0]!.layer).toBe(1)
    d.select('p1')
    d.arrange('front')
    expect(d.draft.value!.plots[0]!.layer).toBeUndefined()
  })
})
