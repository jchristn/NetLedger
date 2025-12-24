import React, { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { useApp } from '../context/AppContext'
import DataTable from '../components/DataTable'
import Pagination from '../components/Pagination'
import ActionMenu from '../components/ActionMenu'
import Modal, { ConfirmModal, ViewMetadataModal } from '../components/Modal'
import CopyButton from '../components/CopyButton'
import { formatDate, formatCurrency, normalizeEnumerationResult, normalizeBalances } from '../api/api'
import './Accounts.css'

export default function Accounts() {
  const { api, setError } = useApp()
  const navigate = useNavigate()

  // Data state
  const [accounts, setAccounts] = useState([])
  const [balances, setBalances] = useState({})
  const [loading, setLoading] = useState(true)
  const [totalRecords, setTotalRecords] = useState(0)

  // Pagination state
  const [currentPage, setCurrentPage] = useState(0)
  const [pageSize, setPageSize] = useState(25)

  // Modal state
  const [showCreateModal, setShowCreateModal] = useState(false)
  const [showDeleteModal, setShowDeleteModal] = useState(false)
  const [showMetadataModal, setShowMetadataModal] = useState(false)
  const [selectedAccount, setSelectedAccount] = useState(null)

  // Form state
  const [formData, setFormData] = useState({ name: '', initialBalance: '', notes: '' })
  const [formLoading, setFormLoading] = useState(false)

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

    try {
      setFormLoading(true)
      const initialBalance = formData.initialBalance
        ? parseFloat(formData.initialBalance)
        : null

      await api.createAccount(
        formData.name.trim(),
        initialBalance,
        formData.notes.trim() || null
      )

      setShowCreateModal(false)
      setFormData({ name: '', initialBalance: '', notes: '' })
      loadAccounts()
    } catch (err) {
      setError(err.message || 'Failed to create account')
    } finally {
      setFormLoading(false)
    }
  }

  // Helper to get GUID from account object with various casing
  const getAccountGuid = (account) => account?.guid || account?.Guid || account?.GUID || null

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

  const viewEntries = (account) => {
    const guid = getAccountGuid(account)
    navigate(`/entries?account=${guid}`)
  }

  const totalPages = Math.ceil(totalRecords / pageSize)

  // Helper to get GUID from row with various casing
  const getRowGuid = (row) => row.guid || row.Guid || row.GUID || ''

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
      label: 'GUID',
      className: 'col-guid',
      sortable: true,
      filterable: true,
      render: (row) => (
        <span className="guid-cell-wrapper">
          <span className="guid-cell">
            {getRowGuid(row)}
          </span>
          <CopyButton text={getRowGuid(row)} title="Copy GUID" />
        </span>
      ),
      filterValue: (row) => getRowGuid(row)
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
              label: 'Delete Account',
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
          <button className="btn btn-primary" onClick={() => setShowCreateModal(true)}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <line x1="12" y1="5" x2="12" y2="19"/>
              <line x1="5" y1="12" x2="19" y2="12"/>
            </svg>
            Create Account
          </button>
        </div>
      </div>

      <DataTable
        columns={columns}
        data={accounts}
        loading={loading}
        emptyMessage="No accounts found"
        rowKey="guid"
      />

      <Pagination
        currentPage={currentPage}
        totalPages={totalPages}
        totalRecords={totalRecords}
        pageSize={pageSize}
        onPageChange={handlePageChange}
        onPageSizeChange={handlePageSizeChange}
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
