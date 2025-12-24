import React, { useState, useEffect, useCallback } from 'react'
import { useApp } from '../context/AppContext'
import DataTable from '../components/DataTable'
import Pagination from '../components/Pagination'
import ActionMenu from '../components/ActionMenu'
import Modal, { ConfirmModal, ViewMetadataModal } from '../components/Modal'
import CopyButton from '../components/CopyButton'
import { formatDate, normalizeEnumerationResult } from '../api/api'
import './ApiKeys.css'

export default function ApiKeys() {
  const { api, setError } = useApp()

  // Data state
  const [apiKeys, setApiKeys] = useState([])
  const [loading, setLoading] = useState(true)
  const [totalRecords, setTotalRecords] = useState(0)

  // Pagination state
  const [currentPage, setCurrentPage] = useState(0)
  const [pageSize, setPageSize] = useState(25)

  // Modal state
  const [showCreateModal, setShowCreateModal] = useState(false)
  const [showDeleteModal, setShowDeleteModal] = useState(false)
  const [showMetadataModal, setShowMetadataModal] = useState(false)
  const [showNewKeyModal, setShowNewKeyModal] = useState(false)
  const [selectedKey, setSelectedKey] = useState(null)
  const [newKeyData, setNewKeyData] = useState(null)

  // Form state
  const [formData, setFormData] = useState({ name: '', isAdmin: false })
  const [formLoading, setFormLoading] = useState(false)

  const loadApiKeys = useCallback(async () => {
    try {
      setLoading(true)
      const result = await api.listApiKeys({
        maxResults: pageSize,
        skip: currentPage * pageSize,
        ordering: 'CreatedDescending'
      })

      const { objects, totalRecords } = normalizeEnumerationResult(result)
      setApiKeys(objects)
      setTotalRecords(totalRecords)
    } catch (err) {
      setError(err.message || 'Failed to load API keys')
    } finally {
      setLoading(false)
    }
  }, [api, currentPage, pageSize, setError])

  useEffect(() => {
    loadApiKeys()
  }, [loadApiKeys])

  const handlePageChange = (page) => {
    setCurrentPage(page)
  }

  const handlePageSizeChange = (size) => {
    setPageSize(size)
    setCurrentPage(0)
  }

  const handleCreate = async (e) => {
    e.preventDefault()

    if (!formData.name.trim()) {
      setError('Name is required')
      return
    }

    try {
      setFormLoading(true)
      const result = await api.createApiKey(formData.name.trim(), formData.isAdmin)

      setShowCreateModal(false)
      setFormData({ name: '', isAdmin: false })

      // Show the new key to the user (only shown once!)
      setNewKeyData(result)
      setShowNewKeyModal(true)

      loadApiKeys()
    } catch (err) {
      setError(err.message || 'Failed to create API key')
    } finally {
      setFormLoading(false)
    }
  }

  // Helper to extract GUID from an object with various casing conventions
  const getGuid = (obj) => {
    if (!obj) return null
    // Check GUID first (server uses uppercase GUID), then lowercase variants
    return obj.GUID || obj.guid || obj.Guid || null
  }

  const handleDelete = async () => {
    if (!selectedKey) return

    const keyGuid = getGuid(selectedKey)
    if (!keyGuid) {
      setError('Cannot revoke key: missing identifier')
      return
    }

    try {
      setFormLoading(true)
      await api.revokeApiKey(keyGuid)
      setShowDeleteModal(false)
      setSelectedKey(null)
      loadApiKeys()
    } catch (err) {
      setError(err.message || 'Failed to revoke API key')
    } finally {
      setFormLoading(false)
    }
  }

  const openDeleteModal = (key) => {
    setSelectedKey(key)
    setShowDeleteModal(true)
  }

  const openMetadataModal = (key) => {
    setSelectedKey(key)
    setShowMetadataModal(true)
  }

  const totalPages = Math.ceil(totalRecords / pageSize)

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
      key: 'name',
      label: 'Name',
      sortable: true,
      filterable: true,
      render: (row) => row.name || row.Name || '-',
      filterValue: (row) => row.name || row.Name || ''
    },
    {
      key: 'key',
      label: 'API Key',
      render: (row) => {
        const apiKey = row.key || row.Key
        const redactedKey = apiKey
          ? `${apiKey.substring(0, 4)}${'•'.repeat(Math.max(0, apiKey.length - 4))}`
          : '••••••••'
        return (
          <code className="api-key-display" title="API key is partially hidden for security">
            {redactedKey}
          </code>
        )
      }
    },
    {
      key: 'isAdmin',
      label: 'Admin',
      className: 'col-status',
      sortable: true,
      render: (row) => {
        const isAdmin = row.isAdmin ?? row.IsAdmin ?? false
        return (
          <span className={`badge ${isAdmin ? 'badge-primary' : 'badge-neutral'}`}>
            {isAdmin ? 'Yes' : 'No'}
          </span>
        )
      },
      sortValue: (row) => row.isAdmin ?? row.IsAdmin ?? false
    },
    {
      key: 'active',
      label: 'Status',
      className: 'col-status',
      sortable: true,
      render: (row) => {
        const active = row.active ?? row.Active ?? true
        return (
          <span className={`badge ${active ? 'badge-success' : 'badge-danger'}`}>
            {active ? 'Active' : 'Inactive'}
          </span>
        )
      },
      sortValue: (row) => row.active ?? row.Active ?? true
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
      render: (row) => (
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
            { divider: true },
            {
              label: 'Revoke Key',
              variant: 'danger',
              icon: (
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <polyline points="3 6 5 6 21 6"/>
                  <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>
                </svg>
              ),
              onClick: () => openDeleteModal(row)
            }
          ]}
        />
      )
    }
  ]

  return (
    <div className="api-keys-page">
      <div className="page-header">
        <div className="page-header-left">
          <h2 className="page-title">API Keys</h2>
          <p className="page-description">Manage API keys for authentication</p>
        </div>
        <div className="page-header-actions">
          <button className="btn btn-primary" onClick={() => setShowCreateModal(true)}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <line x1="12" y1="5" x2="12" y2="19"/>
              <line x1="5" y1="12" x2="19" y2="12"/>
            </svg>
            Create API Key
          </button>
        </div>
      </div>

      <DataTable
        columns={columns}
        data={apiKeys}
        loading={loading}
        emptyMessage="No API keys found"
        rowKey="guid"
      />

      <Pagination
        currentPage={currentPage}
        totalPages={totalPages}
        totalRecords={totalRecords}
        pageSize={pageSize}
        onPageChange={handlePageChange}
        onPageSizeChange={handlePageSizeChange}
      />

      {/* Create Modal */}
      <Modal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        title="Create API Key"
        size="small"
        footer={
          <>
            <button
              className="btn btn-secondary"
              onClick={() => setShowCreateModal(false)}
              disabled={formLoading}
            >
              Cancel
            </button>
            <button
              className="btn btn-primary"
              onClick={handleCreate}
              disabled={formLoading}
            >
              {formLoading ? (
                <>
                  <span className="spinner spinner-sm"></span>
                  Creating...
                </>
              ) : (
                'Create'
              )}
            </button>
          </>
        }
      >
        <form onSubmit={handleCreate}>
          <div className="form-group">
            <label htmlFor="keyName">Name</label>
            <input
              type="text"
              id="keyName"
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
              placeholder="Enter a name for this key"
              disabled={formLoading}
              autoFocus
            />
            <span className="form-hint">A descriptive name to identify this key</span>
          </div>

          <div className="form-group">
            <label className="checkbox-label">
              <input
                type="checkbox"
                checked={formData.isAdmin}
                onChange={(e) => setFormData({ ...formData, isAdmin: e.target.checked })}
                disabled={formLoading}
              />
              <span>Admin privileges</span>
            </label>
            <span className="form-hint">Admin keys can manage other API keys</span>
          </div>
        </form>
      </Modal>

      {/* New Key Modal */}
      <Modal
        isOpen={showNewKeyModal}
        onClose={() => {
          setShowNewKeyModal(false)
          setNewKeyData(null)
        }}
        title="API Key Created"
        size="medium"
        closeOnOverlay={false}
      >
        <div className="new-key-warning">
          <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/>
            <line x1="12" y1="9" x2="12" y2="13"/>
            <line x1="12" y1="17" x2="12.01" y2="17"/>
          </svg>
          <p>Copy this API key now. You won't be able to see it again!</p>
        </div>

        {newKeyData && (
          <div className="new-key-display">
            <label>API Key</label>
            <div className="new-key-value">
              <code>{newKeyData.key || newKeyData.Key}</code>
              <CopyButton
                text={newKeyData.key || newKeyData.Key}
                label="Copy"
                size={16}
              />
            </div>
          </div>
        )}
      </Modal>

      {/* Delete Confirmation Modal */}
      <ConfirmModal
        isOpen={showDeleteModal}
        onClose={() => {
          setShowDeleteModal(false)
          setSelectedKey(null)
        }}
        onConfirm={handleDelete}
        title="Revoke API Key"
        message={`Are you sure you want to revoke the API key "${selectedKey?.name || selectedKey?.Name}"? This action cannot be undone.`}
        confirmText="Revoke"
        variant="danger"
        isLoading={formLoading}
      />

      {/* View Metadata Modal */}
      <ViewMetadataModal
        isOpen={showMetadataModal}
        onClose={() => {
          setShowMetadataModal(false)
          setSelectedKey(null)
        }}
        title="API Key Metadata"
        data={selectedKey}
      />
    </div>
  )
}
