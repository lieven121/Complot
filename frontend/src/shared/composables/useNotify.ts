import { useToast } from 'primevue/usetoast'
import { useI18n } from 'vue-i18n'
import { toApiProblem } from '@/shared/api/problems'

/** Toasts for mutation results; API conflict codes are translated under `errors.<code>`. */
export function useNotify() {
  const toast = useToast()
  const i18n = useI18n()
  const { t } = i18n

  const success = (key: string, params: Record<string, unknown> = {}) =>
    toast.add({ severity: 'success', summary: t(key, params), life: 2500 })

  /** Something to check, not an error (e.g. an AI suggestion that needs a look). */
  const warn = (key: string, params: Record<string, unknown> = {}) => toast.add({ severity: 'warn', summary: t(key, params), life: 6000 })

  const error = (err: unknown) => {
    const problem = toApiProblem(err)
    const key =
      problem?.code && i18n.te(`errors.${problem.code}`)
        ? `errors.${problem.code}`
        : problem?.status === 400
          ? 'errors.validation'
          : 'errors.generic'
    toast.add({ severity: 'error', summary: t(key), life: 5000 })
  }

  /** An error code the API returned inside a successful response (e.g. a calendar sync that failed). */
  const errorCode = (code: string) =>
    toast.add({ severity: 'warn', summary: t(i18n.te(`errors.${code}`) ? `errors.${code}` : 'errors.generic'), life: 6000 })

  return { success, warn, error, errorCode }
}
