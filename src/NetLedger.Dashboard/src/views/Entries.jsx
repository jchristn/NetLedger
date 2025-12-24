import React, { useState, useEffect, useCallback } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useApp } from '../context/AppContext'
import DataTable from '../components/DataTable'
import Pagination from '../components/Pagination'
import ActionMenu from '../components/ActionMenu'
import Modal, { ConfirmModal, ViewMetadataModal } from '../components/Modal'
import CopyButton from '../components/CopyButton'
import { formatDate, formatCurrency, normalizeEnumerationResult } from '../api/api'
import './Entries.css'

// Helper to extract GUID from an object with various casing conventions
const getGuid = (obj) => {
  if (!obj) return null
  // Check GUID first (server uses uppercase GUID), then lowercase variants
  return obj.GUID || obj.guid || obj.Guid || null
}

export default function Entries() {
  const { api, setError } = useApp()
  const [searchParams, setSearchParams] = useSearchParams()

  // Account selection state
  const [accounts, setAccounts] = useState([])
  const [selectedAccountGuid, setSelectedAccountGuid] = useState(searchParams.get('account') || '')
  const [selectedAccount, setSelectedAccount] = useState(null)
  const [balance, setBalance] = useState(null)
  const [accountsLoading, setAccountsLoading] = useState(true)

  // Entries state
  const [entries, setEntries] = useState([])
  const [loading, setLoading] = useState(false)
  const [totalRecords, setTotalRecords] = useState(0)

  // Pagination state
  const [currentPage, setCurrentPage] = useState(0)
  const [pageSize, setPageSize] = useState(25)

  // Filter state
  const [showOnlyPending, setShowOnlyPending] = useState(false)

  // Modal state
  const [showAddEntryModal, setShowAddEntryModal] = useState(false)
  const [showCommitModal, setShowCommitModal] = useState(false)
  const [showCancelModal, setShowCancelModal] = useState(false)
  const [showMetadataModal, setShowMetadataModal] = useState(false)
  const [showCommitEntryModal, setShowCommitEntryModal] = useState(false)
  const [selectedEntry, setSelectedEntry] = useState(null)
  const [entryType, setEntryType] = useState('credit') // 'credit' or 'debit'

  // Form state
  const [formData, setFormData] = useState({ amount: '', description: '', commitImmediately: false })
  const [formLoading, setFormLoading] = useState(false)

  // Load accounts on mount
  useEffect(() => {
    loadAccounts()
  }, [])

  // Load entries when account changes
  useEffect(() => {
    if (selectedAccountGuid) {
      loadEntries()
      loadBalance()
      // Update URL
      setSearchParams({ account: selectedAccountGuid })
    } else {
      setEntries([])
      setTotalRecords(0)
      setBalance(null)
      setSearchParams({})
    }
  }, [selectedAccountGuid, currentPage, pageSize, showOnlyPending])

  const loadAccounts = async () => {
    try {
      setAccountsLoading(true)
      const result = await api.listAccounts({ maxResults: 1000 })
      const { objects: accountsList } = normalizeEnumerationResult(result)
      setAccounts(accountsList)

      // If we have a preselected account from URL, find it
      if (selectedAccountGuid) {
        const account = accountsList.find(a => getGuid(a) === selectedAccountGuid)
        setSelectedAccount(account || null)
      }
    } catch (err) {
      setError(err.message || 'Failed to load accounts')
    } finally {
      setAccountsLoading(false)
    }
  }

  const loadEntries = useCallback(async () => {
    if (!selectedAccountGuid) return

    try {
      setLoading(true)

      let result
      if (showOnlyPending) {
        result = await api.getPendingEntries(selectedAccountGuid)
        // Pending entries endpoint returns an array directly or wrapped
        const entriesList = Array.isArray(result) ? result : (result?.Objects || result?.objects || [])
        setEntries(entriesList)
        setTotalRecords(entriesList.length)
      } else {
        result = await api.listEntries(selectedAccountGuid, {
          maxResults: pageSize,
          skip: currentPage * pageSize,
          ordering: 'CreatedDescending'
        })
        const { objects, totalRecords } = normalizeEnumerationResult(result)
        setEntries(objects)
        setTotalRecords(totalRecords)
      }
    } catch (err) {
      setError(err.message || 'Failed to load entries')
    } finally {
      setLoading(false)
    }
  }, [api, selectedAccountGuid, currentPage, pageSize, showOnlyPending, setError])

  const loadBalance = async () => {
    if (!selectedAccountGuid) return

    try {
      const result = await api.getBalance(selectedAccountGuid)
      setBalance(result)
    } catch (err) {
      // Balance might not exist yet
      setBalance(null)
    }
  }

  const handleAccountChange = (e) => {
    const guid = e.target.value
    setSelectedAccountGuid(guid)
    setCurrentPage(0)

    const account = accounts.find(a => getGuid(a) === guid)
    setSelectedAccount(account || null)
  }

  const handlePageChange = (page) => {
    setCurrentPage(page)
  }

  const handlePageSizeChange = (size) => {
    setPageSize(size)
    setCurrentPage(0)
  }

  const openAddEntryModal = (type) => {
    setEntryType(type)
    setFormData({ amount: '', description: '', commitImmediately: false })
    setShowAddEntryModal(true)
  }

  const handleAddEntry = async (e) => {
    e.preventDefault()

    if (!formData.amount || parseFloat(formData.amount) <= 0) {
      setError('Amount must be greater than 0')
      return
    }

    try {
      setFormLoading(true)

      const entryData = [{
        amount: parseFloat(formData.amount),
        description: formData.description.trim()
      }]

      if (entryType === 'credit') {
        await api.addCredits(selectedAccountGuid, entryData, formData.commitImmediately)
      } else {
        await api.addDebits(selectedAccountGuid, entryData, formData.commitImmediately)
      }

      setShowAddEntryModal(false)
      setFormData({ amount: '', description: '', commitImmediately: false })
      loadEntries()
      loadBalance()
    } catch (err) {
      setError(err.message || `Failed to add ${entryType}`)
    } finally {
      setFormLoading(false)
    }
  }

  const handleCommit = async () => {
    try {
      setFormLoading(true)
      await api.commitEntries(selectedAccountGuid)
      setShowCommitModal(false)
      loadEntries()
      loadBalance()
    } catch (err) {
      setError(err.message || 'Failed to commit entries')
    } finally {
      setFormLoading(false)
    }
  }

  const handleCancelEntry = async () => {
    if (!selectedEntry) return

    try {
      setFormLoading(true)
      await api.cancelEntry(selectedAccountGuid, getGuid(selectedEntry))
      setShowCancelModal(false)
      setSelectedEntry(null)
      loadEntries()
      loadBalance()
    } catch (err) {
      setError(err.message || 'Failed to cancel entry')
    } finally {
      setFormLoading(false)
    }
  }

  const openCancelModal = (entry) => {
    setSelectedEntry(entry)
    setShowCancelModal(true)
  }

  const openMetadataModal = (entry) => {
    setSelectedEntry(entry)
    setShowMetadataModal(true)
  }

  const openCommitEntryModal = (entry) => {
    setSelectedEntry(entry)
    setShowCommitEntryModal(true)
  }

  const handleCommitEntry = async () => {
    if (!selectedEntry) return

    try {
      setFormLoading(true)
      await api.commitEntries(selectedAccountGuid, { entryGuids: [getGuid(selectedEntry)] })
      setShowCommitEntryModal(false)
      setSelectedEntry(null)
      loadEntries()
      loadBalance()
    } catch (err) {
      setError(err.message || 'Failed to commit entry')
    } finally {
      setFormLoading(false)
    }
  }

  const totalPages = Math.ceil(totalRecords / pageSize)

  const hasPendingEntries = balance && (
    (balance.pendingCredits?.count ?? balance.PendingCredits?.Count ?? 0) > 0 ||
    (balance.pendingDebits?.count ?? balance.PendingDebits?.Count ?? 0) > 0
  )

  const columns = [
    {
      key: 'guid',
      label: 'GUID',
      className: 'col-guid',
      sortable: true,
      filterable: true,
      render: (row) => (
        <span className="guid-cell-wrapper">
          <span className="guid-cell">
            {getGuid(row)}
          </span>
          <CopyButton text={getGuid(row)} title="Copy GUID" />
        </span>
      ),
      filterValue: (row) => getGuid(row) || ''
    },
    {
      key: 'type',
      label: 'Type',
      className: 'col-type',
      sortable: true,
      filterable: true,
      render: (row) => {
        const type = row.type || row.Type || 'Unknown'
        let badgeClass = 'badge-neutral'
        if (type === 'Credit') badgeClass = 'badge-success'
        else if (type === 'Debit') badgeClass = 'badge-danger'
        else if (type === 'Balance') badgeClass = 'badge-primary'

        return <span className={`badge ${badgeClass}`}>{type}</span>
      },
      filterValue: (row) => row.type || row.Type || ''
    },
    {
      key: 'amount',
      label: 'Amount',
      className: 'col-amount',
      sortable: true,
      filterable: true,
      filterExact: true,
      render: (row) => {
        const type = row.type || row.Type
        const amount = row.amount || row.Amount || 0
        const isCredit = type === 'Credit'
        const isDebit = type === 'Debit'

        return (
          <span className={`amount ${isCredit ? 'amount-positive' : isDebit ? 'amount-negative' : 'amount-neutral'}`}>
            {isCredit ? '+' : isDebit ? '-' : ''}{formatCurrency(Math.abs(amount))}
          </span>
        )
      },
      sortValue: (row) => row.amount || row.Amount || 0,
      filterValue: (row) => String(row.amount || row.Amount || 0)
    },
    {
      key: 'description',
      label: 'Description',
      sortable: true,
      filterable: true,
      render: (row) => (
        <span className="entry-description" title={row.description || row.Description}>
          {row.description || row.Description || '-'}
        </span>
      ),
      filterValue: (row) => row.description || row.Description || ''
    },
    {
      key: 'isCommitted',
      label: 'Status',
      className: 'col-status',
      sortable: true,
      render: (row) => {
        const isCommitted = row.isCommitted ?? row.IsCommitted ?? false
        return (
          <span className={`badge ${isCommitted ? 'badge-success' : 'badge-warning'}`}>
            {isCommitted ? 'Committed' : 'Pending'}
          </span>
        )
      },
      sortValue: (row) => row.isCommitted ?? row.IsCommitted ?? false
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
      render: (row) => {
        const isCommitted = row.isCommitted ?? row.IsCommitted ?? false
        const isPending = !isCommitted
        const type = row.type || row.Type

        return (
          <ActionMenu
            items={[
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
              isPending && type !== 'Balance' ? { divider: true } : null,
              isPending && type !== 'Balance' ? {
                label: 'Commit Entry',
                variant: 'primary',
                icon: (
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    <polyline points="20 6 9 17 4 12"/>
                  </svg>
                ),
                onClick: () => openCommitEntryModal(row)
              } : null,
              isPending && type !== 'Balance' ? {
                label: 'Cancel Entry',
                variant: 'danger',
                icon: (
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    <circle cx="12" cy="12" r="10"/>
                    <line x1="15" y1="9" x2="9" y2="15"/>
                    <line x1="9" y1="9" x2="15" y2="15"/>
                  </svg>
                ),
                onClick: () => openCancelModal(row)
              } : null
            ]}
          />
        )
      }
    }
  ]

  return (
    <div className="entries-page">
      <div className="page-header">
        <div className="page-header-left">
          <h2 className="page-title">Entries</h2>
          <p className="page-description">View and manage account entries</p>
        </div>
      </div>

      {/* Account Selector */}
      <div className="entries-controls card">
        <div className="card-body">
          <div className="entries-controls-row">
            <div className="account-selector">
              <label htmlFor="accountSelect">Select Account</label>
              <select
                id="accountSelect"
                value={selectedAccountGuid}
                onChange={handleAccountChange}
                disabled={accountsLoading}
              >
                <option value="">-- Select an account --</option>
                {accounts.map(account => (
                  <option key={getGuid(account)} value={getGuid(account)}>
                    {account.name || account.Name}
                  </option>
                ))}
              </select>
            </div>

            <div className="account-guid-input">
              <label htmlFor="accountGuid">Or Enter GUID</label>
              <input
                type="text"
                id="accountGuid"
                value={selectedAccountGuid}
                onChange={(e) => setSelectedAccountGuid(e.target.value.trim())}
                placeholder="Paste account GUID"
                disabled={accountsLoading}
              />
            </div>

            {selectedAccountGuid && (
              <>
                <div className="entries-filter">
                  <label className="checkbox-label">
                    <input
                      type="checkbox"
                      checked={showOnlyPending}
                      onChange={(e) => {
                        setShowOnlyPending(e.target.checked)
                        setCurrentPage(0)
                      }}
                    />
                    <span>Show only pending entries</span>
                  </label>
                </div>

                <div className="entries-actions">
                  <button
                    className="btn btn-success"
                    onClick={() => openAddEntryModal('credit')}
                  >
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <line x1="12" y1="5" x2="12" y2="19"/>
                      <line x1="5" y1="12" x2="19" y2="12"/>
                    </svg>
                    Add Credit
                  </button>
                  <button
                    className="btn btn-danger"
                    onClick={() => openAddEntryModal('debit')}
                  >
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <line x1="5" y1="12" x2="19" y2="12"/>
                    </svg>
                    Add Debit
                  </button>
                  <button
                    className="btn btn-primary"
                    onClick={() => setShowCommitModal(true)}
                    disabled={!hasPendingEntries}
                  >
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <polyline points="20 6 9 17 4 12"/>
                    </svg>
                    Commit Entries
                  </button>
                </div>
              </>
            )}
          </div>

          {/* Balance Summary */}
          {selectedAccountGuid && balance && (
            <div className="balance-summary">
              <div className="balance-item">
                <span className="balance-label">Committed Balance</span>
                <span className={`balance-value ${(balance.committedBalance ?? balance.CommittedBalance ?? 0) >= 0 ? 'amount-positive' : 'amount-negative'}`}>
                  {formatCurrency(balance.committedBalance ?? balance.CommittedBalance ?? 0)}
                </span>
              </div>
              <div className="balance-item">
                <span className="balance-label">Pending Balance</span>
                <span className={`balance-value ${(balance.pendingBalance ?? balance.PendingBalance ?? 0) >= 0 ? 'amount-positive' : 'amount-negative'}`}>
                  {formatCurrency(balance.pendingBalance ?? balance.PendingBalance ?? 0)}
                </span>
              </div>
              <div className="balance-item">
                <span className="balance-label">Pending Credits</span>
                <span className="balance-value amount-positive">
                  +{formatCurrency(balance.pendingCredits?.total ?? balance.PendingCredits?.Total ?? 0)}
                  <span className="balance-count">
                    ({balance.pendingCredits?.count ?? balance.PendingCredits?.Count ?? 0})
                  </span>
                </span>
              </div>
              <div className="balance-item">
                <span className="balance-label">Pending Debits</span>
                <span className="balance-value amount-negative">
                  -{formatCurrency(balance.pendingDebits?.total ?? balance.PendingDebits?.Total ?? 0)}
                  <span className="balance-count">
                    ({balance.pendingDebits?.count ?? balance.PendingDebits?.Count ?? 0})
                  </span>
                </span>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Entries Table */}
      {selectedAccountGuid ? (
        <>
          <DataTable
            columns={columns}
            data={entries}
            loading={loading}
            emptyMessage={showOnlyPending ? 'No pending entries' : 'No entries found'}
            rowKey="guid"
          />

          {!showOnlyPending && (
            <Pagination
              currentPage={currentPage}
              totalPages={totalPages}
              totalRecords={totalRecords}
              pageSize={pageSize}
              onPageChange={handlePageChange}
              onPageSizeChange={handlePageSizeChange}
            />
          )}
        </>
      ) : (
        <div className="card">
          <div className="card-body">
            <div className="empty-state">
              <div className="empty-state-icon">
                <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
                  <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/>
                  <path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/>
                </svg>
              </div>
              <span className="empty-state-title">Select an account</span>
              <span className="empty-state-description">
                Choose an account from the dropdown above to view its entries
              </span>
            </div>
          </div>
        </div>
      )}

      {/* Add Entry Modal */}
      <Modal
        isOpen={showAddEntryModal}
        onClose={() => setShowAddEntryModal(false)}
        title={`Add ${entryType === 'credit' ? 'Credit' : 'Debit'}`}
        size="small"
        footer={
          <>
            <button
              className="btn btn-secondary"
              onClick={() => setShowAddEntryModal(false)}
              disabled={formLoading}
            >
              Cancel
            </button>
            <button
              className={`btn ${entryType === 'credit' ? 'btn-success' : 'btn-danger'}`}
              onClick={handleAddEntry}
              disabled={formLoading}
            >
              {formLoading ? (
                <>
                  <span className="spinner spinner-sm"></span>
                  Adding...
                </>
              ) : (
                `Add ${entryType === 'credit' ? 'Credit' : 'Debit'}`
              )}
            </button>
          </>
        }
      >
        <form onSubmit={handleAddEntry}>
          <div className="form-group">
            <label htmlFor="entryAmount">Amount *</label>
            <input
              type="number"
              id="entryAmount"
              value={formData.amount}
              onChange={(e) => setFormData({ ...formData, amount: e.target.value })}
              placeholder="0.00"
              step="0.01"
              min="0.01"
              disabled={formLoading}
              autoFocus
            />
          </div>

          <div className="form-group">
            <label htmlFor="entryDescription">Description</label>
            <input
              type="text"
              id="entryDescription"
              value={formData.description}
              onChange={(e) => setFormData({ ...formData, description: e.target.value })}
              placeholder="Optional description"
              disabled={formLoading}
            />
          </div>

          <div className="form-group">
            <label className="checkbox-label">
              <input
                type="checkbox"
                checked={formData.commitImmediately}
                onChange={(e) => setFormData({ ...formData, commitImmediately: e.target.checked })}
                disabled={formLoading}
              />
              <span>Commit immediately</span>
            </label>
            <p className="form-help-text">
              {formData.commitImmediately
                ? 'Entry will be committed immediately and reflected in the balance.'
                : 'Entry will be added as pending and can be committed later.'}
            </p>
          </div>
        </form>
      </Modal>

      {/* Commit Modal */}
      <ConfirmModal
        isOpen={showCommitModal}
        onClose={() => setShowCommitModal(false)}
        onConfirm={handleCommit}
        title="Commit Pending Entries"
        message={`Are you sure you want to commit all pending entries for this account? This will finalize ${(balance?.pendingCredits?.count ?? balance?.PendingCredits?.Count ?? 0) + (balance?.pendingDebits?.count ?? balance?.PendingDebits?.Count ?? 0)} pending entries.`}
        confirmText="Commit"
        variant="primary"
        isLoading={formLoading}
      />

      {/* Cancel Entry Modal */}
      <ConfirmModal
        isOpen={showCancelModal}
        onClose={() => {
          setShowCancelModal(false)
          setSelectedEntry(null)
        }}
        onConfirm={handleCancelEntry}
        title="Cancel Entry"
        message={`Are you sure you want to cancel this ${(selectedEntry?.type || selectedEntry?.Type || '').toLowerCase()} entry of ${formatCurrency(selectedEntry?.amount || selectedEntry?.Amount || 0)}? This action cannot be undone.`}
        confirmText="Cancel Entry"
        variant="danger"
        isLoading={formLoading}
      />

      {/* Commit Entry Modal */}
      <ConfirmModal
        isOpen={showCommitEntryModal}
        onClose={() => {
          setShowCommitEntryModal(false)
          setSelectedEntry(null)
        }}
        onConfirm={handleCommitEntry}
        title="Commit Entry"
        message={`Are you sure you want to commit this ${(selectedEntry?.type || selectedEntry?.Type || '').toLowerCase()} entry of ${formatCurrency(selectedEntry?.amount || selectedEntry?.Amount || 0)}?`}
        confirmText="Commit Entry"
        variant="primary"
        isLoading={formLoading}
      />

      {/* View Metadata Modal */}
      <ViewMetadataModal
        isOpen={showMetadataModal}
        onClose={() => {
          setShowMetadataModal(false)
          setSelectedEntry(null)
        }}
        title="Entry Metadata"
        data={selectedEntry}
      />
    </div>
  )
}
