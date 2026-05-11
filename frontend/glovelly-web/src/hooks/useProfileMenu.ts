import { useCallback, useEffect, useRef, useState } from 'react'

export function useProfileMenu() {
  const [isProfileMenuOpen, setIsProfileMenuOpen] = useState(false)
  const profileMenuRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    if (!isProfileMenuOpen) {
      return
    }

    const handlePointerDown = (event: MouseEvent) => {
      if (!profileMenuRef.current?.contains(event.target as Node)) {
        setIsProfileMenuOpen(false)
      }
    }

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsProfileMenuOpen(false)
      }
    }

    document.addEventListener('mousedown', handlePointerDown)
    document.addEventListener('keydown', handleEscape)

    return () => {
      document.removeEventListener('mousedown', handlePointerDown)
      document.removeEventListener('keydown', handleEscape)
    }
  }, [isProfileMenuOpen])

  const closeProfileMenu = useCallback(() => {
    setIsProfileMenuOpen(false)
  }, [])

  const toggleProfileMenu = useCallback(() => {
    setIsProfileMenuOpen((current) => !current)
  }, [])

  return {
    closeProfileMenu,
    isProfileMenuOpen,
    profileMenuRef,
    toggleProfileMenu,
  }
}
