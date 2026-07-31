import React, { useState } from 'react'
import { useApp } from '../context/useApp'
import { HiddenValueDisplay, HiddenValueInput } from './HiddenValue'
import LanguageSelector from '../i18n/LanguageSelector'
import './Login.css'

const LOGIN_STEP_EMAIL = 'email'
const LOGIN_STEP_TENANT = 'tenant'
const LOGIN_STEP_PASSWORD = 'password'

function getConfiguredServerUrl() {
  const configuredUrl = window.NETLEDGER_CONFIG?.serverUrl?.trim()
  if (configuredUrl) return configuredUrl
  return localStorage.getItem('netledger_server_url') || 'http://localhost:8080'
}

function getTenantId(tenant) {
  return tenant?.Id || tenant?.id || ''
}

function getTenantName(tenant) {
  return tenant?.Name || tenant?.name || getTenantId(tenant)
}

function getTenantLabel(tenant) {
  const id = getTenantId(tenant)
  const name = getTenantName(tenant)
  return id && name !== id ? `${name} (${id})` : name
}

export default function Login() {
  const { discoverTenants, loginWithPassword, theme, toggleTheme, locale, setLocale, t } = useApp()
  const [serverUrl, setServerUrl] = useState(() => {
    return getConfiguredServerUrl()
  })
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [tenantId, setTenantId] = useState('')
  const [tenantOptions, setTenantOptions] = useState([])
  const [step, setStep] = useState(LOGIN_STEP_EMAIL)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState(null)

  const resetDiscovery = () => {
    setTenantId('')
    setTenantOptions([])
    setPassword('')
    setStep(LOGIN_STEP_EMAIL)
    setError(null)
  }

  const handleEmailContinue = async () => {
    if (!serverUrl.trim()) {
      setError(t('login.serverRequired'))
      return
    }

    if (!email.trim()) {
      setError(t('login.emailRequired'))
      return
    }

    setIsLoading(true)

    try {
      const tenants = await discoverTenants(serverUrl.trim(), email.trim())
      const matches = Array.isArray(tenants) ? tenants : []

      if (matches.length === 0) {
        setError(t('login.noTenant'))
        return
      }

      setTenantOptions(matches)

      if (matches.length === 1) {
        setTenantId(getTenantId(matches[0]))
        setStep(LOGIN_STEP_PASSWORD)
        return
      }

      const savedTenantId = localStorage.getItem('netledger_tenant_id') || ''
      const savedTenantIsAvailable = matches.some((tenant) => getTenantId(tenant) === savedTenantId)
      setTenantId(savedTenantIsAvailable ? savedTenantId : '')
      setStep(LOGIN_STEP_TENANT)
    } catch (err) {
      setError(err.message || t('login.unableDiscover'))
    } finally {
      setIsLoading(false)
    }
  }

  const handleTenantContinue = () => {
    if (!tenantId.trim()) {
      setError(t('login.tenantRequired'))
      return
    }

    setError(null)
    setStep(LOGIN_STEP_PASSWORD)
  }

  const handlePasswordLogin = async () => {
    if (!password) {
      setError(t('login.passwordRequired'))
      return
    }

    setIsLoading(true)

    try {
      const result = await loginWithPassword(serverUrl.trim(), tenantId.trim(), email.trim(), password)
      if (!result.success) {
        setError(result.error || t('common.error'))
      }
    } catch (err) {
      setError(err.message || t('login.unexpected'))
    } finally {
      setIsLoading(false)
    }
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    setError(null)

    if (step === LOGIN_STEP_EMAIL) {
      await handleEmailContinue()
      return
    }

    if (step === LOGIN_STEP_TENANT) {
      handleTenantContinue()
      return
    }

    await handlePasswordLogin()
  }

  const selectedTenant = tenantOptions.find((tenant) => getTenantId(tenant) === tenantId)
  const submitLabel = step === LOGIN_STEP_PASSWORD ? t('common.connect') : t('common.continue')

  return (
    <div className="login-container">
      <div className="login-shell">
        <div className="login-card">
          <div className="login-header">
            <div className="login-logo">
              <img src="/favicon.ico" alt="NetLedger" className="login-logo-img" />
            </div>
            <h1 className="login-title">NetLedger</h1>
            <p className="login-subtitle">{t('login.subtitle')}</p>
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
              <label htmlFor="serverUrl">{t('login.serverUrl')}</label>
              <input
                type="url"
                id="serverUrl"
                value={serverUrl}
                onChange={(e) => {
                  setServerUrl(e.target.value)
                  resetDiscovery()
                }}
                placeholder="http://localhost:8080"
                disabled={isLoading || step !== LOGIN_STEP_EMAIL}
                autoComplete="url"
              />
              <span className="form-hint">{t('login.serverHint')}</span>
            </div>

            <div className="form-group">
              <label htmlFor="email">{t('login.email')}</label>
              <input
                type="email"
                id="email"
                value={email}
                onChange={(e) => {
                  setEmail(e.target.value)
                  resetDiscovery()
                }}
                placeholder="user@example.com"
                disabled={isLoading || step !== LOGIN_STEP_EMAIL}
                autoComplete="email"
              />
            </div>

            {step === LOGIN_STEP_TENANT && (
              <div className="form-group">
                <label htmlFor="tenantSelect">{t('login.tenant')}</label>
                <select
                  id="tenantSelect"
                  value={tenantId}
                  onChange={(e) => setTenantId(e.target.value)}
                  disabled={isLoading}
                >
                  <option value="">{t('login.tenantSelect')}</option>
                  {tenantOptions.map((tenant) => (
                    <option key={getTenantId(tenant)} value={getTenantId(tenant)}>
                      {getTenantLabel(tenant)}
                    </option>
                  ))}
                </select>
                <span className="form-hint">{t('login.tenantHint')}</span>
              </div>
            )}

            {step === LOGIN_STEP_PASSWORD && (
              <>
                <div className="login-step-summary">
                  <span>{t('login.email')}</span>
                  <strong>{email.trim()}</strong>
                  <span>{t('login.tenant')}</span>
                  <strong>{selectedTenant ? getTenantLabel(selectedTenant) : tenantId}</strong>
                </div>

                <div className="form-group">
                  <label htmlFor="password">{t('login.password')}</label>
                  <HiddenValueInput
                    id="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    placeholder={t('login.passwordPlaceholder')}
                    disabled={isLoading}
                    autoComplete="current-password"
                    autoFocus
                  />
                </div>
              </>
            )}

            <div className="login-actions">
              {step !== LOGIN_STEP_EMAIL && (
                <button
                  type="button"
                  className="btn btn-secondary login-back"
                  onClick={() => {
                    setPassword('')
                    setError(null)
                    setStep(step === LOGIN_STEP_PASSWORD && tenantOptions.length > 1 ? LOGIN_STEP_TENANT : LOGIN_STEP_EMAIL)
                  }}
                  disabled={isLoading}
                >
                  {t('common.back')}
                </button>
              )}

              <button
                type="submit"
                className="btn btn-primary login-submit"
                disabled={isLoading}
              >
                {isLoading ? (
                  <>
                    <span className="spinner spinner-sm"></span>
                    {step === LOGIN_STEP_PASSWORD ? t('common.connecting') : t('common.checking')}
                  </>
                ) : (
                  submitLabel
                )}
              </button>
            </div>
          </form>

          <div className="login-footer">
            <button
              type="button"
              className="btn btn-ghost login-theme-toggle"
              onClick={toggleTheme}
              title={t('login.theme', { mode: theme === 'light' ? t('topbar.dark') : t('topbar.light') })}
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
            <LanguageSelector
              id="loginLanguageSelect"
              className="login-language-selector"
              locale={locale}
              onChange={setLocale}
              label={t('login.language')}
            />
          </div>
        </div>

        <div className="login-default-credentials">
          <span>{t('login.defaultCredentials')}</span>
          <span>tenant <code>default</code></span>
          <span><code>admin@netledger</code></span>
          <span className="login-default-password">
            password
            <HiddenValueDisplay value="password" as="span" className="login-default-password-value" />
          </span>
        </div>
      </div>
    </div>
  )
}
