import type { NoteDto, NoteTag } from '@/shared/api'

export const NOTE_TAGS: readonly NoteTag[] = ['observation', 'harvest', 'disease', 'pest', 'soil', 'weather']
export const TAG_ICON: Record<NoteTag, string> = {
  observation: '👁️',
  harvest: '🧺',
  disease: '🦠',
  pest: '🐛',
  soil: '🪱',
  weather: '🌦️',
}

/** "14 kg · 12 st." — harvest notes summed per unit. */
export function harvestSummary(notes: NoteDto[], format: (value: number) => string): string {
  const totals = new Map<string, number>()
  for (const note of notes) {
    if (note.harvestQuantity && note.harvestUnit) totals.set(note.harvestUnit, (totals.get(note.harvestUnit) ?? 0) + note.harvestQuantity)
  }
  return [...totals].map(([unit, total]) => `${format(total)} ${unit}`).join(' · ')
}
