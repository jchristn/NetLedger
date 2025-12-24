import React from 'react'
import { Routes, Route, Navigate } from 'react-router-dom'
import { useApp } from './context/AppContext'
import Login from './components/Login'
import Dashboard from './components/Dashboard'
import Home from './views/Home'
import ApiKeys from './views/ApiKeys'
import Accounts from './views/Accounts'
import Entries from './views/Entries'

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
        <Route index element={<Home />} />
        <Route path="api-keys" element={<ApiKeys />} />
        <Route path="accounts" element={<Accounts />} />
        <Route path="entries" element={<Entries />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
