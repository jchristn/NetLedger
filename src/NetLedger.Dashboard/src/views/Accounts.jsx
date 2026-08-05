import React, { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { useApp } from '../context/useApp'
import DataTable from '../components/DataTable'
import Pagination from '../components/Pagination'
import ActionMenu from '../components/ActionMenu'
import Modal, { ConfirmModal, RecordModal, ViewMetadataModal } from '../components/Modal'
import CopyButton from '../components/CopyButton'
import { MetadataLabelsEditor, MetadataTagsEditor } from '../components/MetadataEditor'
import { labelsToPayload, tagsToPayload, normalizeLabelRows, normalizeTagRows } from '../components/metadataEditorUtils'
import { formatDate, formatCurrency, normalizeEnumerationResult, normalizeBalances } from '../api/api'
import { getRoleFlags, getTenantId, valueOf } from '../utils/roles'
import './Accounts.css'

const createEmptyFormData = (tenantId = '', userId = '') => ({
  name: '',
  initialBalance: '',
  notes: '',
  units: '',
  labels: [''],
  tags: [{ key: '', value: '' }],
  tenantId,
  userId
})

export default function Accounts() {
  const { api, tenantId, currentUser, currentTenant, effectivePermissions, setError } = useApp()
  const navigate = useNavigate()
  const { isSystemAdmin, isTenantAdmin } = getRoleFlags(currentUser, effectivePermissions)
  const resolvedTenantId = getTenantId(currentUser, effectivePermissions, tenantId)

  // Data state
  const [accounts, setAccounts] = useState([])
  const [balances, setBalances] = useState({})
  const [tenants, setTenants] = useState([])
  const [selectedTenantId, setSelectedTenantId] = useState('')
  const [users, setUsers] = useState([])
  const [identityLoading, setIdentityLoading] = useState(false)
  const [loading, setLoading] = useState(true)
  const [totalRecords, setTotalRecords] = useState(0)

  // Pagination state
  const [currentPage, setCurrentPage] = useState(0)
  const [pageSize, setPageSize] = useState(25)

  // Modal state
  const [showCreateModal, setShowCreateModal] = useState(false)
  const [showEditModal, setShowEditModal] = useState(false)
  const [showViewModal, setShowViewModal] = useState(false)
  const [showDeleteModal, setShowDeleteModal] = useState(false)
  const [showMetadataModal, setShowMetadataModal] = useState(false)
  const [selectedAccount, setSelectedAccount] = useState(null)

  // Form state
  const [formData, setFormData] = useState(createEmptyFormData())
  const [editFormData, setEditFormData] = useState(null)
  const [formLoading, setFormLoading] = useState(false)

  const getObjectId = (obj) => valueOf(obj, 'Id') || valueOf(obj, 'id') || valueOf(obj, 'ID') || ''
  const getUserLabel = (user) => {
    const email = valueOf(user, 'Email') || getObjectId(user)
    const name = [valueOf(user, 'FirstName'), valueOf(user, 'LastName')].filter(Boolean).join(' ')
    return name ? `${name} (${email})` : email
  }
  const getTenantLabel = (tenant) => {
    const id = getObjectId(tenant)
    const name = valueOf(tenant, 'Name') || id
    return id && name !== id ? `${name} (${id})` : name
  }
  const requestTenantId = isSystemAdmin ? selectedTenantId || null : null
  const suppressTenantHeader = isSystemAdmin && !selectedTenantId

  const loadAccounts = useCallback(async () => {
    try {
      setLoading(true)

      const [accountsResult, balancesResult] = await Promise.all([
        api.listAccounts({
          maxResults: pageSize,
          skip: currentPage * pageSize,
          ordering: 'CreatedDescending',
          tenantId: requestTenantId,
          suppressTenantHeader
        }),
        api.getAllBalances({
          tenantId: requestTenantId,
          suppressTenantHeader
        })
      ])

      const { objects: accountsList, totalRecords } = normalizeEnumerationResult(accountsResult)
      setAccounts(accountsList)
      setTotalRecords(totalRecords)

      // Create a map of balances by account ID
      const balanceMap = {}
      const balanceList = normalizeBalances(balancesResult)
      balanceList.forEach(b => {
        const id = b.accountId || b.AccountId
        if (id) {
          balanceMap[id] = b
        }
      })
      setBalances(balanceMap)
    } catch (err) {
      setError(err.message || 'Failed to load accounts')
    } finally {
      setLoading(false)
    }
  }, [api, currentPage, pageSize, requestTenantId, setError, suppressTenantHeader])

  const loadTenants = useCallback(async () => {
    if (!isSystemAdmin) {
      setTenants([])
      return
    }

    try {
      const tenantResult = await api.listTenants({ maxResults: 1000, ordering: 'CreatedDescending' })
      setTenants(normalizeEnumerationResult(tenantResult).objects)
    } catch (err) {
      setError(err.message || 'Failed to load tenants')
    }
  }, [api, isSystemAdmin, setError])

  useEffect(() => {
    loadAccounts()
  }, [loadAccounts])

  useEffect(() => {
    loadTenants()
  }, [loadTenants])

  const loadUsersForTenant = useCallback(async (selectedTenantId, selectedUserId = '') => {
    if (!selectedTenantId) {
      setUsers([])
      return ''
    }

    const userResult = await api.listUsers(selectedTenantId, { maxResults: 1000, ordering: 'CreatedDescending' })
    const userList = normalizeEnumerationResult(userResult).objects
    setUsers(userList)

    const userExists = userList.some(user => getObjectId(user) === selectedUserId)
    return userExists ? selectedUserId : getObjectId(userList[0])
  }, [api])

  const loadCreateIdentity = useCallback(async () => {
    try {
      setIdentityLoading(true)

      let selectedTenantId = formData.tenantId || resolvedTenantId
      let tenantList = []

      if (isSystemAdmin) {
        const tenantResult = await api.listTenants({ maxResults: 1000, ordering: 'CreatedDescending' })
        tenantList = normalizeEnumerationResult(tenantResult).objects
        setTenants(tenantList)
        selectedTenantId = selectedTenantId || getObjectId(tenantList[0])
      } else if (isTenantAdmin) {
        const tenant = currentTenant || (resolvedTenantId ? await api.readTenant(resolvedTenantId) : null)
        tenantList = tenant ? [tenant] : []
        setTenants(tenantList)
        selectedTenantId = resolvedTenantId
      }

      const selectedUserId = await loadUsersForTenant(selectedTenantId, formData.userId)
      setFormData(prev => ({
        ...prev,
        tenantId: selectedTenantId || '',
        userId: selectedUserId || ''
      }))
    } catch (err) {
      setError(err.message || 'Failed to load tenant users')
    } finally {
      setIdentityLoading(false)
    }
  }, [api, currentTenant, formData.tenantId, formData.userId, isSystemAdmin, isTenantAdmin, loadUsersForTenant, resolvedTenantId, setError])

  const openCreateModal = () => {
    setShowCreateModal(true)
    loadCreateIdentity()
  }

  const handleTenantChange = async (e) => {
    const selectedTenantId = e.target.value
    setFormData(prev => ({ ...prev, tenantId: selectedTenantId, userId: '' }))

    try {
      setIdentityLoading(true)
      const selectedUserId = await loadUsersForTenant(selectedTenantId)
      setFormData(prev => ({ ...prev, userId: selectedUserId || '' }))
    } catch (err) {
      setError(err.message || 'Failed to load tenant users')
    } finally {
      setIdentityLoading(false)
    }
  }

  const handleAccountTenantFilterChange = (e) => {
    setSelectedTenantId(e.target.value)
    setCurrentPage(0)
  }

  const handlePageChange = (page) => {
    setCurrentPage(page)
  }

  const handlePageSizeChange = (size) => {
    setPageSize(size)
    setCurrentPage(0)
  }

  const handleCreate = async (e) => {
    e.preventDefault()

    if (!formData.name.trim()) {
      setError('Account name is required')
      return
    }

    if (!formData.tenantId) {
      setError('Tenant is required')
      return
    }

    if (!formData.userId) {
      setError('User mapping is required')
      return
    }

    try {
      setFormLoading(true)
      const initialBalance = formData.initialBalance
        ? parseFloat(formData.initialBalance)
        : null

      const createdAccount = await api.createAccount(
        formData.name.trim(),
        initialBalance,
        formData.notes.trim() || null,
        parseLabels(formData.labels),
        parseTags(formData.tags),
        formData.units.trim() || null,
        formData.tenantId
      )

      const accountId = getAccountId(createdAccount)
      if (!accountId) {
        throw new Error('Created account did not include an identifier')
      }

      await api.mapAccountUser(formData.tenantId, accountId, formData.userId)

      setShowCreateModal(false)
      setFormData(createEmptyFormData(formData.tenantId, formData.userId))
      loadAccounts()
    } catch (err) {
      setError(err.message || 'Failed to create account')
    } finally {
      setFormLoading(false)
    }
  }

  // Helper to get ID from account object with various casing
  const getAccountId = (account) => account?.id || account?.Id || account?.id || account?.Id || account?.ID || null

  const handleDelete = async () => {
    if (!selectedAccount) return

    try {
      setFormLoading(true)
      await api.deleteAccount(getAccountId(selectedAccount))
      setShowDeleteModal(false)
      setSelectedAccount(null)
      loadAccounts()
    } catch (err) {
      setError(err.message || 'Failed to delete account')
    } finally {
      setFormLoading(false)
    }
  }

  const openDeleteModal = (account) => {
    setSelectedAccount(account)
    setShowDeleteModal(true)
  }

  const openMetadataModal = (account) => {
    const id = getAccountId(account)
    const balance = balances[id]
    setSelectedAccount({ ...account, balance })
    setShowMetadataModal(true)
  }

  const openEditModal = (account) => {
    setSelectedAccount(account)
    setEditFormData({
      name: valueOf(account, 'Name') || '',
      notes: valueOf(account, 'Notes') || '',
      units: valueOf(account, 'Units') || '',
      labels: normalizeLabelRows(account.labels || account.Labels),
      tags: normalizeTagRows(account.tags || account.Tags),
      active: valueOf(account, 'Active') ?? true
    })
    setShowEditModal(true)
  }

  const closeEditModal = () => {
    setShowEditModal(false)
    setSelectedAccount(null)
    setEditFormData(null)
  }

  const handleUpdate = async (e) => {
    e.preventDefault()

    if (!editFormData || !editFormData.name.trim()) {
      setError('Account name is required')
      return
    }

    try {
      setFormLoading(true)

      const accountId = getAccountId(selectedAccount)
      const accountTenantId = isSystemAdmin ? (valueOf(selectedAccount, 'TenantId') || null) : null

      await api.updateAccount(
        accountId,
        {
          name: editFormData.name.trim(),
          notes: editFormData.notes.trim() || null,
          units: editFormData.units.trim() || null,
          labels: parseLabels(editFormData.labels),
          tags: parseTags(editFormData.tags),
          active: editFormData.active
        },
        accountTenantId
      )

      closeEditModal()
      loadAccounts()
    } catch (err) {
      setError(err.message || 'Failed to update account')
    } finally {
      setFormLoading(false)
    }
  }

  const openViewModal = (account) => {
    const id = getAccountId(account)
    const balance = balances[id]
    setSelectedAccount({ ...account, balance })
    setShowViewModal(true)
  }

  const viewEntries = (account) => {
    const id = getAccountId(account)
    const accountTenantId = valueOf(account, 'TenantId') || ''
    const params = new URLSearchParams()
    if (accountTenantId) params.set('tenant', accountTenantId)
    if (id) params.set('account', id)
    navigate(`/entries?${params.toString()}`)
  }

  const totalPages = Math.ceil(totalRecords / pageSize)

  // Helper to get ID from row with various casing
  const getRowId = (row) => row.id || row.Id || row.id || row.Id || row.ID || ''

  const parseLabels = (value) => {
    return labelsToPayload(value)
  }

  const parseTags = (value) => {
    return tagsToPayload(value)
  }

  const columns = [
    {
      key: 'name',
      label: 'Name',
      sortable: true,
      filterable: true,
      render: (row) => (
        <div className="account-name-cell">
          <span className="account-name">{row.name || row.Name}</span>
          {(row.notes || row.Notes) && (
            <span className="account-notes" title={row.notes || row.Notes}>
              {row.notes || row.Notes}
            </span>
          )}
        </div>
      ),
      filterValue: (row) => `${row.name || row.Name} ${row.notes || row.Notes || ''}`
    },
    {
      key: 'id',
      label: 'ID',
      className: 'col-id',
      sortable: true,
      filterable: true,
      render: (row) => (
        <span className="id-cell-wrapper">
          <span className="id-cell">
            {getRowId(row)}
          </span>
          <CopyButton text={getRowId(row)} title="Copy ID" />
        </span>
      ),
      filterValue: (row) => getRowId(row)
    },
    {
      key: 'units',
      label: 'Units',
      sortable: true,
      filterable: true,
      render: (row) => {
        const units = row.units || row.Units
        return units ? units : <span className="text-muted">Amount</span>
      },
      filterValue: (row) => row.units || row.Units || '',
      sortValue: (row) => row.units || row.Units || ''
    },
    {
      key: 'labels',
      label: 'Labels',
      filterable: true,
      render: (row) => {
        const labels = row.labels || row.Labels || []
        return labels.length > 0 ? labels.join(', ') : <span className="text-muted">None</span>
      },
      filterValue: (row) => (row.labels || row.Labels || []).join(' ')
    },
    {
      key: 'committedBalance',
      label: 'Committed',
      className: 'col-amount',
      sortable: true,
      render: (row) => {
        const id = getRowId(row)
        const balance = balances[id]
        const amount = balance?.committedBalance ?? balance?.CommittedBalance ?? 0
        return (
          <span className={`amount ${amount >= 0 ? 'amount-positive' : 'amount-negative'}`}>
            {formatCurrency(amount, row.units || row.Units)}
          </span>
        )
      },
      sortValue: (row) => {
        const id = getRowId(row)
        const balance = balances[id]
        return balance?.committedBalance ?? balance?.CommittedBalance ?? 0
      }
    },
    {
      key: 'pendingBalance',
      label: 'Pending',
      className: 'col-amount',
      sortable: true,
      render: (row) => {
        const id = getRowId(row)
        const balance = balances[id]
        const amount = balance?.pendingBalance ?? balance?.PendingBalance ?? 0
        return (
          <span className={`amount ${amount >= 0 ? 'amount-positive' : 'amount-negative'}`}>
            {formatCurrency(amount, row.units || row.Units)}
          </span>
        )
      },
      sortValue: (row) => {
        const id = getRowId(row)
        const balance = balances[id]
        return balance?.pendingBalance ?? balance?.PendingBalance ?? 0
      }
    },
    {
      key: 'pendingEntries',
      label: 'Pending Entries',
      sortable: true,
      render: (row) => {
        const id = getRowId(row)
        const balance = balances[id]
        const credits = balance?.pendingCredits?.count ?? balance?.PendingCredits?.Count ?? 0
        const debits = balance?.pendingDebits?.count ?? balance?.PendingDebits?.Count ?? 0
        const total = credits + debits

        if (total === 0) {
          return <span className="text-muted">None</span>
        }

        return (
          <span className="pending-entries-badge">
            <span className="pending-credits" title="Pending credits">{credits} credits</span>
            <span className="pending-divider">/</span>
            <span className="pending-debits" title="Pending debits">{debits} debits</span>
          </span>
        )
      },
      sortValue: (row) => {
        const id = getRowId(row)
        const balance = balances[id]
        const credits = balance?.pendingCredits?.count ?? balance?.PendingCredits?.Count ?? 0
        const debits = balance?.pendingDebits?.count ?? balance?.PendingDebits?.Count ?? 0
        return credits + debits
      }
    },
    {
      key: 'createdUtc',
      label: 'Created',
      className: 'col-date',
      sortable: true,
      render: (row) => formatDate(row.createdUtc || row.CreatedUtc),
      sortValue: (row) => new Date(row.createdUtc || row.CreatedUtc || 0).getTime()
    },
    {
      key: 'actions',
      label: '',
      className: 'col-actions',
      render: (row) => (
        <ActionMenu
          items={[
            {
              label: 'Edit',
              onClick: () => openEditModal(row)
            },
            {
              label: 'View',
              onClick: () => openViewModal(row)
            },
            {
              label: 'View JSON',
              onClick: () => openMetadataModal(row)
            },
            { divider: true },
            {
              label: 'View Entries',
              icon: (
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <line x1="8" y1="6" x2="21" y2="6"/>
                  <line x1="8" y1="12" x2="21" y2="12"/>
                  <line x1="8" y1="18" x2="21" y2="18"/>
                  <line x1="3" y1="6" x2="3.01" y2="6"/>
                  <line x1="3" y1="12" x2="3.01" y2="12"/>
                  <line x1="3" y1="18" x2="3.01" y2="18"/>
                </svg>
              ),
              onClick: () => viewEntries(row)
            },
            {
              label: 'View Metadata',
              icon: (
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
                  <polyline points="14 2 14 8 20 8"/>
                  <line x1="16" y1="13" x2="8" y2="13"/>
                  <line x1="16" y1="17" x2="8" y2="17"/>
                </svg>
              ),
              onClick: () => openMetadataModal(row)
            },
            { divider: true },
            {
              label: 'Delete',
              variant: 'danger',
              icon: (
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <polyline points="3 6 5 6 21 6"/>
                  <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>
                </svg>
              ),
              onClick: () => openDeleteModal(row)
            }
          ]}
        />
      )
    }
  ]

  return (
    <div className="accounts-page">
      <div className="page-header">
        <div className="page-header-left">
          <h2 className="page-title">Accounts</h2>
          <p className="page-description">Manage ledger accounts and view balances</p>
        </div>
        <div className="page-header-actions">
          <button className="btn btn-primary" onClick={openCreateModal}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <line x1="12" y1="5" x2="12" y2="19"/>
              <line x1="5" y1="12" x2="19" y2="12"/>
            </svg>
            Create Account
          </button>
        </div>
      </div>

      {isSystemAdmin && (
        <div className="accounts-filter-bar">
          <div className="accounts-tenant-filter">
            <label htmlFor="accountsTenantFilter">Tenant</label>
            <select
              id="accountsTenantFilter"
              value={selectedTenantId}
              onChange={handleAccountTenantFilterChange}
              disabled={loading}
            >
              <option value="">All tenants</option>
              {tenants.map(tenant => (
                <option key={getObjectId(tenant)} value={getObjectId(tenant)}>
                  {getTenantLabel(tenant)}
                </option>
              ))}
            </select>
          </div>
        </div>
      )}

      <Pagination
        currentPage={currentPage}
        totalPages={totalPages}
        totalRecords={totalRecords}
        pageSize={pageSize}
        onPageChange={handlePageChange}
        onPageSizeChange={handlePageSizeChange}
      />

      <DataTable
        columns={columns}
        data={accounts}
        loading={loading}
        emptyMessage="No accounts found"
        onRowClick={openEditModal}
        rowKey="id"
      />

      {/* Create Modal */}
      <Modal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        title="Create Account"
        size="medium"
        footer={
          <>
            <button
              className="btn btn-secondary"
              onClick={() => setShowCreateModal(false)}
              disabled={formLoading}
            >
              Cancel
            </button>
            <button
              className="btn btn-primary"
              onClick={handleCreate}
              disabled={formLoading}
            >
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
        <form onSubmit={handleCreate}>
          {isSystemAdmin && (
            <div className="form-group">
              <label htmlFor="accountTenant">Tenant *</label>
              <select
                id="accountTenant"
                value={formData.tenantId}
                onChange={handleTenantChange}
                disabled={formLoading || identityLoading}
              >
                <option value="">Select tenant</option>
                {tenants.map(tenant => (
                  <option key={getObjectId(tenant)} value={getObjectId(tenant)}>
                    {getTenantLabel(tenant)}
                  </option>
                ))}
              </select>
            </div>
          )}

          {!isSystemAdmin && isTenantAdmin && (
            <div className="form-group">
              <label htmlFor="accountTenantLocked">Tenant</label>
              <input
                id="accountTenantLocked"
                value={getTenantLabel(tenants[0]) || formData.tenantId}
                disabled
              />
            </div>
          )}

          <div className="form-group">
            <label htmlFor="accountUser">Mapped User *</label>
            <select
              id="accountUser"
              value={formData.userId}
              onChange={(e) => setFormData({ ...formData, userId: e.target.value })}
              disabled={formLoading || identityLoading || !formData.tenantId}
            >
              <option value="">Select user</option>
              {users.map(user => (
                <option key={getObjectId(user)} value={getObjectId(user)}>
                  {getUserLabel(user)}
                </option>
              ))}
            </select>
            <span className="form-hint">The account will be mapped to this user after creation</span>
          </div>

          <div className="form-group">
            <label htmlFor="accountName">Account Name *</label>
            <input
              type="text"
              id="accountName"
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
              placeholder="Enter account name"
              disabled={formLoading}
              autoFocus
            />
          </div>

          <div className="form-group">
            <label htmlFor="initialBalance">Initial Balance</label>
            <input
              type="number"
              id="initialBalance"
              value={formData.initialBalance}
              onChange={(e) => setFormData({ ...formData, initialBalance: e.target.value })}
              placeholder="0.00"
              step="0.01"
              disabled={formLoading}
            />
            <span className="form-hint">Optional starting balance for this account</span>
          </div>

          <div className="form-group">
            <label htmlFor="accountUnits">Units</label>
            <input
              type="text"
              id="accountUnits"
              value={formData.units}
              onChange={(e) => setFormData({ ...formData, units: e.target.value })}
              placeholder="e.g. USD, tokens, points"
              maxLength={64}
              disabled={formLoading}
            />
            <span className="form-hint">Optional unit of denomination for balances in this account. Leave blank to display a plain amount.</span>
          </div>

          <div className="form-group">
            <label htmlFor="accountNotes">Notes</label>
            <textarea
              id="accountNotes"
              value={formData.notes}
              onChange={(e) => setFormData({ ...formData, notes: e.target.value })}
              placeholder="Optional notes about this account"
              rows={3}
              disabled={formLoading}
            />
          </div>

          <div className="form-group">
            <label>Labels</label>
            <MetadataLabelsEditor
              idPrefix="accountLabels"
              value={formData.labels}
              onChange={(labels) => setFormData({ ...formData, labels })}
              disabled={formLoading}
            />
          </div>

          <div className="form-group">
            <label>Tags</label>
            <MetadataTagsEditor
              idPrefix="accountTags"
              value={formData.tags}
              onChange={(tags) => setFormData({ ...formData, tags })}
              disabled={formLoading}
            />
          </div>
        </form>
      </Modal>

      {/* Delete Confirmation Modal */}
      <ConfirmModal
        isOpen={showDeleteModal}
        onClose={() => {
          setShowDeleteModal(false)
          setSelectedAccount(null)
        }}
        onConfirm={handleDelete}
        title="Delete Account"
        message={`Are you sure you want to delete the account "${selectedAccount?.name || selectedAccount?.Name}"? This will also delete all entries associated with this account. This action cannot be undone.`}
        confirmText="Delete"
        variant="danger"
        isLoading={formLoading}
      />

      {/* Edit Modal */}
      <Modal
        isOpen={showEditModal}
        onClose={closeEditModal}
        title={`Edit ${selectedAccount?.name || selectedAccount?.Name || 'Account'}`}
        size="medium"
        footer={
          <>
            <button
              className="btn btn-secondary"
              onClick={closeEditModal}
              disabled={formLoading}
            >
              Cancel
            </button>
            <button
              className="btn btn-primary"
              onClick={handleUpdate}
              disabled={formLoading}
            >
              {formLoading ? (
                <>
                  <span className="spinner spinner-sm"></span>
                  Saving...
                </>
              ) : (
                'Save Changes'
              )}
            </button>
          </>
        }
      >
        {editFormData && (
          <form onSubmit={handleUpdate}>
            <div className="form-group">
              <label htmlFor="editAccountName">Account Name *</label>
              <input
                type="text"
                id="editAccountName"
                value={editFormData.name}
                onChange={(e) => setEditFormData({ ...editFormData, name: e.target.value })}
                placeholder="Enter account name"
                disabled={formLoading}
                autoFocus
              />
            </div>

            <div className="form-group">
              <label htmlFor="editAccountUnits">Units</label>
              <input
                type="text"
                id="editAccountUnits"
                value={editFormData.units}
                onChange={(e) => setEditFormData({ ...editFormData, units: e.target.value })}
                placeholder="e.g. USD, tokens, points"
                maxLength={64}
                disabled={formLoading}
              />
              <span className="form-hint">Optional unit of denomination for balances in this account. Leave blank to display a plain amount.</span>
            </div>

            <div className="form-group">
              <label htmlFor="editAccountNotes">Notes</label>
              <textarea
                id="editAccountNotes"
                value={editFormData.notes}
                onChange={(e) => setEditFormData({ ...editFormData, notes: e.target.value })}
                placeholder="Optional notes about this account"
                rows={3}
                disabled={formLoading}
              />
            </div>

            <div className="form-group">
              <label>Labels</label>
              <MetadataLabelsEditor
                idPrefix="editAccountLabels"
                value={editFormData.labels}
                onChange={(labels) => setEditFormData({ ...editFormData, labels })}
                disabled={formLoading}
              />
            </div>

            <div className="form-group">
              <label>Tags</label>
              <MetadataTagsEditor
                idPrefix="editAccountTags"
                value={editFormData.tags}
                onChange={(tags) => setEditFormData({ ...editFormData, tags })}
                disabled={formLoading}
              />
            </div>
          </form>
        )}
      </Modal>

      <RecordModal
        isOpen={showViewModal}
        onClose={() => {
          setShowViewModal(false)
          setSelectedAccount(null)
        }}
        title={`View ${selectedAccount?.name || selectedAccount?.Name || 'Account'}`}
        data={selectedAccount}
      />

      {/* View Metadata Modal */}
      <ViewMetadataModal
        isOpen={showMetadataModal}
        onClose={() => {
          setShowMetadataModal(false)
          setSelectedAccount(null)
        }}
        title="Account Metadata"
        data={selectedAccount}
      />
    </div>
  )
}
