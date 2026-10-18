// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { createRef } from 'react'
import type { ComponentProps } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AppShell } from './AppShell'
import { getDashboardCards } from '../dashboardCards'
import type { AppSection } from '../types'

beforeEach(() => {
  vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: false })))
})
afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function renderShell(activeSection: AppSection = 'gigs', overrides: Partial<ComponentProps<typeof AppShell>> = {}) {
  const navigationItems = (['clients', 'gigs', 'invoices', 'admin'] as const).map(id => ({
    id, label: id, eyebrow: 'Workspace', description: 'Long introductory description',
  }))
  const props: ComponentProps<typeof AppShell> = {
    activeSection,
    appMetadata: { title: 'Glovelly', commitId: null, buildTimestamp: null, deploymentName: null },
    authUser: null,
    children: null,
    currentSectionContent: <section>Workspace content</section>,
    dashboardCards: getDashboardCards({
      activeSection, clients: [], gigs: [], invoices: [], isWorkspaceLoading: false,
      paidIncomeSummary: { status: 'error' }, today: '2026-10-18',
    }),
    isAdmin: true,
    isAdminLoading: false,
    isGigLoading: false,
    isLoading: false,
    isProfileMenuOpen: false,
    isSellerProfileSaving: false,
    isUserSettingsSaving: false,
    navigationItems,
    pendingAccessRequestCount: 0,
    pendingGigImportCount: 0,
    onOpenAccessRequests: vi.fn(),
    onOpenGigImports: vi.fn(),
    onOpenSellerProfile: vi.fn(),
    onOpenConnectedServices: vi.fn(),
    onOpenUserSettings: vi.fn(),
    onProfileMenuToggle: vi.fn(),
    onUnifiedIntakeOpen: vi.fn(),
    onSectionChange: vi.fn(),
    onSignOut: vi.fn(),
    onThemePreferenceChange: vi.fn(),
    profileMenuRef: createRef(),
    sellerProfile: {
      id: null, sellerName: null, addressLine1: null, addressLine2: null,
      city: null, region: null, postcode: null, country: null, email: null, phone: null,
      accountName: null, sortCode: null, accountNumber: null, paymentReferenceNote: null,
      isConfigured: false, isInvoiceReady: false, missingFields: [],
    },
    themePreference: 'system',
    ...overrides,
  }
  render(<AppShell {...props} />)
  return props
}

describe('compact workspace shell', () => {
  it.each(['clients', 'gigs', 'invoices', 'admin'] as const)('retains summaries and shared controls in %s', section => {
    const props = renderShell(section)
    expect(document.querySelector('.workspace-title')).toBeNull()
    expect(screen.getByLabelText('Dashboard summary').querySelectorAll('article')).toHaveLength(3)
    for (const card of props.dashboardCards) {
      expect(screen.getByText(card.label)).toBeDefined()
    }
    const nav = screen.getByRole('navigation', { name: 'Primary' })
    expect(within(nav).getByRole('button', { name: section }).getAttribute('aria-current')).toBe('page')
    fireEvent.click(within(nav).getByRole('button', { name: 'clients' }))
    expect(props.onSectionChange).toHaveBeenCalledWith('clients')
    fireEvent.click(screen.getByRole('button', { name: 'Add to Glovelly' }))
    expect(props.onUnifiedIntakeOpen).toHaveBeenCalledOnce()
    fireEvent.click(screen.getByRole('button', { name: 'Open profile menu' }))
    expect(props.onProfileMenuToggle).toHaveBeenCalledOnce()
    expect(screen.queryByText('Your work, all in one place.')).toBeNull()
    expect(screen.queryByText('Long introductory description')).toBeNull()
  })

  it('preserves informational summary values, details, and states without action buttons', () => {
    renderShell('invoices', { dashboardCards: [
      { label: 'Outstanding balance', value: '£0.00', detail: 'No outstanding invoices', state: 'ready' },
      { label: 'Overdue invoices', value: 'Loading...', detail: 'Updating summary', state: 'loading' },
      { label: 'Income this financial year', value: 'Unavailable', detail: 'Try again later', state: 'error' },
    ] })
    expect(screen.getByText('£0.00')).toBeDefined()
    expect(screen.getByText('Loading...')).toBeDefined()
    expect(screen.getByText('Unavailable')).toBeDefined()
    expect(screen.getByText('Try again later')).toBeDefined()
    expect(within(screen.getByLabelText('Dashboard summary')).queryByRole('button')).toBeNull()
    expect(screen.queryByRole('button', { name: 'View invoices' })).toBeNull()
  })

  it('retains settings and account actions in the profile menu', () => {
    const props = renderShell('admin', { isProfileMenuOpen: true })
    fireEvent.click(screen.getByRole('menuitem', { name: 'Settings' }))
    expect(props.onOpenUserSettings).toHaveBeenCalledOnce()
    expect(screen.getByLabelText('Dashboard summary')).toBeDefined()
    expect(screen.getByRole('menuitem', { name: 'Sign out' })).toBeDefined()
  })
})
