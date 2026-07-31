import { localeForIntl } from './localeRegistry'

export function formatLocalizedNumber(locale, value) {
  const number = Number(value ?? 0)
  return new Intl.NumberFormat(localeForIntl(locale)).format(Number.isFinite(number) ? number : 0)
}

export function formatLocalizedDate(locale, value) {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '-'
  return new Intl.DateTimeFormat(localeForIntl(locale), {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    timeZone: 'UTC'
  }).format(date)
}

export function formatLocalizedTime(locale, value) {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '-'
  return new Intl.DateTimeFormat(localeForIntl(locale), {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    timeZone: 'UTC',
    timeZoneName: 'short'
  }).format(date)
}

export function formatLocalizedDateTime(locale, value) {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '-'
  return new Intl.DateTimeFormat(localeForIntl(locale), {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    timeZone: 'UTC',
    timeZoneName: 'short'
  }).format(date)
}

export function formatLocalizedPercent(locale, value) {
  const number = Number(value ?? 0)
  return new Intl.NumberFormat(localeForIntl(locale), { style: 'percent', maximumFractionDigits: 2 }).format(Number.isFinite(number) ? number : 0)
}

export function formatLocalizedBytes(locale, value) {
  const number = Number(value ?? 0)
  if (!Number.isFinite(number) || number <= 0) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB', 'TB', 'PB']
  const index = Math.min(units.length - 1, Math.floor(Math.log(number) / Math.log(1024)))
  const scaled = number / Math.pow(1024, index)
  return `${new Intl.NumberFormat(localeForIntl(locale), { maximumFractionDigits: index === 0 ? 0 : 1 }).format(scaled)} ${units[index]}`
}

export function formatLocalizedDuration(locale, value) {
  if (value === null || value === undefined || value === '') return '-'
  const number = Number(value)
  return `${new Intl.NumberFormat(localeForIntl(locale), { maximumFractionDigits: 1 }).format(Number.isFinite(number) ? number : 0)} ms`
}

export function formatLocalizedList(locale, values) {
  return new Intl.ListFormat(localeForIntl(locale), { style: 'long', type: 'conjunction' }).format(values || [])
}
