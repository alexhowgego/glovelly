import type {
  AdminUserForm,
  ClientForm,
  ClientSettingsForm,
  GigForm,
  GigExternalResourceForm,
  SellerProfileForm,
  UserSettingsForm,
} from './types'

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
  sendInvitationEmail: true,
})

export const emptyUserSettingsForm = (): UserSettingsForm => ({
  mileageRate: '',
  passengerMileageRate: '',
  travelOriginPostcode: '',
  defaultPaymentWindowDays: '',
  invoiceFilenamePattern: '',
  invoiceEmailSubjectPattern: '',
  invoiceEmailBodyTemplate: '',
  invoiceReplyToEmail: '',
  invoiceUploadFolderId: '',
})

export const emptyClientSettingsForm = (): ClientSettingsForm => ({
  mileageRate: '',
  passengerMileageRate: '',
  invoiceFilenamePattern: '',
  invoiceEmailSubjectPattern: '',
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
  travelMiles: '',
  passengerCount: '',
  notes: '',
  wasDriving: true,
  type: 'Performance',
  status: 'Confirmed',
  expenses: [],
})

export const emptyGigExternalResourceForm = (): GigExternalResourceForm => ({
  resourceType: 'Url',
  purpose: 'Other',
  title: '',
  url: '',
  notes: '',
  isPrimary: false,
})

export const defaultAdminStatus = 'User enrolment tools ready.'
export const defaultGigStatus =
  'Create a gig and it will show up in your gigs workspace.'
export const defaultInvoiceStatus =
  'Generated invoices appear here once a gig has been billed.'
