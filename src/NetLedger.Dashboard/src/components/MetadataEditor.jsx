import React from 'react'
import { normalizeLabelRows, normalizeTagRows } from './metadataEditorUtils'
import './MetadataEditor.css'

function DeleteIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
      <polyline points="3 6 5 6 21 6"/>
      <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>
    </svg>
  )
}

function PlusIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
      <line x1="12" y1="5" x2="12" y2="19"/>
      <line x1="5" y1="12" x2="19" y2="12"/>
    </svg>
  )
}

function IconButton({ title, disabled, onClick, children }) {
  return (
    <button
      type="button"
      className="metadata-icon-button"
      onClick={onClick}
      disabled={disabled}
      title={title}
      aria-label={title}
    >
      {children}
    </button>
  )
}

export function MetadataLabelsEditor({ idPrefix = 'metadata-label', value, onChange, disabled = false }) {
  const labels = normalizeLabelRows(value)

  const updateLabel = (index, nextValue) => {
    onChange(labels.map((label, labelIndex) => labelIndex === index ? nextValue : label))
  }

  const deleteLabel = (index) => {
    const nextLabels = labels.filter((_, labelIndex) => labelIndex !== index)
    onChange(nextLabels.length > 0 ? nextLabels : [''])
  }

  const addLabel = () => {
    onChange([...labels, ''])
  }

  return (
    <div className="metadata-editor">
      {labels.map((label, index) => {
        const isLast = index === labels.length - 1
        return (
          <div className="metadata-row metadata-label-row" key={`${idPrefix}-${index}`}>
            <input
              type="text"
              id={`${idPrefix}-${index}`}
              value={label}
              onChange={(event) => updateLabel(index, event.target.value)}
              placeholder={index === 0 ? 'operating' : 'Label'}
              disabled={disabled}
            />
            <div className="metadata-actions">
              <IconButton title="Delete label" disabled={disabled} onClick={() => deleteLabel(index)}>
                <DeleteIcon />
              </IconButton>
              {isLast && (
                <IconButton title="Add label" disabled={disabled} onClick={addLabel}>
                  <PlusIcon />
                </IconButton>
              )}
            </div>
          </div>
        )
      })}
    </div>
  )
}

export function MetadataTagsEditor({ idPrefix = 'metadata-tag', value, onChange, disabled = false }) {
  const tags = normalizeTagRows(value)

  const updateTag = (index, field, nextValue) => {
    onChange(tags.map((tag, tagIndex) => tagIndex === index ? { ...tag, [field]: nextValue } : tag))
  }

  const deleteTag = (index) => {
    const nextTags = tags.filter((_, tagIndex) => tagIndex !== index)
    onChange(nextTags.length > 0 ? nextTags : [{ key: '', value: '' }])
  }

  const addTag = () => {
    onChange([...tags, { key: '', value: '' }])
  }

  return (
    <div className="metadata-editor">
      {tags.map((tag, index) => {
        const isLast = index === tags.length - 1
        return (
          <div className="metadata-row metadata-tag-row" key={`${idPrefix}-${index}`}>
            <input
              type="text"
              id={`${idPrefix}-key-${index}`}
              value={tag.key}
              onChange={(event) => updateTag(index, 'key', event.target.value)}
              placeholder="Key"
              disabled={disabled}
            />
            <input
              type="text"
              id={`${idPrefix}-value-${index}`}
              value={tag.value}
              onChange={(event) => updateTag(index, 'value', event.target.value)}
              placeholder="Value"
              disabled={disabled}
            />
            <div className="metadata-actions">
              <IconButton title="Delete tag" disabled={disabled} onClick={() => deleteTag(index)}>
                <DeleteIcon />
              </IconButton>
              {isLast && (
                <IconButton title="Add tag" disabled={disabled} onClick={addTag}>
                  <PlusIcon />
                </IconButton>
              )}
            </div>
          </div>
        )
      })}
    </div>
  )
}
