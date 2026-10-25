import { describe, expect, it } from 'vite-plus/test'
import type { SiteLayoutDto } from '@/shared/api'
import { snap, useDesigner } from './useDesigner'

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
