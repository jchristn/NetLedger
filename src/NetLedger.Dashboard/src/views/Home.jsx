import React, { useState, useEffect } from 'react'
import { useApp } from '../context/AppContext'
import { formatCurrency, formatDate, normalizeEnumerationResult, normalizeBalances } from '../api/api'
import './Home.css'

export default function Home() {
  const { api, setError, serverInfo } = useApp()
  const [loading, setLoading] = useState(true)
  const [stats, setStats] = useState({
    totalAccounts: 0,
    totalCommittedBalance: 0,
    totalPendingBalance: 0,
    totalPendingCredits: 0,
    totalPendingDebits: 0,
    accounts: []
  })

  useEffect(() => {
    loadStats()
  }, [])

  const loadStats = async () => {
    try {
      setLoading(true)

      // Fetch accounts and balances
      const [accountsResult, balancesResult] = await Promise.all([
        api.listAccounts({ maxResults: 1000 }),
        api.getAllBalances()
      ])

      const { objects: accounts } = normalizeEnumerationResult(accountsResult)
      const balanceList = normalizeBalances(balancesResult)

      // Calculate totals
      let totalCommitted = 0
      let totalPending = 0
      let totalPendingCredits = 0
      let totalPendingDebits = 0

      const accountsWithBalances = accounts.map(account => {
        const balance = balanceList.find(b =>
          b.accountGuid === account.guid || b.AccountGuid === account.guid
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

      setStats({
        totalAccounts: accounts.length,
        totalCommittedBalance: totalCommitted,
        totalPendingBalance: totalPending,
        totalPendingCredits,
        totalPendingDebits,
        accounts: accountsWithBalances.slice(0, 5) // Top 5 accounts
      })
    } catch (err) {
      setError(err.message || 'Failed to load statistics')
    } finally {
      setLoading(false)
    }
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
      {/* Server Info */}
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

      {/* Stats Cards */}
      <div className="stats-grid">
        <div className="stat-card card">
          <div className="card-body">
            <div className="stat-icon stat-icon-accounts">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/>
                <path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/>
              </svg>
            </div>
            <div className="stat-content">
              <span className="stat-value">{stats.totalAccounts}</span>
              <span className="stat-label">Total Accounts</span>
            </div>
          </div>
        </div>

        <div className="stat-card card">
          <div className="card-body">
            <div className="stat-icon stat-icon-balance">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <line x1="12" y1="1" x2="12" y2="23"/>
                <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/>
              </svg>
            </div>
            <div className="stat-content">
              <span className={`stat-value ${stats.totalCommittedBalance >= 0 ? 'amount-positive' : 'amount-negative'}`}>
                {formatCurrency(stats.totalCommittedBalance)}
              </span>
              <span className="stat-label">Committed Balance</span>
            </div>
          </div>
        </div>

        <div className="stat-card card">
          <div className="card-body">
            <div className="stat-icon stat-icon-pending">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <circle cx="12" cy="12" r="10"/>
                <polyline points="12 6 12 12 16 14"/>
              </svg>
            </div>
            <div className="stat-content">
              <span className={`stat-value ${stats.totalPendingBalance >= 0 ? 'amount-positive' : 'amount-negative'}`}>
                {formatCurrency(stats.totalPendingBalance)}
              </span>
              <span className="stat-label">Pending Balance</span>
            </div>
          </div>
        </div>

        <div className="stat-card card">
          <div className="card-body">
            <div className="stat-icon stat-icon-credits">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <line x1="12" y1="5" x2="12" y2="19"/>
                <line x1="5" y1="12" x2="19" y2="12"/>
              </svg>
            </div>
            <div className="stat-content">
              <span className="stat-value amount-positive">{formatCurrency(stats.totalPendingCredits)}</span>
              <span className="stat-label">Pending Credits</span>
            </div>
          </div>
        </div>

        <div className="stat-card card">
          <div className="card-body">
            <div className="stat-icon stat-icon-debits">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <line x1="5" y1="12" x2="19" y2="12"/>
              </svg>
            </div>
            <div className="stat-content">
              <span className="stat-value amount-negative">{formatCurrency(stats.totalPendingDebits)}</span>
              <span className="stat-label">Pending Debits</span>
            </div>
          </div>
        </div>
      </div>

      {/* Recent Accounts */}
      {stats.accounts.length > 0 && (
        <div className="recent-accounts card">
          <div className="card-header">
            <h3>Account Summary</h3>
          </div>
          <div className="card-body">
            <table className="accounts-summary-table">
              <thead>
                <tr>
                  <th>Account</th>
                  <th className="text-right">Committed</th>
                  <th className="text-right">Pending</th>
                </tr>
              </thead>
              <tbody>
                {stats.accounts.map(account => (
                  <tr key={account.guid || account.Guid}>
                    <td>
                      <span className="account-name">{account.name || account.Name}</span>
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
