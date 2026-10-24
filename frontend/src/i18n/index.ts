import type { PrimeVueLocaleOptions } from 'primevue/config'
import { createI18n } from 'vue-i18n'
import primeEn from 'primelocale/en.json'
import primeNl from 'primelocale/nl.json'
import en from './locales/en.json'
import nlBE from './locales/nl-BE.json'

export const SUPPORTED_LOCALES = ['nl-BE', 'en'] as const
export type AppLocale = (typeof SUPPORTED_LOCALES)[number]

const STORAGE_KEY = 'complot.locale'

function isAppLocale(value: unknown): value is AppLocale {
  return SUPPORTED_LOCALES.includes(value as AppLocale)
}

function storedLocale(): AppLocale {
  try {
    const value = localStorage.getItem(STORAGE_KEY)
    if (isAppLocale(value)) return value
  } catch {
    // storage unavailable (private mode): fall back to the default
  }
  return 'nl-BE'
}

export function rememberLocale(locale: AppLocale) {
  try {
    localStorage.setItem(STORAGE_KEY, locale)
  } catch {
    // ignore: the choice just won't survive a reload
  }
}

export type MessageSchema = typeof nlBE

const numberFormats = {
  currency: { style: 'currency', currency: 'EUR' },
  decimal: { maximumFractionDigits: 2 },
} as const

export const i18n = createI18n<[MessageSchema], AppLocale, false>({
  legacy: false,
  locale: storedLocale(),
  fallbackLocale: 'nl-BE',
  messages: { 'nl-BE': nlBE, en },
  numberFormats: { 'nl-BE': numberFormats, en: numberFormats },
})

const primeLocales = { 'nl-BE': primeNl.nl, en: primeEn.en } as unknown as Record<AppLocale, PrimeVueLocaleOptions>

/** PrimeVue's own texts (date picker, paginator, …) for the given app locale. */
export function primeLocale(locale: AppLocale = i18n.global.locale.value): PrimeVueLocaleOptions {
  return primeLocales[locale]
}
