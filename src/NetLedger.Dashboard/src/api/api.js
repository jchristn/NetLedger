/**
 * NetLedger API Client
 * Handles all communication with the NetLedger.Server backend
 */

export class NetLedgerApi {
  constructor(baseUrl, apiKey) {
    this.baseUrl = baseUrl.replace(/\/+$/, '')
    this.apiKey = apiKey
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
        throw new Error(`HTTP ${response.status}: ${response.statusText}`)
      }
      return null
    }

    // Check for API-level errors
    if (!response.ok) {
      const errorMessage = data?.Error?.Message || data?.error?.message || data?.message || `HTTP ${response.status}`
      throw new Error(errorMessage)
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

  // ==================== Service Endpoints ====================

  /**
   * Get server information
   */
  async getServerInfo() {
    return this.get('/')
  }

  // ==================== Account Endpoints ====================

  /**
   * Create a new account
   */
  async createAccount(name, initialBalance = null, notes = null) {
    const body = { Name: name }
    if (initialBalance !== null) {
      body.InitialBalance = initialBalance
    }
    if (notes !== null) {
      body.Notes = notes
    }
    return this.put('/v1/accounts', body)
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
      endTime = null
    } = options

    return this.get('/v1/accounts', {
      maxResults,
      skip,
      ordering,
      search,
      startTime,
      endTime
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
        Notes: e.description || e.Description || e.notes || e.Notes || ''
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
        Notes: e.description || e.Description || e.notes || e.Notes || ''
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
      startTime = null,
      endTime = null,
      amountMin = null,
      amountMax = null
    } = options

    return this.get(`/v1/accounts/${accountGuid}/entries`, {
      maxResults,
      skip,
      ordering,
      startTime,
      endTime,
      amountMin,
      amountMax
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

  // ==================== API Key Endpoints ====================

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

    return this.get('/v1/apikeys', {
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
  async createApiKey(name, isAdmin = false) {
    return this.put('/v1/apikeys', {
      Name: name,
      IsAdmin: isAdmin
    })
  }

  /**
   * Revoke an API key (admin only)
   */
  async revokeApiKey(apiKeyGuid) {
    return this.delete(`/v1/apikeys/${apiKeyGuid}`)
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
  return date.toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
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
