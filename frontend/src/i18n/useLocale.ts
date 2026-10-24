import { computed } from 'vue'
import { usePrimeVue } from 'primevue/config'
import { type AppLocale, i18n, primeLocale, rememberLocale, SUPPORTED_LOCALES } from '.'

export function useLocale() {
  const primevue = usePrimeVue()

  const locale = computed<AppLocale>({
    get: () => i18n.global.locale.value,
    set: (value) => {
      i18n.global.locale.value = value
      primevue.config.locale = primeLocale(value)
      document.documentElement.lang = value
      rememberLocale(value)
    },
  })

  return { locale, locales: SUPPORTED_LOCALES }
}
