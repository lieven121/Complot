import { computed, ref } from 'vue'
import type {
  BedDto,
  BedKind,
  FeatureDto,
  MarkerDto,
  MarkerKind,
  PathDto,
  PlotDto,
  PointDto,
  ShapeDto,
  SiteLayoutDto,
  TreeDto,
} from '@/shared/api'
import { boxOf, type Geometry, snapTargets } from './map'
import { alignBox, cm, type Guide, snap, type SnapOptions } from './snapping'

export type ItemKind = 'plot' | 'bed' | 'path' | 'feature' | 'tree' | 'marker'
export type LayoutItem = PlotDto | BedDto | PathDto | FeatureDto | TreeDto | MarkerDto
export interface Selection {
  kind: ItemKind
  id: string
}

const LISTS = { plot: 'plots', bed: 'beds', path: 'paths', feature: 'features', tree: 'trees', marker: 'markers' } as const
const HISTORY_LIMIT = 100
/** A pasted copy lands this far (m) to the south-east of the original, so both stay visible. */
const PASTE_OFFSET = 0.5

const clone = <T>(value: T): T => JSON.parse(JSON.stringify(value)) as T

type Movable = Geometry & { id: string; layer?: number }

/** The geometry of an item moved by (dx, dy) from `origin`, rounded to whole centimetres. */
function translate(item: Movable, origin: Geometry, dx: number, dy: number) {
  if (origin.points) item.points = origin.points.map((p) => ({ x: cm(p.x + dx), y: cm(p.y + dy) }))
  else if (origin.shape) item.shape = { ...origin.shape, x: cm(origin.shape.x + dx), y: cm(origin.shape.y + dy) }
  else Object.assign(item, { x: cm(origin.x! + dx), y: cm(origin.y! + dy) })
}

/**
 * Editing state of the designer: a local copy of the layout with undo/redo. Nothing reaches the API until the
 * whole layout is saved, so undo/redo is purely local and a cancel simply discards the copy.
 */
export function useDesigner() {
  const draft = ref<SiteLayoutDto>()
  const selection = ref<Selection>()
  const undoStack = ref<string[]>([])
  const redoStack = ref<string[]>([])
  /** Lines the dragged item or point lines up with, while dragging. */
  const guides = ref<Guide[]>([])
  /** Consecutive edits of the same field (typing "2", "2.5", "2.75") form one undo step. */
  let lastEditKey: string | undefined
  let dragOrigin: Geometry | undefined
  /** The copied item; kept across sites, so an item can be copied to another site. */
  let clipboard: { kind: ItemKind; item: LayoutItem } | undefined

  const snapshot = () => JSON.stringify(draft.value)
  const record = () => {
    undoStack.value.push(snapshot())
    if (undoStack.value.length > HISTORY_LIMIT) undoStack.value.shift()
    redoStack.value = []
    lastEditKey = undefined
  }
  const restore = (json: string) => {
    draft.value = JSON.parse(json) as SiteLayoutDto
    if (selection.value && !find(selection.value)) selection.value = undefined
    lastEditKey = undefined
  }
  const undo = () => {
    const previous = undoStack.value.pop()
    if (!previous) return
    redoStack.value.push(snapshot())
    restore(previous)
  }
  const redo = () => {
    const next = redoStack.value.pop()
    if (!next) return
    undoStack.value.push(snapshot())
    restore(next)
  }

  const start = (layout: SiteLayoutDto) => {
    draft.value = clone(layout)
    undoStack.value = []
    redoStack.value = []
    selection.value = undefined
    lastEditKey = undefined
  }

  const list = (kind: ItemKind) => draft.value![LISTS[kind]] as Movable[]
  function find(sel: Selection) {
    return draft.value ? list(sel.kind).find((item) => item.id === sel.id) : undefined
  }
  const selected = computed(() => (selection.value ? (find(selection.value) as LayoutItem | undefined) : undefined))

  const kindOf = (id: string): ItemKind | undefined =>
    draft.value ? (Object.keys(LISTS) as ItemKind[]).find((kind) => list(kind).some((item) => item.id === id)) : undefined
  const select = (id: string | undefined) => {
    const kind = id ? kindOf(id) : undefined
    selection.value = id && kind ? { kind, id } : undefined
  }
  const byId = (id: string) => find({ kind: kindOf(id)!, id })!

  /** Change the selected item; `field` groups keystrokes of one field into one undo step. */
  const edit = (field: string, change: () => void) => {
    const key = `${selection.value?.id}:${field}`
    if (key !== lastEditKey) record()
    lastEditKey = key
    change()
  }

  const moveStart = (id: string) => {
    record()
    select(id)
    dragOrigin = clone(byId(id))
  }
  /** Move a dragged item by (dx, dy) m from where the drag started, snapping as `options` say. */
  const move = (id: string, dx: number, dy: number, options?: SnapOptions) => {
    if (!dragOrigin) return
    const item = byId(id)
    const box = boxOf(dragOrigin)
    const moved = { ...box, x: box.x + dx, y: box.y + dy }
    // A tree's trunk lands on the grid, a line's first point, a shape's corner.
    const anchor = dragOrigin.points?.[0] ?? (dragOrigin.shape ? { x: box.x, y: box.y } : { x: dragOrigin.x!, y: dragOrigin.y! })
    const fit = alignBox(moved, snapTargets(draft.value!, id), options, { x: anchor.x + dx, y: anchor.y + dy })
    translate(item, dragOrigin, dx + fit.dx, dy + fit.dy)
    guides.value = fit.guides
  }
  const moveEnd = () => {
    dragOrigin = undefined
    guides.value = []
  }

  /** The points of a line (path or wall); undefined for anything else. */
  const pointsOf = (id: string) => byId(id).points

  /** Start dragging a point of a line; with `insert` a new point is first added at `index`. */
  const vertexStart = (id: string, index: number, insert?: PointDto) => {
    record()
    select(id)
    if (insert) pointsOf(id)?.splice(index, 0, { x: cm(insert.x), y: cm(insert.y) })
  }
  /** Drag a point of a line to `at`, lining it up with other items and with the line's other points. */
  const moveVertex = (id: string, index: number, at: PointDto, options?: SnapOptions) => {
    const points = pointsOf(id)
    if (!points?.[index]) return
    const others = points.filter((_, i) => i !== index).map((p) => ({ x: p.x, y: p.y, w: 0, h: 0 }))
    const targets = [...snapTargets(draft.value!, id), ...others]
    const fit = alignBox({ x: at.x, y: at.y, w: 0, h: 0 }, targets, options)
    points[index] = { x: cm(at.x + fit.dx), y: cm(at.y + fit.dy) }
    guides.value = fit.guides
  }
  /** Remove a point of a line; a line keeps at least two. */
  const removeVertex = (id: string, index: number) => {
    const points = pointsOf(id)
    if (!points || points.length <= 2) return
    record()
    points.splice(index, 1)
  }

  const plotAt = (x: number, y: number) =>
    draft.value!.plots.find((p) => x >= p.shape.x && x <= p.shape.x + p.shape.width && y >= p.shape.y && y <= p.shape.y + p.shape.height) ??
    draft.value!.plots[0]

  /**
   * Add an item at a point. A bed is a 1.2 × 4 m strips bed, or with `variant` 'spots' a 1 × 1 m spots bed (code M…)
   * for mixed plantings; a path runs 4 m south; a marker is a coloured area or, with `variant` 'line', a 3 m wall.
   */
  function add(
    kind: ItemKind,
    at: { x: number; y: number },
    labels: { path: string; tree: string; plot: string; feature: string },
    variant?: BedKind | MarkerKind,
  ) {
    const x = snap(at.x)
    const y = snap(at.y)
    const plot = kind === 'bed' ? plotAt(x, y) : undefined
    if (kind === 'bed' && !plot) return
    record()
    const id = crypto.randomUUID()
    const shape = (width: number, height: number, kind: ShapeDto['kind'] = 'rect'): ShapeDto => ({ kind, x, y, width, height })
    const layout = draft.value!
    const bedKind: BedKind = variant === 'spots' ? 'spots' : 'strips'
    if (kind === 'bed') {
      layout.beds.push({
        id,
        plotId: plot!.id,
        code: nextBedCode(layout.beds, bedKind === 'spots' ? 'M' : 'N'),
        kind: bedKind,
        shape: bedKind === 'spots' ? shape(1, 1) : shape(1.2, 4),
        sun: 'full',
        soil: undefined,
        notes: undefined,
        area: bedKind === 'spots' ? 1 : 4.8,
      } satisfies BedDto)
    }
    if (kind === 'plot') layout.plots.push({ id, name: labels.plot, type: 'open', shape: shape(5, 5) } satisfies PlotDto)
    if (kind === 'path') {
      const points = [
        { x, y },
        { x, y: y + 4 },
      ]
      layout.paths.push({ id, name: labels.path, material: 'woodChips', points, width: 0.8 } satisfies PathDto)
    }
    if (kind === 'feature') layout.features.push({ id, name: labels.feature, type: 'compost', shape: shape(2.2, 1) } satisfies FeatureDto)
    if (kind === 'marker') {
      const line = variant === 'line'
      layout.markers.push({
        id,
        kind: line ? 'line' : 'area',
        label: undefined,
        color: line ? '#8a7560' : '#d9d4c8',
        shape: line ? undefined : shape(2, 2),
        points: line
          ? [
              { x, y },
              { x: x + 3, y },
            ]
          : undefined,
        thickness: 0.25,
      } satisfies MarkerDto)
    }
    if (kind === 'tree') {
      layout.trees.push({
        id,
        name: labels.tree,
        cultivar: undefined,
        kind: 'tree',
        varietyId: undefined,
        x,
        y,
        outline: 'ellipse',
        crownWidth: 3,
        crownDepth: 3,
        plantedOn: undefined,
        actions: [],
        pollination: undefined,
        pollinators: undefined,
      } satisfies TreeDto)
    }
    selection.value = { kind, id }
  }

  const remove = () => {
    if (!selection.value) return
    record()
    const items = list(selection.value.kind)
    items.splice(
      items.findIndex((i) => i.id === selection.value!.id),
      1,
    )
    selection.value = undefined
  }

  const copy = () => {
    if (!selection.value || !selected.value) return false
    clipboard = { kind: selection.value.kind, item: clone(selected.value) }
    return true
  }
  /** Paste the copied item next to where it was copied (each further paste one step further) and select it. */
  const paste = () => {
    if (!clipboard || !draft.value) return
    const item = clone(clipboard.item) as Movable & { plotId?: string; code?: string; pollinators?: unknown }
    item.id = crypto.randomUUID()
    translate(item, clone(item), PASTE_OFFSET, PASTE_OFFSET)
    if (clipboard.kind === 'bed') {
      const box = boxOf(item)
      // A bed copied from another site lands in the plot under it here.
      if (!draft.value.plots.some((p) => p.id === item.plotId)) item.plotId = plotAt(box.x, box.y)?.id
      if (!item.plotId) return
      item.code = nextBedCode(draft.value.beds, item.code!.replace(/\d+$/, '') || 'N')
    }
    if (clipboard.kind === 'tree') item.pollinators = undefined
    record()
    list(clipboard.kind).push(item)
    selection.value = { kind: clipboard.kind, id: item.id }
    clipboard = { kind: clipboard.kind, item: clone(item) as LayoutItem }
  }
  const duplicate = () => copy() && paste()

  /** Items that are stacked together: plots among plots, everything else among each other. */
  const stackOf = (kind: ItemKind) =>
    kind === 'plot' ? list('plot') : (Object.keys(LISTS) as ItemKind[]).filter((k) => k !== 'plot').flatMap(list)
  /** Draw the selected item above (front) or below (back) everything it is stacked with. */
  const arrange = (to: 'front' | 'back') => {
    const sel = selection.value
    const item = sel && find(sel)
    if (!sel || !item) return
    const layers = stackOf(sel.kind)
      .filter((i) => i.id !== item.id)
      .map((i) => i.layer ?? 0)
    if (!layers.length) return
    const own = item.layer ?? 0
    const target = to === 'front' ? Math.max(...layers) + 1 : Math.min(...layers) - 1
    if (to === 'front' ? own >= target : own <= target) return
    record()
    item.layer = target
  }

  /**
   * Ctrl/Cmd+Z undoes, Ctrl+Y or Ctrl+Shift+Z redoes, Ctrl+C/V copies and pastes, Ctrl+D duplicates, Ctrl+] and Ctrl+[
   * bring to the front or back, Delete removes. Inside text fields the browser's own keys apply.
   */
  const handleKey = (e: KeyboardEvent) => {
    if ((e.target as HTMLElement | null)?.closest?.('input, textarea, select')) return
    if (!(e.ctrlKey || e.metaKey)) {
      if (e.key !== 'Delete' || !selection.value) return
      remove()
      e.preventDefault()
      return
    }
    const key = e.key.toLowerCase()
    if (key === 'z' && !e.shiftKey) undo()
    else if (key === 'y' || (key === 'z' && e.shiftKey)) redo()
    // Copied text on the page goes to the clipboard as usual.
    else if (key === 'c' && !globalThis.getSelection?.()?.toString()) {
      if (!copy()) return
    } else if (key === 'v' && clipboard) paste()
    else if (key === 'd') duplicate()
    else if (key === ']') arrange('front')
    else if (key === '[') arrange('back')
    else return
    e.preventDefault()
  }

  return {
    draft,
    selection,
    selected,
    guides,
    canUndo: computed(() => undoStack.value.length > 0),
    canRedo: computed(() => redoStack.value.length > 0),
    dirty: computed(() => undoStack.value.length > 0),
    start,
    select,
    edit,
    moveStart,
    move,
    moveEnd,
    vertexStart,
    moveVertex,
    removeVertex,
    add,
    remove,
    copy,
    paste,
    duplicate,
    arrange,
    undo,
    redo,
    handleKey,
  }
}

function nextBedCode(beds: BedDto[], prefix: string) {
  const used = new Set(beds.map((b) => b.code.toUpperCase()))
  let i = 1
  while (used.has(`${prefix}${i}`)) i++
  return `${prefix}${i}`
}
