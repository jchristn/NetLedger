import React, { useState, useCallback, useEffect, useRef } from 'react'
import { NetLedgerApi } from '../api/api'
import { AppContext } from './AppContextInstance'

// Local storage keys
const STORAGE_KEY_SERVER_URL = 'netledger_server_url'
const STORAGE_KEY_SESSION_TOKEN = 'netledger_session_token'
const STORAGE_KEY_LEGACY_API_KEY = 'netledger_api_key'
const STORAGE_KEY_TENANT_ID = 'netledger_tenant_id'
const STORAGE_KEY_THEME = 'netledger_theme'

export function AppProvider({ children }) {
  const [isAuthenticated, setIsAuthenticated] = useState(false)
  const [isInitializing, setIsInitializing] = useState(true)
  const [serverUrl, setServerUrl] = useState('')
  const [sessionToken, setSessionToken] = useState('')
  const [tenantId, setTenantId] = useState('')
  const [api, setApi] = useState(null)
  const [theme, setTheme] = useState(() => {
    // Load theme from localStorage on initial render
    return localStorage.getItem(STORAGE_KEY_THEME) || 'light'
  })
  const [error, setError] = useState(null)
  const [serverInfo, setServerInfo] = useState(null)
  const [currentUser, setCurrentUser] = useState(null)
  const [currentTenant, setCurrentTenant] = useState(null)
  const [effectivePermissions, setEffectivePermissions] = useState(null)
  const hasAttemptedAutoLogin = useRef(false)

  const clearStoredCredentials = useCallback(() => {
    localStorage.removeItem(STORAGE_KEY_SERVER_URL)
    localStorage.removeItem(STORAGE_KEY_SESSION_TOKEN)
    localStorage.removeItem(STORAGE_KEY_LEGACY_API_KEY)
    localStorage.removeItem(STORAGE_KEY_TENANT_ID)
  }, [])

  const clearAuthenticatedState = useCallback(() => {
    setIsAuthenticated(false)
    setServerUrl('')
    setSessionToken('')
    setTenantId('')
    setApi(null)
    setServerInfo(null)
    setCurrentUser(null)
    setCurrentTenant(null)
    setEffectivePermissions(null)
  }, [])

  const handleAuthenticationFailed = useCallback(() => {
    clearStoredCredentials()
    clearAuthenticatedState()
    setError('Your session has expired. Sign in again.')
  }, [clearAuthenticatedState, clearStoredCredentials])

  const createAuthenticatedClient = useCallback((url, token, tenant) => {
    return new NetLedgerApi(url, token, tenant, {
      onAuthenticationFailed: handleAuthenticationFailed
    })
  }, [handleAuthenticationFailed])

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
    const savedSessionToken = localStorage.getItem(STORAGE_KEY_SESSION_TOKEN)
    const savedTenantId = localStorage.getItem(STORAGE_KEY_TENANT_ID) || ''
    localStorage.removeItem(STORAGE_KEY_LEGACY_API_KEY)

    if (savedServerUrl && savedSessionToken) {
      // Attempt to restore session
      const restoreSession = async () => {
        try {
          const apiClient = createAuthenticatedClient(savedServerUrl, savedSessionToken, savedTenantId)
          const info = await apiClient.getServerInfo()

          setServerUrl(savedServerUrl)
          setSessionToken(savedSessionToken)
          setTenantId(savedTenantId)
          setApi(apiClient)
          setServerInfo(info)
          try {
            const permissions = await apiClient.getEffectivePermissions()
            setEffectivePermissions(permissions)
            const principalId = permissions?.PrincipalId || permissions?.principalId
            const permissionTenantId = permissions?.TenantId || permissions?.tenantId || savedTenantId

            if (permissionTenantId) {
              try {
                const tenant = await apiClient.readTenant(permissionTenantId)
                setCurrentTenant(tenant)
              } catch (err) {
                if (err?.status === 401) throw err
                setCurrentTenant(null)
              }
            }

            if (permissionTenantId && principalId) {
              try {
                const user = await apiClient.readUser(permissionTenantId, principalId)
                setCurrentUser(user)
              } catch (err) {
                if (err?.status === 401) throw err
                setCurrentUser(null)
              }
            }
          } catch (err) {
            if (err?.status === 401) throw err
            setEffectivePermissions(null)
          }
          setIsAuthenticated(true)
        } catch (err) {
          // Clear invalid credentials
          clearStoredCredentials()
          clearAuthenticatedState()
        } finally {
          setIsInitializing(false)
        }
      }
      restoreSession()
    } else {
      setIsInitializing(false)
    }
  }, [clearAuthenticatedState, clearStoredCredentials, createAuthenticatedClient])

  const discoverTenants = useCallback(async (url, email) => {
    const normalizedUrl = url.replace(/\/+$/, '')
    const apiClient = new NetLedgerApi(normalizedUrl, '')
    return await apiClient.discoverTenants(email)
  }, [])

  const loginWithPassword = useCallback(async (url, tenant, email, password) => {
    try {
      setError(null)
      const normalizedUrl = url.replace(/\/+$/, '')
      const unauthenticatedClient = new NetLedgerApi(normalizedUrl, '')
      const loginResponse = await unauthenticatedClient.loginWithPassword(tenant, email, password)
      const session = loginResponse?.Session || loginResponse?.session
      const user = loginResponse?.User || loginResponse?.user || null
      const tenantInfo = loginResponse?.Tenant || loginResponse?.tenant || null
      const token = session?.Token || session?.token

      if (!token) {
        throw new Error('Login response did not include a session token')
      }

      const apiClient = createAuthenticatedClient(normalizedUrl, token, tenant)
      const info = await apiClient.getServerInfo()
      let permissions = null
      try {
        permissions = await apiClient.getEffectivePermissions()
      } catch (err) {
        if (err?.status === 401) throw err
        permissions = null
      }

      localStorage.setItem(STORAGE_KEY_SERVER_URL, normalizedUrl)
      localStorage.setItem(STORAGE_KEY_SESSION_TOKEN, token)
      localStorage.setItem(STORAGE_KEY_TENANT_ID, tenant)
      localStorage.removeItem(STORAGE_KEY_LEGACY_API_KEY)

      setServerUrl(normalizedUrl)
      setSessionToken(token)
      setTenantId(tenant)
      setApi(apiClient)
      setServerInfo(info)
      setCurrentUser(user)
      setCurrentTenant(tenantInfo)
      setEffectivePermissions(permissions)
      setIsAuthenticated(true)

      return { success: true }
    } catch (err) {
      const message = err.message || 'Failed to login'
      setError(message)
      return { success: false, error: message }
    }
  }, [createAuthenticatedClient])

  // Logout function
  const logout = useCallback(() => {
    // Clear credentials from localStorage
    clearStoredCredentials()
    clearAuthenticatedState()
    setError(null)
  }, [clearAuthenticatedState, clearStoredCredentials])

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
    sessionToken,
    tenantId,
    api,
    theme,
    error,
    serverInfo,
    currentUser,
    currentTenant,
    effectivePermissions,
    discoverTenants,
    loginWithPassword,
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
