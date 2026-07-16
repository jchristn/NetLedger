import React, { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { useApp } from '../context/useApp'
import DataTable from '../components/DataTable'
import Pagination from '../components/Pagination'
import ActionMenu from '../components/ActionMenu'
import Modal, { ConfirmModal, RecordModal, ViewMetadataModal } from '../components/Modal'
import CopyButton from '../components/CopyButton'
import { MetadataLabelsEditor, MetadataTagsEditor } from '../components/MetadataEditor'
import { labelsToPayload, tagsToPayload } from '../components/metadataEditorUtils'
import { formatDate, formatCurrency, normalizeEnumerationResult, normalizeBalances } from '../api/api'
import { getRoleFlags, getTenantId, valueOf } from '../utils/roles'
import './Accounts.css'

const createEmptyFormData = (tenantId = '', userId = '') => ({
  name: '',
  initialBalance: '',
  notes: '',
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
  const [formLoading, setFormLoading] = useState(false)

  const getObjectId = (obj) => valueOf(obj, 'Id') || valueOf(obj, 'GUID') || valueOf(obj, 'Guid') || ''
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

  const loadAccounts = useCallback(async () => {
    try {
      setLoading(true)

      const [accountsResult, balancesResult] = await Promise.all([
        api.listAccounts({
          maxResults: pageSize,
          skip: currentPage * pageSize,
          ordering: 'CreatedDescending'
        }),
        api.getAllBalances()
      ])

      const { objects: accountsList, totalRecords } = normalizeEnumerationResult(accountsResult)
      setAccounts(accountsList)
      setTotalRecords(totalRecords)

      // Create a map of balances by account GUID
      const balanceMap = {}
      const balanceList = normalizeBalances(balancesResult)
      balanceList.forEach(b => {
        const guid = b.accountGuid || b.AccountGuid
        if (guid) {
          balanceMap[guid] = b
        }
      })
      setBalances(balanceMap)
    } catch (err) {
      setError(err.message || 'Failed to load accounts')
    } finally {
      setLoading(false)
    }
  }, [api, currentPage, pageSize, setError])

  useEffect(() => {
    loadAccounts()
  }, [loadAccounts])

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
        formData.tenantId
      )

      const accountGuid = getAccountGuid(createdAccount)
      if (!accountGuid) {
        throw new Error('Created account did not include an identifier')
      }

      await api.mapAccountUser(formData.tenantId, accountGuid, formData.userId)

      setShowCreateModal(false)
      setFormData(createEmptyFormData(formData.tenantId, formData.userId))
      loadAccounts()
    } catch (err) {
      setError(err.message || 'Failed to create account')
    } finally {
      setFormLoading(false)
    }
  }

  // Helper to get GUID from account object with various casing
  const getAccountGuid = (account) => account?.id || account?.Id || account?.guid || account?.Guid || account?.GUID || null

  const handleDelete = async () => {
    if (!selectedAccount) return

    try {
      setFormLoading(true)
      await api.deleteAccount(getAccountGuid(selectedAccount))
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
    const guid = getAccountGuid(account)
    const balance = balances[guid]
    setSelectedAccount({ ...account, balance })
    setShowMetadataModal(true)
  }

  const openEditModal = (account) => {
    setSelectedAccount(account)
    setShowEditModal(true)
  }

  const openViewModal = (account) => {
    const guid = getAccountGuid(account)
    const balance = balances[guid]
    setSelectedAccount({ ...account, balance })
    setShowViewModal(true)
  }

  const viewEntries = (account) => {
    const guid = getAccountGuid(account)
    navigate(`/entries?account=${guid}`)
  }

  const totalPages = Math.ceil(totalRecords / pageSize)

  // Helper to get GUID from row with various casing
  const getRowGuid = (row) => row.id || row.Id || row.guid || row.Guid || row.GUID || ''

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
      key: 'guid',
      label: 'ID',
      className: 'col-guid',
      sortable: true,
      filterable: true,
      render: (row) => (
        <span className="guid-cell-wrapper">
          <span className="guid-cell">
            {getRowGuid(row)}
          </span>
          <CopyButton text={getRowGuid(row)} title="Copy ID" />
        </span>
      ),
      filterValue: (row) => getRowGuid(row)
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
        const guid = getRowGuid(row)
        const balance = balances[guid]
        const amount = balance?.committedBalance ?? balance?.CommittedBalance ?? 0
        return (
          <span className={`amount ${amount >= 0 ? 'amount-positive' : 'amount-negative'}`}>
            {formatCurrency(amount)}
          </span>
        )
      },
      sortValue: (row) => {
        const guid = getRowGuid(row)
        const balance = balances[guid]
        return balance?.committedBalance ?? balance?.CommittedBalance ?? 0
      }
    },
    {
      key: 'pendingBalance',
      label: 'Pending',
      className: 'col-amount',
      sortable: true,
      render: (row) => {
        const guid = getRowGuid(row)
        const balance = balances[guid]
        const amount = balance?.pendingBalance ?? balance?.PendingBalance ?? 0
        return (
          <span className={`amount ${amount >= 0 ? 'amount-positive' : 'amount-negative'}`}>
            {formatCurrency(amount)}
          </span>
        )
      },
      sortValue: (row) => {
        const guid = getRowGuid(row)
        const balance = balances[guid]
        return balance?.pendingBalance ?? balance?.PendingBalance ?? 0
      }
    },
    {
      key: 'pendingEntries',
      label: 'Pending Entries',
      sortable: true,
      render: (row) => {
        const guid = getRowGuid(row)
        const balance = balances[guid]
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
        const guid = getRowGuid(row)
        const balance = balances[guid]
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
        rowKey="guid"
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

          {isTenantAdmin && (
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

      <RecordModal
        isOpen={showEditModal}
        onClose={() => {
          setShowEditModal(false)
          setSelectedAccount(null)
        }}
        title={`Edit ${selectedAccount?.name || selectedAccount?.Name || 'Account'}`}
        data={selectedAccount}
        mode="edit"
      />

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
