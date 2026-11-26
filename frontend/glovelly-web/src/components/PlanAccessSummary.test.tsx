// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, expect, it } from 'vitest'
import { PlanAccessSummary } from './PlanAccessSummary'
import type { EntitlementSnapshot } from '../types'

afterEach(cleanup)

it('distinguishes an expired Pro assignment from effective Free without resetting usage', () => {
  const snapshot: EntitlementSnapshot = {
    effectivePlan: { code: 'free', displayName: 'Free' }, assignedPlan: { code: 'pro', displayName: 'Pro' },
    assignmentStatus: 'Expired', endsAtUtc: '2026-11-20T00:00:00Z', features: { paidIncomeSummary: false },
    invoiceIssuance: {
      used: 10, limit: 2, remaining: 0, periodStartsAtUtc: '2026-11-01T00:00:00Z',
      resetsAtUtc: '2026-12-01T00:00:00Z', timeZone: 'Europe/London',
    },
  }
  render(<PlanAccessSummary entitlements={snapshot} />)
  expect(screen.getByText('Free')).toBeTruthy()
  expect(screen.getByText(/Assigned grant: Pro \(expired\)/)).toBeTruthy()
  expect(screen.getByText(/10 first-issued this month/)).toBeTruthy()
  expect(screen.getByText(/0\/2 remaining/)).toBeTruthy()
  expect(screen.getByText(/all records remain available/)).toBeTruthy()
})
