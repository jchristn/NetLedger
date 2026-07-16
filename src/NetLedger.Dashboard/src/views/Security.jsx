import React, { useCallback, useEffect, useState } from 'react'
import { useApp } from '../context/useApp'
import DataTable from '../components/DataTable'
import CopyButton from '../components/CopyButton'
import { HiddenValueInput } from '../components/HiddenValue'
import { normalizeEnumerationResult, formatDate } from '../api/api'
import './ApiKeys.css'

function renderId(row) {
  const id = row.Id || row.id
  return (
    <span className="id-cell-wrapper">
      <span className="id-cell">{id}</span>
      <CopyButton text={id} title="Copy ID" />
    </span>
  )
}

export default function Security() {
  const { api, tenantId, setError } = useApp()
  const [tenants, setTenants] = useState([])
  const [users, setUsers] = useState([])
  const [sessions, setSessions] = useState([])
  const [audit, setAudit] = useState([])
  const [roles, setRoles] = useState([])
  const [permissions, setPermissions] = useState([])
  const [loading, setLoading] = useState(true)
  const [newTenantName, setNewTenantName] = useState('')
  const [newUser, setNewUser] = useState({ email: '', password: '', firstName: '', lastName: '', isTenantAdmin: false })
  const [newRoleName, setNewRoleName] = useState('')

  const loadSecurity = useCallback(async () => {
    try {
      setLoading(true)
      const tenantResult = await api.listTenants({ maxResults: 100 })
      setTenants(normalizeEnumerationResult(tenantResult).objects)

      if (tenantId) {
        const userResult = await api.listUsers(tenantId, { maxResults: 100 })
        const sessionResult = await api.listSessions(tenantId, { maxResults: 100 })
        const auditResult = await api.listAudit(tenantId, { maxResults: 100 })
        const roleResult = await api.listRoles(tenantId, { maxResults: 100 })
        const permissionResult = await api.listPermissions(tenantId, { maxResults: 100 })
        setUsers(normalizeEnumerationResult(userResult).objects)
        setSessions(normalizeEnumerationResult(sessionResult).objects)
        setAudit(normalizeEnumerationResult(auditResult).objects)
        setRoles(normalizeEnumerationResult(roleResult).objects)
        setPermissions(normalizeEnumerationResult(permissionResult).objects)
      }
    } catch (err) {
      setError(err.message || 'Failed to load security data')
    } finally {
      setLoading(false)
    }
  }, [api, tenantId, setError])

  useEffect(() => {
    loadSecurity()
  }, [loadSecurity])

  const createTenant = async (e) => {
    e.preventDefault()
    if (!newTenantName.trim()) return
    try {
      await api.createTenant({ Name: newTenantName.trim() })
      setNewTenantName('')
      loadSecurity()
    } catch (err) {
      setError(err.message || 'Failed to create tenant')
    }
  }

  const createUser = async (e) => {
    e.preventDefault()
    if (!tenantId || !newUser.email.trim() || !newUser.password) return
    try {
      await api.createUser(tenantId, {
        Email: newUser.email.trim(),
        Password: newUser.password,
        FirstName: newUser.firstName,
        LastName: newUser.lastName,
        IsTenantAdmin: newUser.isTenantAdmin
      })
      setNewUser({ email: '', password: '', firstName: '', lastName: '', isTenantAdmin: false })
      loadSecurity()
    } catch (err) {
      setError(err.message || 'Failed to create user')
    }
  }

  const createRole = async (e) => {
    e.preventDefault()
    if (!tenantId || !newRoleName.trim()) return
    try {
      await api.createRole(tenantId, { Name: newRoleName.trim() })
      setNewRoleName('')
      loadSecurity()
    } catch (err) {
      setError(err.message || 'Failed to create role')
    }
  }

  const tenantColumns = [
    { key: 'id', label: 'ID', className: 'col-id', render: renderId },
    { key: 'name', label: 'Name', render: row => row.Name || row.name },
    { key: 'active', label: 'Active', render: row => (row.Active ?? row.active) ? 'Yes' : 'No' },
    { key: 'created', label: 'Created', className: 'col-date', render: row => formatDate(row.CreatedUtc || row.createdUtc) }
  ]

  const userColumns = [
    { key: 'id', label: 'ID', className: 'col-id', render: renderId },
    { key: 'email', label: 'Email', render: row => row.Email || row.email },
    { key: 'role', label: 'Role', render: row => (row.IsAdmin || row.isAdmin) ? 'System Administrator' : (row.IsTenantAdmin || row.isTenantAdmin) ? 'Tenant Administrator' : 'User' },
    { key: 'active', label: 'Active', render: row => (row.Active ?? row.active) ? 'Yes' : 'No' }
  ]

  const sessionColumns = [
    { key: 'id', label: 'ID', className: 'col-id', render: renderId },
    { key: 'user', label: 'User', render: row => row.UserId || row.userId },
    { key: 'active', label: 'Active', render: row => (row.Active ?? row.active) ? 'Yes' : 'No' },
    { key: 'expires', label: 'Expires', className: 'col-date', render: row => formatDate(row.ExpiresUtc || row.expiresUtc) }
  ]

  const auditColumns = [
    { key: 'created', label: 'Created', className: 'col-date', render: row => formatDate(row.CreatedUtc || row.createdUtc) },
    { key: 'event', label: 'Event', render: row => row.EventType || row.eventType },
    { key: 'resource', label: 'Resource', render: row => row.ResourceType || row.resourceType },
    { key: 'result', label: 'Result', render: row => row.Result || row.result },
    { key: 'reason', label: 'Reason', render: row => row.Reason || row.reason || '-' }
  ]

  const roleColumns = [
    { key: 'id', label: 'ID', className: 'col-id', render: renderId },
    { key: 'name', label: 'Name', render: row => row.Name || row.name },
    { key: 'builtIn', label: 'Built In', render: row => (row.IsBuiltIn || row.isBuiltIn) ? 'Yes' : 'No' },
    { key: 'protected', label: 'Protected', render: row => (row.IsProtected || row.isProtected) ? 'Yes' : 'No' }
  ]

  const permissionColumns = [
    { key: 'id', label: 'ID', className: 'col-id', render: renderId },
    { key: 'name', label: 'Name', render: row => row.Name || row.name },
    { key: 'type', label: 'Type', render: row => row.PermissionType || row.permissionType },
    { key: 'resources', label: 'Resources', render: row => (row.ResourceTypes || row.resourceTypes || []).join(', ') },
    { key: 'operations', label: 'Operations', render: row => (row.OperationTypes || row.operationTypes || []).join(', ') }
  ]

  return (
    <div className="api-keys-page">
      <div className="page-header">
        <div className="page-header-left">
          <h2 className="page-title">Security</h2>
          <p className="page-description">Tenants, users, sessions, and audit events</p>
        </div>
      </div>

      <form className="filter-bar" onSubmit={createTenant}>
        <input value={newTenantName} onChange={(e) => setNewTenantName(e.target.value)} placeholder="Tenant name" />
        <button className="btn btn-primary" type="submit">Create Tenant</button>
      </form>
      <DataTable columns={tenantColumns} data={tenants} loading={loading} emptyMessage="No tenants found" rowKey="Id" />

      {tenantId && (
        <>
          <form className="filter-bar" onSubmit={createUser}>
            <input value={newUser.email} onChange={(e) => setNewUser({ ...newUser, email: e.target.value })} placeholder="Email" />
            <HiddenValueInput value={newUser.password} onChange={(e) => setNewUser({ ...newUser, password: e.target.value })} placeholder="Password" />
            <input value={newUser.firstName} onChange={(e) => setNewUser({ ...newUser, firstName: e.target.value })} placeholder="First name" />
            <input value={newUser.lastName} onChange={(e) => setNewUser({ ...newUser, lastName: e.target.value })} placeholder="Last name" />
            <label className="checkbox-label">
              <input type="checkbox" checked={newUser.isTenantAdmin} onChange={(e) => setNewUser({ ...newUser, isTenantAdmin: e.target.checked })} />
              <span>Tenant Administrator</span>
            </label>
            <button className="btn btn-primary" type="submit">Create User</button>
          </form>
          <DataTable columns={userColumns} data={users} loading={loading} emptyMessage="No users found" rowKey="Id" />
          <form className="filter-bar" onSubmit={createRole}>
            <input value={newRoleName} onChange={(e) => setNewRoleName(e.target.value)} placeholder="Custom role name" />
            <button className="btn btn-primary" type="submit">Create Role</button>
          </form>
          <DataTable columns={roleColumns} data={roles} loading={loading} emptyMessage="No roles found" rowKey="Id" />
          <DataTable columns={permissionColumns} data={permissions} loading={loading} emptyMessage="No permissions found" rowKey="Id" />
          <DataTable columns={sessionColumns} data={sessions} loading={loading} emptyMessage="No sessions found" rowKey="Id" />
          <DataTable columns={auditColumns} data={audit} loading={loading} emptyMessage="No audit records found" rowKey="Id" />
        </>
      )}
    </div>
  )
}
