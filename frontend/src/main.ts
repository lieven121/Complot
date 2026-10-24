import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { VueQueryPlugin } from '@tanstack/vue-query'
import PrimeVue from 'primevue/config'
import ToastService from 'primevue/toastservice'
import ConfirmationService from 'primevue/confirmationservice'
import Tooltip from 'primevue/tooltip'

import App from './App.vue'
import { router } from './app/router'
import { complotTheme } from './app/theme'
import { i18n, primeLocale } from './i18n'
import 'primeicons/primeicons.css'
import './app/styles.css'

createApp(App)
  .use(createPinia())
  .use(router)
  .use(i18n)
  .use(VueQueryPlugin, { queryClientConfig: { defaultOptions: { queries: { staleTime: 30_000, retry: 1 } } } })
  .use(PrimeVue, { theme: { preset: complotTheme, options: { darkModeSelector: false } }, locale: primeLocale() })
  .use(ToastService)
  .use(ConfirmationService)
  .directive('tooltip', Tooltip)
  .mount('#app')
