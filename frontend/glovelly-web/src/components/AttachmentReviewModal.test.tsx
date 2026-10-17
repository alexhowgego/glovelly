// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { IntakeApplication } from '../types'
import { AttachmentReviewModal } from './AttachmentReviewModal'

const request = vi.fn()
vi.mock('../api', () => ({
  buildApiUrl: (path: string) => path, fetchWithSession: (...args: unknown[]) => request(...args),
  getResponseErrorMessage: async () => 'Unable to save changes.', handleSessionExpired: () => false,
  jsonRequestInit: (method: string, body: unknown) => ({ method, body: JSON.stringify(body) }),
}))
const application = {
  kind: 'Receipt', expenseId: 'intake-expense', attachmentId: 'intake-attachment', resource: null,
  gig: {
    id: 'gig', clientId: 'client', title: 'Tonight', date: '2026-01-01', venue: 'Venue', type: 'Other', status: 'Confirmed', externalResources: [],
    expenses: [
      { id: 'other-expense', description: 'Unrelated expense', amount: 99, category: null, attachments: [] },
      { id: 'intake-expense', description: 'Rail Co', amount: 24.5, category: 'Travel', attachments: [{ id: 'intake-attachment', fileName: 'ticket.pdf' }] },
    ],
  },
} as unknown as IntakeApplication

function renderReview() {
  const close = vi.fn()
  const merge = vi.fn()
  render(<AttachmentReviewModal application={application} evidence={[]} clientNamesById={new Map()} onClose={close} onMergeGig={merge} onMergeInvoices={vi.fn()} onSessionExpired={vi.fn()} />)
  return { close, merge }
}

describe('Saved attachment review', () => {
  afterEach(() => { cleanup(); vi.resetAllMocks() })
  it('uses the exact saved expense and closes without saving or reanalysing', () => {
    const { close } = renderReview()
    expect((screen.getByLabelText('Description') as HTMLInputElement).value).toBe('Rail Co')
    expect((screen.getByLabelText('Amount') as HTMLInputElement).value).toBe('24.5')
    fireEvent.click(screen.getByRole('button', { name: 'Done' }))
    expect(close).toHaveBeenCalledOnce()
    expect(request).not.toHaveBeenCalled()
  })
  it('saves corrections to the original expense without resubmitting intake', async () => {
    request.mockResolvedValueOnce(new Response(JSON.stringify({ gig: application.gig, previousGig: null, invoices: [] })))
    const { merge } = renderReview()
    fireEvent.change(screen.getByLabelText('Description'), { target: { value: 'Return train ticket' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }))
    await screen.findByText('Attachment updated.')
    expect(request.mock.calls[0][0]).toBe('/gigs/receipt-drafts/intake-expense')
    expect(JSON.parse(request.mock.calls[0][1].body)).toMatchObject({ description: 'Return train ticket', amount: 24.5, category: 'Travel', gigId: 'gig' })
    expect(merge).toHaveBeenCalledOnce()
  })
  it('keeps failed corrections open with inline feedback', async () => {
    request.mockResolvedValueOnce(new Response(null, { status: 500 }))
    const { close } = renderReview()
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }))
    await screen.findByText('Unable to save changes.')
    expect(screen.getByRole('dialog', { name: 'Review attachment' })).toBeTruthy()
    expect(close).not.toHaveBeenCalled()
  })
})
