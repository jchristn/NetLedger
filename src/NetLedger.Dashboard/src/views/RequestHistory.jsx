import React, { useCallback, useEffect, useMemo, useState } from 'react'
import { useApp } from '../context/useApp'
import DataTable from '../components/DataTable'
import Pagination from '../components/Pagination'
import Modal, { ConfirmModal, ViewMetadataModal } from '../components/Modal'
import CopyButton from '../components/CopyButton'
import { formatDate, normalizeEnumerationResult } from '../api/api'
import { getRoleFlags } from '../utils/roles'
import './RequestHistory.css'

const HTTP_METHODS = ['', 'GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD']

function initialFilters() {
  return {
    method: '',
    statusCode: '',
    pathContains: '',
    principalId: '',
    tenantId: '',
    fromUtc: '',
    toUtc: ''
  }
}

function toUtc(value) {
  if (!value) return null
  return new Date(value).toISOString()
}

function valueOf(row, key) {
  return row?.[key] ?? row?.[key.charAt(0).toLowerCase() + key.slice(1)]
}

function formatDuration(value) {
  if (!value && value !== 0) return '-'
  return `${Number(value).toFixed(1)} ms`
}

function formatBytes(value) {
  if (!value) return '0 B'
  if (value < 1024) return `${value} B`
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`
  return `${(value / (1024 * 1024)).toFixed(1)} MB`
}

function buildYAxisLabels(maxValue, maxLabels = 5) {
  if (maxValue <= 0) return [0, 1]
  if (maxValue <= 4) {
    return Array.from({ length: maxValue + 1 }, (_, index) => index)
  }

  const roughStep = maxValue / (maxLabels - 1)
  const power = Math.pow(10, Math.floor(Math.log10(roughStep)))
  const normalized = roughStep / power
  const step = (normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10) * power
  const ceiling = Math.ceil(maxValue / step) * step
  const labels = []

  for (let value = 0; value <= ceiling && labels.length < maxLabels; value += step) {
    labels.push(value)
  }

  if (labels[labels.length - 1] < maxValue) {
    labels[labels.length - 1] = ceiling
  }

  return labels
}

function shouldShowXAxisLabel(index, totalCount, maxLabels = 8) {
  if (totalCount <= maxLabels) return true
  if (index === 0 || index === totalCount - 1) return true
  return index % Math.ceil(totalCount / maxLabels) === 0
}

function formatChartLabel(timestamp) {
  const date = new Date(timestamp)
  if (Number.isNaN(date.getTime())) return ''
  return date.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
}

export default function RequestHistory() {
  const { api, currentUser, effectivePermissions, setError } = useApp()
  const { isRegularUser } = getRoleFlags(currentUser, effectivePermissions)
  const [entries, setEntries] = useState([])
  const [summary, setSummary] = useState(null)
  const [loading, setLoading] = useState(true)
  const [totalRecords, setTotalRecords] = useState(0)
  const [currentPage, setCurrentPage] = useState(0)
  const [pageSize, setPageSize] = useState(25)
  const [filters, setFilters] = useState(initialFilters)
  const [appliedFilters, setAppliedFilters] = useState(initialFilters)
  const [selectedEntry, setSelectedEntry] = useState(null)
  const [jsonEntry, setJsonEntry] = useState(null)
  const [deleteEntry, setDeleteEntry] = useState(null)
  const [deleteManyOpen, setDeleteManyOpen] = useState(false)
  const [deleting, setDeleting] = useState(false)

  const query = useMemo(() => ({
    maxResults: pageSize,
    skip: currentPage * pageSize,
    method: appliedFilters.method || null,
    statusCode: appliedFilters.statusCode || null,
    pathContains: appliedFilters.pathContains || null,
    principalId: appliedFilters.principalId || null,
    tenantId: appliedFilters.tenantId || null,
    fromUtc: toUtc(appliedFilters.fromUtc),
    toUtc: toUtc(appliedFilters.toUtc)
  }), [appliedFilters, currentPage, pageSize])

  const summaryQuery = useMemo(() => ({
    ...query,
    maxResults: 1000,
    skip: 0,
    bucketMinutes: 15
  }), [query])

  const loadHistory = useCallback(async () => {
    try {
      setLoading(true)
      const [historyResult, summaryResult] = await Promise.all([
        api.listRequestHistory(query),
        api.summarizeRequestHistory(summaryQuery)
      ])
      const normalized = normalizeEnumerationResult(historyResult)
      setEntries(normalized.objects)
      setTotalRecords(normalized.totalRecords)
      setSummary(summaryResult)
    } catch (err) {
      setError(err.message || 'Failed to load request history')
    } finally {
      setLoading(false)
    }
  }, [api, query, setError, summaryQuery])

  useEffect(() => {
    loadHistory()
  }, [loadHistory])

  function updateFilter(key, value) {
    setFilters((current) => ({ ...current, [key]: value }))
  }

  function applyFilters(event) {
    event.preventDefault()
    setCurrentPage(0)
    setAppliedFilters(filters)
  }

  function resetFilters() {
    const empty = initialFilters()
    setFilters(empty)
    setAppliedFilters(empty)
    setCurrentPage(0)
  }

  async function openEntry(row) {
    try {
      const id = valueOf(row, 'Id')
      const fullEntry = id ? await api.readRequestHistoryEntry(id) : row
      setSelectedEntry(fullEntry)
    } catch (err) {
      setError(err.message || 'Failed to load request history entry')
    }
  }

  async function openJson(row) {
    try {
      const id = valueOf(row, 'Id')
      const fullEntry = id ? await api.readRequestHistoryEntry(id) : row
      setJsonEntry(fullEntry)
    } catch (err) {
      setError(err.message || 'Failed to load request history entry')
    }
  }

  async function confirmDeleteEntry() {
    if (!deleteEntry) return
    try {
      setDeleting(true)
      await api.deleteRequestHistoryEntry(valueOf(deleteEntry, 'Id'))
      setDeleteEntry(null)
      loadHistory()
    } catch (err) {
      setError(err.message || 'Failed to delete request history entry')
    } finally {
      setDeleting(false)
    }
  }

  async function confirmDeleteMany() {
    try {
      setDeleting(true)
      await api.deleteRequestHistory({
        method: appliedFilters.method || null,
        statusCode: appliedFilters.statusCode || null,
        pathContains: appliedFilters.pathContains || null,
        principalId: appliedFilters.principalId || null,
        tenantId: appliedFilters.tenantId || null,
        fromUtc: toUtc(appliedFilters.fromUtc),
        toUtc: toUtc(appliedFilters.toUtc)
      })
      setDeleteManyOpen(false)
      setCurrentPage(0)
      loadHistory()
    } catch (err) {
      setError(err.message || 'Failed to delete matching request history')
    } finally {
      setDeleting(false)
    }
  }

  const columns = [
    {
      key: 'method',
      label: 'Method',
      render: row => <span className={`method-pill method-${String(valueOf(row, 'Method') || '').toLowerCase()}`}>{valueOf(row, 'Method')}</span>,
      sortValue: row => valueOf(row, 'Method') || ''
    },
    {
      key: 'path',
      label: 'Path',
      render: row => (
        <div className="request-path-cell">
          <span>{valueOf(row, 'Path')}</span>
          <small>{valueOf(row, 'Url')}</small>
        </div>
      ),
      sortValue: row => valueOf(row, 'Path') || ''
    },
    {
      key: 'statusCode',
      label: 'Status',
      render: row => <span className={Number(valueOf(row, 'StatusCode')) < 400 ? 'status-ok' : 'status-error'}>{valueOf(row, 'StatusCode')}</span>,
      sortValue: row => Number(valueOf(row, 'StatusCode') || 0)
    },
    {
      key: 'durationMs',
      label: 'Duration',
      render: row => formatDuration(valueOf(row, 'DurationMs')),
      sortValue: row => Number(valueOf(row, 'DurationMs') || 0)
    },
    {
      key: 'principalId',
      label: 'Principal',
      render: row => <code>{valueOf(row, 'PrincipalId') || '-'}</code>,
      sortValue: row => valueOf(row, 'PrincipalId') || ''
    },
    {
      key: 'createdUtc',
      label: 'Created',
      className: 'col-date',
      render: row => formatDate(valueOf(row, 'CreatedUtc')),
      sortValue: row => valueOf(row, 'CreatedUtc') || ''
    }
  ]

  const totalPages = Math.ceil(totalRecords / pageSize)

  return (
    <div className="request-history-page">
      <div className="page-header">
        <div className="page-header-left">
          <h2 className="page-title">Request History</h2>
          <p className="page-description">Review API traffic captured for your authorization scope.</p>
        </div>
        {!isRegularUser && (
          <div className="page-header-actions">
            <button className="btn btn-danger" onClick={() => setDeleteManyOpen(true)} disabled={totalRecords === 0}>
              Delete Matching
            </button>
          </div>
        )}
      </div>

      <Summary summary={summary} loading={loading} />

      <section className="request-history-panel">
        <div className="request-history-panel-header">
          <div>
            <h3>Filters</h3>
            <p>Narrow the chart and table by method, status, path, principal, tenant, or timestamp.</p>
          </div>
          <button type="button" className="btn btn-secondary" onClick={loadHistory} disabled={loading}>
            {loading ? 'Refreshing...' : 'Refresh'}
          </button>
        </div>
        <form className="request-history-filters" onSubmit={applyFilters}>
          <label>
            <span>Method</span>
            <select value={filters.method} onChange={(event) => updateFilter('method', event.target.value)}>
              {HTTP_METHODS.map((method) => <option key={method || 'all'} value={method}>{method || 'All'}</option>)}
            </select>
          </label>
          <label>
            <span>Status</span>
            <input value={filters.statusCode} onChange={(event) => updateFilter('statusCode', event.target.value)} placeholder="403" />
          </label>
          <label className="filter-wide">
            <span>Path Contains</span>
            <input value={filters.pathContains} onChange={(event) => updateFilter('pathContains', event.target.value)} placeholder="/v1/accounts" />
          </label>
          <label>
            <span>Principal</span>
            <input value={filters.principalId} onChange={(event) => updateFilter('principalId', event.target.value)} placeholder="user id" />
          </label>
          <label>
            <span>Tenant</span>
            <input value={filters.tenantId} onChange={(event) => updateFilter('tenantId', event.target.value)} placeholder="tenant id" />
          </label>
          <label>
            <span>From</span>
            <input type="datetime-local" value={filters.fromUtc} onChange={(event) => updateFilter('fromUtc', event.target.value)} />
          </label>
          <label>
            <span>To</span>
            <input type="datetime-local" value={filters.toUtc} onChange={(event) => updateFilter('toUtc', event.target.value)} />
          </label>
          <div className="filter-actions">
            <button type="submit" className="btn btn-primary">Apply</button>
            <button type="button" className="btn btn-secondary" onClick={resetFilters}>Reset</button>
          </div>
        </form>
      </section>

      <section className="request-history-panel request-history-table-panel">
        <div className="request-history-panel-header">
          <div>
            <h3>Captured Requests</h3>
            <p>Click a row to inspect request and response capture. Use the row menu for edit, view, JSON, or delete actions.</p>
          </div>
        </div>
        <Pagination
          currentPage={currentPage}
          totalPages={totalPages}
          totalRecords={totalRecords}
          pageSize={pageSize}
          onPageChange={setCurrentPage}
          onPageSizeChange={(size) => {
            setPageSize(size)
            setCurrentPage(0)
          }}
        />

        <DataTable
          columns={columns}
          data={entries}
          loading={loading}
          emptyMessage="No request history found"
          rowKey="Id"
          onRowClick={openEntry}
          onEdit={openEntry}
          onView={openEntry}
          onViewJson={openJson}
          onDelete={isRegularUser ? null : setDeleteEntry}
        />
      </section>

      <RequestDetailModal entry={selectedEntry} onClose={() => setSelectedEntry(null)} />
      <ViewMetadataModal isOpen={Boolean(jsonEntry)} onClose={() => setJsonEntry(null)} title="Request History JSON" data={jsonEntry} />
      <ConfirmModal
        isOpen={Boolean(deleteEntry)}
        onClose={() => setDeleteEntry(null)}
        onConfirm={confirmDeleteEntry}
        title="Delete Request History Entry"
        message="Delete this request history entry?"
        confirmText="Delete"
        isLoading={deleting}
      />
      <ConfirmModal
        isOpen={deleteManyOpen}
        onClose={() => setDeleteManyOpen(false)}
        onConfirm={confirmDeleteMany}
        title="Delete Matching Request History"
        message={`Delete ${totalRecords} matching request history entries in your authorization scope?`}
        confirmText="Delete Matching"
        isLoading={deleting}
      />
    </div>
  )
}

function Summary({ summary, loading }) {
  const total = valueOf(summary, 'TotalCount') || 0
  const success = valueOf(summary, 'TotalSuccess') || 0
  const failed = valueOf(summary, 'TotalFailure') || 0
  const average = valueOf(summary, 'AverageDurationMs')
  const buckets = valueOf(summary, 'Buckets') || []

  return (
    <section className="request-history-summary">
      <div className="summary-stat-grid">
        <div className="summary-stat">
          <span>Total Requests</span>
          <strong>{total.toLocaleString()}</strong>
        </div>
        <div className="summary-stat">
          <span>Success</span>
          <strong className="summary-success">{success.toLocaleString()}</strong>
        </div>
        <div className="summary-stat">
          <span>Failed</span>
          <strong className="summary-failure">{failed.toLocaleString()}</strong>
        </div>
        <div className="summary-stat">
          <span>Average Duration</span>
          <strong>{formatDuration(average)}</strong>
        </div>
      </div>
      <div className="request-history-chart-card">
        <div className="request-history-chart-header">
          <div>
            <h3>Traffic over Time</h3>
            <p>Successful and failed requests grouped by server summary buckets.</p>
          </div>
          {loading && <span>Loading...</span>}
        </div>
        <SummaryChart buckets={buckets} />
        <div className="request-history-chart-legend">
          <span><i className="legend-success"></i>Success</span>
          <span><i className="legend-failure"></i>Failed</span>
        </div>
      </div>
    </section>
  )
}

function SummaryChart({ buckets }) {
  const visibleBuckets = buckets.slice(-48)
  const highestCount = Math.max(0, ...visibleBuckets.map((bucket) => (valueOf(bucket, 'SuccessCount') || 0) + (valueOf(bucket, 'FailureCount') || 0)))
  const yLabels = buildYAxisLabels(highestCount)
  const yMax = yLabels[yLabels.length - 1] || 1
  const chartWidth = 900
  const chartHeight = 260
  const padding = { top: 18, right: 18, bottom: 44, left: 46 }
  const innerWidth = chartWidth - padding.left - padding.right
  const innerHeight = chartHeight - padding.top - padding.bottom
  const slotWidth = innerWidth / Math.max(1, visibleBuckets.length)
  const barWidth = Math.max(4, Math.min(22, slotWidth - 4))

  return (
    <div className="summary-chart" aria-label="Request volume chart">
      <svg viewBox={`0 0 ${chartWidth} ${chartHeight}`} role="img" aria-label="Request history summary chart">
        {yLabels.map((label) => {
          const y = padding.top + innerHeight - (label / yMax) * innerHeight
          return (
            <g key={label}>
              <line x1={padding.left} x2={chartWidth - padding.right} y1={y} y2={y} className={`history-grid-line ${label === 0 ? 'history-axis' : ''}`} />
              <text x={padding.left - 9} y={y + 4} className="history-y-label">{label}</text>
            </g>
          )
        })}
        <line x1={padding.left} x2={padding.left} y1={padding.top} y2={padding.top + innerHeight} className="history-axis" />
        {visibleBuckets.map((bucket, index) => {
          const success = valueOf(bucket, 'SuccessCount') || 0
          const failure = valueOf(bucket, 'FailureCount') || 0
          const total = success + failure
          const totalHeight = total > 0 ? Math.max(2, (total / yMax) * innerHeight) : 0
          const successHeight = total > 0 ? totalHeight * (success / total) : 0
          const failureHeight = total > 0 ? totalHeight * (failure / total) : 0
          const x = padding.left + index * slotWidth + (slotWidth - barWidth) / 2
          const y = padding.top + innerHeight - totalHeight

          return (
            <g key={`${valueOf(bucket, 'BucketStartUtc')}-${index}`}>
              {total > 0 && (
                <>
                  <rect className="history-bar-success" x={x} y={y} width={barWidth} height={successHeight} rx="2" />
                  <rect className="history-bar-failure" x={x} y={y + successHeight} width={barWidth} height={failureHeight} rx="2" />
                </>
              )}
              {shouldShowXAxisLabel(index, visibleBuckets.length) && (
                <text x={x + barWidth / 2} y={chartHeight - 15} className="history-x-label">{formatChartLabel(valueOf(bucket, 'BucketStartUtc'))}</text>
              )}
              <title>{`${formatDate(valueOf(bucket, 'BucketStartUtc'))}: ${total} requests`}</title>
            </g>
          )
        })}
      </svg>
    </div>
  )
}

function RequestDetailModal({ entry, onClose }) {
  if (!entry) return null

  const requestHeaders = valueOf(entry, 'RequestHeaders') || {}
  const responseHeaders = valueOf(entry, 'ResponseHeaders') || {}
  const requestBody = valueOf(entry, 'RequestBody') || ''
  const responseBody = valueOf(entry, 'ResponseBody') || ''

  const statusCode = Number(valueOf(entry, 'StatusCode') || 0)
  const method = valueOf(entry, 'Method') || '-'
  const id = valueOf(entry, 'Id') || ''
  const path = valueOf(entry, 'Path') || '-'

  return (
    <Modal isOpen={Boolean(entry)} onClose={onClose} title="Request History Entry" size="large">
      <div className="request-detail">
        <div className="request-detail-hero">
          <div className="request-detail-route">
            <span className={`method-pill method-${String(method).toLowerCase()}`}>{method}</span>
            <code>{path}</code>
          </div>
          <div className="request-detail-hero-meta">
            <span className={statusCode < 400 ? 'status-ok' : 'status-error'}>HTTP {statusCode || '-'}</span>
            <span>{formatDuration(valueOf(entry, 'DurationMs'))}</span>
            <span>{formatDate(valueOf(entry, 'CreatedUtc'))}</span>
          </div>
        </div>

        <div className="request-detail-id-row">
          <span>Request ID</span>
          <code>{id || '-'}</code>
          <CopyButton text={id} title="Copy request ID" />
        </div>

        <div className="request-detail-grid">
          <DetailItem label="Tenant" value={valueOf(entry, 'TenantId')} />
          <DetailItem label="Principal" value={valueOf(entry, 'PrincipalId')} />
          <DetailItem label="Principal Type" value={valueOf(entry, 'PrincipalType')} />
          <DetailItem label="Source IP" value={valueOf(entry, 'SourceIp')} />
          <DetailItem label="Request Size" value={formatBytes(valueOf(entry, 'RequestBodyBytes'))} />
          <DetailItem label="Response Size" value={formatBytes(valueOf(entry, 'ResponseBodyBytes'))} />
          <DetailItem label="Completed" value={formatDate(valueOf(entry, 'CompletedUtc'))} />
          <DetailItem label="URL" value={valueOf(entry, 'Url')} mono />
        </div>

        <div className="request-detail-sections">
          <CollapsibleBlock title="Request Headers" value={requestHeaders} />
          <CollapsibleBlock title="Request Body" value={requestBody} defaultExpanded />
          <CollapsibleBlock title="Response Headers" value={responseHeaders} />
          <CollapsibleBlock title="Response Body" value={responseBody} defaultExpanded />
        </div>
      </div>
    </Modal>
  )
}

function DetailItem({ label, value, mono = false }) {
  return (
    <div className="detail-item">
      <span>{label}</span>
      <strong className={mono ? 'detail-mono' : ''}>{value || '-'}</strong>
    </div>
  )
}

function CollapsibleBlock({ title, value, defaultExpanded = false }) {
  const [expanded, setExpanded] = useState(defaultExpanded)
  const text = typeof value === 'string' ? value : JSON.stringify(value, null, 2)

  return (
    <div className="history-collapsible">
      <button type="button" className="history-collapsible-header" onClick={() => setExpanded((current) => !current)}>
        <span>{title}</span>
        <span>{expanded ? 'Hide' : 'Show'}</span>
      </button>
      {expanded && (
        <div className="history-collapsible-body">
          <div className="history-code-toolbar">
            <span>Content</span>
            <CopyButton text={text || ''} title={`Copy ${title}`} />
          </div>
          <pre>{text || '(empty)'}</pre>
        </div>
      )}
    </div>
  )
}
