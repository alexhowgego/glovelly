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
  searchQuery: '', quickFilter: 'work-queue', sort, typeFilter: 'all', ...overrides,
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

  it('uses the work queue as the default view', () => {
    const gigs = [
      gig('past-completed-uninvoiced', '2026-07-01', 'Completed'),
      { ...gig('past-completed-invoiced', '2026-07-02', 'Completed'), isInvoiced: true },
      gig('past-draft', '2026-07-03', 'Draft'),
      gig('past-confirmed', '2026-07-04', 'Confirmed'),
      gig('past-cancelled', '2026-07-05', 'Cancelled'),
      gig('today-draft', today, 'Draft'),
      { ...gig('today-completed', today, 'Completed'), isInvoiced: true },
      gig('future-confirmed', '2026-07-25', 'Confirmed'),
      gig('future-cancelled', '2026-07-26', 'Cancelled'),
    ]
    expect(getVisibleGigs(gigs, names, filters(), today).map((value) => value.id))
      .toEqual(['past-completed-uninvoiced', 'past-draft', 'today-completed', 'today-draft', 'future-confirmed'])
  })

  it.each([
    ['Upcoming', 'upcoming', ['today-completed', 'today-draft', 'future-confirmed']],
    ['Uninvoiced', 'uninvoiced', ['past-completed-uninvoiced']],
    ['Drafts', 'drafts', ['past-draft', 'today-draft']],
    ['Completed', 'completed', ['past-completed-uninvoiced', 'past-completed-invoiced', 'today-completed']],
    ['All', 'all', [
      'past-completed-uninvoiced', 'past-completed-invoiced', 'past-draft', 'past-confirmed',
      'past-cancelled', 'today-completed', 'today-draft', 'future-confirmed', 'future-cancelled',
    ]],
  ] as const)('returns the complete %s view', (_label, quickFilter, expectedIds) => {
    const gigs = [
      gig('past-completed-uninvoiced', '2026-07-01', 'Completed'),
      { ...gig('past-completed-invoiced', '2026-07-02', 'Completed'), isInvoiced: true },
      gig('past-draft', '2026-07-03', 'Draft'),
      gig('past-confirmed', '2026-07-04', 'Confirmed'),
      gig('past-cancelled', '2026-07-05', 'Cancelled'),
      gig('today-draft', today, 'Draft'),
      { ...gig('today-completed', today, 'Completed'), isInvoiced: true },
      gig('future-confirmed', '2026-07-25', 'Confirmed'),
      gig('future-cancelled', '2026-07-26', 'Cancelled'),
    ]
    expect(getVisibleGigs(gigs, names, filters({ quickFilter }), today).map((value) => value.id))
      .toEqual(expectedIds)
  })

  it('applies search and type filters after selecting a view', () => {
    const gigs = [
      { ...gig('match', '2026-07-01', 'Completed', 'Match'), type: 'Teaching' as const },
      gig('other', '2026-07-02', 'Completed', 'Other'),
    ]
    expect(getVisibleGigs(gigs, names, filters({
      quickFilter: 'completed', searchQuery: 'match', typeFilter: 'Teaching',
    }), today).map((value) => value.id)).toEqual(['match'])
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

  it('requests filter clearing for a hidden target', () => {
    const historical = gig('past', '2026-07-01', 'Completed')
    expect(getGigReveal(historical, visible)).toEqual({ clearFilters: true, quickFilter: 'all' })
    expect(getGigReveal(visible[0], visible)).toEqual({ clearFilters: false })
  })

  it('clears filters for a non-historical hidden target', () => {
    const target = gig('future', '2026-07-25', 'Confirmed')
    expect(getGigReveal(target, [])).toEqual({ clearFilters: true, quickFilter: 'all' })
  })
})
