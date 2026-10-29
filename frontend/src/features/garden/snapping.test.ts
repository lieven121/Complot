import { describe, expect, it } from 'vite-plus/test'
import { alignBox } from './snapping'

describe('alignBox', () => {
  const other = { x: 2, y: 0, w: 1, h: 4 }

  it('lines up the nearest edge or centre within the tolerance, with a guide over both', () => {
    const fit = alignBox({ x: 0.5, y: 5.03, w: 1.6, h: 1 }, [other], { tolerance: 0.2 })
    // The right edge (2.1) meets the other's left edge (2); its centre (1.3) is not near anything.
    expect(fit.dx).toBeCloseTo(-0.1)
    expect(fit.guides).toEqual([{ x1: 2, x2: 2, y1: 0, y2: 6 }])
    // Not lined up on y: the corner snaps to the 10 cm grid.
    expect(fit.dy).toBeCloseTo(-0.03)
  })

  it('snaps to the grid by the anchor, or to centimetres without the grid or when free', () => {
    expect(alignBox({ x: 0.33, y: 0.47, w: 1, h: 1 }, [], {}, { x: 0.83, y: 0.97 }).dx).toBeCloseTo(-0.03)
    expect(alignBox({ x: 0.333, y: 0, w: 0, h: 0 }, [], { grid: false }).dx).toBeCloseTo(-0.003)
    expect(alignBox({ x: 1.97, y: 0, w: 0, h: 0 }, [other], { free: true, tolerance: 1 })).toMatchObject({ dx: 0, guides: [] })
  })
})
