export const LOCALE_STORAGE_KEY = 'netledger_locale'
export const DEFAULT_LOCALE = 'en'

export const SUPPORTED_LOCALES = [
  { code: 'en', englishName: 'English', nativeName: 'English', dir: 'ltr', fallback: 'en' },
  { code: 'qps-ploc', englishName: 'Pseudo', nativeName: 'Pseudo', dir: 'ltr', fallback: 'en' },
  { code: 'qps-plocm', englishName: 'Pseudo RTL', nativeName: 'Pseudo RTL', dir: 'rtl', fallback: 'en' }
]

export const LOCALES = SUPPORTED_LOCALES.map((locale) => ({
  code: locale.code,
  label: locale.nativeName,
  dir: locale.dir
}))

const LOCALE_ALIASES = {
  en_us: 'en',
  'en-us': 'en',
  pseudo: 'qps-ploc',
  rtl: 'qps-plocm'
}

export function resolveLocale(code) {
  const requested = String(code || '').trim().toLowerCase().replace('_', '-')
  if (!requested) return DEFAULT_LOCALE

  const alias = LOCALE_ALIASES[requested]
  if (alias) return alias

  const exact = SUPPORTED_LOCALES.find((locale) => locale.code === requested)
  if (exact) return exact.code

  const languageOnly = requested.split('-')[0]
  const languageMatch = SUPPORTED_LOCALES.find((locale) => locale.code === languageOnly)
  return languageMatch?.code || DEFAULT_LOCALE
}

export function localeDirection(code) {
  return SUPPORTED_LOCALES.find((locale) => locale.code === resolveLocale(code))?.dir || 'ltr'
}

export function localeForIntl(code) {
  const locale = resolveLocale(code)
  return locale.startsWith('qps-') ? DEFAULT_LOCALE : locale
}
