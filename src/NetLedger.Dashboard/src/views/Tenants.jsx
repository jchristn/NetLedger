import React, { useCallback, useEffect, useState } from 'react'
import { useApp } from '../context/useApp'
import DataTable from '../components/DataTable'
import Pagination from '../components/Pagination'
import Modal from '../components/Modal'
import CopyButton from '../components/CopyButton'
import { normalizeEnumerationResult, formatDate } from '../api/api'
import { getRoleFlags } from '../utils/roles'
import './ApiKeys.css'

export default function Tenants() {
  const { api, tenantId, currentTenant, currentUser, effectivePermissions, setError } = useApp()
  const { isSystemAdmin } = getRoleFlags(currentUser, effectivePermissions)
  const [tenants, setTenants] = useState([])
  const [loading, setLoading] = useState(true)
  const [totalRecords, setTotalRecords] = useState(0)
  const [currentPage, setCurrentPage] = useState(0)
  const [pageSize, setPageSize] = useState(25)
  const [showCreateModal, setShowCreateModal] = useState(false)
  const [newTenantName, setNewTenantName] = useState('')
  const [formLoading, setFormLoading] = useState(false)

  const loadTenants = useCallback(async () => {
    try {
      setLoading(true)

      if (isSystemAdmin) {
        const tenantResult = await api.listTenants({
          maxResults: pageSize,
          skip: currentPage * pageSize,
          ordering: 'CreatedDescending'
        })
        const { objects, totalRecords } = normalizeEnumerationResult(tenantResult)
        setTenants(objects)
        setTotalRecords(totalRecords)
      } else if (tenantId) {
        try {
          const tenant = await api.readTenant(tenantId)
          setTenants(tenant ? [tenant] : [])
          setTotalRecords(tenant ? 1 : 0)
        } catch {
          setTenants(currentTenant ? [currentTenant] : [])
          setTotalRecords(currentTenant ? 1 : 0)
        }
      } else {
        setTenants([])
        setTotalRecords(0)
      }
    } catch (err) {
      setError(err.message || 'Failed to load tenants')
    } finally {
      setLoading(false)
    }
  }, [api, currentPage, currentTenant, isSystemAdmin, pageSize, setError, tenantId])

  useEffect(() => {
    loadTenants()
  }, [loadTenants])

  const createTenant = async (e) => {
    e.preventDefault()
    if (!isSystemAdmin || !newTenantName.trim()) return

    try {
      setFormLoading(true)
      await api.createTenant({ Name: newTenantName.trim() })
      setNewTenantName('')
      setShowCreateModal(false)
      loadTenants()
    } catch (err) {
      setError(err.message || 'Failed to create tenant')
    } finally {
      setFormLoading(false)
    }
  }

  const handlePageChange = (page) => {
    setCurrentPage(page)
  }

  const handlePageSizeChange = (size) => {
    setPageSize(size)
    setCurrentPage(0)
  }

  const totalPages = Math.ceil(totalRecords / pageSize)

  const tenantColumns = [
    {
      key: 'id',
      label: 'ID',
      className: 'col-id',
      render: row => {
        const id = row.Id || row.id
        return (
          <span className="id-cell-wrapper">
            <span className="id-cell">{id}</span>
            <CopyButton text={id} title="Copy ID" />
          </span>
        )
      }
    },
    { key: 'name', label: 'Name', render: row => row.Name || row.name },
    { key: 'active', label: 'Active', render: row => (row.Active ?? row.active) ? 'Yes' : 'No' },
    { key: 'protected', label: 'Protected', render: row => (row.IsProtected ?? row.isProtected) ? 'Yes' : 'No' },
    { key: 'created', label: 'Created', className: 'col-date', render: row => formatDate(row.CreatedUtc || row.createdUtc) }
  ]

  return (
    <div className="api-keys-page">
      <div className="page-header">
        <div className="page-header-left">
          <h2 className="page-title">Tenants</h2>
          <p className="page-description">{isSystemAdmin ? 'Manage deployment tenants' : 'View your tenant'}</p>
        </div>
        {isSystemAdmin && (
          <div className="page-header-actions">
            <button className="btn btn-primary" onClick={() => setShowCreateModal(true)}>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <line x1="12" y1="5" x2="12" y2="19"/>
                <line x1="5" y1="12" x2="19" y2="12"/>
              </svg>
              Create Tenant
            </button>
          </div>
        )}
      </div>

      <Pagination
        currentPage={currentPage}
        totalPages={totalPages}
        totalRecords={totalRecords}
        pageSize={pageSize}
        onPageChange={handlePageChange}
        onPageSizeChange={handlePageSizeChange}
      />

      <DataTable columns={tenantColumns} data={tenants} loading={loading} emptyMessage="No tenants found" rowKey="Id" />

      <Modal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        title="Create Tenant"
        size="small"
        footer={
          <>
            <button className="btn btn-secondary" onClick={() => setShowCreateModal(false)} disabled={formLoading}>
              Cancel
            </button>
            <button className="btn btn-primary" onClick={createTenant} disabled={formLoading}>
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
        <form onSubmit={createTenant}>
          <div className="form-group">
            <label htmlFor="tenantName">Tenant Name</label>
            <input
              type="text"
              id="tenantName"
              value={newTenantName}
              onChange={(e) => setNewTenantName(e.target.value)}
              placeholder="Enter tenant name"
              disabled={formLoading}
              autoFocus
            />
          </div>
        </form>
      </Modal>
    </div>
  )
}
