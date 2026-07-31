import React, { useCallback, useEffect, useMemo, useState } from 'react'
import { useApp } from '../context/useApp'
import DataTable from '../components/DataTable'
import Pagination from '../components/Pagination'
import { ConfirmModal, ViewMetadataModal } from '../components/Modal'
import { normalizeEnumerationResult, truncateId } from '../api/api'
import { getRoleFlags } from '../utils/roles'
import './Archive.css'

function valueOf(row, key) {
  return row?.[key] ?? row?.[key.charAt(0).toLowerCase() + key.slice(1)]
}

function asArray(result) {
  if (Array.isArray(result)) return result
  return normalizeEnumerationResult(result).objects
}

function totalFrom(result, fallback) {
  if (Array.isArray(result)) return fallback
  return normalizeEnumerationResult(result).totalRecords
}

function initialEntryFilters() {
  return {
    accountId: '',
    search: '',
    startTime: '',
    endTime: '',
    ordering: 'CreatedDescending',
    allowPartial: false
  }
}

function initialRequestHistoryFilters() {
  return {
    principalId: '',
    method: '',
    statusCode: '',
    pathContains: '',
    startTime: '',
    endTime: '',
    allowPartial: false
  }
}

export default function Archive() {
  const {
    archiveApi,
    archiveServerUrl,
    setArchiveEndpoint,
    setDataSourceContext,
    setError,
    currentUser,
    effectivePermissions,
    t,
    formatNumber,
    formatDateTime,
    formatDuration
  } = useApp()
  const { isSystemAdmin, isTenantAdmin } = getRoleFlags(currentUser, effectivePermissions)
  const canManageArchiveMetadata = isSystemAdmin || isTenantAdmin
  const canInspectStoragePools = isSystemAdmin
  const [endpointDraft, setEndpointDraft] = useState(archiveServerUrl || '')
  const [health, setHealth] = useState(null)
  const [ranges, setRanges] = useState([])
  const [manifests, setManifests] = useState([])
  const [storagePools, setStoragePools] = useState([])
  const [metadataLoading, setMetadataLoading] = useState(false)
  const [metadataError, setMetadataError] = useState('')
  const [selectedJson, setSelectedJson] = useState(null)
  const [actionLoading, setActionLoading] = useState('')
  const [pendingManifestAction, setPendingManifestAction] = useState(null)
  const [verificationLoading, setVerificationLoading] = useState(false)
  const [entryFilters, setEntryFilters] = useState(initialEntryFilters)
  const [appliedEntryFilters, setAppliedEntryFilters] = useState(initialEntryFilters)
  const [entries, setEntries] = useState([])
  const [entriesLoading, setEntriesLoading] = useState(false)
  const [entryTotalRecords, setEntryTotalRecords] = useState(0)
  const [currentPage, setCurrentPage] = useState(0)
  const [pageSize, setPageSize] = useState(25)
  const [requestHistoryFilters, setRequestHistoryFilters] = useState(initialRequestHistoryFilters)
  const [appliedRequestHistoryFilters, setAppliedRequestHistoryFilters] = useState(initialRequestHistoryFilters)
  const [requestHistoryRows, setRequestHistoryRows] = useState([])
  const [requestHistoryLoading, setRequestHistoryLoading] = useState(false)
  const [requestHistoryTotalRecords, setRequestHistoryTotalRecords] = useState(0)
  const [requestHistoryPage, setRequestHistoryPage] = useState(0)
  const [requestHistoryPageSize, setRequestHistoryPageSize] = useState(25)

  useEffect(() => {
    setDataSourceContext('archive')
    return () => setDataSourceContext('active')
  }, [setDataSourceContext])

  useEffect(() => {
    setEndpointDraft(archiveServerUrl || '')
  }, [archiveServerUrl])

  const isConfigured = Boolean(archiveApi && archiveServerUrl)
  const translateValue = useCallback((prefix, value) => {
    if (!value) return '-'
    return t(`${prefix}.${value}`, { defaultValue: value })
  }, [t])

  const loadArchiveMetadata = useCallback(async () => {
    if (!archiveApi) {
      setHealth(null)
      setRanges([])
      setManifests([])
      setStoragePools([])
      return
    }

    setMetadataLoading(true)
    setMetadataError('')

    try {
      const [healthResult, rangeResult, manifestResult, storagePoolResult] = await Promise.all([
        archiveApi.getArchiveHealth(),
        archiveApi.listArchiveRanges({ maxResults: 100 }),
        archiveApi.listArchiveManifests({ maxResults: 100 }),
        canInspectStoragePools ? archiveApi.listArchiveStoragePools({ maxResults: 100 }) : Promise.resolve([])
      ])

      setHealth(healthResult)
      setRanges(asArray(rangeResult))
      setManifests(asArray(manifestResult))
      setStoragePools(asArray(storagePoolResult))
    } catch (err) {
      const message = err.message || t('archive.error.loadMetadata')
      setMetadataError(message)
      setError(message)
    } finally {
      setMetadataLoading(false)
    }
  }, [archiveApi, canInspectStoragePools, setError, t])

  useEffect(() => {
    loadArchiveMetadata()
  }, [loadArchiveMetadata])

  const entryQuery = useMemo(() => ({
    maxResults: pageSize,
    skip: currentPage * pageSize,
    ordering: appliedEntryFilters.ordering || 'CreatedDescending',
    search: appliedEntryFilters.search || null,
    startTime: appliedEntryFilters.startTime ? new Date(appliedEntryFilters.startTime).toISOString() : null,
    endTime: appliedEntryFilters.endTime ? new Date(appliedEntryFilters.endTime).toISOString() : null,
    allowPartial: appliedEntryFilters.allowPartial
  }), [appliedEntryFilters, currentPage, pageSize])

  const loadArchivedEntries = useCallback(async () => {
    if (!archiveApi || !appliedEntryFilters.accountId.trim()) {
      setEntries([])
      setEntryTotalRecords(0)
      return
    }

    setEntriesLoading(true)
    try {
      const result = await archiveApi.listArchivedEntries(appliedEntryFilters.accountId.trim(), entryQuery)
      const objects = asArray(result)
      setEntries(objects)
      setEntryTotalRecords(totalFrom(result, objects.length))
    } catch (err) {
      const message = err.message || t('archive.error.loadEntries')
      setEntries([])
      setEntryTotalRecords(0)
      setError(message)
    } finally {
      setEntriesLoading(false)
    }
  }, [archiveApi, appliedEntryFilters.accountId, entryQuery, setError, t])

  useEffect(() => {
    loadArchivedEntries()
  }, [loadArchivedEntries])

  const requestHistoryQuery = useMemo(() => ({
    maxResults: requestHistoryPageSize,
    skip: requestHistoryPage * requestHistoryPageSize,
    principalId: appliedRequestHistoryFilters.principalId || null,
    method: appliedRequestHistoryFilters.method || null,
    statusCode: appliedRequestHistoryFilters.statusCode || null,
    pathContains: appliedRequestHistoryFilters.pathContains || null,
    fromUtc: appliedRequestHistoryFilters.startTime ? new Date(appliedRequestHistoryFilters.startTime).toISOString() : null,
    toUtc: appliedRequestHistoryFilters.endTime ? new Date(appliedRequestHistoryFilters.endTime).toISOString() : null,
    allowPartial: appliedRequestHistoryFilters.allowPartial
  }), [appliedRequestHistoryFilters, requestHistoryPage, requestHistoryPageSize])

  const loadArchivedRequestHistory = useCallback(async () => {
    if (!archiveApi) {
      setRequestHistoryRows([])
      setRequestHistoryTotalRecords(0)
      return
    }

    setRequestHistoryLoading(true)
    try {
      const result = await archiveApi.listArchivedRequestHistory(requestHistoryQuery)
      const objects = asArray(result)
      setRequestHistoryRows(objects)
      setRequestHistoryTotalRecords(totalFrom(result, objects.length))
    } catch (err) {
      const message = err.message || t('archive.error.loadRequestHistory')
      setRequestHistoryRows([])
      setRequestHistoryTotalRecords(0)
      setError(message)
    } finally {
      setRequestHistoryLoading(false)
    }
  }, [archiveApi, requestHistoryQuery, setError, t])

  useEffect(() => {
    loadArchivedRequestHistory()
  }, [loadArchivedRequestHistory])

  const saveEndpoint = (event) => {
    event.preventDefault()
    const normalizedUrl = setArchiveEndpoint(endpointDraft)
    setEndpointDraft(normalizedUrl)
  }

  const clearEndpoint = () => {
    setArchiveEndpoint('')
    setEndpointDraft('')
    setMetadataError('')
  }

  const submitEntryQuery = (event) => {
    event.preventDefault()
    setCurrentPage(0)
    setAppliedEntryFilters({ ...entryFilters })
  }

  const submitRequestHistoryQuery = (event) => {
    event.preventDefault()
    setRequestHistoryPage(0)
    setAppliedRequestHistoryFilters({ ...requestHistoryFilters })
  }

  const readManifest = async (manifest) => {
    if (!archiveApi) return
    const manifestId = valueOf(manifest, 'Id')
    if (!manifestId) return

    try {
      const [manifestDetail, objects, checkpoints] = await Promise.all([
        archiveApi.readArchiveManifest(manifestId).catch(() => manifest),
        archiveApi.listArchiveManifestObjects(manifestId).catch(() => []),
        archiveApi.listArchiveManifestCheckpoints(manifestId).catch(() => [])
      ])
      const archiveObjects = asArray(objects)
      const objectMetadata = canManageArchiveMetadata
        ? await Promise.all(archiveObjects.map(object => {
          const objectId = valueOf(object, 'Id')
          return objectId ? archiveApi.readArchiveObjectMetadata(objectId).catch(error => ({ ObjectId: objectId, Error: error.message })) : Promise.resolve(null)
        }))
        : []

      setSelectedJson({
        title: t('archive.manifest.title', { manifestId: truncateId(manifestId) }),
        data: {
          Manifest: manifestDetail || manifest,
          Objects: archiveObjects,
          ObjectMetadata: objectMetadata.filter(Boolean),
          Checkpoints: asArray(checkpoints)
        }
      })
    } catch (err) {
      setError(err.message || t('archive.error.readManifest'))
    }
  }

  const readStoragePoolHealth = async (pool) => {
    if (!archiveApi) return
    const storagePoolId = valueOf(pool, 'Id')
    if (!storagePoolId) return

    try {
      const result = await archiveApi.getArchiveStoragePoolHealth(storagePoolId)
      setSelectedJson({
        title: t('archive.storagePool.title', { storagePoolId: truncateId(storagePoolId) }),
        data: result
      })
    } catch (err) {
      setError(err.message || t('archive.error.readStoragePool'))
    }
  }

  const performManifestAction = async (manifest, action) => {
    if (!archiveApi) return
    const manifestId = valueOf(manifest, 'Id')
    if (!manifestId) return

    const actionKey = `${action}:${manifestId}`
    setActionLoading(actionKey)
    try {
      if (action === 'verify') await archiveApi.verifyArchiveManifest(manifestId)
      if (action === 'quarantine') await archiveApi.quarantineArchiveManifest(manifestId)
      if (action === 'supersede') await archiveApi.supersedeArchiveManifest(manifestId)
      await loadArchiveMetadata()
    } catch (err) {
      setError(err.message || t('archive.error.runAction', { action }))
    } finally {
      setActionLoading('')
    }
  }

  const requestManifestAction = (manifest, action) => {
    if (action === 'quarantine' || action === 'supersede') {
      setPendingManifestAction({ manifest, action })
      return
    }

    performManifestAction(manifest, action)
  }

  const confirmManifestAction = async () => {
    if (!pendingManifestAction) return
    const action = pendingManifestAction
    setPendingManifestAction(null)
    await performManifestAction(action.manifest, action.action)
  }

  const verifyArchivedAccount = async () => {
    if (!archiveApi || !appliedEntryFilters.accountId.trim()) return

    setVerificationLoading(true)
    try {
      const result = await archiveApi.verifyArchivedAccount(appliedEntryFilters.accountId.trim(), entryQuery)
      setSelectedJson({
        title: t('archive.verify.accountResult'),
        data: result
      })
    } catch (err) {
      setError(err.message || t('archive.error.verifyAccount'))
    } finally {
      setVerificationLoading(false)
    }
  }

  const totalEntryPages = Math.max(1, Math.ceil(entryTotalRecords / pageSize))
  const totalRequestHistoryPages = Math.max(1, Math.ceil(requestHistoryTotalRecords / requestHistoryPageSize))
  const healthValue = health
    ? (valueOf(health, 'Healthy') ? t('archive.health.healthy') : t('archive.health.unhealthy'))
    : (isConfigured ? t('archive.health.unknown') : t('archive.health.notConfigured'))

  const manifestColumns = [
    { key: 'Id', label: t('common.manifest'), render: row => <code>{truncateId(valueOf(row, 'Id'))}</code> },
    { key: 'TenantId', label: t('common.tenant'), render: row => valueOf(row, 'TenantId') || '-' },
    { key: 'EntityType', label: t('archive.field.entity'), render: row => translateValue('archive.labels.entityType', valueOf(row, 'EntityType')) },
    { key: 'FromUtc', label: t('common.from'), render: row => formatDateTime(valueOf(row, 'FromUtc')) },
    { key: 'ToUtc', label: t('common.to'), render: row => formatDateTime(valueOf(row, 'ToUtc')) },
    { key: 'RowCount', label: t('common.rows'), render: row => formatNumber(valueOf(row, 'RowCount')) },
    { key: 'Status', label: t('common.status'), render: row => <span className="archive-status-pill">{translateValue('archive.labels.manifestStatus', valueOf(row, 'Status'))}</span> },
    {
      key: '__actions',
      label: '',
      sortable: false,
      render: row => {
        const manifestId = valueOf(row, 'Id')
        return (
          <div className="archive-action-row" data-ignore-row-click="true">
            <button className="btn btn-secondary btn-sm" onClick={() => readManifest(row)} disabled={!manifestId}>{t('common.view')}</button>
            {canManageArchiveMetadata && (
              <>
                <button className="btn btn-secondary btn-sm" onClick={() => requestManifestAction(row, 'verify')} disabled={!manifestId || actionLoading === `verify:${manifestId}`}>{t('archive.action.verify')}</button>
                <button className="btn btn-secondary btn-sm" onClick={() => requestManifestAction(row, 'quarantine')} disabled={!manifestId || actionLoading === `quarantine:${manifestId}`}>{t('archive.action.quarantine')}</button>
                <button className="btn btn-secondary btn-sm" onClick={() => requestManifestAction(row, 'supersede')} disabled={!manifestId || actionLoading === `supersede:${manifestId}`}>{t('archive.action.supersede')}</button>
              </>
            )}
          </div>
        )
      }
    }
  ]

  const rangeColumns = [
    { key: 'TenantId', label: t('common.tenant'), render: row => valueOf(row, 'TenantId') || '-' },
    { key: 'AccountId', label: t('archive.field.account'), render: row => valueOf(row, 'AccountId') || '-' },
    { key: 'EntityType', label: t('archive.field.entity'), render: row => translateValue('archive.labels.entityType', valueOf(row, 'EntityType')) },
    { key: 'FromUtc', label: t('common.from'), render: row => formatDateTime(valueOf(row, 'FromUtc')) },
    { key: 'ToUtc', label: t('common.to'), render: row => formatDateTime(valueOf(row, 'ToUtc')) },
    { key: 'RowCount', label: t('common.rows'), render: row => formatNumber(valueOf(row, 'RowCount')) }
  ]

  const storagePoolColumns = [
    { key: 'Id', label: t('archive.field.pool'), render: row => <code>{valueOf(row, 'Id') || '-'}</code> },
    { key: 'Name', label: t('common.name'), render: row => valueOf(row, 'Name') || '-' },
    { key: 'Type', label: t('common.type'), render: row => translateValue('archive.labels.storageType', valueOf(row, 'Type')) },
    { key: 'Format', label: t('archive.field.format'), render: row => valueOf(row, 'Format') || '-' },
    { key: 'Prefix', label: t('archive.field.prefix'), render: row => valueOf(row, 'Prefix') || '-' },
    {
      key: '__actions',
      label: '',
      sortable: false,
      render: row => (
        <div className="archive-action-row" data-ignore-row-click="true">
          <button className="btn btn-secondary btn-sm" onClick={() => readStoragePoolHealth(row)} disabled={!valueOf(row, 'Id')}>{t('archive.action.health')}</button>
        </div>
      )
    }
  ]

  const entryColumns = [
    { key: 'Id', label: t('common.entry'), render: row => <code>{truncateId(valueOf(row, 'Id'))}</code> },
    { key: 'AccountId', label: t('archive.field.account'), render: row => valueOf(row, 'AccountId') || appliedEntryFilters.accountId || '-' },
    { key: 'Amount', label: t('archive.field.amount'), render: row => formatNumber(valueOf(row, 'Amount')) },
    { key: 'CreatedUtc', label: t('common.created'), render: row => formatDateTime(valueOf(row, 'CreatedUtc')) },
    { key: 'Notes', label: t('common.notes'), render: row => valueOf(row, 'Notes') || '-' },
    {
      key: '__actions',
      label: '',
      sortable: false,
      render: row => (
        <button className="btn btn-secondary btn-sm" data-ignore-row-click="true" onClick={() => setSelectedJson({ title: t('archive.entry.title', { entryId: truncateId(valueOf(row, 'Id')) }), data: row })}>
          {t('common.json')}
        </button>
      )
    }
  ]

  const requestHistoryColumns = [
    { key: 'Id', label: t('archive.field.request'), render: row => <code>{truncateId(valueOf(row, 'Id'))}</code> },
    { key: 'Method', label: t('common.method'), render: row => valueOf(row, 'Method') || '-' },
    { key: 'StatusCode', label: t('common.status'), render: row => <span className="archive-status-pill">{valueOf(row, 'StatusCode') || '-'}</span> },
    { key: 'DurationMs', label: t('archive.field.duration'), render: row => formatDuration(valueOf(row, 'DurationMs')) },
    { key: 'Path', label: t('archive.field.path'), render: row => <span className="archive-path-cell">{valueOf(row, 'Path') || '-'}</span> },
    { key: 'CreatedUtc', label: t('common.created'), render: row => formatDateTime(valueOf(row, 'CreatedUtc')) },
    {
      key: '__actions',
      label: '',
      sortable: false,
      render: row => (
        <button
          className="btn btn-secondary btn-sm"
          data-ignore-row-click="true"
          onClick={async () => {
            const id = valueOf(row, 'Id')
            if (!id || !archiveApi) return
            try {
              const detail = await archiveApi.readArchivedRequestHistoryEntry(id, requestHistoryQuery)
              setSelectedJson({ title: t('archive.request.title', { requestId: truncateId(id) }), data: detail || row })
            } catch (err) {
              setError(err.message || t('archive.error.readRequest'))
            }
          }}
        >
          {t('common.json')}
        </button>
      )
    }
  ]

  return (
    <div className="archive-page">
      <section className="archive-panel">
        <div className="archive-panel-header">
          <div>
            <h2>{t('archive.server.title')}</h2>
            <p>{archiveServerUrl || t('archive.endpoint.unconfigured')}</p>
          </div>
          <button className="btn btn-secondary" onClick={loadArchiveMetadata} disabled={!isConfigured || metadataLoading}>
            {metadataLoading ? <span className="spinner spinner-sm"></span> : null}
            {t('common.refresh')}
          </button>
        </div>

        <form className="archive-endpoint-form" onSubmit={saveEndpoint}>
          <label className="archive-endpoint-field">
            <span>{t('archive.endpoint.label')}</span>
            <input
              type="url"
              value={endpointDraft}
              onChange={(event) => setEndpointDraft(event.target.value)}
              placeholder={t('archive.endpoint.placeholder')}
            />
          </label>
          <div className="archive-form-actions">
            <button className="btn btn-primary" type="submit">{t('common.save')}</button>
            <button className="btn btn-secondary" type="button" onClick={clearEndpoint}>{t('common.clear')}</button>
          </div>
        </form>

        {metadataError && <div className="archive-inline-error">{metadataError}</div>}

        <div className="archive-summary-grid">
          <div className="archive-stat">
            <span>{t('common.health')}</span>
            <strong>{healthValue}</strong>
          </div>
          <div className="archive-stat">
            <span>{t('archive.field.manifests')}</span>
            <strong>{formatNumber(manifests.length)}</strong>
          </div>
          <div className="archive-stat">
            <span>{t('archive.field.ranges')}</span>
            <strong>{formatNumber(ranges.length)}</strong>
          </div>
          <div className="archive-stat">
            <span>{t('archive.field.storagePools')}</span>
            <strong>{formatNumber(storagePools.length)}</strong>
          </div>
        </div>
      </section>

      <section className="archive-panel">
        <div className="archive-panel-header">
          <div>
            <h2>{t('archive.coldEntries.title')}</h2>
            <p>{t('archive.coldEntries.subtitle')}</p>
          </div>
        </div>

        <form className="archive-query-form" onSubmit={submitEntryQuery}>
          <label className="filter-wide">
            <span>{t('archive.field.accountId')}</span>
            <input
              value={entryFilters.accountId}
              onChange={(event) => setEntryFilters({ ...entryFilters, accountId: event.target.value })}
              placeholder={t('archive.placeholder.accountId')}
            />
          </label>
          <label>
            <span>{t('common.search')}</span>
            <input
              value={entryFilters.search}
              onChange={(event) => setEntryFilters({ ...entryFilters, search: event.target.value })}
              placeholder={t('archive.placeholder.search')}
            />
          </label>
          <label>
            <span>{t('archive.ordering.label')}</span>
            <select value={entryFilters.ordering} onChange={(event) => setEntryFilters({ ...entryFilters, ordering: event.target.value })}>
              <option value="CreatedDescending">{t('archive.ordering.createdDescending')}</option>
              <option value="CreatedAscending">{t('archive.ordering.createdAscending')}</option>
            </select>
          </label>
          <label>
            <span>{t('common.from')}</span>
            <input type="datetime-local" value={entryFilters.startTime} onChange={(event) => setEntryFilters({ ...entryFilters, startTime: event.target.value })} />
          </label>
          <label>
            <span>{t('common.to')}</span>
            <input type="datetime-local" value={entryFilters.endTime} onChange={(event) => setEntryFilters({ ...entryFilters, endTime: event.target.value })} />
          </label>
          <label className="archive-checkbox-field">
            <input type="checkbox" checked={entryFilters.allowPartial} onChange={(event) => setEntryFilters({ ...entryFilters, allowPartial: event.target.checked })} />
            <span>{t('archive.allowPartial')}</span>
          </label>
          <div className="archive-form-actions">
            <button className="btn btn-primary" type="submit" disabled={!isConfigured || !entryFilters.accountId.trim()}>
              {entriesLoading ? <span className="spinner spinner-sm"></span> : null}
              {t('common.query')}
            </button>
            <button className="btn btn-secondary" type="button" onClick={verifyArchivedAccount} disabled={!isConfigured || !appliedEntryFilters.accountId.trim() || verificationLoading}>
              {verificationLoading ? <span className="spinner spinner-sm"></span> : null}
              {t('archive.verify.account')}
            </button>
            <button className="btn btn-secondary" type="button" onClick={() => setEntryFilters(initialEntryFilters())}>{t('common.reset')}</button>
          </div>
        </form>

        <DataTable
          columns={entryColumns}
          data={entries}
          loading={entriesLoading}
          emptyMessage={appliedEntryFilters.accountId ? t('archive.empty.entriesNotFound') : t('archive.empty.entries')}
          rowKey="Id"
        />
        <Pagination
          currentPage={currentPage}
          totalPages={totalEntryPages}
          totalRecords={entryTotalRecords}
          pageSize={pageSize}
          onPageChange={setCurrentPage}
          onPageSizeChange={(size) => {
            setPageSize(size)
            setCurrentPage(0)
          }}
        />
      </section>

      <section className="archive-panel">
        <div className="archive-panel-header">
          <div>
            <h2>{t('archive.coldRequestHistory.title')}</h2>
            <p>{t('archive.coldRequestHistory.subtitle')}</p>
          </div>
        </div>

        <form className="archive-query-form" onSubmit={submitRequestHistoryQuery}>
          <label>
            <span>{t('archive.field.principalId')}</span>
            <input
              value={requestHistoryFilters.principalId}
              onChange={(event) => setRequestHistoryFilters({ ...requestHistoryFilters, principalId: event.target.value })}
              placeholder={t('archive.placeholder.principal')}
            />
          </label>
          <label>
            <span>{t('common.method')}</span>
            <select value={requestHistoryFilters.method} onChange={(event) => setRequestHistoryFilters({ ...requestHistoryFilters, method: event.target.value })}>
              <option value="">{t('common.any')}</option>
              <option value="GET">GET</option>
              <option value="POST">POST</option>
              <option value="PUT">PUT</option>
              <option value="DELETE">DELETE</option>
              <option value="PATCH">PATCH</option>
            </select>
          </label>
          <label>
            <span>{t('common.status')}</span>
            <input
              type="number"
              min="100"
              max="599"
              value={requestHistoryFilters.statusCode}
              onChange={(event) => setRequestHistoryFilters({ ...requestHistoryFilters, statusCode: event.target.value })}
              placeholder={t('common.any')}
            />
          </label>
          <label className="filter-wide">
            <span>{t('archive.field.pathContains')}</span>
            <input
              value={requestHistoryFilters.pathContains}
              onChange={(event) => setRequestHistoryFilters({ ...requestHistoryFilters, pathContains: event.target.value })}
              placeholder={t('archive.placeholder.path')}
            />
          </label>
          <label>
            <span>{t('common.from')}</span>
            <input type="datetime-local" value={requestHistoryFilters.startTime} onChange={(event) => setRequestHistoryFilters({ ...requestHistoryFilters, startTime: event.target.value })} />
          </label>
          <label>
            <span>{t('common.to')}</span>
            <input type="datetime-local" value={requestHistoryFilters.endTime} onChange={(event) => setRequestHistoryFilters({ ...requestHistoryFilters, endTime: event.target.value })} />
          </label>
          <label className="archive-checkbox-field">
            <input type="checkbox" checked={requestHistoryFilters.allowPartial} onChange={(event) => setRequestHistoryFilters({ ...requestHistoryFilters, allowPartial: event.target.checked })} />
            <span>{t('archive.allowPartial')}</span>
          </label>
          <div className="archive-form-actions">
            <button className="btn btn-primary" type="submit" disabled={!isConfigured}>
              {requestHistoryLoading ? <span className="spinner spinner-sm"></span> : null}
              {t('common.query')}
            </button>
            <button className="btn btn-secondary" type="button" onClick={() => setRequestHistoryFilters(initialRequestHistoryFilters())}>{t('common.reset')}</button>
          </div>
        </form>

        <DataTable
          columns={requestHistoryColumns}
          data={requestHistoryRows}
          loading={requestHistoryLoading}
          emptyMessage={t('archive.empty.requestHistory')}
          rowKey="Id"
        />
        <Pagination
          currentPage={requestHistoryPage}
          totalPages={totalRequestHistoryPages}
          totalRecords={requestHistoryTotalRecords}
          pageSize={requestHistoryPageSize}
          onPageChange={setRequestHistoryPage}
          onPageSizeChange={(size) => {
            setRequestHistoryPageSize(size)
            setRequestHistoryPage(0)
          }}
        />
      </section>

      <section className="archive-panel archive-table-panel">
        <div className="archive-panel-header">
          <div>
            <h2>{t('archive.manifests.title')}</h2>
            <p>{t('archive.manifests.subtitle')}</p>
          </div>
        </div>
        <DataTable columns={manifestColumns} data={manifests} loading={metadataLoading} emptyMessage={t('archive.empty.manifests')} rowKey="Id" onRowClick={readManifest} />
      </section>

      <section className="archive-panel archive-table-panel">
        <div className="archive-panel-header">
          <div>
            <h2>{t('archive.coverage.title')}</h2>
            <p>{t('archive.coverage.subtitle')}</p>
          </div>
        </div>
        <DataTable columns={rangeColumns} data={ranges} loading={metadataLoading} emptyMessage={t('archive.empty.ranges')} rowKey="TenantId" />
      </section>

      {canInspectStoragePools && (
        <section className="archive-panel archive-table-panel">
          <div className="archive-panel-header">
            <div>
              <h2>{t('archive.storagePools.title')}</h2>
              <p>{t('archive.storagePools.subtitle')}</p>
            </div>
          </div>
          <DataTable columns={storagePoolColumns} data={storagePools} loading={metadataLoading} emptyMessage={t('archive.empty.storagePools')} rowKey="Id" />
        </section>
      )}

      <ViewMetadataModal
        isOpen={Boolean(selectedJson)}
        onClose={() => setSelectedJson(null)}
        title={selectedJson?.title || t('archive.verify.accountResult')}
        data={selectedJson?.data || null}
      />
      <ConfirmModal
        isOpen={Boolean(pendingManifestAction)}
        onClose={() => setPendingManifestAction(null)}
        onConfirm={confirmManifestAction}
        title={pendingManifestAction ? t('archive.action.' + pendingManifestAction.action) : t('archive.action.verify')}
        message={pendingManifestAction ? t('archive.confirmManifestAction', {
          action: t('archive.action.' + pendingManifestAction.action),
          manifestId: valueOf(pendingManifestAction.manifest, 'Id')
        }) : ''}
        confirmText={pendingManifestAction ? t('archive.action.' + pendingManifestAction.action) : t('archive.action.verify')}
        isLoading={Boolean(actionLoading)}
      />
    </div>
  )
}
