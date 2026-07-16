import React, { useState, useEffect, useCallback } from 'react'
import { useApp } from '../context/useApp'
import DataTable from '../components/DataTable'
import Pagination from '../components/Pagination'
import ActionMenu from '../components/ActionMenu'
import Modal, { ConfirmModal, RecordModal, ViewMetadataModal } from '../components/Modal'
import CopyButton from '../components/CopyButton'
import { HiddenValueDisplay } from '../components/HiddenValue'
import { formatDate, normalizeEnumerationResult } from '../api/api'
import { getRoleFlags } from '../utils/roles'
import './ApiKeys.css'

export default function ApiKeys() {
  const { api, currentUser, effectivePermissions, setError } = useApp()
  const { isRegularUser } = getRoleFlags(currentUser, effectivePermissions)

  // Data state
  const [apiKeys, setApiKeys] = useState([])
  const [loading, setLoading] = useState(true)
  const [totalRecords, setTotalRecords] = useState(0)

  // Pagination state
  const [currentPage, setCurrentPage] = useState(0)
  const [pageSize, setPageSize] = useState(25)

  // Modal state
  const [showCreateModal, setShowCreateModal] = useState(false)
  const [showEditModal, setShowEditModal] = useState(false)
  const [showViewModal, setShowViewModal] = useState(false)
  const [showDeleteModal, setShowDeleteModal] = useState(false)
  const [showMetadataModal, setShowMetadataModal] = useState(false)
  const [showNewKeyModal, setShowNewKeyModal] = useState(false)
  const [selectedKey, setSelectedKey] = useState(null)
  const [newKeyData, setNewKeyData] = useState(null)

  // Form state
  const [formData, setFormData] = useState({ name: '' })
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
      setError(err.message || 'Failed to load credentials')
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
      const result = await api.createApiKey(formData.name.trim())

      setShowCreateModal(false)
      setFormData({ name: '' })

      // Show the new secret to the user (only shown once).
      setNewKeyData(result)
      setShowNewKeyModal(true)

      loadApiKeys()
    } catch (err) {
      setError(err.message || 'Failed to create credential')
    } finally {
      setFormLoading(false)
    }
  }

  // Helper to extract GUID from an object with various casing conventions
  const getGuid = (obj) => {
    if (!obj) return null
    // Check GUID first (server uses uppercase GUID), then lowercase variants
    return obj.GUID || obj.guid || obj.Guid || obj.Id || obj.id || null
  }

  const handleDelete = async () => {
    if (!selectedKey) return

    const keyGuid = getGuid(selectedKey)
    if (!keyGuid) {
      setError('Cannot revoke credential: missing identifier')
      return
    }

    try {
      setFormLoading(true)
      await api.revokeApiKey(keyGuid)
      setShowDeleteModal(false)
      setSelectedKey(null)
      loadApiKeys()
    } catch (err) {
      setError(err.message || 'Failed to revoke credential')
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

  const openEditModal = (key) => {
    setSelectedKey(key)
    setShowEditModal(true)
  }

  const openViewModal = (key) => {
    setSelectedKey(key)
    setShowViewModal(true)
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
      label: 'Access Key',
      render: (row) => {
        const apiKey = row.key || row.Key
        return (
          <HiddenValueDisplay
            value={apiKey}
            visiblePrefix={4}
            className="api-key-hidden-display"
            valueClassName="api-key-display"
          />
        )
      }
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
            {
              label: 'Delete',
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
          <h2 className="page-title">Credentials</h2>
          <p className="page-description">{isRegularUser ? 'Manage your credentials' : 'Manage credentials for authentication'}</p>
        </div>
        <div className="page-header-actions">
          <button className="btn btn-primary" onClick={() => setShowCreateModal(true)}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <line x1="12" y1="5" x2="12" y2="19"/>
              <line x1="5" y1="12" x2="19" y2="12"/>
            </svg>
            Create Credential
          </button>
        </div>
      </div>

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
        data={apiKeys}
        loading={loading}
        emptyMessage="No credentials found"
        onRowClick={openEditModal}
        rowKey="guid"
      />

      {/* Create Modal */}
      <Modal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        title="Create Credential"
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
              placeholder="Enter a name for this credential"
              disabled={formLoading}
              autoFocus
            />
            <span className="form-hint">A descriptive name to identify this credential</span>
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
        title="Credential Created"
        size="medium"
        closeOnOverlay={false}
      >
        <div className="new-key-warning">
          <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/>
            <line x1="12" y1="9" x2="12" y2="13"/>
            <line x1="12" y1="17" x2="12.01" y2="17"/>
          </svg>
          <p>Copy this credential secret now. You won't be able to see it again!</p>
        </div>

        {newKeyData && (
          <div className="new-key-display">
            <label>Access Key</label>
            <div className="new-key-value">
              <HiddenValueDisplay
                value={newKeyData.Credential?.Key || newKeyData.credential?.key || newKeyData.key || newKeyData.Key}
                visiblePrefix={4}
                valueClassName="new-key-code"
              />
              <CopyButton
                text={newKeyData.Credential?.Key || newKeyData.credential?.key || newKeyData.key || newKeyData.Key}
                title="Copy access key"
                size={16}
              />
            </div>
            <label>Secret Key</label>
            <div className="new-key-value">
              <HiddenValueDisplay
                value={newKeyData.SecretKey || newKeyData.secretKey}
                valueClassName="new-key-code"
              />
              <CopyButton
                text={newKeyData.SecretKey || newKeyData.secretKey}
                title="Copy secret key"
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
        title="Revoke Credential"
        message={`Are you sure you want to revoke the credential "${selectedKey?.name || selectedKey?.Name}"? This action cannot be undone.`}
        confirmText="Revoke"
        variant="danger"
        isLoading={formLoading}
      />

      <RecordModal
        isOpen={showEditModal}
        onClose={() => {
          setShowEditModal(false)
          setSelectedKey(null)
        }}
        title={`Edit ${selectedKey?.name || selectedKey?.Name || 'Credential'}`}
        data={selectedKey}
        mode="edit"
      />

      <RecordModal
        isOpen={showViewModal}
        onClose={() => {
          setShowViewModal(false)
          setSelectedKey(null)
        }}
        title={`View ${selectedKey?.name || selectedKey?.Name || 'Credential'}`}
        data={selectedKey}
      />

      {/* View Metadata Modal */}
      <ViewMetadataModal
        isOpen={showMetadataModal}
        onClose={() => {
          setShowMetadataModal(false)
          setSelectedKey(null)
        }}
        title="Credential Metadata"
        data={selectedKey}
      />
    </div>
  )
}
