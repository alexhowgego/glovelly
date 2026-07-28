import { describe, expect, it } from 'vitest'
import { getDashboardCards } from './dashboardCards'
import { formatDate } from './formatters'
import type { Client, Gig, Invoice } from './types'

const client = (id: string) => ({ id, name: id }) as Client

const gig = (id: string, overrides: Partial<Gig> = {}) =>
  ({
    id,
    clientId: 'client-a',
    date: '2026-07-30',
    status: 'Confirmed',
    isInvoiced: false,
    ...overrides,
  }) as Gig

const invoice = (id: string, overrides: Partial<Invoice> = {}) =>
  ({
    id,
    clientId: 'client-a',
    status: 'Issued',
    total: 100,
    ...overrides,
  }) as Invoice

const cards = (overrides: Partial<Parameters<typeof getDashboardCards>[0]> = {}) =>
  getDashboardCards({
    activeSection: 'gigs',
    clients: [],
    gigs: [],
    invoices: [],
    isWorkspaceLoading: false,
    paidIncomeSummary: { status: 'ready', summary: {
      financialYearStart: '2026-04-06', financialYearEnd: '2027-04-05', total: 0, invoiceIds: [],
    } },
    today: '2026-07-27',
    ...overrides,
  })

describe('getDashboardCards', () => {
  it('selects the three Gigs metrics', () => {
    const result = cards({
      gigs: [
        gig('upcoming'),
        gig('draft', { status: 'Draft' }),
        gig('completed', { status: 'Completed' }),
        gig('invoiced', { status: 'Completed', isInvoiced: true }),
      ],
    })

    expect(result.map((card) => [card.label, card.value])).toEqual([
      ['Upcoming gigs', '1'],
      ['Awaiting confirmation', '1'],
      ['Completed, uninvoiced', '1'],
    ])
  })

  it('selects the three Invoices metrics including paid income', () => {
    const result = cards({
      activeSection: 'invoices',
      invoices: [invoice('issued', { total: 75 }), invoice('overdue', { status: 'Overdue', total: 25 })],
      paidIncomeSummary: { status: 'ready', summary: {
        financialYearStart: '2026-04-06', financialYearEnd: '2027-04-05', total: 125, invoiceIds: ['paid'],
      } },
    })

    expect(result.map((card) => [card.label, card.value, card.action])).toEqual([
      ['Outstanding balance', '£100.00', 'invoices-outstanding'],
      ['Overdue invoices', '1', 'invoices-overdue'],
      ['Income this financial year', '£125.00', 'invoices-income'],
    ])
  })

  it('selects the three Clients metrics', () => {
    const result = cards({
      activeSection: 'clients',
      clients: [client('client-a'), client('client-b')],
      gigs: [gig('active'), gig('another', { clientId: 'client-b' }), gig('third', { clientId: 'client-b' })],
      invoices: [invoice('outstanding')],
    })

    expect(result.map((card) => [card.label, card.value])).toEqual([
      ['Active clients', '2'],
      ['Clients with outstanding invoices', '1'],
      ['Most frequent client', 'client-b'],
    ])
  })

  it('keeps loading, zero, and error states distinct', () => {
    expect(cards({ activeSection: 'invoices', isWorkspaceLoading: true })[0]).toMatchObject({
      state: 'loading', value: 'Loading...',
    })
    expect(cards({ activeSection: 'invoices' })[2]).toMatchObject({
      state: 'ready',
      value: '£0.00',
      detail: `${formatDate('2026-04-06')} to ${formatDate('2027-04-05')}`,
    })
    expect(cards({ activeSection: 'invoices', paidIncomeSummary: { status: 'error' } })[2]).toMatchObject({
      state: 'error', value: 'Unavailable',
    })
  })
})
