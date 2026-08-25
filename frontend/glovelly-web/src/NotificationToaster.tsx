import { useEffect, useState } from 'react'
import { Toaster } from 'sonner'

export function NotificationToaster() {
  const [isDesktop, setIsDesktop] = useState(() => window.matchMedia('(min-width: 601px)').matches)

  useEffect(() => {
    const mediaQuery = window.matchMedia('(min-width: 601px)')
    const updatePosition = () => setIsDesktop(mediaQuery.matches)

    mediaQuery.addEventListener('change', updatePosition)
    return () => mediaQuery.removeEventListener('change', updatePosition)
  }, [])

  return (
    <Toaster
      closeButton
      containerAriaLabel="Notifications"
      hotkey={[]}
      mobileOffset={{ left: 'max(10px, env(safe-area-inset-left))', right: 'max(10px, env(safe-area-inset-right))', top: 'max(74px, calc(env(safe-area-inset-top) + 64px))' }}
      offset={{ right: '24px', top: '24px' }}
      position={isDesktop ? 'top-right' : 'top-center'}
      richColors
      style={{ zIndex: 75 }}
      toastOptions={{ closeButtonAriaLabel: 'Dismiss notification' }}
      visibleToasts={3}
    />
  )
}
