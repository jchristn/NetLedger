import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { SUPPORTED_LOCALES } from '../src/i18n/localeRegistry.js'
import { resources } from '../src/i18n/resources.js'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const root = path.resolve(__dirname, '..')

const checkedFiles = [
  'src/views/Archive.jsx',
  'src/views/Entries.jsx',
  'src/views/RequestHistory.jsx',
  'src/components/Login.jsx',
  'src/components/Topbar.jsx'
]

const blockedArchiveLiterals = [
  'No archived entries found',
  'No archived request history found',
  'Archive endpoint is not configured',
  'Archive mode queries cold entries directly from Archive Server. Mutation controls are hidden.',
  'Archive mode queries cold request history directly from Archive Server. Delete controls are hidden.'
]

function flattenKeys(value, prefix = '') {
  if (!value || typeof value !== 'object') return []

  return Object.entries(value).flatMap(([key, child]) => {
    const next = prefix ? `${prefix}.${key}` : key
    return typeof child === 'string' ? [next] : flattenKeys(child, next)
  })
}

function fail(message) {
  console.error(message)
  process.exitCode = 1
}

const englishKeys = new Set(flattenKeys(resources.en.translation))
const localeCodes = SUPPORTED_LOCALES.map((locale) => locale.code)

for (const locale of localeCodes) {
  const metadata = SUPPORTED_LOCALES.find((item) => item.code === locale)
  if (!metadata) fail(`Missing locale metadata for ${locale}`)
  if (metadata.dir !== 'ltr' && metadata.dir !== 'rtl') fail(`Invalid direction metadata for ${locale}`)
  if (!resources[locale]?.translation) fail(`Missing translation resources for ${locale}`)

  const localeKeys = new Set(flattenKeys(resources[locale]?.translation || {}))
  for (const key of englishKeys) {
    if (!localeKeys.has(key)) fail(`Missing ${locale} translation key ${key}`)
  }
}

for (const relative of checkedFiles) {
  const absolute = path.join(root, relative)
  const source = fs.readFileSync(absolute, 'utf8')
  if (source.includes('\uFFFD')) fail(`${relative} contains replacement characters`)

  const keyPattern = /\bt\(\s*['"`]([^'"`]+)['"`]/g
  for (const match of source.matchAll(keyPattern)) {
    const key = match[1]
    if (!key.includes('${') && !key.endsWith('.') && !englishKeys.has(key)) {
      fail(`${relative} references missing translation key ${key}`)
    }
  }

  if (relative !== 'src/i18n/resources.js') {
    for (const literal of blockedArchiveLiterals) {
      if (source.includes(literal)) fail(`${relative} contains archive literal "${literal}"`)
    }
  }
}

const archiveSource = fs.readFileSync(path.join(root, 'src/views/Archive.jsx'), 'utf8')
if (archiveSource.includes('window.confirm(') || archiveSource.includes('window.alert(') || archiveSource.includes('window.prompt(')) {
  fail('Archive.jsx must use dashboard modal components instead of browser dialogs')
}

if (!localeCodes.includes('qps-ploc') || !localeCodes.includes('qps-plocm')) {
  fail('Pseudo-locales qps-ploc and qps-plocm must be registered')
}

if (!process.exitCode) {
  console.log('Archive dashboard i18n checks passed')
}
