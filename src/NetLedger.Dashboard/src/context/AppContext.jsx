import React, { createContext, useContext, useState, useCallback, useEffect, useRef } from 'react'
import { NetLedgerApi } from '../api/api'

const AppContext = createContext(null)

// Local storage keys
const STORAGE_KEY_SERVER_URL = 'netledger_server_url'
const STORAGE_KEY_API_KEY = 'netledger_api_key'
const STORAGE_KEY_THEME = 'netledger_theme'

export function AppProvider({ children }) {
  const [isAuthenticated, setIsAuthenticated] = useState(false)
  const [isInitializing, setIsInitializing] = useState(true)
  const [serverUrl, setServerUrl] = useState('')
  const [apiKey, setApiKey] = useState('')
  const [api, setApi] = useState(null)
  const [theme, setTheme] = useState(() => {
    // Load theme from localStorage on initial render
    return localStorage.getItem(STORAGE_KEY_THEME) || 'light'
  })
  const [error, setError] = useState(null)
  const [serverInfo, setServerInfo] = useState(null)
  const hasAttemptedAutoLogin = useRef(false)

  // Apply theme to document
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme)
    localStorage.setItem(STORAGE_KEY_THEME, theme)
  }, [theme])

  // Auto-login from localStorage on mount
  useEffect(() => {
    if (hasAttemptedAutoLogin.current) return
    hasAttemptedAutoLogin.current = true

    const savedServerUrl = localStorage.getItem(STORAGE_KEY_SERVER_URL)
    const savedApiKey = localStorage.getItem(STORAGE_KEY_API_KEY)

    if (savedServerUrl && savedApiKey) {
      // Attempt to restore session
      const restoreSession = async () => {
        try {
          const apiClient = new NetLedgerApi(savedServerUrl, savedApiKey)
          const info = await apiClient.getServerInfo()

          setServerUrl(savedServerUrl)
          setApiKey(savedApiKey)
          setApi(apiClient)
          setServerInfo(info)
          setIsAuthenticated(true)
        } catch (err) {
          // Clear invalid credentials
          localStorage.removeItem(STORAGE_KEY_SERVER_URL)
          localStorage.removeItem(STORAGE_KEY_API_KEY)
        } finally {
          setIsInitializing(false)
        }
      }
      restoreSession()
    } else {
      setIsInitializing(false)
    }
  }, [])

  // Login function
  const login = useCallback(async (url, key) => {
    try {
      setError(null)
      const normalizedUrl = url.replace(/\/+$/, '')
      const apiClient = new NetLedgerApi(normalizedUrl, key)

      // Test the connection by fetching server info
      const info = await apiClient.getServerInfo()

      // Save credentials to localStorage
      localStorage.setItem(STORAGE_KEY_SERVER_URL, normalizedUrl)
      localStorage.setItem(STORAGE_KEY_API_KEY, key)

      setServerUrl(normalizedUrl)
      setApiKey(key)
      setApi(apiClient)
      setServerInfo(info)
      setIsAuthenticated(true)

      return { success: true }
    } catch (err) {
      const message = err.message || 'Failed to connect to server'
      setError(message)
      return { success: false, error: message }
    }
  }, [])

  // Logout function
  const logout = useCallback(() => {
    // Clear credentials from localStorage
    localStorage.removeItem(STORAGE_KEY_SERVER_URL)
    localStorage.removeItem(STORAGE_KEY_API_KEY)

    setIsAuthenticated(false)
    setServerUrl('')
    setApiKey('')
    setApi(null)
    setServerInfo(null)
    setError(null)
  }, [])

  // Toggle theme
  const toggleTheme = useCallback(() => {
    setTheme(prevTheme => prevTheme === 'light' ? 'dark' : 'light')
  }, [])

  // Clear error
  const clearError = useCallback(() => {
    setError(null)
  }, [])

  // Set error (for use by components)
  const setAppError = useCallback((message) => {
    setError(message)
    // Auto-dismiss after 8 seconds
    setTimeout(() => {
      setError(prevError => prevError === message ? null : prevError)
    }, 8000)
  }, [])

  const value = {
    isAuthenticated,
    isInitializing,
    serverUrl,
    apiKey,
    api,
    theme,
    error,
    serverInfo,
    login,
    logout,
    toggleTheme,
    clearError,
    setError: setAppError
  }

  return (
    <AppContext.Provider value={value}>
      {children}
    </AppContext.Provider>
  )
}

export function useApp() {
  const context = useContext(AppContext)
  if (!context) {
    throw new Error('useApp must be used within an AppProvider')
  }
  return context
}
