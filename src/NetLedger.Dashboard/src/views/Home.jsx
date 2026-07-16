import React, { useCallback, useEffect, useMemo, useState } from 'react'
import { useApp } from '../context/useApp'
import Pagination from '../components/Pagination'
import { formatCurrency, formatDate, normalizeEnumerationResult, normalizeBalances } from '../api/api'
import { getRoleFlags, getTenantId, valueOf } from '../utils/roles'
import './Home.css'

const TIME_RANGES = {
  hour: { label: 'Last Hour', durationMs: 60 * 60 * 1000, buckets: 60 },
  day: { label: 'Last Day', durationMs: 24 * 60 * 60 * 1000, buckets: 96 },
  week: { label: 'Last Week', durationMs: 7 * 24 * 60 * 60 * 1000, buckets: 84 },
  month: { label: 'Last Month', durationMs: 30 * 24 * 60 * 60 * 1000, buckets: 90 }
}

function getObjectId(obj) {
  return valueOf(obj, 'Id') || valueOf(obj, 'GUID') || valueOf(obj, 'Guid') || valueOf(obj, 'AccountGuid') || ''
}

function getAccountTenantId(account) {
  return valueOf(account, 'TenantId') || ''
}

function getAccountName(account) {
  return valueOf(account, 'Name') || getObjectId(account)
}

function getEntryType(entry) {
  return valueOf(entry, 'Type') || ''
}

function getEntryAmount(entry) {
  return Number(valueOf(entry, 'Amount') || 0)
}

function getEntryIsCommitted(entry) {
  const value = valueOf(entry, 'IsCommitted')
  return value === null || value === undefined ? true : value !== false
}

function getEntryCreatedUtc(entry) {
  return valueOf(entry, 'CreatedUtc')
}

function getTenantLabel(tenant) {
  const id = getObjectId(tenant)
  const name = valueOf(tenant, 'Name') || id
  return id && name !== id ? `${name} (${id})` : name
}

function getUserLabel(user) {
  const email = valueOf(user, 'Email') || getObjectId(user)
  const name = [valueOf(user, 'FirstName'), valueOf(user, 'LastName')].filter(Boolean).join(' ')
  return name ? `${name} (${email})` : email
}

function buildRange(rangeKey) {
  const definition = TIME_RANGES[rangeKey] || TIME_RANGES.day
  const end = new Date()
  const start = new Date(end.getTime() - definition.durationMs)
  return { ...definition, key: rangeKey, start, end }
}

function buildBuckets(entries, range) {
  const bucketMs = range.durationMs / range.buckets
  const buckets = Array.from({ length: range.buckets }, (_, index) => {
    const startMs = range.start.getTime() + index * bucketMs
    return {
      startUtc: new Date(startMs).toISOString(),
      endUtc: new Date(startMs + bucketMs).toISOString(),
      credits: 0,
      debits: 0,
      count: 0,
      amount: 0,
      creditAmount: 0,
      debitAmount: 0,
      committedCreditAmount: 0,
      committedDebitAmount: 0
    }
  })

  entries.forEach((entry) => {
    const created = new Date(getEntryCreatedUtc(entry)).getTime()
    if (Number.isNaN(created) || created < range.start.getTime() || created > range.end.getTime()) return

    const index = Math.min(range.buckets - 1, Math.max(0, Math.floor((created - range.start.getTime()) / bucketMs)))
    const amount = getEntryAmount(entry)
    const type = getEntryType(entry)
    buckets[index].count += 1
    buckets[index].amount += amount
    if (type === 'Credit') {
      buckets[index].credits += 1
      buckets[index].creditAmount += amount
      if (getEntryIsCommitted(entry)) buckets[index].committedCreditAmount += amount
    }
    if (type === 'Debit') {
      buckets[index].debits += 1
      buckets[index].debitAmount += amount
      if (getEntryIsCommitted(entry)) buckets[index].committedDebitAmount += amount
    }
  })

  return buckets
}

function buildValueBuckets(buckets, currentBalance) {
  const committedDelta = buckets.reduce((sum, bucket) => sum + bucket.committedCreditAmount - bucket.committedDebitAmount, 0)
  let runningBalance = currentBalance - committedDelta

  return buckets.map((bucket) => {
    runningBalance += bucket.committedCreditAmount - bucket.committedDebitAmount
    return {
      ...bucket,
      value: runningBalance
    }
  })
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

function buildLinearYAxisLabels(values, maxLabels = 5) {
  const numericValues = values.filter((value) => Number.isFinite(value))
  if (numericValues.length === 0) return [0, 1]

  let minValue = Math.min(...numericValues)
  let maxValue = Math.max(...numericValues)

  if (minValue === maxValue) {
    const pad = Math.max(1, Math.abs(maxValue) * 0.1)
    minValue -= pad
    maxValue += pad
  }

  const roughStep = (maxValue - minValue) / (maxLabels - 1)
  const power = Math.pow(10, Math.floor(Math.log10(roughStep)))
  const normalized = roughStep / power
  const step = (normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10) * power
  const start = Math.floor(minValue / step) * step
  const end = Math.ceil(maxValue / step) * step
  const labels = []

  for (let value = start; value <= end + step / 2 && labels.length < maxLabels + 1; value += step) {
    labels.push(value)
  }

  if (!labels.includes(0) && start < 0 && end > 0) {
    labels.push(0)
    labels.sort((a, b) => a - b)
  }

  return labels
}

function formatAxisCurrency(value) {
  const absolute = Math.abs(value)
  const formatter = new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: 'USD',
    notation: absolute >= 1000 ? 'compact' : 'standard',
    maximumFractionDigits: absolute >= 1000 ? 1 : 0
  })
  return formatter.format(value)
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

function formatBucketTimestamp(bucket) {
  return `${formatDate(bucket.startUtc)} - ${formatDate(bucket.endUtc)}`
}

function getBucketTimestampRows(bucket) {
  return [
    { label: 'Start', value: formatDate(bucket.startUtc) },
    { label: 'End', value: formatDate(bucket.endUtc) }
  ]
}

function buildChartTooltip(x, y, width, height, title, rows) {
  const yPercent = (y / height) * 100
  const estimatedHeight = 52 + rows.length * 20
  const hasRoomAbove = y > estimatedHeight + 8
  const hasRoomBelow = y < height - estimatedHeight - 8
  return {
    xPercent: (x / width) * 100,
    yPercent,
    placement: hasRoomAbove || !hasRoomBelow ? 'above' : 'below',
    title,
    rows
  }
}

export default function Home() {
  const { api, setError, serverInfo, tenantId, currentUser, currentTenant, effectivePermissions } = useApp()
  const { isSystemAdmin, isTenantAdmin, isRegularUser } = getRoleFlags(currentUser, effectivePermissions)
  const resolvedTenantId = getTenantId(currentUser, effectivePermissions, tenantId)
  const [loading, setLoading] = useState(true)
  const [chartLoading, setChartLoading] = useState(false)
  const [tenants, setTenants] = useState([])
  const [users, setUsers] = useState([])
  const [accountUserMaps, setAccountUserMaps] = useState({})
  const [rangeKey, setRangeKey] = useState('day')
  const [selectedTenantId, setSelectedTenantId] = useState('')
  const [selectedUserId, setSelectedUserId] = useState('')
  const [selectedAccountId, setSelectedAccountId] = useState('')
  const [entries, setEntries] = useState([])
  const [stats, setStats] = useState({
    totalAccounts: 0,
    totalCommittedBalance: 0,
    totalPendingBalance: 0,
    totalPendingCredits: 0,
    totalPendingDebits: 0,
    accounts: []
  })
  const [currentPage, setCurrentPage] = useState(0)
  const [pageSize, setPageSize] = useState(5)

  const visibleAccounts = useMemo(() => {
    let accounts = stats.accounts

    if (isSystemAdmin && selectedTenantId) {
      accounts = accounts.filter((account) => getAccountTenantId(account) === selectedTenantId)
    }

    if ((isTenantAdmin || isRegularUser) && selectedAccountId) {
      accounts = accounts.filter((account) => getObjectId(account) === selectedAccountId)
    }

    if (isTenantAdmin && selectedUserId) {
      accounts = accounts.filter((account) => {
        const accountId = getObjectId(account)
        const mappings = accountUserMaps[accountId] || []
        return mappings.some((mapping) => valueOf(mapping, 'UserId') === selectedUserId)
      })
    }

    return accounts
  }, [accountUserMaps, isRegularUser, isSystemAdmin, isTenantAdmin, selectedAccountId, selectedTenantId, selectedUserId, stats.accounts])

  const range = useMemo(() => buildRange(rangeKey), [rangeKey])
  const chartBuckets = useMemo(() => buildBuckets(entries, range), [entries, range])
  const visibleCommittedBalance = useMemo(
    () => visibleAccounts.reduce((sum, account) => sum + Number(account.committedBalance || 0), 0),
    [visibleAccounts]
  )
  const valueBuckets = useMemo(() => buildValueBuckets(chartBuckets, visibleCommittedBalance), [chartBuckets, visibleCommittedBalance])
  const transactionTotal = entries.length
  const transactionAmount = entries.reduce((sum, entry) => sum + getEntryAmount(entry), 0)
  const totalCreditAmount = chartBuckets.reduce((sum, bucket) => sum + bucket.creditAmount, 0)
  const totalDebitAmount = chartBuckets.reduce((sum, bucket) => sum + bucket.debitAmount, 0)
  const totalPages = Math.ceil(stats.accounts.length / pageSize)
  const pagedAccounts = stats.accounts.slice(currentPage * pageSize, (currentPage + 1) * pageSize)

  const loadStats = useCallback(async () => {
    try {
      setLoading(true)

      const tenantList = isSystemAdmin
        ? normalizeEnumerationResult(await api.listTenants({ maxResults: 1000, ordering: 'CreatedDescending' })).objects
        : currentTenant ? [currentTenant] : []
      setTenants(tenantList)

      const tenantForScopedLists = isSystemAdmin ? selectedTenantId : resolvedTenantId
      const [accountsResult, balancesResult, usersResult] = await Promise.all([
        api.listAccounts({ maxResults: 1000, tenantId: tenantForScopedLists || null }),
        api.getAllBalances(),
        tenantForScopedLists && (isTenantAdmin || isRegularUser)
          ? api.listUsers(tenantForScopedLists, { maxResults: 1000, ordering: 'CreatedDescending' }).catch(() => null)
          : Promise.resolve(null)
      ])

      const accounts = normalizeEnumerationResult(accountsResult).objects
      const balanceList = normalizeBalances(balancesResult)
      const userList = usersResult ? normalizeEnumerationResult(usersResult).objects : []
      setUsers(userList)

      let totalCommitted = 0
      let totalPending = 0
      let totalPendingCredits = 0
      let totalPendingDebits = 0

      const accountsWithBalances = accounts.map(account => {
        const accountId = getObjectId(account)
        const balance = balanceList.find(balanceItem =>
          balanceItem.accountGuid === accountId || balanceItem.AccountGuid === accountId
        )

        const committedBalance = balance?.committedBalance ?? balance?.CommittedBalance ?? 0
        const pendingBalance = balance?.pendingBalance ?? balance?.PendingBalance ?? 0
        const pendingCredits = balance?.pendingCredits?.total ?? balance?.PendingCredits?.Total ?? 0
        const pendingDebits = balance?.pendingDebits?.total ?? balance?.PendingDebits?.Total ?? 0

        totalCommitted += committedBalance
        totalPending += pendingBalance
        totalPendingCredits += pendingCredits
        totalPendingDebits += pendingDebits

        return {
          ...account,
          committedBalance,
          pendingBalance,
          pendingCredits,
          pendingDebits
        }
      })

      if (isTenantAdmin && tenantForScopedLists) {
        const mapPairs = await Promise.all(accountsWithBalances.map(async (account) => {
          try {
            const accountId = getObjectId(account)
            const mapResult = await api.listAccountUsers(tenantForScopedLists, accountId, { maxResults: 1000 })
            return [accountId, normalizeEnumerationResult(mapResult).objects]
          } catch {
            return [getObjectId(account), []]
          }
        }))
        setAccountUserMaps(Object.fromEntries(mapPairs))
      } else {
        setAccountUserMaps({})
      }

      setStats({
        totalAccounts: accounts.length,
        totalCommittedBalance: totalCommitted,
        totalPendingBalance: totalPending,
        totalPendingCredits,
        totalPendingDebits,
        accounts: accountsWithBalances
      })
    } catch (err) {
      setError(err.message || 'Failed to load statistics')
    } finally {
      setLoading(false)
    }
  }, [api, currentTenant, isRegularUser, isSystemAdmin, isTenantAdmin, resolvedTenantId, selectedTenantId, setError])

  const loadChart = useCallback(async () => {
    try {
      setChartLoading(true)
      const accountList = visibleAccounts
      const rangeForQuery = buildRange(rangeKey)
      const entryGroups = await Promise.all(accountList.map(async (account) => {
        const accountId = getObjectId(account)
        const accountTenantId = getAccountTenantId(account) || selectedTenantId || resolvedTenantId
        try {
          const result = await api.listEntries(accountId, {
            maxResults: 1000,
            ordering: 'CreatedAscending',
            startTime: rangeForQuery.start.toISOString(),
            endTime: rangeForQuery.end.toISOString(),
            tenantId: accountTenantId || null
          })
          return normalizeEnumerationResult(result).objects
        } catch {
          return []
        }
      }))
      setEntries(entryGroups.flat())
    } catch (err) {
      setError(err.message || 'Failed to load transaction chart')
    } finally {
      setChartLoading(false)
    }
  }, [api, rangeKey, resolvedTenantId, selectedTenantId, setError, visibleAccounts])

  useEffect(() => {
    loadStats()
  }, [loadStats])

  useEffect(() => {
    if (!loading) {
      loadChart()
    }
  }, [loadChart, loading])

  useEffect(() => {
    setSelectedUserId('')
    setSelectedAccountId('')
  }, [selectedTenantId])

  const handlePageSizeChange = (size) => {
    setPageSize(size)
    setCurrentPage(0)
  }

  if (loading) {
    return (
      <div className="page-loading">
        <span className="spinner spinner-lg"></span>
        <span>Loading dashboard...</span>
      </div>
    )
  }

  return (
    <div className="home-page">
      {serverInfo && (
        <div className="server-info-card card">
          <div className="card-body">
            <div className="server-info-grid">
              <div className="server-info-item">
                <span className="server-info-label">Server</span>
                <span className="server-info-value">{serverInfo.name || serverInfo.Name || 'NetLedger'}</span>
              </div>
              <div className="server-info-item">
                <span className="server-info-label">Version</span>
                <span className="server-info-value">{serverInfo.version || serverInfo.Version || '-'}</span>
              </div>
              <div className="server-info-item">
                <span className="server-info-label">Uptime</span>
                <span className="server-info-value">{serverInfo.uptimeFormatted || serverInfo.UptimeFormatted || '-'}</span>
              </div>
              <div className="server-info-item">
                <span className="server-info-label">Started</span>
                <span className="server-info-value">{formatDate(serverInfo.startTimeUtc || serverInfo.StartTimeUtc)}</span>
              </div>
            </div>
          </div>
        </div>
      )}

      <div className="stats-grid">
        <StatCard iconClass="stat-icon-accounts" label="Total Accounts" value={stats.totalAccounts} icon="accounts" />
        <StatCard iconClass="stat-icon-balance" label="Committed Balance" value={formatCurrency(stats.totalCommittedBalance)} amount={stats.totalCommittedBalance} icon="balance" />
        <StatCard iconClass="stat-icon-pending" label="Pending Balance" value={formatCurrency(stats.totalPendingBalance)} amount={stats.totalPendingBalance} icon="pending" />
        <StatCard iconClass="stat-icon-credits" label="Pending Credits" value={formatCurrency(stats.totalPendingCredits)} icon="credits" />
        <StatCard iconClass="stat-icon-debits" label="Pending Debits" value={formatCurrency(stats.totalPendingDebits)} icon="debits" />
      </div>

      <section className="dashboard-chart-controls">
        <div className="chart-controls">
          <div className="range-toggle">
            {Object.entries(TIME_RANGES).map(([key, item]) => (
              <button key={key} className={rangeKey === key ? 'active' : ''} onClick={() => setRangeKey(key)}>
                {item.label}
              </button>
            ))}
          </div>

          <div className="drilldown-controls">
            {isSystemAdmin && (
              <label>
                <span>Tenant</span>
                <select value={selectedTenantId} onChange={(event) => setSelectedTenantId(event.target.value)}>
                  <option value="">All visible tenants</option>
                  {tenants.map((tenant) => (
                    <option key={getObjectId(tenant)} value={getObjectId(tenant)}>{getTenantLabel(tenant)}</option>
                  ))}
                </select>
              </label>
            )}

            {isTenantAdmin && (
              <label>
                <span>User</span>
                <select value={selectedUserId} onChange={(event) => setSelectedUserId(event.target.value)}>
                  <option value="">All tenant users</option>
                  {users.map((user) => (
                    <option key={getObjectId(user)} value={getObjectId(user)}>{getUserLabel(user)}</option>
                  ))}
                </select>
              </label>
            )}

            {(isTenantAdmin || isRegularUser) && (
              <label>
                <span>Account</span>
                <select value={selectedAccountId} onChange={(event) => setSelectedAccountId(event.target.value)}>
                  <option value="">All visible accounts</option>
                  {stats.accounts.map((account) => (
                    <option key={getObjectId(account)} value={getObjectId(account)}>{getAccountName(account)}</option>
                  ))}
                </select>
              </label>
            )}
          </div>
        </div>
      </section>

      <ChartPanel
        title="Value Recorded"
        summary={`${formatCurrency(visibleCommittedBalance)} current committed value`}
        loading={chartLoading}
      >
        <ValueRecordedChart buckets={valueBuckets} />
      </ChartPanel>

      <ChartPanel
        title="Transactions over Time"
        summary={`${transactionTotal} transactions, ${formatCurrency(transactionAmount)} total amount`}
        loading={chartLoading}
      >
        <TransactionsChart buckets={chartBuckets} />
      </ChartPanel>

      <ChartPanel
        title="Amounts over Time"
        summary={`${formatCurrency(totalCreditAmount)} credits, ${formatCurrency(totalDebitAmount)} debits`}
        loading={chartLoading}
      >
        <AmountsChart buckets={chartBuckets} />
      </ChartPanel>

      {stats.accounts.length > 0 && (
        <div className="recent-accounts card">
          <div className="card-header">
            <h3>Account Summary</h3>
          </div>
          <div className="card-body">
            <Pagination
              currentPage={currentPage}
              totalPages={totalPages}
              totalRecords={stats.accounts.length}
              pageSize={pageSize}
              pageSizeOptions={[5, 10, 25, 50]}
              onPageChange={setCurrentPage}
              onPageSizeChange={handlePageSizeChange}
            />
            <table className="accounts-summary-table">
              <thead>
                <tr>
                  <th>Account</th>
                  <th className="text-right">Committed</th>
                  <th className="text-right">Pending</th>
                </tr>
              </thead>
              <tbody>
                {pagedAccounts.map(account => (
                  <tr key={getObjectId(account)}>
                    <td>
                      <span className="account-name">{getAccountName(account)}</span>
                    </td>
                    <td className="text-right">
                      <span className={`amount ${account.committedBalance >= 0 ? 'amount-positive' : 'amount-negative'}`}>
                        {formatCurrency(account.committedBalance)}
                      </span>
                    </td>
                    <td className="text-right">
                      <span className={`amount ${account.pendingBalance >= 0 ? 'amount-positive' : 'amount-negative'}`}>
                        {formatCurrency(account.pendingBalance)}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  )
}

function ChartPanel({ title, summary, loading, children }) {
  return (
    <section className="transactions-chart-section">
      <div className="chart-header">
        <div>
          <h3>{title}</h3>
          <p>{summary}</p>
        </div>
        {loading && <span className="chart-loading">Loading...</span>}
      </div>
      {children}
    </section>
  )
}

function ChartTooltip({ tooltip }) {
  if (!tooltip) return null

  return (
    <div
      className={`chart-tooltip ${tooltip.placement === 'below' ? 'chart-tooltip-below' : 'chart-tooltip-above'}`}
      style={{
        left: `clamp(8.5rem, ${tooltip.xPercent}%, calc(100% - 8.5rem))`,
        top: `${tooltip.yPercent}%`
      }}
    >
      <div className="chart-tooltip-title">{tooltip.title}</div>
      {tooltip.rows.map((row) => (
        <div key={row.label} className="chart-tooltip-row">
          <span>{row.label}</span>
          <strong>{row.value}</strong>
        </div>
      ))}
    </div>
  )
}

function StatCard({ iconClass, label, value, amount = null, icon }) {
  return (
    <div className="stat-card card">
      <div className="card-body">
        <div className={`stat-icon ${iconClass}`}>
          <StatIcon icon={icon} />
        </div>
        <div className="stat-content">
          <span className={`stat-value ${amount === null ? '' : amount >= 0 ? 'amount-positive' : 'amount-negative'}`}>
            {value}
          </span>
          <span className="stat-label">{label}</span>
        </div>
      </div>
    </div>
  )
}

function StatIcon({ icon }) {
  if (icon === 'accounts') {
    return (
      <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
        <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/>
        <path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/>
      </svg>
    )
  }

  if (icon === 'balance') {
    return (
      <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
        <line x1="12" y1="1" x2="12" y2="23"/>
        <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/>
      </svg>
    )
  }

  if (icon === 'pending') {
    return (
      <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
        <circle cx="12" cy="12" r="10"/>
        <polyline points="12 6 12 12 16 14"/>
      </svg>
    )
  }

  if (icon === 'credits') {
    return (
      <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
        <line x1="12" y1="5" x2="12" y2="19"/>
        <line x1="5" y1="12" x2="19" y2="12"/>
      </svg>
    )
  }

  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
      <line x1="5" y1="12" x2="19" y2="12"/>
    </svg>
  )
}

function ValueRecordedChart({ buckets }) {
  const [tooltip, setTooltip] = useState(null)
  const yLabels = buildLinearYAxisLabels(buckets.map((bucket) => bucket.value))
  const yMin = yLabels[0] || 0
  const yMax = yLabels[yLabels.length - 1] || 1
  const width = 900
  const height = 360
  const padding = { top: 20, right: 18, bottom: 46, left: 58 }
  const innerWidth = width - padding.left - padding.right
  const innerHeight = height - padding.top - padding.bottom
  const pointSlot = buckets.length > 1 ? innerWidth / (buckets.length - 1) : innerWidth
  const valueRange = yMax - yMin || 1
  const points = buckets.map((bucket, index) => {
    const x = padding.left + (buckets.length > 1 ? index * pointSlot : innerWidth / 2)
    const y = padding.top + innerHeight - ((bucket.value - yMin) / valueRange) * innerHeight
    return { x, y, bucket }
  })
  const linePath = points.map((point, index) => `${index === 0 ? 'M' : 'L'} ${point.x} ${point.y}`).join(' ')
  const areaPath = points.length > 0
    ? `${linePath} L ${points[points.length - 1].x} ${padding.top + innerHeight} L ${points[0].x} ${padding.top + innerHeight} Z`
    : ''

  return (
    <div className="transactions-chart">
      <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Value recorded over time chart">
        {yLabels.map((label) => {
          const y = padding.top + innerHeight - ((label - yMin) / valueRange) * innerHeight
          return (
            <g key={label}>
              <line x1={padding.left} y1={y} x2={padding.left + innerWidth} y2={y} className={`chart-grid-line ${label === 0 ? 'chart-axis' : ''}`} />
              <text x={padding.left - 9} y={y + 4} className="chart-y-label">{formatAxisCurrency(label)}</text>
            </g>
          )
        })}
        <line x1={padding.left} y1={padding.top} x2={padding.left} y2={padding.top + innerHeight} className="chart-axis" />
        {areaPath && <path className="chart-area-value" d={areaPath} />}
        {linePath && <path className="chart-line-value" d={linePath} />}
        {points.map(({ x, y, bucket }, index) => (
          <g key={bucket.startUtc}>
            <circle className="chart-point-value" cx={x} cy={y} r="2.5" />
            {shouldShowXAxisLabel(index, buckets.length) && (
              <text x={x} y={height - 16} className="chart-x-label">{formatChartLabel(bucket.startUtc)}</text>
            )}
            <title>{`${formatDate(bucket.endUtc)}: ${formatCurrency(bucket.value)}`}</title>
          </g>
        ))}
        {points.map(({ x, y, bucket }, index) => {
          const hoverWidth = buckets.length > 1 ? pointSlot : innerWidth
          const hoverX = Math.max(padding.left, Math.min(padding.left + innerWidth - hoverWidth, x - hoverWidth / 2))
          return (
            <rect
              key={`hover-${bucket.startUtc}`}
              className="chart-hover-target"
              x={hoverX}
              y={padding.top}
              width={hoverWidth}
              height={innerHeight}
              tabIndex="0"
              aria-label={`${formatDate(bucket.endUtc)} value recorded ${formatCurrency(bucket.value)}`}
              onFocus={() => setTooltip(buildChartTooltip(x, y, width, height, 'Value Recorded', [
                ...getBucketTimestampRows(bucket),
                { label: 'Value', value: formatCurrency(bucket.value) }
              ]))}
              onMouseEnter={() => setTooltip(buildChartTooltip(x, y, width, height, 'Value Recorded', [
                ...getBucketTimestampRows(bucket),
                { label: 'Value', value: formatCurrency(bucket.value) }
              ]))}
              onBlur={() => setTooltip(null)}
              onMouseLeave={() => setTooltip(null)}
            />
          )
        })}
      </svg>
      <ChartTooltip tooltip={tooltip} />
      <div className="chart-legend">
        <span><i className="legend-value"></i>Committed value</span>
      </div>
    </div>
  )
}

function TransactionsChart({ buckets }) {
  const [tooltip, setTooltip] = useState(null)
  const highestCount = Math.max(0, ...buckets.map((bucket) => bucket.count))
  const yLabels = buildYAxisLabels(highestCount)
  const yMax = yLabels[yLabels.length - 1] || 1
  const width = 900
  const height = 360
  const padding = { top: 20, right: 18, bottom: 46, left: 46 }
  const innerWidth = width - padding.left - padding.right
  const innerHeight = height - padding.top - padding.bottom
  const barSlot = innerWidth / Math.max(1, buckets.length)
  const barWidth = Math.max(4, Math.min(26, barSlot - 4))

  return (
    <div className="transactions-chart">
      <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Transactions over time chart">
        {yLabels.map((label) => {
          const y = padding.top + innerHeight - (label / yMax) * innerHeight
          return (
            <g key={label}>
              <line x1={padding.left} y1={y} x2={padding.left + innerWidth} y2={y} className={`chart-grid-line ${label === 0 ? 'chart-axis' : ''}`} />
              <text x={padding.left - 9} y={y + 4} className="chart-y-label">{label}</text>
            </g>
          )
        })}
        <line x1={padding.left} y1={padding.top} x2={padding.left} y2={padding.top + innerHeight} className="chart-axis" />
        {buckets.map((bucket, index) => {
          const barHeight = bucket.count > 0 ? Math.max(2, (bucket.count / yMax) * innerHeight) : 0
          const x = padding.left + index * barSlot + (barSlot - barWidth) / 2
          const y = padding.top + innerHeight - barHeight
          const tooltipY = bucket.count > 0 ? y : padding.top + innerHeight
          return (
            <g key={bucket.startUtc}>
              {bucket.count > 0 && (
                <>
                  <rect className="chart-bar chart-bar-credit" x={x} y={y} width={barWidth} height={barHeight * (bucket.credits / Math.max(1, bucket.count))} rx="2" />
                  <rect className="chart-bar chart-bar-debit" x={x} y={y + barHeight * (bucket.credits / Math.max(1, bucket.count))} width={barWidth} height={barHeight * (bucket.debits / Math.max(1, bucket.count))} rx="2" />
                </>
              )}
              {shouldShowXAxisLabel(index, buckets.length) && (
                <text x={x + barWidth / 2} y={height - 16} className="chart-x-label">{formatChartLabel(bucket.startUtc)}</text>
              )}
              <title>{`${formatDate(bucket.startUtc)}: ${bucket.count} transactions`}</title>
              <rect
                className="chart-hover-target"
                x={padding.left + index * barSlot}
                y={padding.top}
                width={barSlot}
                height={innerHeight}
                tabIndex="0"
                aria-label={`${formatBucketTimestamp(bucket)} ${bucket.count} transactions`}
                onFocus={() => setTooltip(buildChartTooltip(x + barWidth / 2, tooltipY, width, height, 'Transactions', [
                  ...getBucketTimestampRows(bucket),
                  { label: 'Total', value: bucket.count },
                  { label: 'Credits', value: bucket.credits },
                  { label: 'Debits', value: bucket.debits }
                ]))}
                onMouseEnter={() => setTooltip(buildChartTooltip(x + barWidth / 2, tooltipY, width, height, 'Transactions', [
                  ...getBucketTimestampRows(bucket),
                  { label: 'Total', value: bucket.count },
                  { label: 'Credits', value: bucket.credits },
                  { label: 'Debits', value: bucket.debits }
                ]))}
                onBlur={() => setTooltip(null)}
                onMouseLeave={() => setTooltip(null)}
              />
            </g>
          )
        })}
      </svg>
      <ChartTooltip tooltip={tooltip} />
      <div className="chart-legend">
        <span><i className="legend-credit"></i>Credits</span>
        <span><i className="legend-debit"></i>Debits</span>
      </div>
    </div>
  )
}

function AmountsChart({ buckets }) {
  const [tooltip, setTooltip] = useState(null)
  const highestAmount = Math.max(0, ...buckets.map((bucket) => bucket.creditAmount + bucket.debitAmount))
  const yLabels = buildYAxisLabels(Math.ceil(highestAmount))
  const yMax = yLabels[yLabels.length - 1] || 1
  const width = 900
  const height = 360
  const padding = { top: 20, right: 18, bottom: 46, left: 58 }
  const innerWidth = width - padding.left - padding.right
  const innerHeight = height - padding.top - padding.bottom
  const barSlot = innerWidth / Math.max(1, buckets.length)
  const barWidth = Math.max(4, Math.min(26, barSlot - 4))

  return (
    <div className="transactions-chart">
      <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Debit and credit amounts over time chart">
        {yLabels.map((label) => {
          const y = padding.top + innerHeight - (label / yMax) * innerHeight
          return (
            <g key={label}>
              <line x1={padding.left} y1={y} x2={padding.left + innerWidth} y2={y} className={`chart-grid-line ${label === 0 ? 'chart-axis' : ''}`} />
              <text x={padding.left - 9} y={y + 4} className="chart-y-label">{formatAxisCurrency(label)}</text>
            </g>
          )
        })}
        <line x1={padding.left} y1={padding.top} x2={padding.left} y2={padding.top + innerHeight} className="chart-axis" />
        {buckets.map((bucket, index) => {
          const totalAmount = bucket.creditAmount + bucket.debitAmount
          const barHeight = totalAmount > 0 ? Math.max(2, (totalAmount / yMax) * innerHeight) : 0
          const x = padding.left + index * barSlot + (barSlot - barWidth) / 2
          const y = padding.top + innerHeight - barHeight
          const creditHeight = barHeight * (bucket.creditAmount / Math.max(1, totalAmount))
          const debitHeight = barHeight * (bucket.debitAmount / Math.max(1, totalAmount))
          const tooltipY = totalAmount > 0 ? y : padding.top + innerHeight
          return (
            <g key={bucket.startUtc}>
              {totalAmount > 0 && (
                <>
                  <rect className="chart-bar chart-bar-credit" x={x} y={y} width={barWidth} height={creditHeight} rx="2" />
                  <rect className="chart-bar chart-bar-debit" x={x} y={y + creditHeight} width={barWidth} height={debitHeight} rx="2" />
                </>
              )}
              {shouldShowXAxisLabel(index, buckets.length) && (
                <text x={x + barWidth / 2} y={height - 16} className="chart-x-label">{formatChartLabel(bucket.startUtc)}</text>
              )}
              <title>{`${formatDate(bucket.startUtc)}: ${formatCurrency(bucket.creditAmount)} credits, ${formatCurrency(bucket.debitAmount)} debits`}</title>
              <rect
                className="chart-hover-target"
                x={padding.left + index * barSlot}
                y={padding.top}
                width={barSlot}
                height={innerHeight}
                tabIndex="0"
                aria-label={`${formatBucketTimestamp(bucket)} ${formatCurrency(totalAmount)} total amount`}
                onFocus={() => setTooltip(buildChartTooltip(x + barWidth / 2, tooltipY, width, height, 'Amounts', [
                  ...getBucketTimestampRows(bucket),
                  { label: 'Total', value: formatCurrency(totalAmount) },
                  { label: 'Credits', value: formatCurrency(bucket.creditAmount) },
                  { label: 'Debits', value: formatCurrency(bucket.debitAmount) }
                ]))}
                onMouseEnter={() => setTooltip(buildChartTooltip(x + barWidth / 2, tooltipY, width, height, 'Amounts', [
                  ...getBucketTimestampRows(bucket),
                  { label: 'Total', value: formatCurrency(totalAmount) },
                  { label: 'Credits', value: formatCurrency(bucket.creditAmount) },
                  { label: 'Debits', value: formatCurrency(bucket.debitAmount) }
                ]))}
                onBlur={() => setTooltip(null)}
                onMouseLeave={() => setTooltip(null)}
              />
            </g>
          )
        })}
      </svg>
      <ChartTooltip tooltip={tooltip} />
      <div className="chart-legend">
        <span><i className="legend-credit"></i>Credits</span>
        <span><i className="legend-debit"></i>Debits</span>
      </div>
    </div>
  )
}
