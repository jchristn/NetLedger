import React, { useEffect, useCallback, useMemo, useState } from 'react'
import CopyButton from './CopyButton'
import { HiddenValueDisplay } from './HiddenValue'
import { MetadataLabelsEditor, MetadataTagsEditor } from './MetadataEditor'
import { formatDate } from '../api/api'
import {
  isLabelsField,
  isTagsField,
  labelsToPayload,
  normalizeLabelRows,
  normalizeTagRows,
  tagsToPayload
} from './metadataEditorUtils'
import './Modal.css'

export default function Modal({
  isOpen,
  onClose,
  title,
  children,
  footer,
  size = 'medium',
  closeOnOverlay = true,
  showCloseButton = true
}) {
  const handleKeyDown = useCallback((e) => {
    if (e.key === 'Escape' && onClose) {
      onClose()
    }
  }, [onClose])

  useEffect(() => {
    if (isOpen) {
      document.addEventListener('keydown', handleKeyDown)
      document.body.style.overflow = 'hidden'
    }

    return () => {
      document.removeEventListener('keydown', handleKeyDown)
      document.body.style.overflow = ''
    }
  }, [isOpen, handleKeyDown])

  if (!isOpen) return null

  const handleOverlayClick = (e) => {
    if (closeOnOverlay && e.target === e.currentTarget) {
      onClose?.()
    }
  }

  return (
    <div className="modal-overlay" onClick={handleOverlayClick}>
      <div className={`modal modal-${size} animate-slide-up`}>
        <div className="modal-header">
          <h2 className="modal-title">{title}</h2>
          {showCloseButton && (
            <button className="modal-close" onClick={onClose} title="Close">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <line x1="18" y1="6" x2="6" y2="18"/>
                <line x1="6" y1="6" x2="18" y2="18"/>
              </svg>
            </button>
          )}
        </div>

        <div className="modal-body">
          {children}
        </div>

        {footer && (
          <div className="modal-footer">
            {footer}
          </div>
        )}
      </div>
    </div>
  )
}

// Confirmation dialog helper
export function ConfirmModal({
  isOpen,
  onClose,
  onConfirm,
  title = 'Confirm',
  message,
  confirmText = 'Confirm',
  cancelText = 'Cancel',
  variant = 'danger',
  isLoading = false
}) {
  const handleConfirm = () => {
    onConfirm?.()
  }

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={title}
      size="small"
      footer={
        <>
          <button
            className="btn btn-secondary"
            onClick={onClose}
            disabled={isLoading}
          >
            {cancelText}
          </button>
          <button
            className={`btn btn-${variant}`}
            onClick={handleConfirm}
            disabled={isLoading}
          >
            {isLoading ? (
              <>
                <span className="spinner spinner-sm"></span>
                Loading...
              </>
            ) : (
              confirmText
            )}
          </button>
        </>
      }
    >
      <p className="confirm-message">{message}</p>
    </Modal>
  )
}

const FIELD_LABELS = {
  GUID: 'ID',
  Guid: 'ID',
  guid: 'ID',
  Id: 'ID',
  id: 'ID',
  TenantId: 'Tenant ID',
  tenantId: 'Tenant ID',
  UserId: 'User ID',
  userId: 'User ID',
  AccountId: 'Account ID',
  accountId: 'Account ID',
  ApiKeyGuid: 'Credential ID',
  apiKeyGuid: 'Credential ID',
  IsAdmin: 'System Administrator',
  isAdmin: 'System Administrator',
  IsTenantAdmin: 'Tenant Administrator',
  isTenantAdmin: 'Tenant Administrator',
  IsProtected: 'Protected',
  isProtected: 'Protected',
  IsCommitted: 'Committed',
  isCommitted: 'Committed',
  CreatedUtc: 'Created',
  createdUtc: 'Created',
  LastUpdateUtc: 'Last Updated',
  lastUpdateUtc: 'Last Updated',
  LastUsedUtc: 'Last Used',
  lastUsedUtc: 'Last Used',
  ExpiresUtc: 'Expires',
  expiresUtc: 'Expires',
  SecretKeyLast4: 'Secret Key Last 4',
  secretKeyLast4: 'Secret Key Last 4',
  RawSecretKey: 'Secret Key',
  rawSecretKey: 'Secret Key',
  AccessKey: 'Access Key',
  accessKey: 'Access Key',
  AuthMode: 'Authentication Mode',
  authMode: 'Authentication Mode',
  PasswordSha256: 'Password Hash',
  passwordSha256: 'Password Hash'
}

const ID_FIELD_NAMES = new Set([
  'GUID',
  'Guid',
  'guid',
  'Id',
  'id',
  'TenantId',
  'tenantId',
  'UserId',
  'userId',
  'AccountId',
  'accountId',
  'CredentialId',
  'credentialId',
  'ApiKeyGuid',
  'apiKeyGuid'
])

const TIMESTAMP_FIELD_NAMES = new Set([
  'Created',
  'created',
  'CreatedUtc',
  'createdUtc',
  'LastUpdate',
  'lastUpdate',
  'LastUpdateUtc',
  'lastUpdateUtc',
  'Updated',
  'updated',
  'UpdatedUtc',
  'updatedUtc',
  'Modified',
  'modified',
  'ModifiedUtc',
  'modifiedUtc',
  'LastModified',
  'lastModified',
  'LastModifiedUtc',
  'lastModifiedUtc',
  'LastUsed',
  'lastUsed',
  'LastUsedUtc',
  'lastUsedUtc',
  'Expires',
  'expires',
  'ExpiresUtc',
  'expiresUtc',
  'Committed',
  'committed',
  'CommittedUtc',
  'committedUtc'
])

const humanizeFieldName = (key) => {
  if (FIELD_LABELS[key]) return FIELD_LABELS[key]
  return key
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/[_-]+/g, ' ')
    .replace(/\bUtc\b/g, '')
    .replace(/\bId\b/g, 'ID')
    .trim()
}

const formatRecordValue = (value) => {
  if (value === null || value === undefined) return ''
  if (typeof value === 'object') return JSON.stringify(value)
  return String(value)
}

const isTimestampField = (key) => TIMESTAMP_FIELD_NAMES.has(key)

const isSensitiveField = (key) => {
  const normalized = String(key || '').toLowerCase()
  return normalized === 'key' ||
    normalized === 'accesskey' ||
    normalized === 'apikey' ||
    normalized.endsWith('key') ||
    normalized.includes('password') ||
    normalized.includes('secret') ||
    normalized.includes('token')
}

const isReadOnlyRecordField = (key) => {
  return ID_FIELD_NAMES.has(key) || isTimestampField(key) || isSensitiveField(key)
}

const formatDisplayValue = (key, value) => {
  if (isTimestampField(key) && value) {
    const date = new Date(value)
    if (!Number.isNaN(date.getTime())) {
      return formatDate(value)
    }
  }

  if (typeof value === 'boolean') return value ? 'Yes' : 'No'
  return formatRecordValue(value) || '-'
}

const prepareEditableRecord = (record) => {
  return Object.entries(record || {}).reduce((fields, [key, value]) => {
    if (isLabelsField(key)) {
      fields[key] = normalizeLabelRows(value)
    } else if (isTagsField(key)) {
      fields[key] = normalizeTagRows(value)
    } else {
      fields[key] = value
    }

    return fields
  }, {})
}

const serializeEditableRecord = (record) => {
  return Object.entries(record || {}).reduce((fields, [key, value]) => {
    if (isLabelsField(key)) {
      fields[key] = labelsToPayload(value)
    } else if (isTagsField(key)) {
      fields[key] = tagsToPayload(value)
    } else {
      fields[key] = value
    }

    return fields
  }, {})
}

function ReadOnlyLabels({ value }) {
  const labels = labelsToPayload(value)

  if (labels.length === 0) {
    return <span className="record-empty-value">None</span>
  }

  return (
    <div className="record-label-badges">
      {labels.map((label, index) => (
        <span className="record-label-badge" key={`${label}-${index}`}>{label}</span>
      ))}
    </div>
  )
}

function ReadOnlyTags({ value }) {
  const tags = normalizeTagRows(value)
    .map(tag => ({ key: tag.key.trim(), value: tag.value }))
    .filter(tag => tag.key)

  if (tags.length === 0) {
    return <span className="record-empty-value">None</span>
  }

  return (
    <dl className="record-tag-list">
      {tags.map((tag, index) => (
        <div className="record-tag-item" key={`${tag.key}-${index}`}>
          <dt>{tag.key}</dt>
          <dd>{tag.value || <span className="record-empty-value">Empty</span>}</dd>
        </div>
      ))}
    </dl>
  )
}

function ReadOnlyRecordValue({ fieldKey, value, isIdentifier = false }) {
  const displayValue = formatDisplayValue(fieldKey, value)

  if (isSensitiveField(fieldKey)) {
    return (
      <HiddenValueDisplay
        value={formatRecordValue(value)}
        visiblePrefix={fieldKey.toLowerCase() === 'key' || fieldKey.toLowerCase() === 'accesskey' ? 4 : 0}
        valueClassName="record-hidden-value"
      />
    )
  }

  return (
    <>
      <span>{displayValue}</span>
      {isIdentifier && displayValue && displayValue !== '-' && <CopyButton text={displayValue} title={`Copy ${humanizeFieldName(fieldKey)}`} />}
    </>
  )
}

export function RecordModal({ isOpen, onClose, title, data, mode = 'view', onSave = null }) {
  const [formData, setFormData] = useState({})

  useEffect(() => {
    if (isOpen && data) {
      setFormData(mode === 'edit' ? prepareEditableRecord(data) : data)
    }
  }, [data, isOpen, mode])

  if (!isOpen || !data) return null

  const readOnly = mode === 'view'
  const recordEntries = Object.entries(formData)
  const editableEntries = readOnly ? recordEntries : recordEntries.filter(([key]) => !isReadOnlyRecordField(key))
  const readOnlyEntries = readOnly ? [] : recordEntries.filter(([key]) => isReadOnlyRecordField(key))

  const handleFieldChange = (key, value) => {
    setFormData((current) => ({
      ...current,
      [key]: value
    }))
  }

  const handleSave = () => {
    if (onSave) {
      onSave(serializeEditableRecord(formData))
      return
    }

    onClose?.()
  }

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={title}
      size="large"
      footer={
        readOnly ? (
          <button className="btn btn-secondary" onClick={onClose}>
            Close
          </button>
        ) : (
          <>
            <button className="btn btn-secondary" onClick={onClose}>
              Cancel
            </button>
            <button className="btn btn-primary" onClick={handleSave}>
              Save
            </button>
          </>
        )
      }
    >
      <div className="record-fields">
        {editableEntries.map(([key, value]) => {
          const isBoolean = typeof value === 'boolean'
          const isIdentifier = ID_FIELD_NAMES.has(key)
          const isTimestamp = isTimestampField(key)
          const isLabels = isLabelsField(key)
          const isTags = isTagsField(key)
          const formattedValue = formatRecordValue(value)

          return (
            <label className="record-field" key={key}>
              <span>{humanizeFieldName(key)}</span>
              {readOnly && isLabels ? (
                <div className="record-field-value record-metadata-value">
                  <ReadOnlyLabels value={value} />
                </div>
              ) : readOnly && isTags ? (
                <div className="record-field-value record-metadata-value">
                  <ReadOnlyTags value={value} />
                </div>
              ) : readOnly ? (
                <div className={`record-field-value ${isIdentifier ? 'record-field-id-value' : ''} ${isTimestamp ? 'record-timestamp-value' : ''}`}>
                  <ReadOnlyRecordValue fieldKey={key} value={value} isIdentifier={isIdentifier} />
                </div>
              ) : isBoolean ? (
                <label className="record-checkbox-field">
                  <input
                    type="checkbox"
                    checked={value}
                    onChange={(event) => handleFieldChange(key, event.target.checked)}
                  />
                  <span>{value ? 'Yes' : 'No'}</span>
                </label>
              ) : isLabels ? (
                <MetadataLabelsEditor
                  idPrefix={`record-${key}`}
                  value={value}
                  onChange={(labels) => handleFieldChange(key, labels)}
                />
              ) : isTags ? (
                <MetadataTagsEditor
                  idPrefix={`record-${key}`}
                  value={value}
                  onChange={(tags) => handleFieldChange(key, tags)}
                />
              ) : (
                <div className={`record-input-row ${isIdentifier ? 'record-field-id-value' : ''}`}>
                  <input
                    className="form-input"
                    value={formattedValue}
                    onChange={(event) => handleFieldChange(key, event.target.value)}
                  />
                  {isIdentifier && formattedValue && <CopyButton text={formattedValue} title={`Copy ${humanizeFieldName(key)}`} />}
                </div>
              )}
            </label>
          )
        })}
      </div>
      {!readOnly && readOnlyEntries.length > 0 && (
        <div className="record-readonly-summary">
          {readOnlyEntries.map(([key, value]) => {
            const isIdentifier = ID_FIELD_NAMES.has(key)
            const isTimestamp = isTimestampField(key)
            return (
              <div className="record-readonly-item" key={key}>
                <span className="record-readonly-label">{humanizeFieldName(key)}</span>
                <div className={`record-readonly-value ${isIdentifier ? 'record-field-id-value' : ''} ${isTimestamp ? 'record-timestamp-value' : ''}`}>
                  <ReadOnlyRecordValue fieldKey={key} value={value} isIdentifier={isIdentifier} />
                </div>
              </div>
            )
          })}
        </div>
      )}
    </Modal>
  )
}

// View JSON metadata modal
export function ViewMetadataModal({ isOpen, onClose, title, data }) {
  const jsonString = useMemo(() => {
    return data ? JSON.stringify(data, null, 2) : ''
  }, [data])

  // Don't render anything if not open or no data
  if (!isOpen || !data) return null

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={
        <div className="metadata-header">
          <span>{title || 'View Metadata'}</span>
          <CopyButton text={jsonString} title="Copy to clipboard" size={16} className="metadata-copy-btn-header" />
        </div>
      }
      size="large"
    >
      <div className="metadata-container">
        <pre className="metadata-json">{jsonString}</pre>
      </div>
    </Modal>
  )
}
