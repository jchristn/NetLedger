import React from 'react'
import { LOCALES } from './localeRegistry'

export default function LanguageSelector({
  id = 'languageSelect',
  locale,
  onChange,
  label = 'Language',
  className = '',
  showLabel = true
}) {
  return (
    <label className={className} htmlFor={id} title={label}>
      {showLabel && <span>{label}</span>}
      <select
        id={id}
        value={locale}
        onChange={(event) => onChange(event.target.value)}
        aria-label={label}
      >
        {LOCALES.map((option) => (
          <option key={option.code} value={option.code}>
            {option.label}
          </option>
        ))}
      </select>
    </label>
  )
}
