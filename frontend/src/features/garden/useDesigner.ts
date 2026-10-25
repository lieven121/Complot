import { computed, ref } from 'vue'
import type { BedDto, BedKind, FeatureDto, MarkerDto, MarkerKind, PathDto, PlotDto, ShapeDto, SiteLayoutDto, TreeDto } from '@/shared/api'

export type ItemKind = 'plot' | 'bed' | 'path' | 'feature' | 'tree' | 'marker'
export type LayoutItem = PlotDto | BedDto | PathDto | FeatureDto | TreeDto | MarkerDto
export interface Selection {
  kind: ItemKind
  id: string
}

const LISTS = { plot: 'plots', bed: 'beds', path: 'paths', feature: 'features', tree: 'trees', marker: 'markers' } as const
const HISTORY_LIMIT = 100

/** Positions snap to a 10 cm grid. */
export const snap = (value: number) => Math.round(value * 10) / 10

const clone = (layout: SiteLayoutDto): SiteLayoutDto => JSON.parse(JSON.stringify(layout)) as SiteLayoutDto

/**
 * Editing state of the designer: a local copy of the layout with undo/redo. Nothing reaches the API until the
 * whole layout is saved, so undo/redo is purely local and a cancel simply discards the copy.
 */
export function useDesigner() {
  const draft = ref<SiteLayoutDto>()
  const selection = ref<Selection>()
  const undoStack = ref<string[]>([])
  const redoStack = ref<string[]>([])
  /** Consecutive edits of the same field (typing "2", "2.5", "2.75") form one undo step. */
  let lastEditKey: string | undefined
  let dragOrigin: { x: number; y: number } | undefined

  const snapshot = () => JSON.stringify(draft.value)
  const record = () => {
    undoStack.value.push(snapshot())
    if (undoStack.value.length > HISTORY_LIMIT) undoStack.value.shift()
    redoStack.value = []
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

  const list = (kind: ItemKind) => draft.value![LISTS[kind]] as { id: string }[]
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

  /** Change the selected item; `field` groups keystrokes of one field into one undo step. */
  const edit = (field: string, change: () => void) => {
    const key = `${selection.value?.id}:${field}`
    if (key !== lastEditKey) record()
    lastEditKey = key
    change()
  }

  const position = (id: string) => {
    const item = find({ kind: kindOf(id)!, id }) as Partial<TreeDto & PlotDto>
    return item.shape ? { x: item.shape.x, y: item.shape.y } : { x: item.x!, y: item.y! }
  }
  const moveStart = (id: string) => {
    record()
    lastEditKey = undefined
    select(id)
    dragOrigin = position(id)
  }
  const move = (id: string, dx: number, dy: number) => {
    if (!dragOrigin) return
    const item = find({ kind: kindOf(id)!, id }) as Partial<TreeDto & PlotDto>
    const x = snap(dragOrigin.x + dx)
    const y = snap(dragOrigin.y + dy)
    if (item.shape) item.shape = { ...item.shape, x, y }
    else Object.assign(item, { x, y })
  }
  const moveEnd = () => (dragOrigin = undefined)

  /**
   * Add an item at a point. A bed is a 1.2 × 4 m strips bed, or with `variant` 'spots' a 1 × 1 m spots bed (code M…)
   * for mixed plantings; a marker is a coloured area or, with `variant` 'line', a 3 m wall.
   */
  function add(
    kind: ItemKind,
    at: { x: number; y: number },
    labels: { path: string; tree: string; plot: string; feature: string },
    variant?: BedKind | MarkerKind,
  ) {
    record()
    lastEditKey = undefined
    const id = crypto.randomUUID()
    const x = snap(at.x)
    const y = snap(at.y)
    const shape = (width: number, height: number, kind: ShapeDto['kind'] = 'rect'): ShapeDto => ({ kind, x, y, width, height })
    const layout = draft.value!
    const bedKind: BedKind = variant === 'spots' ? 'spots' : 'strips'
    if (kind === 'bed') {
      const plot =
        layout.plots.find((p) => x >= p.shape.x && x <= p.shape.x + p.shape.width && y >= p.shape.y && y <= p.shape.y + p.shape.height) ??
        layout.plots[0]
      if (!plot) return
      layout.beds.push({
        id,
        plotId: plot.id,
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
    if (kind === 'path') layout.paths.push({ id, name: labels.path, material: 'woodChips', shape: shape(0.8, 4) } satisfies PathDto)
    if (kind === 'feature') layout.features.push({ id, name: labels.feature, type: 'compost', shape: shape(2.2, 1) } satisfies FeatureDto)
    if (kind === 'marker') {
      const line = variant === 'line'
      layout.markers.push({
        id,
        kind: line ? 'line' : 'area',
        label: undefined,
        color: line ? '#8a7560' : '#d9d4c8',
        shape: line ? shape(3, 0) : shape(2, 2),
        thickness: 0.25,
      } satisfies MarkerDto)
    }
    if (kind === 'tree') {
      layout.trees.push({
        id,
        name: labels.tree,
        cultivar: undefined,
        kind: 'tree',
        x,
        y,
        crownDiameter: 3,
        plantedOn: undefined,
        actions: [],
      } satisfies TreeDto)
    }
    selection.value = { kind, id }
  }

  const remove = () => {
    if (!selection.value) return
    record()
    lastEditKey = undefined
    const items = list(selection.value.kind)
    items.splice(
      items.findIndex((i) => i.id === selection.value!.id),
      1,
    )
    selection.value = undefined
  }

  /** Ctrl/Cmd+Z undoes, Ctrl+Y or Ctrl+Shift+Z redoes; inside text fields the browser's own undo applies. */
  const handleKey = (e: KeyboardEvent) => {
    if (!(e.ctrlKey || e.metaKey) || (e.target as HTMLElement | null)?.closest?.('input, textarea, select')) return
    const key = e.key.toLowerCase()
    if (key === 'z' && !e.shiftKey) undo()
    else if (key === 'y' || (key === 'z' && e.shiftKey)) redo()
    else return
    e.preventDefault()
  }

  return {
    draft,
    selection,
    selected,
    canUndo: computed(() => undoStack.value.length > 0),
    canRedo: computed(() => redoStack.value.length > 0),
    dirty: computed(() => undoStack.value.length > 0),
    start,
    select,
    edit,
    moveStart,
    move,
    moveEnd,
    add,
    remove,
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
