import React from 'react'
import './Pagination.css'

/**
 * Pagination component
 *
 * @param {Object} props
 * @param {number} props.currentPage - Current page (0-indexed)
 * @param {number} props.totalPages - Total number of pages
 * @param {number} props.totalRecords - Total number of records
 * @param {number} props.pageSize - Records per page
 * @param {Function} props.onPageChange - Called when page changes
 * @param {Function} props.onPageSizeChange - Called when page size changes
 * @param {Array} props.pageSizeOptions - Available page size options
 */
export default function Pagination({
  currentPage,
  totalPages,
  totalRecords,
  pageSize,
  onPageChange,
  onPageSizeChange,
  pageSizeOptions = [10, 25, 50, 100]
}) {
  const startRecord = totalRecords > 0 ? currentPage * pageSize + 1 : 0
  const endRecord = Math.min((currentPage + 1) * pageSize, totalRecords)

  const handlePageInput = (e) => {
    const value = e.target.value
    if (value === '') return

    const page = parseInt(value, 10) - 1
    if (!isNaN(page) && page >= 0 && page < totalPages) {
      onPageChange(page)
    }
  }

  const handlePageInputKeyDown = (e) => {
    if (e.key === 'Enter') {
      e.target.blur()
    }
  }

  return (
    <div className="pagination">
      <div className="pagination-info">
        <span className="pagination-records">
          {totalRecords > 0 ? (
            <>
              Showing <strong>{startRecord}</strong> - <strong>{endRecord}</strong> of <strong>{totalRecords}</strong>
            </>
          ) : (
            'No records'
          )}
        </span>
      </div>

      <div className="pagination-controls">
        <div className="pagination-page-size">
          <label htmlFor="pageSize">Rows:</label>
          <select
            id="pageSize"
            value={pageSize}
            onChange={(e) => onPageSizeChange(parseInt(e.target.value, 10))}
          >
            {pageSizeOptions.map(size => (
              <option key={size} value={size}>{size}</option>
            ))}
          </select>
        </div>

        <div className="pagination-nav">
          <button
            className="pagination-btn"
            onClick={() => onPageChange(0)}
            disabled={currentPage === 0}
            title="First page"
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <polyline points="11 17 6 12 11 7"/>
              <polyline points="18 17 13 12 18 7"/>
            </svg>
          </button>
          <button
            className="pagination-btn"
            onClick={() => onPageChange(currentPage - 1)}
            disabled={currentPage === 0}
            title="Previous page"
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <polyline points="15 18 9 12 15 6"/>
            </svg>
          </button>

          <div className="pagination-page-input">
            <input
              type="number"
              min="1"
              max={totalPages}
              value={currentPage + 1}
              onChange={handlePageInput}
              onKeyDown={handlePageInputKeyDown}
              disabled={totalPages <= 1}
            />
            <span>of {totalPages || 1}</span>
          </div>

          <button
            className="pagination-btn"
            onClick={() => onPageChange(currentPage + 1)}
            disabled={currentPage >= totalPages - 1}
            title="Next page"
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <polyline points="9 18 15 12 9 6"/>
            </svg>
          </button>
          <button
            className="pagination-btn"
            onClick={() => onPageChange(totalPages - 1)}
            disabled={currentPage >= totalPages - 1}
            title="Last page"
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <polyline points="13 17 18 12 13 7"/>
              <polyline points="6 17 11 12 6 7"/>
            </svg>
          </button>
        </div>
      </div>
    </div>
  )
}
