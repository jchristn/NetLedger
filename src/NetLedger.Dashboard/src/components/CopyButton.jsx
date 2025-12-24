import React, { useState, useCallback } from 'react'
import './CopyButton.css'

export default function CopyButton({ text, title = 'Copy', size = 14, className = '', label = null }) {
  const [copied, setCopied] = useState(false)

  const handleCopy = useCallback((e) => {
    if (e) e.stopPropagation()
    if (!text) return

    navigator.clipboard.writeText(text).catch(() => {
      // Fallback for older browsers
      const textarea = document.createElement('textarea')
      textarea.value = text
      document.body.appendChild(textarea)
      textarea.select()
      document.execCommand('copy')
      document.body.removeChild(textarea)
    })

    setCopied(true)
    setTimeout(() => setCopied(false), 1500)
  }, [text])

  return (
    <button
      className={`copy-btn ${copied ? 'copy-btn-success' : ''} ${label ? 'copy-btn-with-label' : ''} ${className}`}
      onClick={handleCopy}
      title={copied ? 'Copied!' : title}
    >
      {copied ? (
        <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <polyline points="20 6 9 17 4 12"/>
        </svg>
      ) : (
        <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <rect x="9" y="9" width="13" height="13" rx="2" ry="2"/>
          <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/>
        </svg>
      )}
      {label && <span>{copied ? 'Copied!' : label}</span>}
    </button>
  )
}
