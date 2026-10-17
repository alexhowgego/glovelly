// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ReceiptAnalysisModal } from './ReceiptAnalysisModal'

describe('ReceiptAnalysisModal', () => {
  afterEach(() => {
    cleanup()
    vi.unstubAllGlobals()
  })

  it('applies the suggested category with the receipt details', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      status: 'Succeeded',
      merchant: { value: 'Station Cafe', confidence: 'High' },
      totalAmount: { value: 18.5, confidence: 'High' },
      transactionDate: { value: null, confidence: 'None' },
      currency: { value: 'GBP', confidence: 'High' },
      suggestedCategory: { value: 'Travel', confidence: 'Medium' },
      warnings: [],
    }), { status: 200 })))
    const onApply = vi.fn()

    render(
      <ReceiptAnalysisModal
        onApply={onApply}
        onClose={vi.fn()}
        onSessionExpired={vi.fn()}
        target={{ gigId: 'gig-1', expenseId: 'expense-1', attachmentId: 'attachment-1', fileName: 'receipt.pdf' }}
      />
    )

    fireEvent.click(await screen.findByRole('button', { name: 'Use suggestions' }))

    expect(onApply).toHaveBeenCalledWith({
      merchant: 'Station Cafe',
      totalAmount: 18.5,
      suggestedCategory: 'Travel',
    })
  })

  it('keeps a foreign total as evidence instead of copying it into GBP fields', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      status: 'Succeeded', merchant: { value: 'Hotel', confidence: 'High' }, totalAmount: { value: 100, confidence: 'High' },
      transactionDate: { value: null, confidence: 'None' }, currency: { value: 'USD', confidence: 'High' },
      suggestedCategory: { value: 'Accommodation', confidence: 'High' }, warnings: [],
    }))))
    const apply = vi.fn()
    render(<ReceiptAnalysisModal target={{ gigId: 'gig', expenseId: 'expense', attachmentId: 'attachment', fileName: 'receipt.pdf' }} onApply={apply} onClose={vi.fn()} onSessionExpired={vi.fn()} />)
    fireEvent.click(await screen.findByRole('button', { name: 'Use suggestions' }))
    expect(apply).toHaveBeenCalledWith({ merchant: 'Hotel', totalAmount: null, suggestedCategory: 'Accommodation' })
    expect(screen.getByText('USD')).toBeTruthy()
  })
})
