import React, { useState, useRef, useEffect, useCallback } from 'react'
import './ActionMenu.css'

// Global tracking for open menus to ensure only one is open at a time
let globalCloseHandler = null

export default function ActionMenu({ items }) {
  const [isOpen, setIsOpen] = useState(false)
  const [dropdownPosition, setDropdownPosition] = useState({ top: 0, left: 0 })
  const menuRef = useRef(null)
  const triggerRef = useRef(null)

  const closeMenu = useCallback(() => {
    setIsOpen(false)
  }, [])

  useEffect(() => {
    const handleClickOutside = (event) => {
      if (menuRef.current && !menuRef.current.contains(event.target)) {
        setIsOpen(false)
      }
    }

    const handleScroll = () => {
      if (isOpen) {
        setIsOpen(false)
      }
    }

    if (isOpen) {
      // Close any other open menu
      if (globalCloseHandler && globalCloseHandler !== closeMenu) {
        globalCloseHandler()
      }
      globalCloseHandler = closeMenu

      document.addEventListener('mousedown', handleClickOutside)
      window.addEventListener('scroll', handleScroll, true)
    } else if (globalCloseHandler === closeMenu) {
      globalCloseHandler = null
    }

    return () => {
      document.removeEventListener('mousedown', handleClickOutside)
      window.removeEventListener('scroll', handleScroll, true)
      if (globalCloseHandler === closeMenu) {
        globalCloseHandler = null
      }
    }
  }, [isOpen, closeMenu])

  const updateDropdownPosition = () => {
    if (triggerRef.current) {
      const rect = triggerRef.current.getBoundingClientRect()
      setDropdownPosition({
        top: rect.bottom + 4,
        left: rect.right - 160 // 160px is the min-width of the dropdown
      })
    }
  }

  const handleItemClick = (item) => {
    if (item.onClick && !item.disabled) {
      item.onClick()
    }
    setIsOpen(false)
  }

  // Filter out null/undefined items
  const filteredItems = items.filter(Boolean)

  if (filteredItems.length === 0) {
    return null
  }

  const handleTriggerClick = () => {
    if (!isOpen) {
      updateDropdownPosition()
    }
    setIsOpen(!isOpen)
  }

  return (
    <div className="action-menu" ref={menuRef}>
      <button
        ref={triggerRef}
        className="action-menu-trigger"
        onClick={handleTriggerClick}
        title="Actions"
      >
        <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
          <circle cx="12" cy="5" r="2"/>
          <circle cx="12" cy="12" r="2"/>
          <circle cx="12" cy="19" r="2"/>
        </svg>
      </button>

      {isOpen && (
        <>
          {/* Invisible overlay to block clicks to elements beneath */}
          <div
            className="action-menu-overlay"
            onClick={() => setIsOpen(false)}
          />
          <div
            className="action-menu-dropdown animate-fade-in"
            style={{
              position: 'fixed',
              top: dropdownPosition.top,
              left: dropdownPosition.left,
              right: 'auto'
            }}
          >
            {filteredItems.map((item, index) => {
              if (item.divider) {
                return <div key={index} className="action-menu-divider" />
              }

              return (
                <button
                  key={index}
                  className={`action-menu-item ${item.variant || ''} ${item.disabled ? 'disabled' : ''}`}
                  onClick={() => handleItemClick(item)}
                  disabled={item.disabled}
                >
                  {item.icon && <span className="action-menu-icon">{item.icon}</span>}
                  <span className="action-menu-label">{item.label}</span>
                </button>
              )
            })}
          </div>
        </>
      )}
    </div>
  )
}
