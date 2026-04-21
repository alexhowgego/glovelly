export type Address = {
  line1: string
  line2: string
  city: string
  stateOrCounty: string
  postalCode: string
  country: string
}

export type Client = {
  id: string
  name: string
  email: string
  billingAddress: Address
  mileageRate: number | null
  passengerMileageRate: number | null
}

export type ClientForm = {
  name: string
  email: string
  billingAddress: Address
}

export type AuthUser = {
  userId: string
  role: string
  name: string
  email: string
  profileImageUrl: string
  mileageRate: number | null
  passengerMileageRate: number | null
}

export type AdminUser = {
  id: string
  email: string
  displayName: string | null
  googleSubject: string | null
  isEnrolled: boolean
  role: string
  isActive: boolean
  createdUtc: string
  lastLoginUtc: string | null
}

export type AdminUserForm = {
  email: string
  displayName: string
  googleSubject: string
  role: 'Admin' | 'User'
  isActive: boolean
}

export type UserSettingsForm = {
  mileageRate: string
  passengerMileageRate: string
}

export type ClientSettingsForm = {
  mileageRate: string
  passengerMileageRate: string
}

export type SellerProfile = {
  id: string | null
  sellerName: string | null
  addressLine1: string | null
  addressLine2: string | null
  city: string | null
  region: string | null
  postcode: string | null
  country: string | null
  email: string | null
  phone: string | null
  accountName: string | null
  sortCode: string | null
  accountNumber: string | null
  paymentReferenceNote: string | null
  isConfigured: boolean
  isInvoiceReady: boolean
  missingFields: string[]
}

export type SellerProfileForm = {
  sellerName: string
  addressLine1: string
  addressLine2: string
  city: string
  region: string
  postcode: string
  country: string
  email: string
  phone: string
  accountName: string
  sortCode: string
  accountNumber: string
  paymentReferenceNote: string
}

export type GigStatus = 'Draft' | 'Confirmed' | 'Completed' | 'Cancelled'

export type GigExpense = {
  id: string
  sortOrder: number
  description: string
  amount: number
}

export type GigExpenseForm = {
  id: string
  sortOrder: number
  description: string
  amount: string
}

export type Gig = {
  id: string
  clientId: string
  invoiceId: string | null
  title: string
  date: string
  venue: string
  fee: number
  travelMiles: number
  passengerCount: number | null
  notes: string | null
  wasDriving: boolean
  status: GigStatus
  invoicedAt: string | null
  isInvoiced: boolean
  expenses: GigExpense[]
}

export type InvoiceLine = {
  id: string
  createdByUserId: string | null
  createdUtc: string
  sortOrder: number
  type: string
  description: string
  quantity: number
  unitPrice: number
  lineTotal: number
  gigId: string | null
  calculationNotes: string | null
  isSystemGenerated: boolean
}

export type InvoiceStatus = 'Draft' | 'Issued' | 'Paid' | 'Overdue' | 'Cancelled'

export type Invoice = {
  id: string
  invoiceNumber: string
  clientId: string
  invoiceDate: string
  dueDate: string
  status: InvoiceStatus
  reissueCount: number
  lastReissuedUtc: string | null
  description: string | null
  pdfBlob: string | null
  total: number
  lines: InvoiceLine[]
}

export type GigForm = {
  clientId: string
  title: string
  date: string
  venue: string
  fee: string
  notes: string
  wasDriving: boolean
  status: GigStatus
  expenses: GigExpenseForm[]
}

export type AppSection = 'clients' | 'admin' | 'gigs' | 'invoices'
export type ThemePreference = 'system' | 'light' | 'dark'
export type AppMetadata = {
  title: string
  deploymentName: string | null
  commitId: string | null
  buildTimestamp: string | null
}

export const themeStorageKey = 'glovelly.theme-preference'

export const emptyForm = (): ClientForm => ({
  name: '',
  email: '',
  billingAddress: {
    line1: '',
    line2: '',
    city: '',
    stateOrCounty: '',
    postalCode: '',
    country: 'United Kingdom',
  },
})

export const emptyAdminForm = (): AdminUserForm => ({
  email: '',
  displayName: '',
  googleSubject: '',
  role: 'User',
  isActive: true,
})

export const emptyUserSettingsForm = (): UserSettingsForm => ({
  mileageRate: '',
  passengerMileageRate: '',
})

export const emptyClientSettingsForm = (): ClientSettingsForm => ({
  mileageRate: '',
  passengerMileageRate: '',
})

export const emptySellerProfileForm = (): SellerProfileForm => ({
  sellerName: '',
  addressLine1: '',
  addressLine2: '',
  city: '',
  region: '',
  postcode: '',
  country: 'United Kingdom',
  email: '',
  phone: '',
  accountName: '',
  sortCode: '',
  accountNumber: '',
  paymentReferenceNote: '',
})

export const emptyGigForm = (): GigForm => ({
  clientId: '',
  title: '',
  date: '',
  venue: '',
  fee: '',
  notes: '',
  wasDriving: true,
  status: 'Confirmed',
  expenses: [],
})

export const defaultAdminStatus = 'User enrolment tools ready.'
export const defaultGigStatus =
  'Create a gig and it will show up in your gigs workspace.'
export const defaultInvoiceStatus =
  'Generated invoices appear here once a gig has been billed.'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, '') ?? ''

export function buildApiUrl(path: string) {
  return `${apiBaseUrl}${path}`
}

export function buildReturnUrl() {
  return window.location.href
}

export async function loadAppMetadata(): Promise<AppMetadata> {
  try {
    const response = await fetch(buildApiUrl('/app/metadata'), {
      cache: 'no-store',
      credentials: 'same-origin',
    })

    if (!response.ok) {
      throw new Error('Unable to load app metadata.')
    }

    const metadata = (await response.json()) as Partial<AppMetadata>
    return {
      title: metadata.title?.trim() || 'Glovelly',
      deploymentName: metadata.deploymentName?.trim() || null,
      commitId: metadata.commitId?.trim() || null,
      buildTimestamp: metadata.buildTimestamp?.trim() || null,
    }
  } catch {
    return {
      title: 'Glovelly',
      deploymentName: null,
      commitId: null,
      buildTimestamp: null,
    }
  }
}

export async function fetchWithSession(input: string, init?: RequestInit) {
  return fetch(input, {
    ...init,
    credentials: 'include',
    cache: 'no-store',
  })
}

export async function parseProblemDetails(response: Response) {
  const contentType = response.headers.get('content-type') ?? ''
  if (!contentType.includes('application/json')) {
    return null
  }

  try {
    return (await response.json()) as {
      title?: string
      detail?: string
      errors?: Record<string, string[]>
    }
  } catch {
    return null
  }
}

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

export function formatBuildMetadata(commitId: string | null, buildTimestamp: string | null) {
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

export function getStoredThemePreference(): ThemePreference {
  const rawPreference = window.localStorage.getItem(themeStorageKey)
  if (
    rawPreference === 'system' ||
    rawPreference === 'light' ||
    rawPreference === 'dark'
  ) {
    return rawPreference
  }

  return 'system'
}
