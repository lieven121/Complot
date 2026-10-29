import type { Box } from './map'

/** Positions snap to a 10 cm grid. */
export const snap = (value: number) => Math.round(value * 10) / 10

/** Rounds to whole centimetres: what is left of a position once snapping is off. */
export const cm = (value: number) => Math.round(value * 100) / 100

/** A line drawn while an item lines up with another one's edge or centre. */
export interface Guide {
  x1: number
  y1: number
  x2: number
  y2: number
}

/**
 * How a dragged item snaps: with `free` (Alt held) not at all; otherwise an edge or centre within `tolerance` m of
 * another item's edge or centre lines up with it, and else (with `grid`, the default) it snaps to the 10 cm grid.
 */
export interface SnapOptions {
  free?: boolean
  tolerance?: number
  grid?: boolean
}

interface AxisSnap {
  delta: number
  at?: number
}

/** The nearest line-up on one axis: the box's start, centre or end against the targets' start, centre or end. */
function alignAxis(start: number, size: number, targets: number[], tolerance: number): AxisSnap | undefined {
  let best: AxisSnap | undefined
  for (const own of [start, start + size / 2, start + size])
    for (const at of targets) {
      const delta = at - own
      if (Math.abs(delta) <= tolerance && (!best || Math.abs(delta) < Math.abs(best.delta))) best = { delta, at }
    }
  return best
}

const edges = (start: number, size: number) => [start, start + size / 2, start + size]

/**
 * How far to shift `box` so it snaps (see {@link SnapOptions}), and the guide lines to draw for the edges it lines
 * up with. On the grid, `anchor` is the point that lands on it (a tree's trunk; by default the box's corner).
 */
export function alignBox(
  box: Box,
  targets: Box[],
  { free, tolerance = 0, grid = true }: SnapOptions = {},
  anchor = { x: box.x, y: box.y },
): { dx: number; dy: number; guides: Guide[] } {
  if (free) return { dx: cm(anchor.x) - anchor.x, dy: cm(anchor.y) - anchor.y, guides: [] }
  const xs = targets.flatMap((t) => edges(t.x, t.w))
  const ys = targets.flatMap((t) => edges(t.y, t.h))
  const fallback = (value: number) => (grid ? snap(value) : cm(value)) - value
  const sx = alignAxis(box.x, box.w, xs, tolerance) ?? { delta: fallback(anchor.x) }
  const sy = alignAxis(box.y, box.h, ys, tolerance) ?? { delta: fallback(anchor.y) }
  const moved = { ...box, x: box.x + sx.delta, y: box.y + sy.delta }

  // A guide runs along the lined-up edge, over the moved box and every target that has an edge there.
  const guides: Guide[] = []
  const on = (at: number, start: number, size: number) => edges(start, size).some((v) => Math.abs(v - at) < 1e-6)
  if (sx.at !== undefined) {
    const span = [moved, ...targets.filter((t) => on(sx.at!, t.x, t.w))]
    guides.push({ x1: sx.at, x2: sx.at, y1: Math.min(...span.map((b) => b.y)), y2: Math.max(...span.map((b) => b.y + b.h)) })
  }
  if (sy.at !== undefined) {
    const span = [moved, ...targets.filter((t) => on(sy.at!, t.y, t.h))]
    guides.push({ y1: sy.at, y2: sy.at, x1: Math.min(...span.map((b) => b.x)), x2: Math.max(...span.map((b) => b.x + b.w)) })
  }
  return { dx: sx.delta, dy: sy.delta, guides }
}
