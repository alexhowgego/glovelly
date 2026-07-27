import { formatCurrency, formatDate } from './formatters'
import type { AppSection, Client, Gig, Invoice, PaidIncomeSummary } from './types'

export type DashboardCardAction = 'invoices-outstanding' | 'invoices-overdue' | 'invoices-income'
export type DashboardCardState = 'loading' | 'ready' | 'error'

export type DashboardCard = {
  action?: DashboardCardAction
  detail: string
  label: string
  state: DashboardCardState
  value: string
}

export type PaidIncomeSummaryState =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'ready'; summary: PaidIncomeSummary }

type DashboardCardOptions = {
  activeSection: AppSection
  clients: Client[]
  gigs: Gig[]
  invoices: Invoice[]
  isWorkspaceLoading: boolean
  paidIncomeSummary: PaidIncomeSummaryState
  today: string
}

const loadingCard = (label: string): DashboardCard => ({
  label,
  value: 'Loading...',
  detail: 'Updating your workspace summary.',
  state: 'loading',
})

export function getDashboardCards({
  activeSection,
  clients,
  gigs,
  invoices,
  isWorkspaceLoading,
  paidIncomeSummary,
  today,
}: DashboardCardOptions): DashboardCard[] {
  if (isWorkspaceLoading) {
    const labels = activeSection === 'clients'
      ? ['Active clients', 'Clients with outstanding invoices', 'Recently added clients']
      : activeSection === 'invoices'
        ? ['Outstanding balance', 'Overdue invoices', 'Income this financial year']
        : ['Upcoming gigs', 'Awaiting confirmation', 'Completed, uninvoiced']
    return labels.map(loadingCard)
  }

  if (activeSection === 'clients') {
    const activeClientIds = new Set(
      gigs.filter((gig) => gig.status !== 'Cancelled').map((gig) => gig.clientId)
    )
    const outstandingClientIds = new Set(
      invoices
        .filter((invoice) => invoice.status !== 'Paid' && invoice.status !== 'Cancelled')
        .map((invoice) => invoice.clientId)
    )
    const gigCountByClientId = gigs
      .filter((gig) => gig.status !== 'Cancelled')
      .reduce((counts, gig) => counts.set(gig.clientId, (counts.get(gig.clientId) ?? 0) + 1), new Map<string, number>())
    const mostFrequentClient = clients
      .map((client) => ({ client, gigCount: gigCountByClientId.get(client.id) ?? 0 }))
      .filter(({ gigCount }) => gigCount > 0)
      .sort((left, right) => right.gigCount - left.gigCount || left.client.name.localeCompare(right.client.name))[0]

    return [
      {
        label: 'Active clients',
        value: String(activeClientIds.size),
        detail: activeClientIds.size === 1 ? '1 client with active work' : `${activeClientIds.size} clients with active work`,
        state: 'ready',
      },
      {
        label: 'Clients with outstanding invoices',
        value: String(outstandingClientIds.size),
        detail: outstandingClientIds.size === 1 ? '1 client awaiting payment' : `${outstandingClientIds.size} clients awaiting payment`,
        state: 'ready',
      },
      {
        label: 'Most frequent client',
        value: mostFrequentClient?.client.name ?? 'None yet',
        detail: mostFrequentClient
          ? mostFrequentClient.gigCount === 1
            ? '1 non-cancelled gig'
            : `${mostFrequentClient.gigCount} non-cancelled gigs`
          : 'No gig history yet',
        state: 'ready',
      },
    ]
  }

  if (activeSection === 'invoices') {
    const outstandingInvoices = invoices.filter(
      (invoice) => invoice.status !== 'Paid' && invoice.status !== 'Cancelled'
    )
    const overdueInvoices = invoices.filter((invoice) => invoice.status === 'Overdue')
    const incomeCard: DashboardCard = paidIncomeSummary.status === 'loading'
      ? loadingCard('Income this financial year')
      : paidIncomeSummary.status === 'error'
        ? {
            label: 'Income this financial year',
            value: 'Unavailable',
            detail: 'Unable to load paid income.',
            state: 'error',
          }
        : {
            action: 'invoices-income',
            label: 'Income this financial year',
            value: formatCurrency(paidIncomeSummary.summary.total),
            detail: `${formatDate(paidIncomeSummary.summary.financialYearStart)} to ${formatDate(paidIncomeSummary.summary.financialYearEnd)}`,
            state: 'ready',
          }

    return [
      {
        action: 'invoices-outstanding',
        label: 'Outstanding balance',
        value: formatCurrency(outstandingInvoices.reduce((total, invoice) => total + invoice.total, 0)),
        detail: outstandingInvoices.length === 1 ? '1 invoice needs attention' : `${outstandingInvoices.length} invoices need attention`,
        state: 'ready',
      },
      {
        action: 'invoices-overdue',
        label: 'Overdue invoices',
        value: String(overdueInvoices.length),
        detail: overdueInvoices.length === 1 ? '1 invoice is overdue' : `${overdueInvoices.length} invoices are overdue`,
        state: 'ready',
      },
      incomeCard,
    ]
  }

  const upcomingGigs = gigs.filter(
    (gig) => gig.status === 'Confirmed' && gig.date >= today
  )
  const draftGigs = gigs.filter((gig) => gig.status === 'Draft')
  const completedUninvoicedGigs = gigs.filter(
    (gig) => gig.status === 'Completed' && !gig.isInvoiced
  )
  return [
    {
      label: 'Upcoming gigs',
      value: String(upcomingGigs.length),
      detail: upcomingGigs.length === 1 ? '1 confirmed gig ahead' : `${upcomingGigs.length} confirmed gigs ahead`,
      state: 'ready',
    },
    {
      label: 'Awaiting confirmation',
      value: String(draftGigs.length),
      detail: draftGigs.length === 1 ? '1 draft gig to confirm' : `${draftGigs.length} draft gigs to confirm`,
      state: 'ready',
    },
    {
      label: 'Completed, uninvoiced',
      value: String(completedUninvoicedGigs.length),
      detail: completedUninvoicedGigs.length === 1 ? '1 gig ready to invoice' : `${completedUninvoicedGigs.length} gigs ready to invoice`,
      state: 'ready',
    },
  ]
}
