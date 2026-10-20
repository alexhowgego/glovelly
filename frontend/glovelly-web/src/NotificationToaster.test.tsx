// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { NotificationToaster } from './NotificationToaster'

const toaster = vi.hoisted(() => vi.fn())
vi.mock('sonner', () => ({ Toaster: (props: unknown) => { toaster(props); return null } }))
afterEach(() => { cleanup(); vi.unstubAllGlobals(); vi.clearAllMocks() })

describe('notification placement', () => {
  it.each([true, false])('retains a layer below modals even when there are no notifications (desktop: %s)', desktop => {
    vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: desktop, addEventListener() {}, removeEventListener() {} })))
    render(<NotificationToaster />)
    expect(screen.getByTestId('notification-layer').style.zIndex).toBe('75')
    const props = toaster.mock.calls[0][0]
    expect(props.position).toBe(desktop ? 'top-right' : 'top-center')
    expect(props.offset).toEqual({ right: '24px', top: '24px' })
    expect(props.mobileOffset.top).toContain('74px')
    expect(props.closeButton).toBe(true)
  })
})
