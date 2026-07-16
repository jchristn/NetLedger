import React, { useCallback, useEffect, useState } from 'react'
import { useApp } from '../context/useApp'
import DataTable from '../components/DataTable'
import Pagination from '../components/Pagination'
import Modal from '../components/Modal'
import CopyButton from '../components/CopyButton'
import { HiddenValueInput } from '../components/HiddenValue'
import { normalizeEnumerationResult } from '../api/api'
import { getPrincipalId, getRoleFlags } from '../utils/roles'
import './ApiKeys.css'

export default function Users() {
  const { api, tenantId, currentUser, effectivePermissions, setError } = useApp()
  const { isAdmin } = getRoleFlags(currentUser, effectivePermissions)
  const principalId = getPrincipalId(currentUser, effectivePermissions)
  const [users, setUsers] = useState([])
  const [loading, setLoading] = useState(true)
  const [totalRecords, setTotalRecords] = useState(0)
  const [currentPage, setCurrentPage] = useState(0)
  const [pageSize, setPageSize] = useState(25)
  const [showCreateModal, setShowCreateModal] = useState(false)
  const [newUser, setNewUser] = useState({ email: '', password: '', firstName: '', lastName: '', isTenantAdmin: false })
  const [formLoading, setFormLoading] = useState(false)

  const loadUsers = useCallback(async () => {
    try {
      setLoading(true)

      if (isAdmin) {
        const userResult = await api.listUsers(tenantId, {
          maxResults: pageSize,
          skip: currentPage * pageSize,
          ordering: 'CreatedDescending'
        })
        const { objects, totalRecords } = normalizeEnumerationResult(userResult)
        setUsers(objects)
        setTotalRecords(totalRecords)
      } else if (tenantId && principalId) {
        try {
          const user = await api.readUser(tenantId, principalId)
          setUsers(user ? [user] : [])
          setTotalRecords(user ? 1 : 0)
        } catch {
          setUsers(currentUser ? [currentUser] : [])
          setTotalRecords(currentUser ? 1 : 0)
        }
      } else {
        setUsers([])
        setTotalRecords(0)
      }
    } catch (err) {
      setError(err.message || 'Failed to load users')
    } finally {
      setLoading(false)
    }
  }, [api, currentPage, currentUser, isAdmin, pageSize, principalId, setError, tenantId])

  useEffect(() => {
    loadUsers()
  }, [loadUsers])

  const createUser = async (e) => {
    e.preventDefault()
    if (!isAdmin || !tenantId || !newUser.email.trim() || !newUser.password) return

    try {
      setFormLoading(true)
      await api.createUser(tenantId, {
        Email: newUser.email.trim(),
        Password: newUser.password,
        FirstName: newUser.firstName,
        LastName: newUser.lastName,
        IsTenantAdmin: newUser.isTenantAdmin
      })
      setNewUser({ email: '', password: '', firstName: '', lastName: '', isTenantAdmin: false })
      setShowCreateModal(false)
      loadUsers()
    } catch (err) {
      setError(err.message || 'Failed to create user')
    } finally {
      setFormLoading(false)
    }
  }

  const handlePageChange = (page) => {
    setCurrentPage(page)
  }

  const handlePageSizeChange = (size) => {
    setPageSize(size)
    setCurrentPage(0)
  }

  const totalPages = Math.ceil(totalRecords / pageSize)

  const userColumns = [
    {
      key: 'id',
      label: 'ID',
      className: 'col-id',
      render: row => {
        const id = row.Id || row.id
        return (
          <span className="id-cell-wrapper">
            <span className="id-cell">{id}</span>
            <CopyButton text={id} title="Copy ID" />
          </span>
        )
      }
    },
    { key: 'email', label: 'Email', render: row => row.Email || row.email },
    { key: 'name', label: 'Name', render: row => [row.FirstName || row.firstName, row.LastName || row.lastName].filter(Boolean).join(' ') || '-' },
    { key: 'role', label: 'Role', render: row => (row.IsAdmin || row.isAdmin) ? 'System Administrator' : (row.IsTenantAdmin || row.isTenantAdmin) ? 'Tenant Administrator' : 'User' },
    { key: 'active', label: 'Active', render: row => (row.Active ?? row.active) ? 'Yes' : 'No' }
  ]

  return (
    <div className="api-keys-page">
      <div className="page-header">
        <div className="page-header-left">
          <h2 className="page-title">Users</h2>
          <p className="page-description">{isAdmin ? 'Manage tenant users' : 'View your user account'}</p>
        </div>
        {isAdmin && (
          <div className="page-header-actions">
            <button className="btn btn-primary" onClick={() => setShowCreateModal(true)}>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <line x1="12" y1="5" x2="12" y2="19"/>
                <line x1="5" y1="12" x2="19" y2="12"/>
              </svg>
              Create User
            </button>
          </div>
        )}
      </div>

      <Pagination
        currentPage={currentPage}
        totalPages={totalPages}
        totalRecords={totalRecords}
        pageSize={pageSize}
        onPageChange={handlePageChange}
        onPageSizeChange={handlePageSizeChange}
      />

      <DataTable columns={userColumns} data={users} loading={loading} emptyMessage="No users found" rowKey="Id" />

      <Modal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        title="Create User"
        size="medium"
        footer={
          <>
            <button className="btn btn-secondary" onClick={() => setShowCreateModal(false)} disabled={formLoading}>
              Cancel
            </button>
            <button className="btn btn-primary" onClick={createUser} disabled={formLoading}>
              {formLoading ? (
                <>
                  <span className="spinner spinner-sm"></span>
                  Creating...
                </>
              ) : (
                'Create'
              )}
            </button>
          </>
        }
      >
        <form onSubmit={createUser}>
          <div className="form-group">
            <label htmlFor="userEmail">Email</label>
            <input
              id="userEmail"
              value={newUser.email}
              onChange={(e) => setNewUser({ ...newUser, email: e.target.value })}
              placeholder="user@example.com"
              disabled={formLoading}
              autoFocus
            />
          </div>
          <div className="form-group">
            <label htmlFor="userPassword">Password</label>
            <HiddenValueInput
              id="userPassword"
              value={newUser.password}
              onChange={(e) => setNewUser({ ...newUser, password: e.target.value })}
              placeholder="Enter password"
              disabled={formLoading}
            />
          </div>
          <div className="form-row">
            <div className="form-group">
              <label htmlFor="userFirstName">First Name</label>
              <input
                id="userFirstName"
                value={newUser.firstName}
                onChange={(e) => setNewUser({ ...newUser, firstName: e.target.value })}
                placeholder="First name"
                disabled={formLoading}
              />
            </div>
            <div className="form-group">
              <label htmlFor="userLastName">Last Name</label>
              <input
                id="userLastName"
                value={newUser.lastName}
                onChange={(e) => setNewUser({ ...newUser, lastName: e.target.value })}
                placeholder="Last name"
                disabled={formLoading}
              />
            </div>
          </div>
          <div className="form-group">
            <label className="checkbox-label">
              <input
                type="checkbox"
                checked={newUser.isTenantAdmin}
                onChange={(e) => setNewUser({ ...newUser, isTenantAdmin: e.target.checked })}
                disabled={formLoading}
              />
              <span>Tenant Administrator</span>
            </label>
          </div>
        </form>
      </Modal>
    </div>
  )
}
