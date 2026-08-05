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
    this.localeProvider = typeof options.localeProvider === 'function' ? options.localeProvider : null
  }

  handleAuthenticationFailure(error) {
    if (this.apiKey && error?.status === 401 && this.onAuthenticationFailed) {
      this.onAuthenticationFailed(error)
    }
  }

  extractRouteTenantId(path) {
    const match = path.match(/^\/v1(?:\.0\/api)?\/tenants\/([^/?#]+)/i)
    return match ? decodeURIComponent(match[1]) : ''
  }

  resolveTenantHeader(path, requestOptions = {}) {
    if (requestOptions.suppressTenantHeader) return ''

    if (this.extractRouteTenantId(path)) return ''

    if (Object.prototype.hasOwnProperty.call(requestOptions, 'tenantId')) {
      return requestOptions.tenantId || ''
    }

    return this.tenantId || ''
  }

  /**
   * Make an authenticated request to the API
   */
  async request(method, path, body = null, queryParams = null, requestOptions = {}) {
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
    const locale = this.localeProvider ? this.localeProvider() : ''
    if (locale) {
      headers['Accept-Language'] = locale
    }

    if (this.apiKey) {
      headers['Authorization'] = `Bearer ${this.apiKey}`
    }
    const tenantHeader = this.resolveTenantHeader(path, requestOptions)
    if (tenantHeader) {
      headers['x-tenant-id'] = tenantHeader
    }

    const fetchOptions = {
      method,
      headers
    }

    if (body && (method === 'POST' || method === 'PUT' || method === 'PATCH')) {
      fetchOptions.body = JSON.stringify(body)
    }

    const response = await fetch(url, fetchOptions)

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
  async get(path, queryParams = null, requestOptions = {}) {
    return this.request('GET', path, null, queryParams, requestOptions)
  }

  async post(path, body = null, queryParams = null, requestOptions = {}) {
    return this.request('POST', path, body, queryParams, requestOptions)
  }

  async put(path, body = null, queryParams = null, requestOptions = {}) {
    return this.request('PUT', path, body, queryParams, requestOptions)
  }

  async delete(path, queryParams = null, requestOptions = {}) {
    return this.request('DELETE', path, null, queryParams, requestOptions)
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
    const locale = this.localeProvider ? this.localeProvider() : ''
    if (locale && !headers['Accept-Language'] && !headers['accept-language']) {
      headers['Accept-Language'] = locale
    }
    if (body !== null && body !== undefined && body !== '' && !headers['Content-Type'] && !headers['content-type']) {
      headers['Content-Type'] = 'application/json'
    }
    if (this.apiKey && !headers.Authorization && !headers.authorization) {
      headers.Authorization = `Bearer ${this.apiKey}`
    }
    const tenantHeader = this.resolveTenantHeader(path, options)
    if (tenantHeader && !headers['x-tenant-id'] && !headers['X-Tenant-Id']) {
      headers['x-tenant-id'] = tenantHeader
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
  async createAccount(name, initialBalance = null, notes = null, labels = [], tags = {}, units = null, tenantId = null) {
    const body = { Name: name }
    if (initialBalance !== null) {
      body.InitialBalance = initialBalance
    }
    if (notes !== null) {
      body.Notes = notes
    }
    if (units !== null && units !== '') {
      body.Units = units
    }
    body.Labels = labels
    body.Tags = tags
    const path = tenantId ? `/v1/tenants/${tenantId}/accounts` : '/v1/accounts'
    return this.put(path, body)
  }

  /**
   * Update an existing account. Replaces the editable fields (name, notes, units, labels, tags).
   */
  async updateAccount(accountId, { name, notes = null, units = null, labels = [], tags = {}, active } = {}, tenantId = null) {
    const body = {
      Name: name,
      Notes: notes,
      Units: units !== null && units !== '' ? units : null,
      Labels: labels,
      Tags: tags
    }
    if (active !== undefined) {
      body.Active = active
    }
    const path = tenantId ? `/v1/tenants/${tenantId}/accounts/${accountId}` : `/v1/accounts/${accountId}`
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
      tenantId = null,
      suppressTenantHeader = false
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
    }, {
      suppressTenantHeader
    })
  }

  /**
   * Get a specific account by ID
   */
  async getAccount(accountId) {
    return this.get(`/v1/accounts/${accountId}`)
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
  async deleteAccount(accountId) {
    return this.delete(`/v1/accounts/${accountId}`)
  }

  async mapAccountUser(tenantId, accountId, userId) {
    return this.put(`/v1/tenants/${tenantId}/accounts/${accountId}/users/${userId}`)
  }

  async listAccountUsers(tenantId, accountId, options = {}) {
    return this.get(`/v1/tenants/${tenantId}/accounts/${accountId}/users`, options)
  }

  // ==================== Balance Endpoints ====================

  /**
   * Get balance for an account
   */
  async getBalance(accountId, tenantId = null) {
    const path = tenantId ? `/v1/tenants/${tenantId}/accounts/${accountId}/balance` : `/v1/accounts/${accountId}/balance`
    return this.get(path)
  }

  /**
   * Get historical balance as of a specific time
   */
  async getBalanceAsOf(accountId, asOf, tenantId = null) {
    const path = tenantId ? `/v1/tenants/${tenantId}/accounts/${accountId}/balance/asof` : `/v1/accounts/${accountId}/balance/asof`
    return this.get(path, { asOf })
  }

  /**
   * Get all account balances
   */
  async getAllBalances(options = {}) {
    const {
      tenantId = null,
      suppressTenantHeader = false
    } = options

    return this.get('/v1/balances', tenantId ? { tenantId } : null, {
      suppressTenantHeader: suppressTenantHeader || Boolean(tenantId)
    })
  }

  /**
   * Verify balance chain integrity
   */
  async verifyBalance(accountId) {
    return this.get(`/v1/accounts/${accountId}/verify`)
  }

  // ==================== Entry Endpoints ====================

  /**
   * Add credits to an account
   */
  async addCredits(accountId, entries, isCommitted = false, tenantId = null) {
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
    const path = tenantId ? `/v1/tenants/${tenantId}/accounts/${accountId}/credits` : `/v1/accounts/${accountId}/credits`
    return this.put(path, body)
  }

  /**
   * Add debits to an account
   */
  async addDebits(accountId, entries, isCommitted = false, tenantId = null) {
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
    const path = tenantId ? `/v1/tenants/${tenantId}/accounts/${accountId}/debits` : `/v1/accounts/${accountId}/debits`
    return this.put(path, body)
  }

  /**
   * List entries for an account with pagination
   */
  async listEntries(accountId, options = {}) {
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

    const path = tenantId ? `/v1/tenants/${tenantId}/accounts/${accountId}/entries` : `/v1/accounts/${accountId}/entries`
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
  async getPendingEntries(accountId, tenantId = null) {
    const path = tenantId ? `/v1/tenants/${tenantId}/accounts/${accountId}/entries/pending` : `/v1/accounts/${accountId}/entries/pending`
    return this.get(path)
  }

  /**
   * Get pending credits for an account
   */
  async getPendingCredits(accountId, tenantId = null) {
    const path = tenantId ? `/v1/tenants/${tenantId}/accounts/${accountId}/entries/pending/credits` : `/v1/accounts/${accountId}/entries/pending/credits`
    return this.get(path)
  }

  /**
   * Get pending debits for an account
   */
  async getPendingDebits(accountId, tenantId = null) {
    const path = tenantId ? `/v1/tenants/${tenantId}/accounts/${accountId}/entries/pending/debits` : `/v1/accounts/${accountId}/entries/pending/debits`
    return this.get(path)
  }

  /**
   * Cancel a pending entry
   */
  async cancelEntry(accountId, entryId, tenantId = null) {
    const path = tenantId ? `/v1/tenants/${tenantId}/accounts/${accountId}/entries/${entryId}` : `/v1/accounts/${accountId}/entries/${entryId}`
    return this.delete(path)
  }

  /**
   * Commit pending entries
   */
  async commitEntries(accountId, options = {}) {
    const {
      maxResults = 1000,
      startTime = null,
      endTime = null,
      amountMin = null,
      amountMax = null,
      entryIds = null,
      tenantId = null
    } = options

    const body = {
      MaxResults: maxResults,
      CreatedAfterUtc: startTime,
      CreatedBeforeUtc: endTime,
      MinimumAmount: amountMin,
      MaximumAmount: amountMax
    }

    // If specific entry IDs are provided, use them
    if (entryIds && entryIds.length > 0) {
      body.EntryIds = entryIds
    }

    const path = tenantId ? `/v1/tenants/${tenantId}/accounts/${accountId}/commit` : `/v1/accounts/${accountId}/commit`
    return this.post(path, body)
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
  async revokeApiKey(credentialId) {
    return this.delete(`/v1/credentials/${credentialId}`)
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

  async getArchiveHealth() {
    return this.get('/v1/health')
  }

  async listArchiveRanges(options = {}) {
    return this.get('/v1/archive/ranges', options)
  }

  async listArchiveManifests(options = {}) {
    return this.get('/v1/archive/manifests', options)
  }

  async readArchiveManifest(manifestId) {
    return this.get(`/v1/archive/manifests/${encodeURIComponent(manifestId)}`)
  }

  async listArchiveManifestObjects(manifestId, options = {}) {
    return this.get(`/v1/archive/manifests/${encodeURIComponent(manifestId)}/objects`, options)
  }

  async listArchiveManifestCheckpoints(manifestId, options = {}) {
    return this.get(`/v1/archive/manifests/${encodeURIComponent(manifestId)}/checkpoints`, options)
  }

  async verifyArchiveManifest(manifestId) {
    return this.post(`/v1/archive/manifests/${encodeURIComponent(manifestId)}/verify`)
  }

  async quarantineArchiveManifest(manifestId) {
    return this.post(`/v1/archive/manifests/${encodeURIComponent(manifestId)}/quarantine`)
  }

  async supersedeArchiveManifest(manifestId) {
    return this.post(`/v1/archive/manifests/${encodeURIComponent(manifestId)}/supersede`)
  }

  async listArchiveStoragePools(options = {}) {
    return this.get('/v1/archive/storage-pools', options)
  }

  async getArchiveStoragePoolHealth(storagePoolId) {
    return this.get(`/v1/archive/storage-pools/${encodeURIComponent(storagePoolId)}/health`)
  }

  async listArchiveMigrations(options = {}) {
    return this.get('/v1/archive/migrations', options)
  }

  async exportEntriesToArchive(request = {}) {
    return this.post('/v1/archive/exports/entries', request)
  }

  async exportRequestHistoryToArchive(request = {}) {
    return this.post('/v1/archive/exports/request-history', request)
  }

  async exportTenantAccountEntriesToArchive(tenantId, accountId, request = {}) {
    return this.post(`/v1/tenants/${encodeURIComponent(tenantId)}/accounts/${encodeURIComponent(accountId)}/archive/export`, request)
  }

  async listArchivedEntries(accountId, options = {}) {
    return this.get(`/v1/archive/accounts/${encodeURIComponent(accountId)}/entries`, options)
  }

  async listArchivedTenantEntries(tenantId, accountId, options = {}) {
    return this.get(`/v1/tenants/${encodeURIComponent(tenantId)}/accounts/${encodeURIComponent(accountId)}/entries`, options)
  }

  async verifyArchivedAccount(accountId, options = {}) {
    return this.get(`/v1/archive/accounts/${encodeURIComponent(accountId)}/verify`, options)
  }

  async verifyArchivedTenantAccount(tenantId, accountId, options = {}) {
    return this.get(`/v1/tenants/${encodeURIComponent(tenantId)}/accounts/${encodeURIComponent(accountId)}/verify`, options)
  }

  async readArchiveObjectMetadata(objectId) {
    return this.get(`/v1/archive/objects/${encodeURIComponent(objectId)}/metadata`)
  }

  async listArchivedRequestHistory(options = {}) {
    return this.get('/v1/request-history', options)
  }

  async summarizeArchivedRequestHistory(options = {}) {
    return this.get('/v1/request-history/summary', options)
  }

  async readArchivedRequestHistoryEntry(id, options = {}) {
    return this.get(`/v1/request-history/${encodeURIComponent(id)}`, options)
  }

  async listArchiveServerRequestHistory(options = {}) {
    return this.get('/v1/archive-server/request-history', options)
  }

  async summarizeArchiveServerRequestHistory(options = {}) {
    return this.get('/v1/archive-server/request-history/summary', options)
  }

  async readArchiveServerRequestHistoryEntry(id, options = {}) {
    return this.get(`/v1/archive-server/request-history/${encodeURIComponent(id)}`, options)
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
 * Converts { "id1": Balance, "id2": Balance } to array format
 */
export function normalizeBalances(balancesDict) {
  if (!balancesDict) {
    return []
  }

  // If it's already an array, return it
  if (Array.isArray(balancesDict)) {
    return balancesDict
  }

  // Convert dictionary to array with accountId attached
  return Object.entries(balancesDict).map(([id, balance]) => ({
    ...balance,
    accountId: id,
    AccountId: id
  }))
}

/**
 * Format a decimal number as currency
 */
export function formatCurrency(amount, units = null, showSign = false) {
  const parsed = amount === null || amount === undefined ? 0 : parseFloat(amount)
  const num = Number.isFinite(parsed) ? parsed : 0
  const unit = typeof units === 'string' ? units.trim() : ''

  let formatted = null

  // A three-letter unit is treated as an ISO 4217 currency code so it gets the correct
  // symbol and default fraction digits (e.g. USD -> $, JPY -> 0 decimals).
  if (unit.length === 3) {
    try {
      formatted = Math.abs(num).toLocaleString('en-US', {
        style: 'currency',
        currency: unit.toUpperCase()
      })
    } catch {
      formatted = null
    }
  }

  if (formatted === null) {
    const number = Math.abs(num).toLocaleString('en-US', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    })
    // No unit -> a plain amount with no currency symbol; a non-currency unit -> suffix label.
    formatted = unit ? `${number} ${unit}` : number
  }

  if (num < 0) {
    return `-${formatted}`
  }

  if (showSign && num > 0) {
    return `+${formatted}`
  }

  return formatted
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
 * Truncate an ID for display
 */
export function truncateId(id) {
  if (!id) return '-'
  if (id.length <= 13) return id
  return `${id.substring(0, 8)}...${id.substring(id.length - 4)}`
}
