import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ErrorDetail } from '@/shared/api'
import type { ApiProblem } from '@/shared/api/problems'

/**
 * Validation errors from the API, translated per form field. The API sends codes ("notEmpty", "inclusiveBetween")
 * with parameters; messages live in i18n under `validation.<code>`.
 */
export function useServerErrors() {
  const i18n = useI18n()
  const { t } = i18n
  const errors = ref<Record<string, ErrorDetail[]>>({})

  const message = (detail: ErrorDetail) =>
    i18n.te(`validation.${detail.code}`) ? t(`validation.${detail.code}`, detail.params ?? {}) : t('validation.invalid')

  /** Errors for a field path, including nested ones ("harvest" also shows "harvest.to"). */
  const field = (path: string): string | undefined => {
    const details = Object.entries(errors.value)
      .filter(([key]) => key === path || key.startsWith(`${path}.`) || key.startsWith(`${path}[`))
      .flatMap(([, list]) => list)
    return details.length ? [...new Set(details.map(message))].join(' ') : undefined
  }

  return {
    errors,
    message,
    field,
    set: (problem?: ApiProblem) => (errors.value = problem?.errors ?? {}),
    clear: () => (errors.value = {}),
  }
}
