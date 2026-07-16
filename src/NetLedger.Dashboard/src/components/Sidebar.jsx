import React from 'react'
import { NavLink } from 'react-router-dom'
import { useApp } from '../context/useApp'
import { getRoleFlags } from '../utils/roles'
import './Sidebar.css'

const navSections = [
  {
    label: 'Home',
    items: [
      {
        path: '/home',
        label: 'Home',
        icon: (
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>
            <polyline points="9 22 9 12 15 12 15 22"/>
          </svg>
        )
      },
      {
        path: '/accounts',
        label: 'Accounts',
        audience: 'ledger',
        icon: (
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/>
            <path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/>
          </svg>
        )
      },
      {
        path: '/entries',
        label: 'Entries',
        audience: 'ledger',
        icon: (
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <line x1="8" y1="6" x2="21" y2="6"/>
            <line x1="8" y1="12" x2="21" y2="12"/>
            <line x1="8" y1="18" x2="21" y2="18"/>
            <line x1="3" y1="6" x2="3.01" y2="6"/>
            <line x1="3" y1="12" x2="3.01" y2="12"/>
            <line x1="3" y1="18" x2="3.01" y2="18"/>
          </svg>
        )
      }
    ]
  },
  {
    label: 'Manage',
    items: [
      {
        path: '/tenants',
        label: 'Tenants',
        audience: 'tenants',
        icon: (
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <rect x="3" y="3" width="7" height="7"/>
            <rect x="14" y="3" width="7" height="7"/>
            <rect x="14" y="14" width="7" height="7"/>
            <rect x="3" y="14" width="7" height="7"/>
          </svg>
        )
      },
      {
        path: '/users',
        label: 'Users',
        audience: 'users',
        icon: (
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/>
            <circle cx="9" cy="7" r="4"/>
            <path d="M22 21v-2a4 4 0 0 0-3-3.87"/>
            <path d="M16 3.13a4 4 0 0 1 0 7.75"/>
          </svg>
        )
      },
      {
        path: '/credentials',
        label: 'Credentials',
        audience: 'credentials',
        icon: (
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M21 2l-2 2m-7.61 7.61a5.5 5.5 0 1 1-7.778 7.778 5.5 5.5 0 0 1 7.777-7.777zm0 0L15.5 7.5m0 0l3 3L22 7l-3-3m-3.5 3.5L19 4"/>
          </svg>
        )
      }
    ]
  },
  {
    label: 'Operate',
    items: [
      {
        path: '/request-history',
        label: 'Request History',
        audience: 'authenticated',
        icon: (
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M3 3v18h18"/>
            <path d="M7 14l3-3 3 2 5-6"/>
          </svg>
        )
      },
      {
        path: '/api-explorer',
        label: 'API Explorer',
        audience: 'authenticated',
        icon: (
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="16 18 22 12 16 6"/>
            <polyline points="8 6 2 12 8 18"/>
          </svg>
        )
      }
    ]
  }
]

export default function Sidebar() {
  const { currentUser, effectivePermissions } = useApp()
  const { isSystemAdmin, isTenantAdmin, isRegularUser } = getRoleFlags(currentUser, effectivePermissions)

  const canShowItem = (item) => {
    if (!item.audience) return true
    if (item.audience === 'tenants') return isSystemAdmin || isRegularUser
    if (item.audience === 'users' || item.audience === 'credentials') return true
    if (item.audience === 'ledger') return isSystemAdmin || isTenantAdmin
    if (item.audience === 'authenticated') return true
    return true
  }

  const visibleSections = navSections
    .map((section) => ({
      ...section,
      items: section.items.filter(canShowItem)
    }))
    .filter((section) => section.items.length > 0)

  return (
    <aside className="sidebar">
      <div className="sidebar-header">
        <img src="/favicon.ico" alt="NetLedger" className="sidebar-logo" />
        <span className="sidebar-brand">NetLedger</span>
      </div>

      <nav className="sidebar-nav">
        {visibleSections.map((section) => (
          <div className="sidebar-nav-section" key={section.label}>
            <div className="sidebar-nav-section-label">{section.label}</div>
            {section.items.map((item) => (
              <NavLink
                key={item.path}
                to={item.path}
                className={({ isActive }) =>
                  `sidebar-nav-item ${isActive ? 'active' : ''}`
                }
                end={item.path === '/'}
              >
                <span className="sidebar-nav-icon">{item.icon}</span>
                <span className="sidebar-nav-label">{item.label}</span>
              </NavLink>
            ))}
          </div>
        ))}
      </nav>

      <div className="sidebar-footer">
        <span className="sidebar-version">v3.0.0</span>
      </div>
    </aside>
  )
}
