import { beforeEach, describe, expect, it, vi } from 'vitest'

const toast = vi.hoisted(() => ({
  success: vi.fn(),
  info: vi.fn(),
  error: vi.fn(),
  dismiss: vi.fn(),
}))

vi.mock('sonner', () => ({ toast }))

import { notifications } from './notifications'

describe('notifications', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('uses a semantic key to replace repeated success feedback', () => {
    notifications.success('Receipt uploaded.', { dedupeKey: 'gig:receipt-upload' })

    expect(toast.success).toHaveBeenCalledWith('Receipt uploaded.', {
      duration: 5000,
      id: 'gig:receipt-upload',
    })
  })

  it('uses the standard information timeout', () => {
    notifications.info('Invoice is ready for review.')

    expect(toast.info).toHaveBeenCalledWith('Invoice is ready for review.', { duration: 6000 })
  })

  it('keeps errors visible until dismissed', () => {
    notifications.error('Unable to download attachment file.', { dedupeKey: 'gig:attachment-download' })

    expect(toast.error).toHaveBeenCalledWith('Unable to download attachment file.', {
      duration: Infinity,
      id: 'gig:attachment-download',
    })
  })

  it('supports individual dismissal and session reset', () => {
    notifications.dismiss('gig:attachment-download')
    notifications.resetSession()

    expect(toast.dismiss).toHaveBeenNthCalledWith(1, 'gig:attachment-download')
    expect(toast.dismiss).toHaveBeenNthCalledWith(2)
  })
})
