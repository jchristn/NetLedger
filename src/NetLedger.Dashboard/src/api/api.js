/**
 * NetLedger API Client
 * Handles all communication with the NetLedger.Server backend
 */

export class NetLedgerApiError extends Error {
  constructor(message, options = {}) {
    super(message)
    this.name = 'NetLedgerApiError'
    this.status = options.status || 0
    this.statusText = options.statusText || ''
    this.data = options.data || null
    this.path = options.path || ''
  }
}

export class NetLedgerApi {
  constructor(baseUrl, apiKey, tenantId = '', options = {}) {
    this.baseUrl = baseUrl.replace(/\/+$/, '')
    this.apiKey = apiKey
    this.tenantId = tenantId
    this.onAuthenticationFailed = typeof options.onAuthenticationFailed === 'function' ? options.onAuthenticationFailed : null
  }

  handleAuthenticationFailure(error) {
    if (this.apiKey && error?.status === 401 && this.onAuthenticationFailed) {
      this.onAuthenticationFailed(error)
    }
  }

  /**
   * Make an authenticated request to the API
   */
  async request(method, path, body = null, queryParams = null) {
    let url = `${this.baseUrl}${path}`

    if (queryParams) {
      const params = new URLSearchParams()
      Object.entries(queryParams).forEach(([key, value]) => {
        if (value !== null && value !== undefined && value !== '') {
          params.append(key, value)
        }
      })
      const queryString = params.toString()
      if (queryString) {
        url += `?${queryString}`
      }
    }

    const headers = {
      'Content-Type': 'application/json'
    }

    if (this.apiKey) {
      headers['Authorization'] = `Bearer ${this.apiKey}`
    }
    if (this.tenantId) {
      headers['x-tenant-id'] = this.tenantId
    }

    const options = {
      method,
      headers
    }

    if (body && (method === 'POST' || method === 'PUT' || method === 'PATCH')) {
      options.body = JSON.stringify(body)
    }

    const response = await fetch(url, options)

    // Handle no content responses
    if (response.status === 204) {
      return null
    }

    // Parse JSON response
    let data
    try {
      data = await response.json()
    } catch {
      if (!response.ok) {
        const error = new NetLedgerApiError(`HTTP ${response.status}: ${response.statusText}`, {
          status: response.status,
          statusText: response.statusText,
          path
        })
        this.handleAuthenticationFailure(error)
        throw error
      }
      return null
    }

    // Check for API-level errors
    if (!response.ok) {
      const errorMessage = data?.Error?.Message || data?.error?.message || data?.message || data?.Description || data?.description || `HTTP ${response.status}`
      const error = new NetLedgerApiError(errorMessage, {
        status: response.status,
        statusText: response.statusText,
        data,
        path
      })
      this.handleAuthenticationFailure(error)
      throw error
    }

    // Return data from response wrapper if present (handle both PascalCase and camelCase)
    if (data && typeof data === 'object') {
      if ('Data' in data) {
        return data.Data
      }
      if ('data' in data) {
        return data.data
      }
    }

    return data
  }

  // Convenience methods
  async get(path, queryParams = null) {
    return this.request('GET', path, null, queryParams)
  }

  async post(path, body = null, queryParams = null) {
    return this.request('POST', path, body, queryParams)
  }

  async put(path, body = null, queryParams = null) {
    return this.request('PUT', path, body, queryParams)
  }

  async delete(path, queryParams = null) {
    return this.request('DELETE', path, null, queryParams)
  }

  async requestRaw(method, path, options = {}) {
    let url = `${this.baseUrl}${path}`
    const {
      body = null,
      headers: extraHeaders = {},
      queryParams = null,
      signal = null
    } = options

    if (queryParams) {
      const params = new URLSearchParams()
      Object.entries(queryParams).forEach(([key, value]) => {
        if (value !== null && value !== undefined && value !== '') {
          params.append(key, value)
        }
      })
      const queryString = params.toString()
      if (queryString) {
        url += url.includes('?') ? `&${queryString}` : `?${queryString}`
      }
    }

    const headers = { ...extraHeaders }
    if (body !== null && body !== undefined && body !== '' && !headers['Content-Type'] && !headers['content-type']) {
      headers['Content-Type'] = 'application/json'
    }
    if (this.apiKey && !headers.Authorization && !headers.authorization) {
      headers.Authorization = `Bearer ${this.apiKey}`
    }
    if (this.tenantId && !headers['x-tenant-id'] && !headers['X-Tenant-Id']) {
      headers['x-tenant-id'] = this.tenantId
    }

    const started = performance.now()
    const response = await fetch(url, {
      method,
      headers,
      body: body !== null && body !== undefined && method !== 'GET' && method !== 'HEAD' ? body : undefined,
      signal
    })
    const text = await response.text()
    const responseHeaders = {}
    response.headers.forEach((value, key) => {
      responseHeaders[key] = value
    })

    let json = null
    try {
      json = text ? JSON.parse(text) : null
    } catch {
      json = null
    }

    const result = {
      ok: response.ok,
      status: response.status,
      statusText: response.statusText,
      headers: responseHeaders,
      text,
      json,
      contentType: response.headers.get('content-type') || '',
      durationMs: performance.now() - started,
      requestId: response.headers.get('x-request-id') || response.headers.get('x-netledger-request-id') || ''
    }

    if (!result.ok && result.status === 401) {
      this.handleAuthenticationFailure(new NetLedgerApiError('Authentication failed', {
        status: result.status,
        statusText: result.statusText,
        data: json,
        path
      }))
    }

    return result
  }

  // ==================== Service Endpoints ====================

  /**
   * Get server information
   */
  async getServerInfo() {
    return this.get('/')
  }

  async getOpenApiSpec() {
    const response = await this.requestRaw('GET', '/openapi.json')
    if (!response.ok) {
      throw new NetLedgerApiError(`HTTP ${response.status}: ${response.statusText}`, {
        status: response.status,
        statusText: response.statusText,
        data: response.json,
        path: '/openapi.json'
      })
    }
    return response.json
  }

  // ==================== Authentication Endpoints ====================

  async discoverTenants(email) {
    return this.post('/v1/auth/tenants', { Email: email })
  }

  async loginWithPassword(tenantId, email, password) {
    return this.post('/v1/auth/login', { TenantId: tenantId, Email: email, Password: password })
  }

  async logoutSession() {
    return this.post('/v1/auth/logout')
  }

  async getEffectivePermissions() {
    return this.get('/v1/me/permissions')
  }

  // ==================== Account Endpoints ====================

  /**
   * Create a new account
   */
  async createAccount(name, initialBalance = null, notes = null, labels = [], tags = {}, tenantId = null) {
    const body = { Name: name }
    if (initialBalance !== null) {
      body.InitialBalance = initialBalance
    }
    if (notes !== null) {
      body.Notes = notes
    }
    body.Labels = labels
    body.Tags = tags
    const path = tenantId ? `/v1/tenants/${tenantId}/accounts` : '/v1/accounts'
    return this.put(path, body)
  }

  /**
   * List accounts with pagination
   */
  async listAccounts(options = {}) {
    const {
      maxResults = 50,
      skip = 0,
      ordering = 'CreatedDescending',
      search = null,
      startTime = null,
      endTime = null,
      labels = null,
      tags = null,
      tenantId = null
    } = options

    const path = tenantId ? `/v1/tenants/${tenantId}/accounts` : '/v1/accounts'
    return this.get(path, {
      maxResults,
      skip,
      ordering,
      search,
      startTime,
      endTime,
      labels: Array.isArray(labels) ? labels.join(',') : labels,
      tags: tags && typeof tags === 'object' ? Object.entries(tags).map(([key, value]) => `${key}=${value}`).join(',') : tags
    })
  }

  /**
   * Get a specific account by GUID
   */
  async getAccount(accountGuid) {
    return this.get(`/v1/accounts/${accountGuid}`)
  }

  /**
   * Get a specific account by name
   */
  async getAccountByName(accountName) {
    return this.get(`/v1/accounts/byname/${encodeURIComponent(accountName)}`)
  }

  /**
   * Delete an account
   */
  async deleteAccount(accountGuid) {
    return this.delete(`/v1/accounts/${accountGuid}`)
  }

  async mapAccountUser(tenantId, accountGuid, userId) {
    return this.put(`/v1/tenants/${tenantId}/accounts/${accountGuid}/users/${userId}`)
  }

  async listAccountUsers(tenantId, accountGuid, options = {}) {
    return this.get(`/v1/tenants/${tenantId}/accounts/${accountGuid}/users`, options)
  }

  // ==================== Balance Endpoints ====================

  /**
   * Get balance for an account
   */
  async getBalance(accountGuid) {
    return this.get(`/v1/accounts/${accountGuid}/balance`)
  }

  /**
   * Get historical balance as of a specific time
   */
  async getBalanceAsOf(accountGuid, asOf) {
    return this.get(`/v1/accounts/${accountGuid}/balance/asof`, { asOf })
  }

  /**
   * Get all account balances
   */
  async getAllBalances() {
    return this.get('/v1/balances')
  }

  /**
   * Verify balance chain integrity
   */
  async verifyBalance(accountGuid) {
    return this.get(`/v1/accounts/${accountGuid}/verify`)
  }

  // ==================== Entry Endpoints ====================

  /**
   * Add credits to an account
   */
  async addCredits(accountGuid, entries, isCommitted = false) {
    // Server expects AddEntriesRequest with Entries array of { Amount, Notes }
    const body = {
      Entries: entries.map(e => ({
        Amount: e.amount || e.Amount,
        Notes: e.description || e.Description || e.notes || e.Notes || '',
        Labels: e.labels || e.Labels || [],
        Tags: e.tags || e.Tags || {}
      })),
      IsCommitted: isCommitted
    }
    return this.put(`/v1/accounts/${accountGuid}/credits`, body)
  }

  /**
   * Add debits to an account
   */
  async addDebits(accountGuid, entries, isCommitted = false) {
    // Server expects AddEntriesRequest with Entries array of { Amount, Notes }
    const body = {
      Entries: entries.map(e => ({
        Amount: e.amount || e.Amount,
        Notes: e.description || e.Description || e.notes || e.Notes || '',
        Labels: e.labels || e.Labels || [],
        Tags: e.tags || e.Tags || {}
      })),
      IsCommitted: isCommitted
    }
    return this.put(`/v1/accounts/${accountGuid}/debits`, body)
  }

  /**
   * List entries for an account with pagination
   */
  async listEntries(accountGuid, options = {}) {
    const {
      maxResults = 50,
      skip = 0,
      ordering = 'CreatedDescending',
      search = null,
      startTime = null,
      endTime = null,
      amountMin = null,
      amountMax = null,
      creditMin = null,
      creditMax = null,
      debitMin = null,
      debitMax = null,
      labels = null,
      tags = null,
      tenantId = null
    } = options

    const path = tenantId ? `/v1/tenants/${tenantId}/accounts/${accountGuid}/entries` : `/v1/accounts/${accountGuid}/entries`
    return this.get(path, {
      maxResults,
      skip,
      ordering,
      search,
      startTime,
      endTime,
      amountMin,
      amountMax,
      creditMin,
      creditMax,
      debitMin,
      debitMax,
      labels: Array.isArray(labels) ? labels.join(',') : labels,
      tags: tags && typeof tags === 'object' ? Object.entries(tags).map(([key, value]) => `${key}=${value}`).join(',') : tags
    })
  }

  /**
   * Get pending entries for an account
   */
  async getPendingEntries(accountGuid) {
    return this.get(`/v1/accounts/${accountGuid}/entries/pending`)
  }

  /**
   * Get pending credits for an account
   */
  async getPendingCredits(accountGuid) {
    return this.get(`/v1/accounts/${accountGuid}/entries/pending/credits`)
  }

  /**
   * Get pending debits for an account
   */
  async getPendingDebits(accountGuid) {
    return this.get(`/v1/accounts/${accountGuid}/entries/pending/debits`)
  }

  /**
   * Cancel a pending entry
   */
  async cancelEntry(accountGuid, entryGuid) {
    return this.delete(`/v1/accounts/${accountGuid}/entries/${entryGuid}`)
  }

  /**
   * Commit pending entries
   */
  async commitEntries(accountGuid, options = {}) {
    const {
      maxResults = 1000,
      startTime = null,
      endTime = null,
      amountMin = null,
      amountMax = null,
      entryGuids = null
    } = options

    const body = {
      MaxResults: maxResults,
      CreatedAfterUtc: startTime,
      CreatedBeforeUtc: endTime,
      MinimumAmount: amountMin,
      MaximumAmount: amountMax
    }

    // If specific entry GUIDs are provided, use them
    if (entryGuids && entryGuids.length > 0) {
      body.EntryGuids = entryGuids
    }

    return this.post(`/v1/accounts/${accountGuid}/commit`, body)
  }

  // ==================== Credential Endpoints ====================

  /**
   * List API keys (admin only)
   */
  async listApiKeys(options = {}) {
    const {
      maxResults = 50,
      skip = 0,
      ordering = 'CreatedDescending',
      search = null,
      createdAfterUtc = null,
      createdBeforeUtc = null
    } = options

    return this.get('/v1/credentials', {
      maxResults,
      skip,
      ordering,
      search,
      createdAfterUtc,
      createdBeforeUtc
    })
  }

  /**
   * Create a new API key (admin only)
   */
  async createApiKey(name) {
    return this.put('/v1/credentials', {
      Name: name
    })
  }

  /**
   * Revoke an API key (admin only)
   */
  async revokeApiKey(apiKeyGuid) {
    return this.delete(`/v1/credentials/${apiKeyGuid}`)
  }

  async listTenants(options = {}) {
    return this.get('/v1/tenants', options)
  }

  async readTenant(tenantId) {
    return this.get(`/v1/tenants/${tenantId}`)
  }

  async createTenant(tenant) {
    return this.put('/v1/tenants', tenant)
  }

  async listUsers(tenantId, options = {}) {
    return this.get(`/v1/tenants/${tenantId}/users`, options)
  }

  async readUser(tenantId, userId) {
    return this.get(`/v1/tenants/${tenantId}/users/${userId}`)
  }

  async createUser(tenantId, user) {
    return this.put(`/v1/tenants/${tenantId}/users`, user)
  }

  async listSessions(tenantId, options = {}) {
    return this.get(`/v1/tenants/${tenantId}/sessions`, options)
  }

  async listAudit(tenantId, options = {}) {
    return this.get(`/v1/tenants/${tenantId}/audit`, options)
  }

  async listRoles(tenantId, options = {}) {
    return this.get(`/v1/tenants/${tenantId}/roles`, options)
  }

  async createRole(tenantId, role) {
    return this.put(`/v1/tenants/${tenantId}/roles`, role)
  }

  async listPermissions(tenantId, options = {}) {
    return this.get(`/v1/tenants/${tenantId}/permissions`, options)
  }

  async createPermission(tenantId, permission) {
    return this.put(`/v1/tenants/${tenantId}/permissions`, permission)
  }

  async assignUserRole(tenantId, userId, assignment) {
    return this.put(`/v1/tenants/${tenantId}/users/${userId}/roles`, assignment)
  }

  async listRequestHistory(options = {}) {
    return this.get('/v1.0/api/request-history', options)
  }

  async summarizeRequestHistory(options = {}) {
    return this.get('/v1.0/api/request-history/summary', options)
  }

  async readRequestHistoryEntry(id) {
    return this.get(`/v1.0/api/request-history/${encodeURIComponent(id)}`)
  }

  async deleteRequestHistoryEntry(id) {
    return this.delete(`/v1.0/api/request-history/${encodeURIComponent(id)}`)
  }

  async deleteRequestHistory(options = {}) {
    return this.delete('/v1.0/api/request-history', options)
  }
}

/**
 * Normalize an enumeration result from the API (handles PascalCase)
 * Converts { Objects, TotalRecords, ... } to { objects, totalRecords, ... }
 */
export function normalizeEnumerationResult(result) {
  if (!result) {
    return { objects: [], totalRecords: 0 }
  }

  return {
    objects: result.Objects || result.objects || [],
    totalRecords: result.TotalRecords ?? result.totalRecords ?? 0,
    recordsRemaining: result.RecordsRemaining ?? result.recordsRemaining ?? 0,
    endOfResults: result.EndOfResults ?? result.endOfResults ?? true,
    maxResults: result.MaxResults ?? result.maxResults ?? 0,
    skip: result.Skip ?? result.skip ?? 0,
    continuationToken: result.ContinuationToken || result.continuationToken || null
  }
}

/**
 * Normalize balances from the API (dictionary format)
 * Converts { "guid1": Balance, "guid2": Balance } to array format
 */
export function normalizeBalances(balancesDict) {
  if (!balancesDict) {
    return []
  }

  // If it's already an array, return it
  if (Array.isArray(balancesDict)) {
    return balancesDict
  }

  // Convert dictionary to array with accountGuid attached
  return Object.entries(balancesDict).map(([guid, balance]) => ({
    ...balance,
    accountGuid: guid,
    AccountGuid: guid
  }))
}

/**
 * Format a decimal number as currency
 */
export function formatCurrency(amount, showSign = false) {
  if (amount === null || amount === undefined) {
    return '$0.00'
  }

  const num = parseFloat(amount)
  const formatted = Math.abs(num).toLocaleString('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  })

  if (showSign && num !== 0) {
    return num > 0 ? `+${formatted}` : `-${formatted}`
  }

  return num < 0 ? `-${formatted}` : formatted
}

/**
 * Format a date string for display
 */
export function formatDate(dateString) {
  if (!dateString) return '-'

  const date = new Date(dateString)
  if (Number.isNaN(date.getTime())) return '-'

  const text = String(dateString)
  const fractionalMatch = text.match(/\.(\d+)/)
  const fractionalSeconds = fractionalMatch
    ? fractionalMatch[1].slice(0, 6).padEnd(6, '0')
    : String(date.getUTCMilliseconds()).padStart(3, '0').padEnd(6, '0')

  const year = date.getUTCFullYear()
  const month = String(date.getUTCMonth() + 1).padStart(2, '0')
  const day = String(date.getUTCDate()).padStart(2, '0')
  const hours = String(date.getUTCHours()).padStart(2, '0')
  const minutes = String(date.getUTCMinutes()).padStart(2, '0')
  const seconds = String(date.getUTCSeconds()).padStart(2, '0')

  return `${year}-${month}-${day}T${hours}:${minutes}:${seconds}.${fractionalSeconds}Z`
}

/**
 * Format a date for API queries (ISO string)
 */
export function formatDateForApi(date) {
  if (!date) return null
  if (typeof date === 'string') {
    date = new Date(date)
  }
  return date.toISOString()
}

/**
 * Truncate a GUID for display
 */
export function truncateGuid(guid) {
  if (!guid) return '-'
  if (guid.length <= 13) return guid
  return `${guid.substring(0, 8)}...${guid.substring(guid.length - 4)}`
}
