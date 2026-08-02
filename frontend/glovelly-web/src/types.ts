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

export type SortDirection = 'asc' | 'desc'
export type ClientSortKey = 'name' | 'email' | 'city' | 'country'
export type ClientSort = {
  key: ClientSortKey
  direction: SortDirection
}

export type AuthUser = {
  userId: string
  role: string
  name: string
  email: string
  profileImageUrl: string
  mileageRate: number | null
  passengerMileageRate: number | null
  travelOriginPostcode: string | null
  defaultPaymentWindowDays: number | null
  invoiceFilenamePattern: string | null
  invoiceEmailSubjectPattern: string | null
  invoiceEmailBodyTemplate: string | null
  invoiceReplyToEmail: string | null
  invoiceUploadFolderId: string | null
  isGoogleDriveConnected: boolean
  isGoogleSheetsConnected: boolean
}

export type GoogleCalendarStatus = {
  isConnected: boolean
  isEnabled: boolean
  hasRequiredScope: boolean
  calendarId: string | null
  calendarName: string | null
  lastSuccessfulSyncAtUtc: string | null
  pendingWorkCount: number
  failedWorkCount: number
  lastError: string | null
}

export type ForScoreLibrarySnapshot = {
  id: string
  originalFileName: string
  sourceFormat: string
  backupVersion: string | null
  isActive: boolean
  chartCount: number
  warnings: string[]
  importedAtUtc: string
  createdAtUtc: string
}

export type ForScoreChart = {
  id: string
  filePath: string
  title: string
  normalizedTitle: string
  keywords: string | null
  addedAt: string | null
  printNumber: number | null
  version: number | null
}

export type ForScoreLibraryChartsResponse = {
  snapshotId: string
  charts: ForScoreChart[]
}

export type ForScoreLibraryImportImpactSetList = {
  gigId: string
  setListImportId: string
  gigTitle: string
  gigDate: string
  gigStatus: string
  autoRelinkedItemCount: number
  needsReviewItemCount: number
}

export type ForScoreLibraryImportImpact = {
  checkedSetListCount: number
  affectedSetListCount: number
  checkedItemCount: number
  autoRelinkedItemCount: number
  needsReviewItemCount: number
  setLists: ForScoreLibraryImportImpactSetList[]
}

export type ForScoreLibraryImportResponse = {
  snapshot: ForScoreLibrarySnapshot
  impact: ForScoreLibraryImportImpact
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
  sendInvitationEmail: boolean
}

export type AdminSortKey =
  | 'displayName'
  | 'email'
  | 'role'
  | 'access'
  | 'enrolment'
  | 'lastLogin'
export type AdminSort = {
  key: AdminSortKey
  direction: SortDirection
}

export type AccessRequestStatus = 'Pending' | 'Provisioned' | 'Declined' | 'Expired'

export type AccessRequest = {
  id: string
  email: string
  normalizedEmail: string
  displayName: string | null
  subject: string | null
  requestedAtUtc: string
  notificationSentAtUtc: string | null
  notificationSuppressionReason: string | null
  status: AccessRequestStatus
  decisionAtUtc: string | null
  reviewedByUserId: string | null
  provisionedUserId: string | null
  decisionNote: string | null
}

export type AccessRequestApproval = {
  role: 'Admin' | 'User'
  isActive: boolean
  sendInvitationEmail: boolean
  decisionNote?: string
}

export type AccessRequestApprovalResult = {
  accessRequest: AccessRequest
  decisionApplied: boolean
  userCreated: boolean
  existingUser: boolean
  invitationEmailSent: boolean | null
}

export type AccessRequestDeclineResult = {
  accessRequest: AccessRequest
  decisionApplied: boolean
}

export type UserSettingsForm = {
  mileageRate: string
  passengerMileageRate: string
  travelOriginPostcode: string
  defaultPaymentWindowDays: string
  invoiceFilenamePattern: string
  invoiceEmailSubjectPattern: string
  invoiceEmailBodyTemplate: string
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
export type GigType = 'Performance' | 'Teaching' | 'Rehearsal' | 'Recording' | 'Admin' | 'Other'
export type GigExternalResourceType =
  | 'GoogleSheet'
  | 'GoogleDoc'
  | 'Url'
  | 'Email'
  | 'File'
  | 'Other'
export type GigExternalResourcePurpose =
  | 'SetList'
  | 'GigPlan'
  | 'Contract'
  | 'Travel'
  | 'Other'
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

export type ReceiptAnalysisConfidence = 'None' | 'Low' | 'Medium' | 'High'

export type ReceiptAnalysisField<T> = {
  value: T
  confidence: ReceiptAnalysisConfidence
}

export type ReceiptAnalysisResult = {
  id: string
  status: 'Succeeded' | 'Failed'
  provider: string
  model: string
  promptVersion: string
  requestedAt: string
  completedAt: string
  merchant: ReceiptAnalysisField<string | null>
  transactionDate: ReceiptAnalysisField<string | null>
  totalAmount: ReceiptAnalysisField<number | null>
  currency: ReceiptAnalysisField<string | null>
  suggestedCategory: ReceiptAnalysisField<string | null>
  warnings: string[]
  failureCode: string | null
  failureMessage: string | null
}

export type ReceiptAnalysisTarget = {
  gigId: string
  expenseId: string
  attachmentId: string
  fileName: string
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

export type GigExternalResource = {
  id: string
  gigId: string
  resourceType: GigExternalResourceType
  purpose: GigExternalResourcePurpose
  title: string
  url: string | null
  notes: string | null
  isPrimary: boolean
  createdAt: string
  updatedAt: string
  attachments: GigExternalResourceAttachment[]
}

export type GigExternalResourceAttachment = {
  id: string
  gigExternalResourceId: string
  fileName: string
  contentType: string
  sizeBytes: number
  createdAt: string
}

export type GigSetListItemKind = 'Song' | 'Separator' | 'Comment'
export type GigSetListItemConfidence = 'Low' | 'Medium' | 'High'
export type ForScoreMappingStatus = 'Unmapped' | 'Linked' | 'Suggested' | 'NeedsReview' | 'MissingFromLatestLibrary' | 'NotApplicable' | 'NoActiveLibrary'
export type ForScoreMappingConfidence = 'None' | 'Low' | 'Medium' | 'High' | 'Manual'

export type ForScoreChartReference = {
  id: string
  snapshotId: string
  title: string
  filePath: string
  normalizedTitle: string
}

export type SetListChartMatchCandidate = {
  chart: ForScoreChartReference
  score: number
  reason: string
  evidence: string[]
}

export type SetListChartMatchResult = {
  itemId: string | null
  sourceRowNumber: number
  status: ForScoreMappingStatus
  confidence: ForScoreMappingConfidence
  reason: string
  selectedChart: ForScoreChartReference | null
  candidates: SetListChartMatchCandidate[]
}

export type SetListChartMatchJobStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Cancelled'

export type SetListChartMatchJobResponse = {
  jobId: string
  gigId: string
  status: SetListChartMatchJobStatus
  correlationId: string | null
  errorMessage: string | null
  createdAtUtc: string
  updatedAtUtc: string
  startedAtUtc: string | null
  completedAtUtc: string | null
  result: SetListChartMatchResult[] | null
}

export type SetListSavedChartMapping = {
  snapshotId: string | null
  chartId: string | null
  chartTitle: string | null
  chartFilePath: string | null
  status: ForScoreMappingStatus
  confidence: ForScoreMappingConfidence
  updatedAtUtc: string | null
}

export type GigSetListWorksheet = {
  sheetId: string
  title: string
  index: number
}

export type GigSetListSource = {
  resourceId: string
  resourceTitle: string
  resourceUrl: string
  spreadsheetId: string
  worksheets: GigSetListWorksheet[]
}

export type GigSetListImportItemDraft = {
  sourceRowNumber: number
  sortOrder: number
  kind: GigSetListItemKind
  include: boolean
  section: string | null
  padNumber: string | null
  key: string | null
  title: string
  notes: string | null
  rawCellsJson: string
  confidence: GigSetListItemConfidence
  forScoreChartId: string | null
  forScoreMatch: SetListChartMatchResult | null
}

export type GigSetListPreview = {
  resourceId: string
  resourceTitle: string
  resourceUrl: string
  spreadsheetId: string
  worksheetId: string | null
  worksheetName: string
  items: GigSetListImportItemDraft[]
}

export type GigSetListImport = {
  id: string
  gigId: string
  resourceId: string | null
  spreadsheetId: string
  worksheetId: string | null
  worksheetName: string
  sourceUrl: string | null
  isActive: boolean
  importedAtUtc: string
  items: (GigSetListImportItemDraft & { id: string; forScoreMapping: SetListSavedChartMapping })[]
}

export type ExpenseStatementGig = {
  gigId: string
  title: string
  date: string
  venue: string
  isInvoiced: boolean
  expenses: ExpenseStatementExpense[]
  total: number
}

export type ExpenseStatementExpense = {
  expenseId: string
  description: string
  amount: number
  sortOrder: number
  attachments: ExpenseStatementAttachment[]
}

export type ExpenseStatementAttachment = {
  attachmentId: string
  fileName: string
  contentType: string
  sizeBytes: number
  createdAt: string
}

export type ExpenseStatementProjection = {
  clientId: string
  clientName: string
  statementDate: string
  gigs: ExpenseStatementGig[]
  total: number
  expenseCount: number
  receiptAttachmentCount: number
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
  sourceImportBatchId: string | null
  sourceImportDraftId: string | null
  title: string
  date: string
  venue: string
  fee: number
  travelMiles: number
  passengerCount: number | null
  notes: string | null
  wasDriving: boolean
  type: GigType
  status: GigStatus
  invoicedAt: string | null
  isInvoiced: boolean
  expenses: GigExpense[]
  externalResources: GigExternalResource[]
}

export type GigSortKey = 'priority' | 'date' | 'title' | 'client' | 'venue' | 'fee' | 'status'
export type GigSort = {
  key: GigSortKey
  direction: SortDirection
}
export type GigQuickFilter = 'all' | 'upcoming' | 'uninvoiced' | 'drafts' | 'completed'

export type GigImportDraftConfidence = 'Low' | 'Medium' | 'High'
export type GigImportDraftStatus = 'Pending' | 'Accepted' | 'Rejected' | 'Committed'
export type GigImportBatchStatus = 'Draft' | 'Committed' | 'Abandoned'

export type GigImportBatchSummary = {
  batchId: string
  sourceName: string
  sourceFingerprint: string | null
  status: GigImportBatchStatus
  createdAtUtc: string
  notes: string | null
  draftCount: number
  pendingCount: number
  acceptedCount: number
  rejectedCount: number
  committedCount: number
  lowConfidenceCount: number
  mediumConfidenceCount: number
  highConfidenceCount: number
}

export type GigImportDraft = {
  draftId: string
  batchId: string
  proposedClientId: string | null
  clientName: string | null
  contactName: string | null
  contactEmail: string | null
  projectName: string | null
  title: string | null
  date: string | null
  arrivalTime: string | null
  rehearsalStartTime: string | null
  rehearsalEndTime: string | null
  showStartTime: string | null
  showEndTime: string | null
  venueName: string | null
  venueAddress: string | null
  postcode: string | null
  fee: number | null
  perDiem: number | null
  notes: string | null
  accommodationNotes: string | null
  travelNotes: string | null
  sourceReference: string | null
  gigType: GigType
  confidence: GigImportDraftConfidence
  warnings: string[]
  status: GigImportDraftStatus
  missingFields: string[]
}

export type GigImportBatchDetail = {
  batch: GigImportBatchSummary
  drafts: GigImportDraft[]
}

export type GigImportCommitResult = {
  createdCount: number
  gigIds: string[]
  batch: GigImportBatchDetail
}

export type QuickGigCandidate = Pick<
  Gig,
  'id' | 'clientId' | 'title' | 'date' | 'venue' | 'type' | 'status'
> & {
  daysFromToday: number
  isSelected: boolean
}

export type QuickReceiptCandidate = QuickGigCandidate

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

export type QuickExternalResourceDraftResponse = {
  gig: Gig
  resourceId: string
  attachmentId: string | null
  inferredGig: boolean
  candidates: QuickGigCandidate[]
  autoAttachWindowDays: number
  hasNearbyCandidates: boolean
}

export type QuickExternalResourceDraftUpdateResponse = {
  gig: Gig
  previousGig: Gig | null
  resourceId: string
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
  paidOn: string | null
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
  pdfStorageKey: string | null
  pdfFileName: string | null
  pdfContentType: string | null
  pdfSizeBytes: number | null
  pdfGeneratedAt: string | null
  total: number
  lines: InvoiceLine[]
}

export type PaidIncomeSummary = {
  financialYearStart: string
  financialYearEnd: string
  total: number
  invoiceIds: string[]
}

export type InvoiceSortKey =
  | 'priority'
  | 'invoiceDate'
  | 'dueDate'
  | 'invoiceNumber'
  | 'client'
  | 'status'
  | 'total'
export type InvoiceSort = {
  key: InvoiceSortKey
  direction: SortDirection
}
export type InvoiceQuickFilter =
  | 'all'
  | 'outstanding'
  | 'drafts'
  | 'overdue'
  | 'paid'
  | 'income-this-financial-year'

export type GigForm = {
  clientId: string
  title: string
  date: string
  venue: string
  fee: string
  travelMiles: string
  passengerCount: string
  notes: string
  wasDriving: boolean
  type: GigType
  status: GigStatus
  expenses: GigExpenseForm[]
}

export type GigExternalResourceForm = {
  resourceType: GigExternalResourceType
  purpose: GigExternalResourcePurpose
  title: string
  url: string
  notes: string
  isPrimary: boolean
}

export type AppSection = 'clients' | 'admin' | 'gigs' | 'invoices'
export type ThemePreference =
  | 'system'
  | 'light'
  | 'dark'
  | 'neon'
  | 'mahogany'
  | 'candy'
  | 'blue-note'
  | 'parchment'
  | 'studio-tape'
  | 'synthwave'
  | 'sunset-soundcheck'
  | 'velvet-rope'
export type AppMetadata = {
  title: string
  deploymentName: string | null
  commitId: string | null
  buildTimestamp: string | null
}
