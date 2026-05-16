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
  defaultPaymentWindowDays: number | null
  invoiceFilenamePattern: string | null
  invoiceEmailSubjectPattern: string | null
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
  defaultPaymentWindowDays: number | null
  invoiceFilenamePattern: string | null
  invoiceEmailSubjectPattern: string | null
  invoiceReplyToEmail: string | null
  invoiceUploadFolderId: string | null
  isGoogleDriveConnected: boolean
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
  defaultPaymentWindowDays: string
  invoiceFilenamePattern: string
  invoiceEmailSubjectPattern: string
  invoiceReplyToEmail: string
  invoiceUploadFolderId: string
}

export type ClientSettingsForm = {
  mileageRate: string
  passengerMileageRate: string
  invoiceFilenamePattern: string
  invoiceEmailSubjectPattern: string
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
export type GigExpenseReimbursementStatus =
  | 'Unreimbursed'
  | 'Reimbursed'
  | 'NotClaimable'

export type ExpenseAttachment = {
  id: string
  gigExpenseId: string
  fileName: string
  contentType: string
  sizeBytes: number
  createdAt: string
}

export type GigExpense = {
  id: string
  sortOrder: number
  description: string
  amount: number
  reimbursementStatus: GigExpenseReimbursementStatus
  reimbursedAt: string | null
  reimbursementUpdatedAt: string | null
  reimbursementMethod: string | null
  reimbursementNote: string | null
  attachments: ExpenseAttachment[]
}

export type GigExpenseForm = {
  id: string
  sortOrder: number
  description: string
  amount: string
  reimbursementStatus: GigExpenseReimbursementStatus
  reimbursedAt: string | null
  reimbursementUpdatedAt: string | null
  reimbursementMethod: string | null
  reimbursementNote: string | null
  attachments: ExpenseAttachment[]
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

export type QuickReceiptCandidate = Pick<
  Gig,
  'id' | 'clientId' | 'title' | 'date' | 'venue' | 'status'
> & {
  daysFromToday: number
  isSelected: boolean
}

export type QuickReceiptDraftResponse = {
  gig: Gig
  expenseId: string
  attachmentId: string
  inferredGig: boolean
  candidates: QuickReceiptCandidate[]
  autoAttachWindowDays: number
  hasNearbyCandidates: boolean
}

export type QuickReceiptDraftUpdateResponse = {
  gig: Gig
  previousGig: Gig | null
  expenseId: string
  moved: boolean
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
  firstIssuedUtc: string | null
  firstIssuedByUserId: string | null
  reissueCount: number
  lastReissuedUtc: string | null
  lastReissuedByUserId: string | null
  deliveryCount: number
  lastDeliveryChannel: string | null
  lastDeliveryRecipient: string | null
  lastDeliveredUtc: string | null
  lastDeliveredByUserId: string | null
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
