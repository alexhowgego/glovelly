// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { createRef } from 'react'
import type { ComponentProps } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AdminSection } from './AdminSection'
import { ClientsSection } from './ClientsSection'
import { GigsSection } from './GigsSection'
import { InvoicesSection } from './InvoicesSection'
import { emptyAdminForm, emptyForm, emptyGigForm, emptyGigExternalResourceForm } from '../forms'
import type { AdminUser, Client, Gig, Invoice } from '../types'
import { toEditableGigExpenses } from '../hooks/gigWorkspaceHelpers'

const overviewClient: Client = {
  id: 'client', name: 'Long client identity', email: 'client@example.com',
  billingAddress: { line1: 'Full address '.repeat(30), line2: '', city: 'Bristol', stateOrCounty: '', postalCode: 'BS1 1AA', country: 'United Kingdom' },
  mileageRate: null, passengerMileageRate: null, defaultPaymentWindowDays: null, invoiceFilenamePattern: null, invoiceEmailSubjectPattern: null,
}
const overviewGig: Gig = {
  id: 'gig', clientId: overviewClient.id, invoiceId: null, sourceImportBatchId: null, sourceImportDraftId: null,
  title: 'Concert', date: '2099-01-01', venue: 'Hall', fee: 100, travelMiles: 0, passengerCount: null,
  notes: 'Complete long notes '.repeat(100), wasDriving: false, type: 'Performance', status: 'Confirmed',
  invoicedAt: null, isInvoiced: false, expenses: [], externalResources: [],
}
const invoice: Invoice = {
  id: 'invoice', invoiceNumber: 'GLV-001', clientId: overviewClient.id,
  invoiceDate: '2026-10-01', dueDate: '2026-10-15', status: 'Draft', paidOn: null, lines: [], total: 100,
  description: 'Complete invoice description '.repeat(100), deliveryCount: 0,
  lastDeliveryChannel: null, lastDeliveryRecipient: null, lastDeliveredUtc: null, lastDeliveredByUserId: null,
  pdfStorageKey: null, pdfFileName: null, pdfContentType: null, pdfSizeBytes: null, pdfGeneratedAt: null,
  documentState: 'Missing', documentRevision: 1, pdfDocumentRevision: null, documentFailureMessage: null,
  firstIssuedUtc: null, firstIssuedByUserId: null, reissueCount: 0, lastReissuedUtc: null, lastReissuedByUserId: null,
}
const user: AdminUser = {
  id: 'user', email: 'user@example.com', displayName: 'Musician', googleSubject: 'Full identity '.repeat(30),
  isEnrolled: true, role: 'User', isActive: true, createdUtc: '2026-01-01T00:00:00Z', lastLoginUtc: null,
}

beforeEach(() => {
  vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: false })))
  Object.defineProperties(HTMLDialogElement.prototype, {
    showModal: { configurable: true, value: function (this: HTMLDialogElement) { this.setAttribute('open', '') } },
    close: { configurable: true, value: function (this: HTMLDialogElement) { this.removeAttribute('open') } },
  })
})
afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
  Reflect.deleteProperty(HTMLDialogElement.prototype, 'showModal')
  Reflect.deleteProperty(HTMLDialogElement.prototype, 'close')
})

function gigOverviewProps(): ComponentProps<typeof GigsSection> {
  return {
    clientNamesById: new Map([[overviewClient.id, overviewClient.name]]), clients: [overviewClient], completedGigCount: 0,
    filteredGigs: [overviewGig], externalResourceForm: emptyGigExternalResourceForm(), externalResourceMode: 'create',
    gigForm: emptyGigForm(), isEditorOpen: false, gigMode: 'edit', gigQuickFilter: 'all', gigTypeFilter: 'all',
    gigSearchQuery: '', gigSort: { key: 'date', direction: 'asc' }, gigStatus: '', gigs: [overviewGig],
    isGigLoading: false, isInvoiceLoading: false, isMileageEstimating: false, isExternalResourceEditorOpen: false,
    onDetailContextChange: vi.fn(), onCancelExternalResourceEdit: vi.fn(), onCloseEditor: vi.fn(),
    onDeleteExpenseDraft: vi.fn(async () => true), onDeleteExternalResource: vi.fn(), onDeleteExternalResourceAttachment: vi.fn(),
    onDownloadExternalResourceAttachment: vi.fn(), onGenerateExpenseStatement: vi.fn(), onGenerateInvoice: vi.fn(),
    onEstimateMileage: vi.fn(), onDeleteGig: vi.fn(), onDownloadExpenseAttachment: vi.fn(), onCloneGig: vi.fn(),
    onOpenClient: vi.fn(), onOpenLinkedInvoice: vi.fn(), onSessionExpired: vi.fn(), onUploadExpenseAttachment: vi.fn(),
    onUploadExternalResourceAttachment: vi.fn(), onDeleteExpenseAttachment: vi.fn(), onResetForm: vi.fn(),
    onQuickFilterChange: vi.fn(), onGigTypeFilterChange: vi.fn(), onSearchQueryChange: vi.fn(), onSelectGig: vi.fn(),
    onSortChange: vi.fn(), onToggleGigSelection: vi.fn(), onStartEditing: vi.fn(), onStartExternalResourceCreate: vi.fn(),
    onStartExternalResourceEdit: vi.fn(), onSaveExpenseDraft: vi.fn(async () => true), onSubmit: vi.fn(),
    onSubmitExternalResource: vi.fn(), onUpdateExpenseReimbursement: vi.fn(), onUpdateGigField: vi.fn(),
    onUpdateExternalResourceField: vi.fn(), plannedGigCount: 1, selectedGig: overviewGig, selectedGigIds: [], selectedGigs: [],
  }
}

function expectFullPaneComposition() {
  const overview = document.querySelector('.record-overview')!
  const body = overview.querySelector('.overview-body')!
  const dock = overview.querySelector('.overview-dock')!
  expect(body.querySelector('.overview-facts')).not.toBeNull()
  expect(body.previousElementSibling?.classList.contains('panel-heading')).toBe(true)
  expect(body.nextElementSibling).toBe(dock)
  expect(overview.lastElementChild).toBe(dock ?? body)
}

describe('compact record overviews', () => {
  it('shows complete gig notes inline and opens only resource and expense workflows on demand', () => {
    const props = gigOverviewProps()
    render(<GigsSection {...props} />)
    expectFullPaneComposition()
    expect(screen.queryByTestId('add-gig-attachment-button')).toBeNull()
    expect(screen.queryByTestId('open-gig-expense-dialog-button')).toBeNull()
    expect(screen.queryByText('Notes and details')).toBeNull()
    expect(within(document.querySelector('.record-overview') as HTMLElement).getByText(overviewGig.notes!.trim())).toBeDefined()
    fireEvent.click(screen.getByTestId('gig-resources-button'))
    fireEvent.click(screen.getByTestId('add-gig-attachment-button'))
    expect(props.onStartExternalResourceCreate).toHaveBeenCalledOnce()
    fireEvent.click(screen.getByRole('button', { name: 'Close' }))
    fireEvent.click(screen.getByTestId('gig-expenses-button'))
    expect(screen.getByText('No expenses added yet.')).toBeDefined()
    fireEvent.click(screen.getByTestId('open-gig-expense-dialog-button'))
    expect(screen.getByRole('dialog', { name: 'Add expense' })).toBeDefined()
  })

  it('confirms dirty expense dismissal and preserves an unsaved draft when cancelled', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false)
    render(<GigsSection {...gigOverviewProps()} />)
    fireEvent.click(screen.getByTestId('gig-expenses-button'))
    fireEvent.click(screen.getByTestId('open-gig-expense-dialog-button'))
    fireEvent.change(screen.getByTestId('gig-expense-description-input'), { target: { value: 'Parking' } })
    const editor = screen.getByRole('dialog', { name: 'Add expense' })
    fireEvent.click(within(editor).getByRole('button', { name: 'Close' }))
    expect(window.confirm).toHaveBeenCalledOnce()
    expect(screen.getByTestId<HTMLInputElement>('gig-expense-description-input').value).toBe('Parking')
  })

  it('refreshes authoritative totals and saves an edited expense by ID after rows reorder', async () => {
    const selected: Gig = { ...overviewGig, expenses: ['Parking', 'Train'].map((description, index) => ({
      id: `expense-${index}`, sortOrder: index + 1, description, amount: 10,
      category: '', reimbursementStatus: 'Unreimbursed', reimbursedAt: null, reimbursementUpdatedAt: null,
      reimbursementMethod: null, reimbursementNote: null, attachments: [],
    })) }
    const props = { ...gigOverviewProps(), selectedGig: selected, gigForm: { ...emptyGigForm(), expenses: toEditableGigExpenses(selected) } }
    const { rerender } = render(<GigsSection {...props} />)
    expect(screen.getByTestId('gig-expenses-button').textContent).toContain('£20.00')
    fireEvent.click(screen.getByTestId('gig-expenses-button'))
    const parking = screen.getAllByTestId('gig-expense-item')[0]
    fireEvent.click(within(parking).getByRole('button', { expanded: false }))
    fireEvent.click(within(parking).getByRole('button', { name: 'Edit expense' }))
    fireEvent.change(screen.getByTestId('gig-expense-description-input'), { target: { value: 'Parking revised' } })
    const reordered = { ...selected, expenses: [...selected.expenses].reverse().map((expense, index) => ({ ...expense, sortOrder: index + 1 })) }
    rerender(<GigsSection {...props} selectedGig={reordered} gigForm={{ ...props.gigForm, expenses: toEditableGigExpenses(reordered) }} />)
    fireEvent.click(screen.getByTestId('add-gig-expense-button'))
    await waitFor(() => expect(props.onSaveExpenseDraft).toHaveBeenCalledWith(1, expect.objectContaining({ description: 'Parking revised' })))
  })

  it('retains Client actions and shows complete contact/billing information inline', () => {
    const props: ComponentProps<typeof ClientsSection> = {
      filteredClients: [overviewClient], form: emptyForm(), clientSort: { key: 'name', direction: 'asc' },
      canDeleteSelectedClient: false, clientDeleteHelperText: 'Has gigs', isApiConnected: true, isEditorOpen: false,
      isMonthlyInvoiceReady: true, isInvoiceLoading: false, isLoading: false, monthlyInvoiceHelperText: '', monthlyInvoiceMonth: '2026-10',
      onCloseEditor: vi.fn(), mode: 'edit', onDelete: vi.fn(), onGenerateMonthlyInvoice: vi.fn(), onMonthlyInvoiceMonthChange: vi.fn(),
      onOpenClientSettings: vi.fn(), onResetForm: vi.fn(), onSearchQueryChange: vi.fn(), onSelectClient: vi.fn(), onSortChange: vi.fn(),
      onStartEditing: vi.fn(), onSubmit: vi.fn(), onUpdateAddressField: vi.fn(), onUpdateField: vi.fn(), searchQuery: '', selectedClient: overviewClient, status: '',
    }
    render(<ClientsSection {...props} />)
    expectFullPaneComposition()
    fireEvent.click(screen.getByTestId('generate-monthly-invoice-button'))
    expect(props.onGenerateMonthlyInvoice).toHaveBeenCalledOnce()
    expect(screen.queryByText('Contact and billing details')).toBeNull()
    expect(document.querySelector('.record-overview')?.textContent).toContain(overviewClient.billingAddress.line1)
    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('keeps invoice actions restricted and line items in the existing editor', () => {
    const props: ComponentProps<typeof InvoicesSection> = {
      adjustmentAmount: '', adjustmentReason: '', invoiceDescription: '', clientNamesById: new Map([[overviewClient.id, overviewClient.name]]),
      draftInvoiceCount: 1, filteredInvoices: [invoice], googleDrivePublishLink: null, isEditorOpen: false,
      invoiceQuickFilter: 'all', invoiceSearchQuery: '', invoiceSort: { key: 'invoiceNumber', direction: 'asc' }, invoiceStatus: '',
      invoices: [invoice], issuedInvoiceCount: 0, isGoogleDriveConnected: false, isInvoiceLoading: false, isSellerProfileConfigured: false,
      onAdjustmentAmountChange: vi.fn(), onAdjustmentReasonChange: vi.fn(), onAddAdjustment: vi.fn(async () => {}), onCloseEditor: vi.fn(),
      onDeleteAdjustment: vi.fn(async () => {}), onDeleteInvoice: vi.fn(async () => {}), onDownloadPdf: vi.fn(async () => {}),
      onInvoiceDescriptionChange: vi.fn(), onInvoiceDescriptionSave: vi.fn(async () => {}), onInvoiceStatusChange: vi.fn(async () => null),
      onOpenClient: vi.fn(), onOpenGig: vi.fn(), onOpenSellerProfile: vi.fn(), onPreviewPdf: vi.fn(async () => {}),
      onPublishGoogleDrive: vi.fn(async () => null), onRegeneratePdf: vi.fn(async () => {}), onReissue: vi.fn(async () => null),
      onSendEmail: vi.fn(async () => null), onQuickFilterChange: vi.fn(), onSearchQueryChange: vi.fn(), onSelectInvoice: vi.fn(), onSortChange: vi.fn(),
      onStartEditing: vi.fn(), sellerProfileNotice: 'Configure your seller profile', selectedInvoice: invoice, sendButtonRef: createRef(),
    }
    render(<InvoicesSection {...props} />)
    expectFullPaneComposition()
    expect(screen.getByTestId<HTMLButtonElement>('invoice-download-pdf-button').disabled).toBe(true)
    expect(screen.getByText('Configure your seller profile')).toBeDefined()
    fireEvent.click(screen.getByTestId('invoice-line-items-button'))
    expect(props.onStartEditing).toHaveBeenCalledOnce()
    expect(screen.queryByText('Description and workflow details')).toBeNull()
    expect(within(document.querySelector('.record-overview') as HTMLElement).getByText(invoice.description!.trim())).toBeDefined()
    expect(screen.getByTestId('invoice-status-select')).toBeDefined()
  })

  it('shows Admin access facts and complete identity metadata without weakening deletion rules', () => {
    const props: ComponentProps<typeof AdminSection> = {
      adminForm: emptyAdminForm(), isEditorOpen: false, adminMode: 'edit', adminSearchQuery: '', adminSort: { key: 'displayName', direction: 'asc' },
      adminStatus: '', filteredAdminUsers: [user], isAdminLoading: false, onCloseEditor: vi.fn(), onDeleteUser: vi.fn(), onResetForm: vi.fn(),
      onSearchQueryChange: vi.fn(), onSelectUser: vi.fn(), onSortChange: vi.fn(), onStartEditing: vi.fn(), onSubmit: vi.fn(), onUpdateField: vi.fn(), selectedAdminUser: user,
    }
    render(<AdminSection {...props} />)
    expectFullPaneComposition()
    expect(screen.getByRole<HTMLButtonElement>('button', { name: 'Delete user' }).disabled).toBe(true)
    expect(screen.queryByText('Account details')).toBeNull()
    expect(within(document.querySelector('.record-overview') as HTMLElement).getByText(user.googleSubject!.trim())).toBeDefined()
    expect(screen.queryByRole('dialog')).toBeNull()
  })
})
