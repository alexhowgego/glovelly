// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { AccessRequestsModal } from './AccessRequestsModal'
import type { AccessRequest } from '../types'

afterEach(cleanup)

it('approves a request without offering or submitting an administrative role', () => {
  const request: AccessRequest = {
    id: 'request', email: 'new@example.test', normalizedEmail: 'new@example.test',
    displayName: 'New User', subject: null, requestedAtUtc: '2026-01-01T00:00:00Z',
    notificationSentAtUtc: null, notificationSuppressionReason: null, status: 'Pending',
    decisionAtUtc: null, reviewedByUserId: null, provisionedUserId: null, decisionNote: null,
  }
  const approve = vi.fn()
  render(<AccessRequestsModal accessRequestStatus="" accessRequests={[request]} isLoading={false}
    isOpen onApprove={approve} onClose={vi.fn()} onDecline={vi.fn()} onRefresh={vi.fn()}
    onSelect={vi.fn()} selectedAccessRequest={request} />)
  expect(screen.queryByRole('combobox', { name: /role/i })).toBeNull()
  fireEvent.click(screen.getByRole('button', { name: 'Approve access' }))
  expect(approve).toHaveBeenCalledWith({ isActive: true, sendInvitationEmail: true, decisionNote: undefined })
})
