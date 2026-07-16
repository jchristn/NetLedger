import React, { useState, useRef, useEffect, useCallback } from 'react'
import { createPortal } from 'react-dom'
import './ActionMenu.css'

// Global tracking for open menus to ensure only one is open at a time
let globalCloseHandler = null

export default function ActionMenu({ items }) {
  const [isOpen, setIsOpen] = useState(false)
  const [dropdownPosition, setDropdownPosition] = useState({ top: 0, left: 0 })
  const menuRef = useRef(null)
  const triggerRef = useRef(null)
  const dropdownRef = useRef(null)

  const closeMenu = useCallback(() => {
    setIsOpen(false)
  }, [])

  useEffect(() => {
    const handleClickOutside = (event) => {
      const clickedTrigger = menuRef.current && menuRef.current.contains(event.target)
      const clickedDropdown = dropdownRef.current && dropdownRef.current.contains(event.target)

      if (!clickedTrigger && !clickedDropdown) {
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
      const dropdownWidth = 180
      const dropdownMaxHeight = Math.min(320, window.innerHeight - 16)
      const maxLeft = Math.max(8, window.innerWidth - dropdownWidth - 8)
      const left = Math.min(
        Math.max(8, rect.right - dropdownWidth),
        maxLeft
      )
      const topBelow = rect.bottom + 4
      const topAbove = rect.top - dropdownMaxHeight - 4
      const top = topBelow + dropdownMaxHeight > window.innerHeight - 8
        ? Math.max(8, topAbove)
        : topBelow

      setDropdownPosition({
        top,
        left,
        maxHeight: dropdownMaxHeight
      })
    }
  }

  const handleItemClick = (event, item) => {
    event.stopPropagation()
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

  const handleTriggerClick = (event) => {
    event.stopPropagation()
    if (!isOpen) {
      updateDropdownPosition()
    }
    setIsOpen(!isOpen)
  }

  return (
    <div className="action-menu" ref={menuRef} data-ignore-row-click="true">
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

      {isOpen && createPortal(
        <>
          {/* Invisible overlay to block clicks to elements beneath */}
          <div
            className="action-menu-overlay"
            onClick={(event) => {
              event.stopPropagation()
              setIsOpen(false)
            }}
          />
          <div
            ref={dropdownRef}
            className="action-menu-dropdown animate-fade-in"
            style={{
              position: 'fixed',
              top: dropdownPosition.top,
              left: dropdownPosition.left,
              right: 'auto',
              maxHeight: dropdownPosition.maxHeight
            }}
            data-ignore-row-click="true"
          >
            {filteredItems.map((item, index) => {
              if (item.divider) {
                return <div key={index} className="action-menu-divider" />
              }

              return (
                <button
                  key={index}
                  className={`action-menu-item ${item.variant || ''} ${item.disabled ? 'disabled' : ''}`}
                  onClick={(event) => handleItemClick(event, item)}
                  disabled={item.disabled}
                >
                  {item.icon && <span className="action-menu-icon">{item.icon}</span>}
                  <span className="action-menu-label">{item.label}</span>
                </button>
              )
            })}
          </div>
        </>,
        document.body
      )}
    </div>
  )
}
