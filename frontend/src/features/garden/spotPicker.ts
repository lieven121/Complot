import { ref } from 'vue'

export interface Point {
  x: number
  y: number
}

/**
 * The spot being planned or edited, drawn live on the map: the crop, how many plants, and the block they form
 * (`columns` plants wide, rows north–south when `vertical`; no columns = a round cluster). `plantingId` is set when
 * an existing planting is edited: the map then draws that planting as the draft.
 */
export interface SpotDraft {
  plantingId?: string
  varietyId?: string
  count: number
  columns?: number
  vertical: boolean
}

const bedId = ref<string>()
const point = ref<Point>()
const draft = ref<SpotDraft>()

/**
 * Placing a spot by tapping the map: a planting form starts it for its bed, the map reports the tapped point
 * (metres from the bed's top-left corner). Shared state, because the forms and the map are siblings on the map page.
 */
export function useSpotPicker() {
  return {
    bedId,
    point,
    draft,
    start: (id: string, initial: Point) => {
      bedId.value = id
      point.value = initial
    },
    stop: () => {
      bedId.value = undefined
      point.value = undefined
      draft.value = undefined
    },
  }
}
