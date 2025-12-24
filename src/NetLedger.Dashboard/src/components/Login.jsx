import React, { useState } from 'react'
import { useApp } from '../context/AppContext'
import './Login.css'

export default function Login() {
  const { login, theme, toggleTheme } = useApp()
  const [serverUrl, setServerUrl] = useState(() => {
    // Pre-fill server URL from localStorage if available
    return localStorage.getItem('netledger_server_url') || 'http://localhost:8080'
  })
  const [apiKey, setApiKey] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState(null)

  const handleSubmit = async (e) => {
    e.preventDefault()
    setError(null)

    if (!serverUrl.trim()) {
      setError('Server URL is required')
      return
    }

    if (!apiKey.trim()) {
      setError('API Key is required')
      return
    }

    setIsLoading(true)

    try {
      const result = await login(serverUrl.trim(), apiKey.trim())
      if (!result.success) {
        setError(result.error || 'Failed to connect')
      }
    } catch (err) {
      setError(err.message || 'An unexpected error occurred')
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="login-container">
      <div className="login-card">
        <div className="login-header">
          <div className="login-logo">
            <img src="/favicon.ico" alt="NetLedger" className="login-logo-img" />
          </div>
          <h1 className="login-title">NetLedger</h1>
          <p className="login-subtitle">Connect to your ledger server</p>
        </div>

        <form onSubmit={handleSubmit} className="login-form">
          {error && (
            <div className="login-error">
              <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor">
                <path d="M8 15A7 7 0 1 1 8 1a7 7 0 0 1 0 14zm0-1A6 6 0 1 0 8 2a6 6 0 0 0 0 12zM8 4a.75.75 0 0 1 .75.75v3.5a.75.75 0 0 1-1.5 0v-3.5A.75.75 0 0 1 8 4zm0 8a1 1 0 1 1 0-2 1 1 0 0 1 0 2z"/>
              </svg>
              <span>{error}</span>
            </div>
          )}

          <div className="form-group">
            <label htmlFor="serverUrl">Server URL</label>
            <input
              type="url"
              id="serverUrl"
              value={serverUrl}
              onChange={(e) => setServerUrl(e.target.value)}
              placeholder="http://localhost:8080"
              disabled={isLoading}
              autoComplete="url"
            />
            <span className="form-hint">The URL of your NetLedger server</span>
          </div>

          <div className="form-group">
            <label htmlFor="apiKey">API Key</label>
            <input
              type="password"
              id="apiKey"
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              placeholder="Enter your API key"
              disabled={isLoading}
              autoComplete="current-password"
            />
            <span className="form-hint">Your authentication API key</span>
          </div>

          <button
            type="submit"
            className="btn btn-primary login-submit"
            disabled={isLoading}
          >
            {isLoading ? (
              <>
                <span className="spinner spinner-sm"></span>
                Connecting...
              </>
            ) : (
              'Connect'
            )}
          </button>
        </form>

        <div className="login-footer">
          <button
            type="button"
            className="btn btn-ghost login-theme-toggle"
            onClick={toggleTheme}
            title={`Switch to ${theme === 'light' ? 'dark' : 'light'} mode`}
          >
            {theme === 'light' ? (
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/>
              </svg>
            ) : (
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <circle cx="12" cy="12" r="5"/>
                <line x1="12" y1="1" x2="12" y2="3"/>
                <line x1="12" y1="21" x2="12" y2="23"/>
                <line x1="4.22" y1="4.22" x2="5.64" y2="5.64"/>
                <line x1="18.36" y1="18.36" x2="19.78" y2="19.78"/>
                <line x1="1" y1="12" x2="3" y2="12"/>
                <line x1="21" y1="12" x2="23" y2="12"/>
                <line x1="4.22" y1="19.78" x2="5.64" y2="18.36"/>
                <line x1="18.36" y1="5.64" x2="19.78" y2="4.22"/>
              </svg>
            )}
          </button>
        </div>
      </div>
    </div>
  )
}
