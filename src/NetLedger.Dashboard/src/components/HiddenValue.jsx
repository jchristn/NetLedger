import React, { useState } from 'react'
import './HiddenValue.css'

function EyeIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
      <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z"/>
      <circle cx="12" cy="12" r="3"/>
    </svg>
  )
}

function maskValue(value, visiblePrefix = 0) {
  const text = String(value ?? '')
  if (!text) return ''
  const prefix = text.slice(0, visiblePrefix)
  return `${prefix}${'*'.repeat(Math.max(8, text.length - visiblePrefix))}`
}

function useTemporaryReveal() {
  const [revealed, setRevealed] = useState(false)

  const revealEvents = {
    onMouseDown: (event) => {
      event.preventDefault()
      setRevealed(true)
    },
    onMouseUp: () => setRevealed(false),
    onMouseLeave: () => setRevealed(false),
    onTouchStart: () => setRevealed(true),
    onTouchEnd: () => setRevealed(false),
    onTouchCancel: () => setRevealed(false),
    onKeyDown: (event) => {
      if (event.key === ' ' || event.key === 'Enter') {
        event.preventDefault()
        setRevealed(true)
      }
    },
    onKeyUp: () => setRevealed(false),
    onBlur: () => setRevealed(false)
  }

  return [revealed, revealEvents]
}

export function HiddenValueInput({ id, value, disabled, className = '', inputClassName = '', ...inputProps }) {
  const [revealed, revealEvents] = useTemporaryReveal()

  return (
    <div className={`hidden-value-control ${className}`}>
      <input
        {...inputProps}
        id={id}
        value={value}
        disabled={disabled}
        type={revealed ? 'text' : 'password'}
        className={inputClassName}
      />
      <button
        type="button"
        className="hidden-value-toggle"
        title="Hold to show value"
        aria-label="Hold to show value"
        disabled={disabled || !value}
        {...revealEvents}
      >
        <EyeIcon />
      </button>
    </div>
  )
}

export function HiddenValueDisplay({ value, as = 'code', visiblePrefix = 0, className = '', valueClassName = '' }) {
  const [revealed, revealEvents] = useTemporaryReveal()
  const text = String(value ?? '')
  const displayValue = revealed ? text : maskValue(text, visiblePrefix)
  const ValueElement = as

  return (
    <div className={`hidden-value-control hidden-value-display ${className}`}>
      <ValueElement className={valueClassName}>{displayValue || '-'}</ValueElement>
      <button
        type="button"
        className="hidden-value-toggle"
        title="Hold to show value"
        aria-label="Hold to show value"
        disabled={!text}
        {...revealEvents}
      >
        <EyeIcon />
      </button>
    </div>
  )
}
