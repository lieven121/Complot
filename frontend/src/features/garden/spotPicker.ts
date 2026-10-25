import { ref } from 'vue'

export interface Point {
  x: number
  y: number
}

const bedId = ref<string>()
const point = ref<Point>()

/**
 * Placing a spot by tapping the map: the planting form starts it for its bed, the map reports the tapped point
 * (metres from the bed's top-left corner). Shared state, because the form and the map are siblings on the map page.
 */
export function useSpotPicker() {
  return {
    bedId,
    point,
    start: (id: string, initial: Point) => {
      bedId.value = id
      point.value = initial
    },
    stop: () => {
      bedId.value = undefined
      point.value = undefined
    },
  }
}
