import React, { useState, useEffect, useCallback } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useApp } from '../context/useApp'
import DataTable from '../components/DataTable'
import Pagination from '../components/Pagination'
import ActionMenu from '../components/ActionMenu'
import Modal, { ConfirmModal, RecordModal, ViewMetadataModal } from '../components/Modal'
import CopyButton from '../components/CopyButton'
import { MetadataLabelsEditor, MetadataTagsEditor } from '../components/MetadataEditor'
import { labelsToPayload, tagsToPayload } from '../components/metadataEditorUtils'
import { formatDate, formatCurrency, normalizeEnumerationResult } from '../api/api'
import { getRoleFlags, valueOf } from '../utils/roles'
import './Entries.css'

// Helper to extract GUID from an object with various casing conventions
const getGuid = (obj) => {
  if (!obj) return null
  // Check GUID first (server uses uppercase GUID), then lowercase variants
  return obj.Id || obj.id || obj.GUID || obj.guid || obj.Guid || null
}

const getAccountTenantId = (account) => valueOf(account, 'TenantId') || ''

const getTenantLabel = (tenant) => {
  const id = getGuid(tenant)
  const name = valueOf(tenant, 'Name') || id
  return id && name !== id ? `${name} (${id})` : name
}

const createEmptyFormData = () => ({
  amount: '',
  description: '',
  labels: [''],
  tags: [{ key: '', value: '' }],
  commitImmediately: false
})

const createEmptyEntryFilters = () => ({
  search: '',
  ordering: 'CreatedDescending',
  startTime: '',
  endTime: '',
  amountMin: '',
  amountMax: '',
  creditMin: '',
  creditMax: '',
  debitMin: '',
  debitMax: '',
  labels: [''],
  tags: [{ key: '', value: '' }]
})

const toNullableNumber = (value) => {
  if (value === null || value === undefined || value === '') return null
  const parsed = Number(value)
  return Number.isFinite(parsed) ? parsed : null
}

const toApiDate = (value) => value ? new Date(value).toISOString() : null

const formatTags = (tags) => {
  if (!tags || typeof tags !== 'object') return ''
  return Object.entries(tags)
    .map(([key, value]) => `${key}=${value}`)
    .join(', ')
}

export default function Entries() {
  const { api, setError, currentUser, effectivePermissions } = useApp()
  const { isSystemAdmin } = getRoleFlags(currentUser, effectivePermissions)
  const [searchParams, setSearchParams] = useSearchParams()

  // Account selection state
  const [accounts, setAccounts] = useState([])
  const [tenants, setTenants] = useState([])
  const [selectedTenantId, setSelectedTenantId] = useState(searchParams.get('tenant') || '')
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
  const [isSearchPanelOpen, setIsSearchPanelOpen] = useState(false)
  const [filterDraft, setFilterDraft] = useState(createEmptyEntryFilters())
  const [appliedFilters, setAppliedFilters] = useState(createEmptyEntryFilters())

  // Modal state
  const [showAddEntryModal, setShowAddEntryModal] = useState(false)
  const [showCommitModal, setShowCommitModal] = useState(false)
  const [showEditModal, setShowEditModal] = useState(false)
  const [showViewModal, setShowViewModal] = useState(false)
  const [showCancelModal, setShowCancelModal] = useState(false)
  const [showMetadataModal, setShowMetadataModal] = useState(false)
  const [showCommitEntryModal, setShowCommitEntryModal] = useState(false)
  const [selectedEntry, setSelectedEntry] = useState(null)
  const [entryType, setEntryType] = useState('credit') // 'credit' or 'debit'

  // Form state
  const [formData, setFormData] = useState(createEmptyFormData())
  const [formLoading, setFormLoading] = useState(false)

  const selectedAccountTenantId = selectedAccount ? getAccountTenantId(selectedAccount) : ''
  const requestTenantId = isSystemAdmin ? selectedTenantId || selectedAccountTenantId || null : null

  const loadTenants = useCallback(async () => {
    if (!isSystemAdmin) {
      setTenants([])
      return
    }

    try {
      const result = await api.listTenants({ maxResults: 1000, ordering: 'CreatedDescending' })
      setTenants(normalizeEnumerationResult(result).objects)
    } catch (err) {
      setError(err.message || 'Failed to load tenants')
    }
  }, [api, isSystemAdmin, setError])

  const loadAccounts = useCallback(async () => {
    try {
      setAccountsLoading(true)
      const result = await api.listAccounts({
        maxResults: 1000,
        tenantId: isSystemAdmin ? selectedTenantId || null : null
      })
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
  }, [api, isSystemAdmin, selectedAccountGuid, selectedTenantId, setError])

  const loadEntries = useCallback(async () => {
    if (!selectedAccountGuid) return

    try {
      setLoading(true)

      let result
      if (showOnlyPending) {
        result = await api.getPendingEntries(selectedAccountGuid, requestTenantId)
        // Pending entries endpoint returns an array directly or wrapped
        const entriesList = Array.isArray(result) ? result : (result?.Objects || result?.objects || [])
        setEntries(entriesList.slice(currentPage * pageSize, (currentPage + 1) * pageSize))
        setTotalRecords(entriesList.length)
      } else {
        result = await api.listEntries(selectedAccountGuid, {
          maxResults: pageSize,
          skip: currentPage * pageSize,
          ordering: appliedFilters.ordering,
          search: appliedFilters.search.trim() || null,
          startTime: toApiDate(appliedFilters.startTime),
          endTime: toApiDate(appliedFilters.endTime),
          amountMin: toNullableNumber(appliedFilters.amountMin),
          amountMax: toNullableNumber(appliedFilters.amountMax),
          creditMin: toNullableNumber(appliedFilters.creditMin),
          creditMax: toNullableNumber(appliedFilters.creditMax),
          debitMin: toNullableNumber(appliedFilters.debitMin),
          debitMax: toNullableNumber(appliedFilters.debitMax),
          labels: parseLabels(appliedFilters.labels),
          tags: parseTags(appliedFilters.tags),
          tenantId: requestTenantId
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
  }, [api, selectedAccountGuid, currentPage, pageSize, showOnlyPending, appliedFilters, requestTenantId, setError])

  const loadBalance = useCallback(async () => {
    if (!selectedAccountGuid) return

    try {
      const result = await api.getBalance(selectedAccountGuid, requestTenantId)
      setBalance(result)
    } catch (err) {
      // Balance might not exist yet
      setBalance(null)
    }
  }, [api, selectedAccountGuid, requestTenantId])

  useEffect(() => {
    loadTenants()
  }, [loadTenants])

  // Load accounts on mount and when tenant scope changes
  useEffect(() => {
    loadAccounts()
  }, [loadAccounts])

  // Load entries when account changes
  useEffect(() => {
    if (selectedAccountGuid) {
      loadEntries()
      loadBalance()
      // Update URL
      setSearchParams({
        ...(isSystemAdmin && selectedTenantId ? { tenant: selectedTenantId } : {}),
        account: selectedAccountGuid
      })
    } else {
      setEntries([])
      setTotalRecords(0)
      setBalance(null)
      setSearchParams(isSystemAdmin && selectedTenantId ? { tenant: selectedTenantId } : {})
    }
  }, [isSystemAdmin, selectedAccountGuid, selectedTenantId, loadEntries, loadBalance, setSearchParams])

  const handleTenantChange = (e) => {
    setSelectedTenantId(e.target.value)
    setSelectedAccountGuid('')
    setSelectedAccount(null)
    setEntries([])
    setTotalRecords(0)
    setBalance(null)
    setShowOnlyPending(false)
    setCurrentPage(0)
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
    setFormData(createEmptyFormData())
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
        description: formData.description.trim(),
        labels: parseLabels(formData.labels),
        tags: parseTags(formData.tags)
      }]

      if (entryType === 'credit') {
        await api.addCredits(selectedAccountGuid, entryData, formData.commitImmediately, requestTenantId)
      } else {
        await api.addDebits(selectedAccountGuid, entryData, formData.commitImmediately, requestTenantId)
      }

      setShowAddEntryModal(false)
      setFormData(createEmptyFormData())
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
      await api.commitEntries(selectedAccountGuid, { tenantId: requestTenantId })
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
      await api.cancelEntry(selectedAccountGuid, getGuid(selectedEntry), requestTenantId)
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

  const openEditModal = (entry) => {
    setSelectedEntry(entry)
    setShowEditModal(true)
  }

  const openViewModal = (entry) => {
    setSelectedEntry(entry)
    setShowViewModal(true)
  }

  const openCommitEntryModal = (entry) => {
    setSelectedEntry(entry)
    setShowCommitEntryModal(true)
  }

  const handleCommitEntry = async () => {
    if (!selectedEntry) return

    try {
      setFormLoading(true)
      await api.commitEntries(selectedAccountGuid, { entryGuids: [getGuid(selectedEntry)], tenantId: requestTenantId })
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

  const parseLabels = (value) => {
    return labelsToPayload(value)
  }

  const parseTags = (value) => {
    return tagsToPayload(value)
  }

  const applyEntryFilters = () => {
    setAppliedFilters(filterDraft)
    setCurrentPage(0)
  }

  const clearEntryFilters = () => {
    const emptyFilters = createEmptyEntryFilters()
    setFilterDraft(emptyFilters)
    setAppliedFilters(emptyFilters)
    setCurrentPage(0)
  }

  const hasEntryFilters = (filters) => Boolean(
    filters.search.trim() ||
    filters.startTime ||
    filters.endTime ||
    filters.amountMin ||
    filters.amountMax ||
    filters.creditMin ||
    filters.creditMax ||
    filters.debitMin ||
    filters.debitMax ||
    parseLabels(filters.labels).length > 0 ||
    Object.keys(parseTags(filters.tags)).length > 0 ||
    filters.ordering !== 'CreatedDescending'
  )

  const hasDraftFilters = hasEntryFilters(filterDraft)
  const hasAppliedFilters = hasEntryFilters(appliedFilters)
  const activeFilterCount = [
    appliedFilters.search.trim(),
    appliedFilters.startTime,
    appliedFilters.endTime,
    appliedFilters.amountMin,
    appliedFilters.amountMax,
    appliedFilters.creditMin,
    appliedFilters.creditMax,
    appliedFilters.debitMin,
    appliedFilters.debitMax,
    parseLabels(appliedFilters.labels).length > 0,
    Object.keys(parseTags(appliedFilters.tags)).length > 0,
    appliedFilters.ordering !== 'CreatedDescending'
  ].filter(Boolean).length

  const hasPendingEntries = balance && (
    (balance.pendingCredits?.count ?? balance.PendingCredits?.Count ?? 0) > 0 ||
    (balance.pendingDebits?.count ?? balance.PendingDebits?.Count ?? 0) > 0
  )

  const columns = [
    {
      key: 'guid',
      label: 'ID',
      className: 'col-guid',
      sortable: true,
      filterable: true,
      render: (row) => (
        <span className="guid-cell-wrapper">
          <span className="guid-cell">
            {getGuid(row)}
          </span>
          <CopyButton text={getGuid(row)} title="Copy ID" />
        </span>
      ),
      filterValue: (row) => getGuid(row) || ''
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
      key: 'tags',
      label: 'Tags',
      filterable: true,
      render: (row) => {
        const tagText = formatTags(row.tags || row.Tags || {})
        return tagText ? <span className="entry-tags" title={tagText}>{tagText}</span> : <span className="text-muted">None</span>
      },
      filterValue: (row) => formatTags(row.tags || row.Tags || {})
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
                label: 'Edit',
                onClick: () => openEditModal(row)
              },
              {
                label: 'View',
                onClick: () => openViewModal(row)
              },
              {
                label: 'View JSON',
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
              {
                label: 'Delete',
                variant: 'danger',
                disabled: !isPending || type === 'Balance',
                icon: (
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    <circle cx="12" cy="12" r="10"/>
                    <line x1="15" y1="9" x2="9" y2="15"/>
                    <line x1="9" y1="9" x2="15" y2="15"/>
                  </svg>
                ),
                onClick: () => openCancelModal(row)
              }
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
            {isSystemAdmin && (
              <div className="account-selector">
                <label htmlFor="tenantSelect">Tenant</label>
                <select
                  id="tenantSelect"
                  value={selectedTenantId}
                  onChange={handleTenantChange}
                  disabled={accountsLoading}
                >
                  <option value="">All visible tenants</option>
                  {tenants.map(tenant => (
                    <option key={getGuid(tenant)} value={getGuid(tenant)}>
                      {getTenantLabel(tenant)}
                    </option>
                  ))}
                </select>
              </div>
            )}

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
                    {account.name || account.Name}{isSystemAdmin && !selectedTenantId && getAccountTenantId(account) ? ` (${getAccountTenantId(account)})` : ''}
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

          {selectedAccountGuid && (
            <div className="entry-search-panel">
              <button
                className="entry-search-toggle"
                type="button"
                onClick={() => setIsSearchPanelOpen((current) => !current)}
                aria-expanded={isSearchPanelOpen}
                aria-controls="entrySearchFilters"
              >
                <span className="entry-search-toggle-label">
                  <svg className={isSearchPanelOpen ? 'entry-search-toggle-icon open' : 'entry-search-toggle-icon'} width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    <polyline points="9 18 15 12 9 6"/>
                  </svg>
                  Search Filters
                </span>
                {(showOnlyPending || activeFilterCount > 0) && (
                  <span className="entry-search-toggle-meta">
                    {showOnlyPending ? 'Disabled while showing pending' : `${activeFilterCount} active`}
                  </span>
                )}
              </button>

              {isSearchPanelOpen && (
                <div id="entrySearchFilters" className="entry-search-panel-body">
                  <div className="entry-search-grid">
                    <div className="entry-filter-field entry-filter-field-wide">
                      <label htmlFor="entrySearch">Search</label>
                      <input
                        id="entrySearch"
                        type="text"
                        value={filterDraft.search}
                        onChange={(e) => setFilterDraft({ ...filterDraft, search: e.target.value })}
                        placeholder="Description"
                        disabled={showOnlyPending}
                      />
                    </div>

                    <div className="entry-filter-field">
                      <label htmlFor="entryOrdering">Order</label>
                      <select
                        id="entryOrdering"
                        value={filterDraft.ordering}
                        onChange={(e) => setFilterDraft({ ...filterDraft, ordering: e.target.value })}
                        disabled={showOnlyPending}
                      >
                        <option value="CreatedDescending">Newest first</option>
                        <option value="CreatedAscending">Oldest first</option>
                        <option value="AmountDescending">Amount descending</option>
                        <option value="AmountAscending">Amount ascending</option>
                      </select>
                    </div>

                    <div className="entry-filter-field">
                      <label htmlFor="entryStartTime">Created After</label>
                      <input
                        id="entryStartTime"
                        type="datetime-local"
                        value={filterDraft.startTime}
                        onChange={(e) => setFilterDraft({ ...filterDraft, startTime: e.target.value })}
                        disabled={showOnlyPending}
                      />
                    </div>

                    <div className="entry-filter-field">
                      <label htmlFor="entryEndTime">Created Before</label>
                      <input
                        id="entryEndTime"
                        type="datetime-local"
                        value={filterDraft.endTime}
                        onChange={(e) => setFilterDraft({ ...filterDraft, endTime: e.target.value })}
                        disabled={showOnlyPending}
                      />
                    </div>

                    <div className="entry-filter-field">
                      <label htmlFor="entryAmountMin">Any Amount Min</label>
                      <input
                        id="entryAmountMin"
                        type="number"
                        value={filterDraft.amountMin}
                        onChange={(e) => setFilterDraft({ ...filterDraft, amountMin: e.target.value })}
                        placeholder="0.00"
                        step="0.01"
                        disabled={showOnlyPending}
                      />
                    </div>

                    <div className="entry-filter-field">
                      <label htmlFor="entryAmountMax">Any Amount Max</label>
                      <input
                        id="entryAmountMax"
                        type="number"
                        value={filterDraft.amountMax}
                        onChange={(e) => setFilterDraft({ ...filterDraft, amountMax: e.target.value })}
                        placeholder="0.00"
                        step="0.01"
                        disabled={showOnlyPending}
                      />
                    </div>

                    <div className="entry-filter-field">
                      <label htmlFor="entryCreditMin">Credit Min</label>
                      <input
                        id="entryCreditMin"
                        type="number"
                        value={filterDraft.creditMin}
                        onChange={(e) => setFilterDraft({ ...filterDraft, creditMin: e.target.value })}
                        placeholder="0.00"
                        step="0.01"
                        disabled={showOnlyPending}
                      />
                    </div>

                    <div className="entry-filter-field">
                      <label htmlFor="entryCreditMax">Credit Max</label>
                      <input
                        id="entryCreditMax"
                        type="number"
                        value={filterDraft.creditMax}
                        onChange={(e) => setFilterDraft({ ...filterDraft, creditMax: e.target.value })}
                        placeholder="0.00"
                        step="0.01"
                        disabled={showOnlyPending}
                      />
                    </div>

                    <div className="entry-filter-field">
                      <label htmlFor="entryDebitMin">Debit Min</label>
                      <input
                        id="entryDebitMin"
                        type="number"
                        value={filterDraft.debitMin}
                        onChange={(e) => setFilterDraft({ ...filterDraft, debitMin: e.target.value })}
                        placeholder="5.00"
                        step="0.01"
                        disabled={showOnlyPending}
                      />
                    </div>

                    <div className="entry-filter-field">
                      <label htmlFor="entryDebitMax">Debit Max</label>
                      <input
                        id="entryDebitMax"
                        type="number"
                        value={filterDraft.debitMax}
                        onChange={(e) => setFilterDraft({ ...filterDraft, debitMax: e.target.value })}
                        placeholder="50.00"
                        step="0.01"
                        disabled={showOnlyPending}
                      />
                    </div>

                    <div className="entry-filter-field entry-filter-field-wide">
                      <label>Labels</label>
                      <MetadataLabelsEditor
                        idPrefix="entryFilterLabels"
                        value={filterDraft.labels}
                        onChange={(labels) => setFilterDraft({ ...filterDraft, labels })}
                        disabled={showOnlyPending}
                      />
                    </div>

                    <div className="entry-filter-field entry-filter-field-wide">
                      <label>Tags</label>
                      <MetadataTagsEditor
                        idPrefix="entryFilterTags"
                        value={filterDraft.tags}
                        onChange={(tags) => setFilterDraft({ ...filterDraft, tags })}
                        disabled={showOnlyPending}
                      />
                    </div>
                  </div>

                  <div className="entry-filter-actions">
                    <button className="btn btn-secondary" type="button" onClick={clearEntryFilters} disabled={showOnlyPending || (!hasDraftFilters && !hasAppliedFilters)}>
                      Clear
                    </button>
                    <button className="btn btn-primary" type="button" onClick={applyEntryFilters} disabled={showOnlyPending}>
                      Apply Search
                    </button>
                  </div>
                </div>
              )}
            </div>
          )}

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
            data={entries}
            loading={loading}
            emptyMessage={showOnlyPending ? 'No pending entries' : 'No entries found'}
            onRowClick={openEditModal}
            rowKey="guid"
          />
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
            <label>Labels</label>
            <MetadataLabelsEditor
              idPrefix="entryLabels"
              value={formData.labels}
              onChange={(labels) => setFormData({ ...formData, labels })}
              disabled={formLoading}
            />
          </div>

          <div className="form-group">
            <label>Tags</label>
            <MetadataTagsEditor
              idPrefix="entryTags"
              value={formData.tags}
              onChange={(tags) => setFormData({ ...formData, tags })}
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

      <RecordModal
        isOpen={showEditModal}
        onClose={() => {
          setShowEditModal(false)
          setSelectedEntry(null)
        }}
        title={`Edit ${getGuid(selectedEntry) || 'Entry'}`}
        data={selectedEntry}
        mode="edit"
      />

      <RecordModal
        isOpen={showViewModal}
        onClose={() => {
          setShowViewModal(false)
          setSelectedEntry(null)
        }}
        title={`View ${getGuid(selectedEntry) || 'Entry'}`}
        data={selectedEntry}
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
