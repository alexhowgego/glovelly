import type {
  GigExpense,
  GigExpenseForm,
  GigStatus,
  InvoiceStatus,
} from './types'

export function formatDateTime(value: string | null) {
  if (!value) {
    return 'Never'
  }

  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return 'Unknown'
  }

  return dateTimeFormatter.format(date)
}

export function formatCommitId(value: string | null) {
  if (!value) {
    return 'Unknown commit'
  }

  return value.slice(0, 7)
}

export function formatBuildMetadata(
  commitId: string | null,
  buildTimestamp: string | null
) {
  const parts: string[] = []

  if (commitId) {
    parts.push(`Build ${formatCommitId(commitId)}`)
  }

  if (buildTimestamp) {
    parts.push(formatDateTime(buildTimestamp))
  }

  return parts.length > 0 ? parts.join(' • ') : 'Build details unavailable'
}

export function formatRate(value: number | null) {
  if (value === null) {
    return 'Using default'
  }

  return `${value.toFixed(2)} per mile`
}

export function formatCurrency(value: number) {
  return currencyFormatter.format(value)
}

export function formatDate(value: string) {
  if (!value) {
    return 'No date'
  }

  const date = new Date(`${value}T00:00:00`)
  if (Number.isNaN(date.getTime())) {
    return value
  }

  return dateFormatter.format(date)
}

function formatEditableAmount(value: number) {
  return Number.isInteger(value) ? String(value) : value.toFixed(2)
}

export function toGigExpenseForm(expense: GigExpense): GigExpenseForm {
  return {
    id: expense.id,
    sortOrder: expense.sortOrder,
    description: expense.description,
    amount: formatEditableAmount(expense.amount),
    reimbursementStatus: expense.reimbursementStatus,
    reimbursedAt: expense.reimbursedAt,
    reimbursementUpdatedAt: expense.reimbursementUpdatedAt,
    reimbursementMethod: expense.reimbursementMethod,
    reimbursementNote: expense.reimbursementNote,
    attachments: expense.attachments ?? [],
  }
}

export function formatGigStatus(status: GigStatus) {
  return status === 'Confirmed' ? 'Planned' : status
}

export function getAllowedInvoiceStatusTransitions(status: InvoiceStatus) {
  switch (status) {
    case 'Draft':
      return ['Issued', 'Cancelled'] as InvoiceStatus[]
    case 'Issued':
      return ['Paid', 'Overdue', 'Cancelled'] as InvoiceStatus[]
    case 'Overdue':
      return ['Paid', 'Cancelled'] as InvoiceStatus[]
    case 'Cancelled':
      return ['Draft'] as InvoiceStatus[]
    case 'Paid':
      return [] as InvoiceStatus[]
  }
}

const dateTimeFormatter = new Intl.DateTimeFormat(undefined, {
  dateStyle: 'medium',
  timeStyle: 'short',
})

const currencyFormatter = new Intl.NumberFormat(undefined, {
  style: 'currency',
  currency: 'GBP',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
})

const dateFormatter = new Intl.DateTimeFormat(undefined, {
  dateStyle: 'medium',
})
