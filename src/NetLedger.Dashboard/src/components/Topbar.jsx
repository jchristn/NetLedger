import React from 'react'
import { useApp } from '../context/useApp'
import LanguageSelector from '../i18n/LanguageSelector'
import './Topbar.css'

export default function Topbar({ title }) {
  const { logout, theme, toggleTheme, serverInfo, serverUrl, dataSourceContext, locale, setLocale, t } = useApp()
  const nextTheme = theme === 'light' ? t('topbar.dark') : t('topbar.light')
  const themeTitle = t('topbar.switchTheme', { mode: nextTheme })
  const dataSourceLabel = dataSourceContext === 'archive' ? t('dataSource.archive') : t('dataSource.active')

  return (
    <header className="topbar">
      <div className="topbar-left">
        <h1 className="topbar-title">{title}</h1>
      </div>

      <div className="topbar-right">
        {serverInfo && (
          <div className="topbar-server-info">
            <span className="topbar-server-label">{t('topbar.connectedTo')}</span>
            <span className="topbar-server-url" title={serverUrl}>
              {serverUrl.replace(/^https?:\/\//, '')}
            </span>
          </div>
        )}

        <span
          className={`topbar-data-source topbar-data-source-${dataSourceContext === 'archive' ? 'archive' : 'active'}`}
          title={t('dataSource.contextTitle', { source: dataSourceLabel })}
        >
          {dataSourceLabel}
        </span>

        <LanguageSelector
          id="topbarLanguageSelect"
          className="topbar-locale"
          locale={locale}
          onChange={setLocale}
          label={t('login.language')}
        />

        <button
          className="topbar-btn"
          onClick={toggleTheme}
          title={themeTitle}
          aria-label={themeTitle}
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

        <a
          className="topbar-btn"
          href="https://github.com/jchristn/netledger"
          target="_blank"
          rel="noreferrer"
          title={t('topbar.github')}
          aria-label={t('topbar.github')}
        >
          <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
            <path d="M12 2C6.48 2 2 6.58 2 12.26c0 4.53 2.87 8.37 6.84 9.73.5.09.68-.22.68-.49 0-.24-.01-.88-.01-1.73-2.78.62-3.37-1.37-3.37-1.37-.45-1.18-1.11-1.5-1.11-1.5-.91-.64.07-.63.07-.63 1 .07 1.53 1.06 1.53 1.06.89 1.56 2.34 1.11 2.91.85.09-.66.35-1.11.63-1.37-2.22-.26-4.56-1.14-4.56-5.07 0-1.12.39-2.03 1.03-2.75-.1-.26-.45-1.3.1-2.71 0 0 .84-.28 2.75 1.05A9.3 9.3 0 0 1 12 6.98c.85 0 1.7.12 2.5.35 1.91-1.33 2.75-1.05 2.75-1.05.55 1.41.2 2.45.1 2.71.64.72 1.03 1.63 1.03 2.75 0 3.94-2.34 4.8-4.57 5.06.36.32.68.95.68 1.92 0 1.38-.01 2.5-.01 2.84 0 .27.18.59.69.49A10.1 10.1 0 0 0 22 12.26C22 6.58 17.52 2 12 2z"/>
          </svg>
        </a>

        <button
          className="topbar-btn topbar-btn-logout"
          onClick={logout}
          title={t('topbar.disconnect')}
          aria-label={t('topbar.disconnect')}
        >
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/>
            <polyline points="16 17 21 12 16 7"/>
            <line x1="21" y1="12" x2="9" y2="12"/>
          </svg>
        </button>
      </div>
    </header>
  )
}
