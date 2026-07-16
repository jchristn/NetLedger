import React from 'react'
import { Routes, Route, Navigate } from 'react-router-dom'
import { useApp } from './context/useApp'
import Login from './components/Login'
import Dashboard from './components/Dashboard'
import Home from './views/Home'
import ApiKeys from './views/ApiKeys'
import Accounts from './views/Accounts'
import Entries from './views/Entries'
import Tenants from './views/Tenants'
import Users from './views/Users'
import RequestHistory from './views/RequestHistory'
import ApiExplorer from './views/ApiExplorer'
import { getRoleFlags } from './utils/roles'

function PrivateRoute({ children }) {
  const { isAuthenticated, isInitializing } = useApp()

  if (isInitializing) {
    return null // Will show app-level loading
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  return children
}

function PublicRoute({ children }) {
  const { isAuthenticated, isInitializing } = useApp()

  if (isInitializing) {
    return null // Will show app-level loading
  }

  if (isAuthenticated) {
    return <Navigate to="/" replace />
  }

  return children
}

function RoleRoute({ children, allow }) {
  const { currentUser, effectivePermissions } = useApp()
  const flags = getRoleFlags(currentUser, effectivePermissions)

  if (!allow(flags)) {
    return <Navigate to={flags.isRegularUser ? '/tenants' : '/'} replace />
  }

  return children
}

function DefaultRoute() {
  return <Navigate to="/home" replace />
}

function AppLoading() {
  return (
    <div className="app-loading">
      <div className="app-loading-content">
        <span className="spinner spinner-lg"></span>
        <span>Loading...</span>
      </div>
    </div>
  )
}

export default function App() {
  const { isInitializing } = useApp()

  if (isInitializing) {
    return <AppLoading />
  }

  return (
    <Routes>
      <Route
        path="/login"
        element={
          <PublicRoute>
            <Login />
          </PublicRoute>
        }
      />
      <Route
        path="/"
        element={
          <PrivateRoute>
            <Dashboard />
          </PrivateRoute>
        }
      >
        <Route index element={<DefaultRoute />} />
        <Route path="home" element={<Home />} />
        <Route path="credentials" element={<ApiKeys />} />
        <Route path="api-keys" element={<Navigate to="/credentials" replace />} />
        <Route path="tenants" element={<RoleRoute allow={(flags) => flags.isSystemAdmin || flags.isRegularUser}><Tenants /></RoleRoute>} />
        <Route path="users" element={<Users />} />
        <Route path="accounts" element={<RoleRoute allow={(flags) => flags.isAdmin}><Accounts /></RoleRoute>} />
        <Route path="entries" element={<RoleRoute allow={(flags) => flags.isAdmin}><Entries /></RoleRoute>} />
        <Route path="request-history" element={<RequestHistory />} />
        <Route path="api-explorer" element={<ApiExplorer />} />
        <Route path="security" element={<Navigate to="/tenants" replace />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
