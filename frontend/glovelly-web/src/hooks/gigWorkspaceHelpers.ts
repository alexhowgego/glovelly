import type { FormEvent } from 'react'
import { emptyGigForm } from '../forms'
import { toGigExpenseForm } from '../formatters'
import type {
  Client,
  Gig,
  GigExpenseForm,
  GigExpenseReimbursementStatus,
  GigForm,
  InvoiceStatus,
} from '../types'

export type NormalizedGigExpensePayload = {
  sortOrder: number
  description: string
  amount: number
}

export function formatEditableNumber(value: number | null) {
  if (value === null || value === 0) {
    return ''
  }

  return Number.isInteger(value) ? String(value) : value.toFixed(2)
}

export function shouldCloseAfterSave(event: FormEvent<HTMLFormElement>) {
  const submitter = (event.nativeEvent as SubmitEvent).submitter as
    | HTMLButtonElement
    | null

  return submitter?.dataset.closeAfterSave !== 'false'
}

export function toEditableGigForm(gig: Gig): GigForm {
  return {
    clientId: gig.clientId,
    title: gig.title,
    date: gig.date,
    venue: gig.venue,
    fee: String(gig.fee),
    travelMiles: formatEditableNumber(gig.travelMiles),
    passengerCount: formatEditableNumber(gig.passengerCount),
    notes: gig.notes ?? '',
    wasDriving: gig.wasDriving,
    status: gig.status,
    expenses: toEditableGigExpenses(gig),
  }
}

export function toEditableGigExpenses(gig: Gig): GigExpenseForm[] {
  return gig.expenses
    .slice()
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map(toGigExpenseForm)
}

export function toCreateGigForm(clients: Client[]): GigForm {
  return {
    ...emptyGigForm(),
    clientId: clients[0]?.id ?? '',
  }
}

export function formatReimbursementStatus(status: GigExpenseReimbursementStatus) {
  switch (status) {
    case 'Unreimbursed':
      return 'Claimable'
    case 'NotClaimable':
      return 'Not claimable'
    default:
      return status
  }
}

export function canCancelInvoice(status: InvoiceStatus) {
  return status === 'Draft' || status === 'Issued' || status === 'Overdue'
}

export function hasInvoiceRelevantGigChanges(
  gig: Gig,
  payload: {
    clientId: string
    title: string
    date: string
    venue: string
    fee: string
    notes: string
    wasDriving: boolean
    travelMiles: string
    passengerCount: string
    status: Gig['status']
    expenses: GigExpenseForm[]
  },
  fee: number,
  normalizedExpenses: NormalizedGigExpensePayload[]
) {
  if (
    gig.clientId !== payload.clientId ||
    gig.title !== payload.title ||
    gig.date !== payload.date ||
    gig.venue !== payload.venue ||
    gig.fee !== fee ||
    gig.travelMiles !== (payload.travelMiles ? Number(payload.travelMiles) : 0) ||
    (gig.passengerCount ?? 0) !==
      (payload.passengerCount ? Number(payload.passengerCount) : 0) ||
    (gig.notes ?? '') !== (payload.notes || '') ||
    gig.wasDriving !== payload.wasDriving
  ) {
    return true
  }

  const currentExpenses = gig.expenses
    .slice()
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map((expense, index) => ({
      sortOrder: index + 1,
      description: expense.description,
      amount: expense.amount,
    }))

  if (currentExpenses.length !== normalizedExpenses.length) {
    return true
  }

  return currentExpenses.some((expense, index) => {
    const nextExpense = normalizedExpenses[index]
    return (
      expense.sortOrder !== nextExpense.sortOrder ||
      expense.description !== nextExpense.description ||
      expense.amount !== nextExpense.amount
    )
  })
}
