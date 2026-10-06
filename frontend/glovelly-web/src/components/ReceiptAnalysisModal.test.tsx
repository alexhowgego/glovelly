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
      currency: { value: null, confidence: 'None' },
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
})
