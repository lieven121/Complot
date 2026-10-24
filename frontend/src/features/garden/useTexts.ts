import { useI18n } from 'vue-i18n'
import type { PlantingWarningDto, TaskDto } from '@/shared/api'
import { useFormat } from '@/shared/composables/useFormat'

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/

/** Translates rule findings and task titles, which the API sends as codes with parameters. */
export function useTexts() {
  const { t } = useI18n()
  const { date, month } = useFormat()

  const warning = (w: PlantingWarningDto) => {
    const params: Record<string, unknown> = {}
    for (const [key, value] of Object.entries(w.params ?? {})) {
      if (typeof value === 'string' && ISO_DATE.test(value)) params[key] = date(value)
      else if (key === 'bedSun') params[key] = t(`sun.${String(value)}`)
      else if (w.code === 'outsideWindow' && (key === 'from' || key === 'to')) params[key] = month(Number(value))
      else params[key] = value
    }
    return t(`checks.${w.code}`, params)
  }

  const task = (task: Pick<TaskDto, 'kind' | 'subject' | 'title'>) =>
    t(`tasks.kind.${task.kind}`, { subject: task.subject ?? '', title: task.title ?? '' })

  return { warning, task }
}
