import React, { useState, useMemo, useCallback } from 'react'
import ActionMenu from './ActionMenu'
import { RecordModal, ViewMetadataModal } from './Modal'
import './DataTable.css'

/**
 * Reusable DataTable component with sorting and filtering
 *
 * @param {Object} props
 * @param {Array} props.columns - Column configuration
 * @param {Array} props.data - Data to display
 * @param {boolean} props.loading - Loading state
 * @param {string} props.emptyMessage - Message when no data
 * @param {Function} props.onRowClick - Optional row click handler
 * @param {string} props.rowKey - Key to use for row identification (default: 'guid')
 */
export default function DataTable({
  columns,
  data,
  loading = false,
  emptyMessage = 'No data available',
  onRowClick,
  onEdit,
  onView,
  onViewJson,
  onDelete,
  actions,
  rowKey = 'guid'
}) {
  const [sortColumn, setSortColumn] = useState(null)
  const [sortDirection, setSortDirection] = useState('asc')
  const [filters, setFilters] = useState({})
  const [editRow, setEditRow] = useState(null)
  const [viewRow, setViewRow] = useState(null)
  const [jsonRow, setJsonRow] = useState(null)

  const getRecordTitle = (row) => {
    if (!row) return 'Record'

    return row.name || row.Name || row.email || row.Email || row.id || row.Id || row.guid || row.Guid || 'Record'
  }

  const openEditRow = useCallback((row) => {
    if (onEdit) {
      onEdit(row)
      return
    }

    setEditRow(row)
  }, [onEdit])

  const openViewRow = useCallback((row) => {
    if (onView) {
      onView(row)
      return
    }

    setViewRow(row)
  }, [onView])

  const openJsonRow = useCallback((row) => {
    if (onViewJson) {
      onViewJson(row)
      return
    }

    setJsonRow(row)
  }, [onViewJson])

  const hasActionColumn = columns.some(column => column.key === 'actions' || column.key === '__actions')

  const tableColumns = useMemo(() => {
    if (hasActionColumn) {
      return columns
    }

    return [
      ...columns,
      {
        key: '__actions',
        label: '',
        className: 'col-actions',
        sortable: false,
        render: (row) => {
          const rowActions = actions ? actions(row) : []
          const customActions = Array.isArray(rowActions) ? rowActions.filter(Boolean) : []
          const defaultActions = [
            { label: 'Edit', onClick: () => openEditRow(row) },
            { label: 'View', onClick: () => openViewRow(row) },
            { label: 'View JSON', onClick: () => openJsonRow(row) },
            { divider: true },
            {
              label: 'Delete',
              variant: 'danger',
              disabled: !onDelete,
              onClick: () => onDelete?.(row)
            }
          ]

          return (
            <ActionMenu
              items={customActions.length > 0 ? [...defaultActions, { divider: true }, ...customActions] : defaultActions}
            />
          )
        }
      }
    ]
  }, [actions, columns, hasActionColumn, onDelete, openEditRow, openViewRow, openJsonRow])

  const isColumnSortable = (column) => {
    return column.sortable !== false && column.label !== '' && column.key !== 'actions' && column.key !== '__actions'
  }

  // Handle column header click for sorting
  const handleSort = (columnKey) => {
    if (sortColumn === columnKey) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc')
    } else {
      setSortColumn(columnKey)
      setSortDirection('asc')
    }
  }

  // Handle filter change
  const handleFilterChange = (columnKey, value) => {
    setFilters(prev => ({
      ...prev,
      [columnKey]: value
    }))
  }

  // Filter and sort data
  const processedData = useMemo(() => {
    let result = [...(data || [])]

    // Apply filters
    Object.entries(filters).forEach(([key, value]) => {
      if (value) {
        const column = tableColumns.find(c => c.key === key)
        result = result.filter(row => {
          const cellValue = column?.filterValue
            ? column.filterValue(row)
            : String(row[key] ?? '')
          // Use exact match for columns with filterExact: true
          if (column?.filterExact) {
            return cellValue === value
          }
          return cellValue.toLowerCase().includes(value.toLowerCase())
        })
      }
    })

    // Apply sorting
    if (sortColumn) {
      const column = tableColumns.find(c => c.key === sortColumn)
      result.sort((a, b) => {
        const aValue = column?.sortValue ? column.sortValue(a) : a[sortColumn]
        const bValue = column?.sortValue ? column.sortValue(b) : b[sortColumn]

        if (aValue === null || aValue === undefined) return 1
        if (bValue === null || bValue === undefined) return -1

        let comparison = 0
        if (typeof aValue === 'string' || typeof bValue === 'string') {
          comparison = String(aValue).localeCompare(String(bValue))
        } else {
          comparison = aValue < bValue ? -1 : aValue > bValue ? 1 : 0
        }

        return sortDirection === 'desc' ? -comparison : comparison
      })
    }

    return result
  }, [data, filters, sortColumn, sortDirection, tableColumns])

  // Check if any column has filtering enabled
  const hasFilters = tableColumns.some(col => col.filterable)

  const handleRowClick = (event, row) => {
    if (
      event.target.closest('button, a, input, select, textarea, [data-ignore-row-click="true"]')
    ) {
      return
    }

    if (onRowClick) {
      onRowClick(row)
      return
    }

    openEditRow(row)
  }

  return (
    <>
      <div className="data-table-container">
        <table className="data-table">
          <thead>
            <tr>
              {tableColumns.map(column => {
                const columnSortable = isColumnSortable(column)

                return (
                  <th
                    key={column.key}
                    className={`
                      ${columnSortable ? 'sortable' : ''}
                      ${sortColumn === column.key ? `sorted-${sortDirection}` : ''}
                      ${column.className || ''}
                    `}
                    style={column.width ? { width: column.width } : undefined}
                    onClick={columnSortable ? () => handleSort(column.key) : undefined}
                  >
                    <div className="th-content">
                      <span>{column.label}</span>
                      {columnSortable && (
                        <span className="sort-icon">
                          {sortColumn === column.key ? (
                            sortDirection === 'asc' ? (
                              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                                <path d="M18 15l-6-6-6 6"/>
                              </svg>
                            ) : (
                              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                                <path d="M6 9l6 6 6-6"/>
                              </svg>
                            )
                          ) : (
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" opacity="0.3">
                              <path d="M8 9l4-4 4 4M16 15l-4 4-4-4"/>
                            </svg>
                          )}
                        </span>
                      )}
                    </div>
                  </th>
                )
              })}
            </tr>
            {hasFilters && (
              <tr className="filter-row">
                {tableColumns.map(column => (
                  <th key={`filter-${column.key}`} className="filter-cell">
                    {column.filterable && (
                      <input
                        type="text"
                        className="filter-input"
                        placeholder="Filter..."
                        value={filters[column.key] || ''}
                        onChange={(e) => handleFilterChange(column.key, e.target.value)}
                      />
                    )}
                  </th>
                ))}
              </tr>
            )}
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={tableColumns.length} className="loading-cell">
                  <div className="page-loading">
                    <span className="spinner"></span>
                    <span>Loading...</span>
                  </div>
                </td>
              </tr>
            ) : processedData.length === 0 ? (
              <tr>
                <td colSpan={tableColumns.length} className="empty-cell">
                  <div className="empty-state">
                    <span className="empty-state-title">{emptyMessage}</span>
                  </div>
                </td>
              </tr>
            ) : (
              processedData.map((row, index) => (
                <tr
                  key={row[rowKey] || index}
                  className="clickable"
                  onClick={(event) => handleRowClick(event, row)}
                >
                  {tableColumns.map(column => (
                    <td
                      key={column.key}
                      className={column.className || ''}
                    >
                      {column.render ? column.render(row) : row[column.key]}
                    </td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <RecordModal
        isOpen={Boolean(editRow)}
        onClose={() => setEditRow(null)}
        title={`Edit ${getRecordTitle(editRow)}`}
        data={editRow}
        mode="edit"
      />

      <RecordModal
        isOpen={Boolean(viewRow)}
        onClose={() => setViewRow(null)}
        title={`View ${getRecordTitle(viewRow)}`}
        data={viewRow}
      />

      <ViewMetadataModal
        isOpen={Boolean(jsonRow)}
        onClose={() => setJsonRow(null)}
        title={`View JSON: ${getRecordTitle(jsonRow)}`}
        data={jsonRow}
      />
    </>
  )
}
