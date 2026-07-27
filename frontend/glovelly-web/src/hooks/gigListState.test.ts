import { describe, expect, it } from 'vitest'
import type { Gig, GigSort } from '../types'
import {
  type GigListFilters,
  getGigReveal,
  getLocalDate,
  getVisibleGigs,
  reconcileSelectedGigId,
} from './gigListState'

const sort: GigSort = { key: 'date', direction: 'asc' }
const filters = (overrides: Partial<GigListFilters> = {}): GigListFilters => ({
  searchQuery: '', quickFilter: 'all', showPastGigs: false, sort, typeFilter: 'all', ...overrides,
})
const gig = (id: string, date: string, status: Gig['status'], title = id): Gig => ({
  id, clientId: 'client', invoiceId: null, sourceImportBatchId: null, sourceImportDraftId: null,
  title, date, venue: 'Venue', fee: 0, travelMiles: 0, passengerCount: null, notes: null,
  wasDriving: false, type: 'Performance', status, invoicedAt: null, isInvoiced: false,
  expenses: [], externalResources: [],
})
const names = new Map([['client', 'Client']])
const today = '2026-07-24'

describe('getVisibleGigs', () => {
  it('formats the local calendar date without UTC conversion', () => {
    expect(getLocalDate(new Date(2026, 6, 24, 0, 30))).toBe('2026-07-24')
  })

  it('hides only past completed and cancelled gigs by default', () => {
    const gigs = [
      gig('completed', '2026-07-23', 'Completed'), gig('cancelled', '2026-07-23', 'Cancelled'),
      gig('draft', '2026-07-23', 'Draft'), gig('confirmed', '2026-07-23', 'Confirmed'),
      gig('today', today, 'Completed'),
    ]
    expect(getVisibleGigs(gigs, names, filters(), today).map((value) => value.id))
      .toEqual(['confirmed', 'draft', 'today'])
  })

  it('includes historical gigs without discarding active filters or sorting', () => {
    const gigs = [
      gig('older', '2026-07-01', 'Completed', 'Match'),
      gig('newer', '2026-07-02', 'Completed', 'Match'),
      gig('other', '2026-07-03', 'Completed', 'Other'),
    ]
    expect(getVisibleGigs(gigs, names, filters({ showPastGigs: true, searchQuery: 'match' }), today)
      .map((value) => value.id)).toEqual(['older', 'newer'])
  })
})

describe('gig selection state', () => {
  const visible = [gig('first', '2026-07-25', 'Confirmed'), gig('second', '2026-07-26', 'Confirmed')]

  it('uses the first visible gig initially and retains a visible selected gig', () => {
    expect(reconcileSelectedGigId('', visible)).toBe('first')
    expect(reconcileSelectedGigId('second', visible)).toBe('second')
  })

  it('falls back to the first visible gig or clears selection', () => {
    expect(reconcileSelectedGigId('hidden', visible)).toBe('first')
    expect(reconcileSelectedGigId('hidden', [])).toBe('')
  })

  it('requests filter clearing and historical visibility for a hidden target', () => {
    const historical = gig('past', '2026-07-01', 'Completed')
    expect(getGigReveal(historical, visible, today)).toEqual({ clearFilters: true, showPastGigs: true })
    expect(getGigReveal(visible[0], visible, today)).toEqual({ clearFilters: false, showPastGigs: false })
  })

  it('clears filters without showing history for a non-historical hidden target', () => {
    const target = gig('future', '2026-07-25', 'Confirmed')
    expect(getGigReveal(target, [], today)).toEqual({ clearFilters: true, showPastGigs: false })
  })
})
