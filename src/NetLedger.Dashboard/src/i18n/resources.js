const ENGLISH = {
  common: {
    active: 'Active',
    archive: 'Archive',
    any: 'Any',
    back: 'Back',
    cancel: 'Cancel',
    checking: 'Checking...',
    clear: 'Clear',
    connect: 'Connect',
    connecting: 'Connecting...',
    continue: 'Continue',
    created: 'Created',
    dashboard: 'Dashboard',
    dismiss: 'Dismiss',
    entry: 'Entry',
    error: 'Error',
    failed: 'Failed',
    from: 'From',
    health: 'Health',
    home: 'Home',
    json: 'JSON',
    loading: 'Loading...',
    manifest: 'Manifest',
    method: 'Method',
    name: 'Name',
    none: 'None',
    notes: 'Notes',
    query: 'Query',
    refresh: 'Refresh',
    refreshing: 'Refreshing...',
    reset: 'Reset',
    rows: 'Rows',
    save: 'Save',
    search: 'Search',
    status: 'Status',
    success: 'Success',
    tenant: 'Tenant',
    to: 'To',
    total: 'Total',
    type: 'Type',
    view: 'View'
  },
  archive: {
    action: {
      health: 'Health',
      quarantine: 'Quarantine',
      supersede: 'Supersede',
      verify: 'Verify'
    },
    allowPartial: 'Allow partial coverage',
    coldEntries: {
      subtitle: 'Queries are sent directly to Archive Server',
      title: 'Cold Entries'
    },
    coldRequestHistory: {
      subtitle: 'Queries are sent directly to Archive Server',
      title: 'Cold Request History'
    },
    confirmManifestAction: 'Confirm {action} for archive manifest {manifestId}?',
    coverage: {
      subtitle: 'Tenant, account, entity, and time ranges available in cold storage',
      title: 'Archive Coverage'
    },
    empty: {
      entries: 'Enter an account ID to query archived entries',
      entriesNotFound: 'No archived entries found',
      manifests: 'No archive manifests found',
      ranges: 'No archive ranges found',
      requestHistory: 'No archived request history found',
      storagePools: 'No archive storage pools configured'
    },
    endpoint: {
      label: 'Archive Server URL',
      placeholder: 'http://localhost:8081',
      unconfigured: 'No archive endpoint configured'
    },
    entry: {
      title: 'Archived entry {entryId}'
    },
    error: {
      loadEntries: 'Unable to query archived entries',
      loadMetadata: 'Unable to load archive metadata',
      loadRequestHistory: 'Unable to query archived request history',
      readManifest: 'Unable to read archive manifest',
      readRequest: 'Unable to read archived request history entry',
      readStoragePool: 'Unable to read storage pool health',
      runAction: 'Unable to {action} archive manifest',
      verifyAccount: 'Unable to verify archived account'
    },
    field: {
      account: 'Account',
      accountId: 'Account ID',
      amount: 'Amount',
      duration: 'Duration',
      entity: 'Entity',
      format: 'Format',
      path: 'Path',
      pathContains: 'Path Contains',
      pool: 'Pool',
      prefix: 'Prefix',
      principalId: 'Principal ID',
      request: 'Request',
      storagePools: 'Storage Pools',
      manifests: 'Manifests',
      ranges: 'Ranges',
      statusCode: 'Status'
    },
    health: {
      healthy: 'Healthy',
      notConfigured: 'Not configured',
      unknown: 'Unknown',
      unhealthy: 'Unhealthy'
    },
    labels: {
      entityType: {
        Entry: 'Entry',
        RequestHistory: 'Request History'
      },
      manifestStatus: {
        Committed: 'Committed',
        Failed: 'Failed',
        Open: 'Open',
        Quarantined: 'Quarantined',
        Sealed: 'Sealed',
        Superseded: 'Superseded'
      },
      storageType: {
        FileSystem: 'File system',
        S3: 'S3'
      }
    },
    manifest: {
      title: 'Archive manifest {manifestId}'
    },
    manifests: {
      subtitle: 'Verify, quarantine, supersede, and inspect cold metadata',
      title: 'Archive Manifests'
    },
    ordering: {
      createdAscending: 'Oldest first',
      createdDescending: 'Newest first',
      label: 'Ordering'
    },
    placeholder: {
      accountId: 'Account identifier',
      path: '/v1/accounts',
      principal: 'User or credential',
      search: 'Notes, labels, tags'
    },
    request: {
      title: 'Archived request {requestId}'
    },
    server: {
      title: 'Archive Server'
    },
    storagePool: {
      title: 'Storage pool {storagePoolId}'
    },
    storagePools: {
      subtitle: 'Configured filesystem or object storage pools used by Archive Server',
      title: 'Storage Pools'
    },
    verify: {
      account: 'Verify account',
      accountResult: 'Archived account verification',
      latestCheckpoint: 'Latest archived checkpoint balance is {balance}.'
    }
  },
  dataSource: {
    active: 'Active data',
    archive: 'Archive data',
    archiveUnavailable: 'Archive endpoint is not configured',
    contextTitle: 'Current data source: {source}',
    entriesArchiveDescription: 'Archive mode queries cold entries directly from Archive Server. Mutation controls are hidden.',
    label: 'Data Source',
    requestHistoryArchiveDescription: 'Archive mode queries cold request history directly from Archive Server. Delete controls are hidden.'
  },
  login: {
    defaultCredentials: 'Deployment defaults:',
    email: 'Email',
    emailRequired: 'Email is required',
    language: 'Language',
    noTenant: 'No tenant was found for that email',
    password: 'Password',
    passwordPlaceholder: 'Enter your password',
    passwordRequired: 'Password is required',
    serverHint: 'The URL of your NetLedger server',
    serverRequired: 'Server URL is required',
    serverUrl: 'Server URL',
    subtitle: 'Connect to your ledger server',
    tenant: 'Tenant',
    tenantHint: 'This email is mapped to multiple tenants',
    tenantSelect: 'Select tenant',
    tenantRequired: 'Select a tenant to continue',
    theme: 'Switch to {mode} mode',
    unableDiscover: 'Unable to discover tenants for that email',
    unexpected: 'An unexpected error occurred'
  },
  topbar: {
    connectedTo: 'Connected to',
    dark: 'dark',
    disconnect: 'Disconnect',
    github: 'Open NetLedger on GitHub',
    light: 'light',
    switchTheme: 'Switch to {mode} mode'
  }
}

function pseudoExpandText(text) {
  return `[!! ${text.replace(/[aeiou]/gi, '$&$&')} !!]`
}

function mapStrings(value, mapper) {
  if (typeof value === 'string') return mapper(value)
  if (Array.isArray(value)) return value.map((item) => mapStrings(item, mapper))
  if (!value || typeof value !== 'object') return value

  return Object.entries(value).reduce((result, [key, child]) => {
    result[key] = mapStrings(child, mapper)
    return result
  }, {})
}

export const resources = {
  en: { translation: ENGLISH },
  'qps-ploc': { translation: mapStrings(ENGLISH, pseudoExpandText) },
  'qps-plocm': { translation: mapStrings(ENGLISH, pseudoExpandText) }
}
