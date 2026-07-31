import i18next from 'i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import { initReactI18next } from 'react-i18next'
import { resources } from './resources'
import {
  DEFAULT_LOCALE,
  LOCALES,
  LOCALE_STORAGE_KEY,
  SUPPORTED_LOCALES,
  localeDirection,
  resolveLocale
} from './localeRegistry'
import {
  formatLocalizedBytes,
  formatLocalizedDate,
  formatLocalizedDateTime,
  formatLocalizedDuration,
  formatLocalizedList,
  formatLocalizedNumber,
  formatLocalizedPercent,
  formatLocalizedTime
} from './formatters'

const initialLocale = resolveLocale(localStorage.getItem(LOCALE_STORAGE_KEY) || navigator.language || DEFAULT_LOCALE)

i18next
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources,
    lng: initialLocale,
    fallbackLng: DEFAULT_LOCALE,
    supportedLngs: SUPPORTED_LOCALES.map((locale) => locale.code),
    interpolation: {
      escapeValue: false,
      prefix: '{',
      suffix: '}'
    },
    detection: {
      order: ['localStorage', 'navigator'],
      lookupLocalStorage: LOCALE_STORAGE_KEY,
      caches: ['localStorage']
    },
    returnEmptyString: false,
    returnNull: false
  })

export function applyDocumentLocale(locale) {
  const resolved = resolveLocale(locale)
  document.documentElement.lang = resolved
  document.documentElement.dir = localeDirection(resolved)
}

export async function setActiveLocale(locale) {
  const resolved = resolveLocale(locale)
  await i18next.changeLanguage(resolved)
  localStorage.setItem(LOCALE_STORAGE_KEY, resolved)
  applyDocumentLocale(resolved)
  return resolved
}

export function translate(locale, key, params = {}) {
  return i18next.getFixedT(resolveLocale(locale))(key, params)
}

applyDocumentLocale(initialLocale)

export {
  DEFAULT_LOCALE,
  LOCALES,
  LOCALE_STORAGE_KEY,
  SUPPORTED_LOCALES,
  formatLocalizedBytes,
  formatLocalizedDate,
  formatLocalizedDateTime,
  formatLocalizedDuration,
  formatLocalizedList,
  formatLocalizedNumber,
  formatLocalizedPercent,
  formatLocalizedTime,
  i18next,
  localeDirection,
  resolveLocale
}
