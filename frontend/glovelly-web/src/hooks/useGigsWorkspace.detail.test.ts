// @vitest-environment jsdom
import { act, cleanup, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useGigsWorkspace } from './useGigsWorkspace'
import { fetchWithSession } from '../api'
import { notifications } from '../notifications'
import type { Client, Gig } from '../types'

vi.mock('../api', async importOriginal => ({
  ...await importOriginal<typeof import('../api')>(), fetchWithSession: vi.fn(),
}))
vi.mock('../notifications', () => ({ notifications: { success: vi.fn(), error: vi.fn(), info: vi.fn() } }))
afterEach(() => { cleanup(); vi.clearAllMocks() })

const client: Client = {
  id: 'client', name: 'Client', email: '', billingAddress: { line1: '', line2: '', city: '', stateOrCounty: '', postalCode: '', country: '' },
  mileageRate: null, passengerMileageRate: null, defaultPaymentWindowDays: null, invoiceFilenamePattern: null, invoiceEmailSubjectPattern: null,
}
const gig = (id: string): Gig => ({
  id, clientId: client.id, invoiceId: null, sourceImportBatchId: null, sourceImportDraftId: null,
  title: `Gig ${id}`, date: '2099-01-01', venue: 'Hall', fee: 100, travelMiles: 0, passengerCount: null,
  notes: null, wasDriving: false, type: 'Performance', status: 'Confirmed', invoicedAt: null,
  isInvoiced: false, expenses: [], externalResources: [],
})
const savedA: Gig = { ...gig('a'), expenses: [{ id: 'expense-a', sortOrder: 1, description: 'Parking', amount: 10,
  category: null, reimbursementStatus: 'Unreimbursed', reimbursedAt: null, reimbursementUpdatedAt: null,
  reimbursementMethod: null, reimbursementNote: null, attachments: [],
}] }
const options = { clients: [client], clientNamesById: new Map([[client.id, client.name]]), onLinkedInvoiceUpdated: vi.fn(), onSessionExpired: vi.fn() }

function setup() {
  const hook = renderHook(() => useGigsWorkspace(options))
  act(() => hook.result.current.setGigs([gig('a'), gig('b')]))
  act(() => { hook.result.current.selectGig('a'); hook.result.current.setDetailContext('a') })
  return hook
}

describe('detail workflow identity', () => {
  it('merges an expense save under its original ID without replacing a new selection or editor', async () => {
    let complete!: (response: Response) => void
    vi.mocked(fetchWithSession).mockReturnValueOnce(new Promise(resolve => { complete = resolve }))
    const { result } = setup()
    let save!: Promise<boolean>
    act(() => { save = result.current.saveExpenseDraft(null, { description: 'Parking', amount: '10', category: '' }) })
    expect(result.current.isGigLoading).toBe(true)
    act(() => { result.current.selectGig('b'); result.current.setDetailContext(null) })
    await act(async () => {
      complete(new Response(JSON.stringify(savedA), { status: 200 }))
      await save
    })
    expect(result.current.selectedGig?.id).toBe('b')
    expect(result.current.gigForm.title).toBe('Gig b')
    expect(result.current.gigForm.expenses).toHaveLength(0)
    expect(result.current.gigs.find(value => value.id === 'a')?.expenses).toHaveLength(1)
    expect(vi.mocked(fetchWithSession).mock.calls[0][0]).toContain('/gigs/a')
    expect(notifications.success).toHaveBeenCalledWith('Expense added.')
  })

  it('keeps an in-context expense save inline and does not reopen or navigate the editor', async () => {
    vi.mocked(fetchWithSession).mockResolvedValueOnce(new Response(JSON.stringify(savedA), { status: 200 }))
    const { result } = setup()
    await act(async () => { await result.current.saveExpenseDraft(null, { description: 'Parking', amount: '10', category: '' }) })
    expect(result.current.selectedGig?.id).toBe('a')
    expect(result.current.isGigEditorOpen).toBe(false)
    expect(result.current.gigStatus).toBe('Expense added.')
    expect(result.current.gigForm.expenses).toHaveLength(1)
    expect(notifications.success).not.toHaveBeenCalled()
  })

  it('does not apply an old merge callback to another gig form', () => {
    const { result } = setup()
    const mergeFromA = result.current.mergeSavedGig
    act(() => { result.current.selectGig('b') })
    act(() => mergeFromA(savedA))
    expect(result.current.gigForm.title).toBe('Gig b')
    expect(result.current.gigForm.expenses).toHaveLength(0)
    expect(result.current.gigs.find(value => value.id === 'a')?.expenses).toHaveLength(1)
  })

  it('does not clear the busy guard of a newer operation when an old save finishes', async () => {
    let finishA!: (response: Response) => void
    let finishB!: (response: Response) => void
    vi.mocked(fetchWithSession)
      .mockReturnValueOnce(new Promise(resolve => { finishA = resolve }))
      .mockReturnValueOnce(new Promise(resolve => { finishB = resolve }))
    const { result } = setup()
    let saveA!: Promise<boolean>
    let saveB!: Promise<boolean>
    act(() => { saveA = result.current.saveExpenseDraft(null, { description: 'Parking', amount: '10', category: '' }) })
    act(() => { result.current.selectGig('b'); result.current.setDetailContext('b') })
    act(() => { saveB = result.current.saveExpenseDraft(null, { description: 'Parking', amount: '10', category: '' }) })
    await act(async () => { finishA(new Response(JSON.stringify(savedA), { status: 200 })); await saveA })
    expect(result.current.isGigLoading).toBe(true)
    await act(async () => { finishB(new Response(JSON.stringify({ ...savedA, id: 'b', title: 'Gig b' }), { status: 200 })); await saveB })
    expect(result.current.isGigLoading).toBe(false)
    expect(result.current.selectedGig?.id).toBe('b')
  })
})
