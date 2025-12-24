import React from 'react'
import { Outlet, useLocation } from 'react-router-dom'
import Sidebar from './Sidebar'
import Topbar from './Topbar'
import { useApp } from '../context/AppContext'
import './Dashboard.css'

const pageTitles = {
  '/': 'Home',
  '/api-keys': 'API Keys',
  '/accounts': 'Accounts',
  '/entries': 'Entries'
}

export default function Dashboard() {
  const location = useLocation()
  const { error, clearError } = useApp()

  const title = pageTitles[location.pathname] || 'Dashboard'

  return (
    <div className="dashboard">
      <Sidebar />
      <Topbar title={title} />

      <main className="dashboard-main">
        {error && (
          <div className="error-banner animate-slide-up">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="12" cy="12" r="10"/>
              <line x1="12" y1="8" x2="12" y2="12"/>
              <line x1="12" y1="16" x2="12.01" y2="16"/>
            </svg>
            <span>{error}</span>
            <button className="error-banner-dismiss" onClick={clearError} title="Dismiss">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <line x1="18" y1="6" x2="6" y2="18"/>
                <line x1="6" y1="6" x2="18" y2="18"/>
              </svg>
            </button>
          </div>
        )}

        <Outlet />
      </main>
    </div>
  )
}
