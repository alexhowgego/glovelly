import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  AdminSection,
  AppShell,
  ClientSettingsModal,
  ClientsSection,
  ExpenseStatementModal,
  GigImportsModal,
  GigsSection,
  InvoiceGenerationPreviewModal,
  InvoicesSection,
  QuickReceiptModal,
  SellerProfileModal,
  SessionCheckingScreen,
  SignInScreen,
  UserSettingsModal,
} from './AppSections'
import type { AppNavigationItem, DashboardSummary } from './AppSections'
import {
  buildApiUrl,
  buildReturnUrl,
  fetchWithSession,
  getResponseErrorMessage,
  isSessionExpiredError,
  isSessionExpiredResponse,
  jsonRequestInit,
} from './api'
import {
  buildInvoiceEmailSubjectPreview,
  buildInvoiceFilenamePreview,
  invoiceFilenameTokens,
} from './invoicePreview'
import { formatCurrency, formatDate } from './formatters'
import { useAdminWorkspace } from './hooks/useAdminWorkspace'
import { useClientsWorkspace } from './hooks/useClientsWorkspace'
import { useGigsWorkspace } from './hooks/useGigsWorkspace'
import { useGigImportsWorkspace } from './hooks/useGigImportsWorkspace'
import { useInvoicePreview } from './hooks/useInvoicePreview'
import { useInvoicesWorkspace } from './hooks/useInvoicesWorkspace'
import { useProfileMenu } from './hooks/useProfileMenu'
import { useQuickReceipt } from './hooks/useQuickReceipt'
import { useSellerProfile } from './hooks/useSellerProfile'
import { useThemePreference } from './hooks/useThemePreference'
import { useUserSettings } from './hooks/useUserSettings'
import type {
  AppMetadata,
  AppSection,
  AuthUser,
  Client,
  Gig,
  GigImportBatchSummary,
  Invoice,
  InvoiceStatus,
  SellerProfile,
} from './types'
import './App.css'

function getCurrentMonthValue() {
  return new Date().toISOString().slice(0, 7)
}

function buildMonthlyInvoiceNumber(month: string, sequence: number) {
  return `GLV-${month.replace('-', '')}-${String(sequence).padStart(3, '0')}`
}


type AppProps = {
  appMetadata: AppMetadata
}

function App({ appMetadata }: AppProps) {
  const [activeSection, setActiveSection] = useState<AppSection>('gigs')
  const [status, setStatus] = useState('Checking your session...')
  const [isLoading, setIsLoading] = useState(false)
  const [isApiConnected, setIsApiConnected] = useState(false)
  const [isAuthenticated, setIsAuthenticated] = useState(false)
  const [authUser, setAuthUser] = useState<AuthUser | null>(null)
  const [isCheckingSession, setIsCheckingSession] = useState(true)
  const [shouldCloseBrowserNotice, setShouldCloseBrowserNotice] = useState(false)
  const {
    closeProfileMenu,
    isProfileMenuOpen,
    profileMenuRef,
    toggleProfileMenu,
  } = useProfileMenu()
  const { setThemePreference, themePreference } = useThemePreference()
  const [monthlyInvoiceMonth, setMonthlyInvoiceMonth] = useState(getCurrentMonthValue)
  const [monthlyInvoiceStatus, setMonthlyInvoiceStatus] = useState('')
  const [isGigImportsOpen, setIsGigImportsOpen] = useState(false)

  const isAdmin = authUser?.role === 'Admin'
  const clearSession = useCallback(() => {
    setIsAuthenticated(false)
    setAuthUser(null)
    setIsApiConnected(false)
  }, [])
  const expireSession = useCallback(
    (message: string) => {
      clearSession()
      setStatus(message)
    },
    [clearSession]
  )

  const {
    activeUsersCount,
    adminForm,
    adminMode,
    adminSearchQuery,
    adminSort,
    adminStatus,
    adminUsers,
    closeAdminEditor,
    deleteAdminUser,
    filteredAdminUsers,
    handleAdminSubmit,
    isAdminEditorOpen,
    isAdminLoading,
    loadAdminUsers,
    markAdminLoadFailed,
    resetAdminWorkspace,
    selectedAdminUser,
    selectAdminUser,
    setAdminSearchQuery,
    setAdminSort,
    startAdminCreate,
    startAdminEdit,
    totalAdmins,
    updateAdminField,
  } = useAdminWorkspace({
    onSessionExpired: expireSession,
  })
  const {
    applyClients,
    clientNamesById,
    clientSettingsForm,
    clientSettingsStatus,
    clientSort,
    clients,
    closeClientEditor,
    closeClientSettings,
    filteredClients,
    form,
    handleClientSettingsSubmit,
    handleDelete,
    handleSubmit,
    isClientEditorOpen,
    isClientSettingsOpen,
    isClientSettingsSaving,
    mode,
    openClientSettings,
    resetClientsWorkspace,
    searchQuery,
    selectedClient,
    selectClient,
    setClientSort,
    setSearchQuery,
    startCreating,
    startEditing,
    updateAddressField,
    updateClientSettingsField,
    updateField,
  } = useClientsWorkspace({
    isApiConnected,
    onSessionExpired: expireSession,
    setIsLoading,
    setStatus,
  })
  const {
    applyGigs,
    cloneSelectedGig,
    closeGigEditor,
    closeExpenseStatement,
    completedGigCount,
    deleteGig,
    deleteExpenseAttachment,
    downloadExpenseAttachment,
    downloadExpenseStatementPdf,
    expenseStatementExpenseIds,
    expenseStatementGigs,
    expenseStatementPreviewUrl,
    expenseStatementReceiptCount,
    expenseStatementStatus,
    expenseStatementTotal,
    filteredGigs,
    gigExpenseAmount,
    gigExpenseDescription,
    gigForm,
    gigMode,
    gigSearchQuery,
    gigSort,
    gigStatus,
    gigs,
    gigsById,
    estimateGigMileage,
    handleAddGigExpense,
    handleGigSubmit,
    handleToggleGigSelection,
    includeStatementReceiptAppendix,
    includeStatementReceiptAttachments,
    isExpenseStatementLoading,
    isExpenseStatementOpen,
    isGigEditorOpen,
    isGigLoading,
    isMileageEstimating,
    mergeSavedGig,
    openExpenseStatement,
    openGigReceiptDraft,
    plannedGigCount,
    previewExpenseStatement,
    removeGigExpense,
    resetGigsWorkspace,
    selectedGig,
    selectedGigIds,
    selectedGigs,
    selectGig,
    setGigExpenseAmount,
    setGigExpenseDescription,
    setGigs,
    setGigSearchQuery,
    setGigSort,
    setGigStatus,
    setIncludeStatementReceiptAppendix,
    setIncludeStatementReceiptAttachments,
    setSelectedGigId,
    setSelectedGigIds,
    startGigCreate,
    startGigEdit,
    updateGigExpenseField,
    updateGigField,
    updateExpenseReimbursement,
    uploadExpenseAttachment,
    toggleExpenseStatementExpense,
  } = useGigsWorkspace({
    clientNamesById,
    clients,
    onLinkedInvoiceUpdated: (invoice, message) => {
      setInvoices((current) => [
        invoice,
        ...current.filter((value) => value.id !== invoice.id),
      ])
      setSelectedInvoiceId(invoice.id)
      setInvoiceStatus(message)
    },
    onOpenSection: (section) => setActiveSection(section),
    onSessionExpired: expireSession,
  })
  const {
    applyGigImportBatches,
    batchDetail: gigImportBatchDetail,
    batches: gigImportBatches,
    commitGigImportDecisions,
    gigImportStatus,
    isGigImportLoading,
    loadGigImportBatch,
    loadGigImportBatches,
    resetGigImportsWorkspace,
    selectedBatchId: selectedGigImportBatchId,
    selectGigImportBatch,
    setGigImportDraftStatus,
    updateGigImportDraftField,
  } = useGigImportsWorkspace({
    onGigsCommitted: (committedGigs, message) => {
      applyGigs(committedGigs)
      setStatus(message)
    },
    onSessionExpired: expireSession,
  })
  const {
    adjustmentAmount,
    adjustmentReason,
    applyInvoices,
    closeInvoiceEditor,
    draftInvoiceCount,
    filteredInvoices,
    googleDrivePublishLink,
    handleAddInvoiceAdjustment,
    handleDeleteInvoice,
    handleDownloadInvoicePdf,
    handleInvoiceReissue,
    handleInvoiceStatusChange,
    handlePublishInvoiceGoogleDrive,
    handleSendInvoiceEmail,
    invoices,
    invoiceSearchQuery,
    invoiceSort,
    invoiceStatus,
    isInvoiceEditorOpen,
    issuedInvoiceCount,
    isInvoiceLoading,
    resetInvoicesWorkspace,
    selectedInvoice,
    setAdjustmentAmount,
    setAdjustmentReason,
    setInvoices,
    setInvoiceStatus,
    setIsInvoiceLoading,
    setSelectedInvoiceId,
    setInvoiceSearchQuery,
    setInvoiceSort,
    startInvoiceEdit,
  } = useInvoicesWorkspace({
    clientNamesById,
    onInvoiceDeleted: (invoice) => {
      setGigs((current) =>
        current.map((gig) =>
          gig.invoiceId === invoice.id
            ? {
                ...gig,
                invoiceId: null,
                invoicedAt: null,
                isInvoiced: false,
              }
            : gig
        )
      )
    },
  })
  const {
    clearQuickReceiptDialog,
    closeQuickReceiptPrompt,
    goToQuickReceiptGig,
    handleQuickReceiptFile,
    isQuickReceiptSaving,
    pendingReceiptFile,
    quickReceiptAmount,
    quickReceiptCandidates,
    quickReceiptDescription,
    quickReceiptDraft,
    quickReceiptSelectedGigId,
    quickReceiptStatus,
    savePendingReceiptToSelectedGig,
    saveQuickReceiptDetails,
    setQuickReceiptAmount,
    setQuickReceiptDescription,
    setQuickReceiptSelectedGigId,
  } = useQuickReceipt({
    getGigById: (gigId) => gigsById.get(gigId),
    onMergeSavedGig: (gig) => mergeSavedGig(gig),
    onOpenReceiptDraft: (gig) => openGigReceiptDraft(gig),
    onSelectGig: setSelectedGigId,
    onSessionExpired: expireSession,
    setGigStatus,
  })
  const {
    closeUserSettings,
    connectGoogleCalendar,
    connectGoogleDrive,
    disconnectGoogleCalendar,
    googleCalendarStatus,
    handleUserSettingsSubmit,
    isGoogleCalendarBusy,
    isUserSettingsOpen,
    isUserSettingsSaving,
    openUserSettings,
    resetUserSettings,
    updateUserSettingsField,
    userSettingsForm,
    userSettingsStatus,
  } = useUserSettings({
    authUser,
    onCloseProfileMenu: closeProfileMenu,
    onSessionExpired: expireSession,
    setAuthUser,
  })
  const {
    applySellerProfile,
    closeSellerProfile,
    handleSellerProfileSubmit,
    isSellerProfileOpen,
    isSellerProfileSaving,
    openSellerProfile: openSellerProfileModal,
    resetSellerProfile,
    sellerProfile,
    sellerProfileForm,
    sellerProfileStatus,
    updateSellerProfileField,
  } = useSellerProfile({
    onCloseProfileMenu: closeProfileMenu,
    onSessionExpired: expireSession,
  })
  const {
    closeInvoicePreview,
    downloadInvoicePreviewPdf,
    invoicePreviewInvoice,
    invoicePreviewPdfUrl,
    invoicePreviewStatus,
    isInvoicePreviewLoading,
    openInvoicePreview,
  } = useInvoicePreview({
    onDownloaded: setInvoiceStatus,
  })

  useEffect(() => {
    if (!isAdmin && activeSection === 'admin') {
      setActiveSection('gigs')
    }
  }, [activeSection, isAdmin])

  useEffect(() => {
    let ignore = false

    const resetSignedInState = () => {
      resetClientsWorkspace()
      resetGigsWorkspace()
      resetGigImportsWorkspace()
      setIsGigImportsOpen(false)
      setMonthlyInvoiceMonth(getCurrentMonthValue())
      setMonthlyInvoiceStatus('')
      resetInvoicesWorkspace()
      clearQuickReceiptDialog()
      resetUserSettings()
      resetSellerProfile()
      resetAdminWorkspace()
    }

    const expireSignedInSession = (message: string) => {
      expireSession(message)
      resetSignedInState()
    }

    const loadApp = async () => {
      setIsCheckingSession(true)

      try {
        const sessionResponse = await fetchWithSession(buildApiUrl('/auth/me'))
        if (isSessionExpiredResponse(sessionResponse)) {
          if (ignore) {
            return
          }

          expireSignedInSession('Sign in to access Glovelly.')
          setShouldCloseBrowserNotice(false)
          return
        }

        if (!sessionResponse.ok) {
          throw new Error('Unable to verify your session.')
        }

        const user = (await sessionResponse.json()) as AuthUser
        if (ignore) {
          return
        }

        setIsAuthenticated(true)
        setAuthUser(user)
        setIsLoading(true)

        const [clientsResponse, gigsResponse, gigImportsResponse, invoicesResponse, sellerProfileResponse] = await Promise.all([
          fetchWithSession(buildApiUrl('/clients')),
          fetchWithSession(buildApiUrl('/gigs')),
          fetchWithSession(buildApiUrl('/gig-imports')),
          fetchWithSession(buildApiUrl('/invoices')),
          fetchWithSession(buildApiUrl('/seller-profile')),
        ])

        if (
          isSessionExpiredResponse(clientsResponse) ||
          isSessionExpiredResponse(gigsResponse) ||
          isSessionExpiredResponse(gigImportsResponse) ||
          isSessionExpiredResponse(invoicesResponse) ||
          isSessionExpiredResponse(sellerProfileResponse)
        ) {
          expireSignedInSession('Your session expired. Sign in again to keep working.')
          return
        }

        if (!clientsResponse.ok) {
          throw new Error('Unable to load clients.')
        }

        if (!gigsResponse.ok) {
          throw new Error('Unable to load gigs.')
        }

        if (!invoicesResponse.ok) {
          throw new Error('Unable to load invoices.')
        }

        if (!gigImportsResponse.ok) {
          throw new Error('Unable to load gig imports.')
        }

        if (!sellerProfileResponse.ok) {
          throw new Error('Unable to load seller profile.')
        }

        const data = (await clientsResponse.json()) as Client[]
        const gigData = (await gigsResponse.json()) as Gig[]
        const gigImportData = (await gigImportsResponse.json()) as GigImportBatchSummary[]
        const invoiceData = (await invoicesResponse.json()) as Invoice[]
        const sellerProfileData = (await sellerProfileResponse.json()) as SellerProfile
        if (ignore) {
          return
        }

        applyClients(data)
        applyGigs(gigData)
        applyGigImportBatches(gigImportData)
        if (gigImportData[0]?.batchId) {
          await loadGigImportBatch(gigImportData[0].batchId)
        }
        applyInvoices(invoiceData)
        applySellerProfile(sellerProfileData)
        setIsApiConnected(true)
        setShouldCloseBrowserNotice(false)
        setStatus(
          data.length > 0
            ? `Signed in as ${user.email}.`
            : `Signed in as ${user.email}. No clients yet.`
        )

        if (user.role === 'Admin') {
          try {
            await loadAdminUsers()
          } catch {
            if (!ignore) {
              markAdminLoadFailed()
            }
          }
        }
      } catch (error) {
        if (!ignore) {
          if (isSessionExpiredError(error)) {
            expireSignedInSession('Your session expired. Sign in again to keep working.')
          } else {
            setIsApiConnected(false)
            resetClientsWorkspace()
            markAdminLoadFailed()
            setShouldCloseBrowserNotice(false)
            setStatus('We could not load your workspace right now. Please try again.')
          }
        }
      } finally {
        if (!ignore) {
          setIsLoading(false)
          setIsCheckingSession(false)
        }
      }
    }

    void loadApp()

    return () => {
      ignore = true
    }
  }, [
    applyClients,
    applyGigImportBatches,
    applyGigs,
    applyInvoices,
    applySellerProfile,
    clearQuickReceiptDialog,
    expireSession,
    loadAdminUsers,
    loadGigImportBatch,
    markAdminLoadFailed,
    resetClientsWorkspace,
    resetAdminWorkspace,
    resetGigImportsWorkspace,
    resetGigsWorkspace,
    resetInvoicesWorkspace,
    resetSellerProfile,
    resetUserSettings,
  ])

  useEffect(() => {
    if (!isAuthenticated) {
      return
    }

    const intervalId = window.setInterval(() => {
      void loadGigImportBatches(true)
    }, 30000)

    return () => window.clearInterval(intervalId)
  }, [isAuthenticated, loadGigImportBatches])

  const monthlyInvoiceEligibleGigs = useMemo(() => {
    if (!selectedClient || !monthlyInvoiceMonth) {
      return []
    }

    return gigs
      .filter(
        (gig) =>
          gig.clientId === selectedClient.id &&
          !gig.isInvoiced &&
          gig.date.startsWith(`${monthlyInvoiceMonth}-`) &&
          gig.status !== 'Cancelled'
      )
      .sort((left, right) => left.date.localeCompare(right.date))
  }, [gigs, monthlyInvoiceMonth, selectedClient])

  const isMonthlyInvoiceReady =
    Boolean(selectedClient) &&
    Boolean(monthlyInvoiceMonth) &&
    monthlyInvoiceEligibleGigs.length > 0

  const monthlyInvoiceHelperText = monthlyInvoiceStatus || (() => {
    if (!selectedClient) {
      return 'Select a client to review monthly invoice eligibility.'
    }

    if (!monthlyInvoiceMonth) {
      return 'Choose a month to check which gigs are ready to invoice.'
    }

    if (monthlyInvoiceEligibleGigs.length === 0) {
      return `No eligible gigs found for ${selectedClient.name} in ${monthlyInvoiceMonth}.`
    }

    return `${monthlyInvoiceEligibleGigs.length} eligible gig(s) ready to invoice for ${selectedClient.name} in ${monthlyInvoiceMonth}.`
  })()

  const clientDeleteEligibility = useMemo(() => {
    if (!selectedClient) {
      return {
        canDelete: false,
        helperText: 'Select a client before deleting.',
      }
    }

    const relatedGigs = gigs.filter((gig) => gig.clientId === selectedClient.id)
    if (relatedGigs.length > 0) {
      return {
        canDelete: false,
        helperText: `Cannot delete ${selectedClient.name} while ${relatedGigs.length} gig record(s) are linked.`,
      }
    }

    const relatedInvoices = invoices.filter(
      (invoice) => invoice.clientId === selectedClient.id
    )

    if (relatedInvoices.length > 0) {
      return {
        canDelete: false,
        helperText: `Cannot delete ${selectedClient.name} while ${relatedInvoices.length} invoice record(s) are linked.`,
      }
    }

    return {
      canDelete: true,
      helperText: `Delete ${selectedClient.name} after confirmation.`,
    }
  }, [gigs, invoices, selectedClient])

  const handleClientDelete = () => {
    if (!selectedClient) {
      return
    }

    if (!clientDeleteEligibility.canDelete) {
      setStatus(clientDeleteEligibility.helperText)
      return
    }

    if (!window.confirm(`Delete ${selectedClient.name}? This cannot be undone.`)) {
      return
    }

    void handleDelete()
  }

  const openSelectedGigInvoice = () => {
    if (!selectedGig?.invoiceId) {
      return
    }

    setSelectedInvoiceId(selectedGig.invoiceId)
    setActiveSection('invoices')
  }

  const openInvoiceLineGig = (gigId: string) => {
    setSelectedGigId(gigId)
    setSelectedGigIds([])
    setGigSearchQuery('')
    closeInvoiceEditor()
    setActiveSection('gigs')
  }

  const openClientShortcut = (clientId: string) => {
    if (!selectClient(clientId)) {
      return
    }

    setSearchQuery('')
    setActiveSection('clients')
  }

  useEffect(() => {
    setMonthlyInvoiceStatus('')
  }, [selectedClient?.id, monthlyInvoiceMonth])

  const sellerProfileMissingLabels = useMemo(() => {
    const labels: Record<string, string> = {
      sellerName: 'seller name',
      addressLine1: 'address line 1',
      city: 'city',
      country: 'country',
      accountName: 'account name',
      sortCode: 'sort code',
      accountNumber: 'account number',
    }

    return sellerProfile.missingFields.map((field) => labels[field] ?? field)
  }, [sellerProfile.missingFields])
  const sellerProfileNotice = sellerProfile.isInvoiceReady
    ? ' Seller profile is invoice-ready, so PDFs will include your sender and payment details.'
    : sellerProfile.isConfigured
      ? ` Seller profile is incomplete. Missing: ${sellerProfileMissingLabels.join(', ')}. You can still generate invoices, but some sender details may be omitted.`
      : ' Seller profile is not set up yet. You can still generate invoices, but sender and payment details will be missing until you configure them.'

  const navigationItems: AppNavigationItem[] = [
    {
      id: 'clients',
      label: 'Clients',
      eyebrow: 'Live',
      description: 'Booking contacts, billing details and client records.',
    },
    {
      id: 'gigs',
      label: 'Gigs',
      eyebrow: 'Live',
      description: 'Bookings, delivery status and the first invoicing-ready gig records.',
    },
    {
      id: 'invoices',
      label: 'Invoices',
      eyebrow: 'Generated',
      description: 'One-off invoices, line items and downloadable PDFs.',
    },
    ...(isAdmin
      ? [
          {
            id: 'admin' as const,
            label: 'Admin',
            eyebrow: 'Restricted',
            description: 'Manage access, roles and account status.',
          },
        ]
      : []),
  ]

  const currentSection = navigationItems.find((item) => item.id === activeSection)
  const pendingGigImportCount = gigImportBatches.reduce(
    (count, batch) => count + batch.pendingCount + batch.acceptedCount,
    0
  )
  const nextDashboardGig = useMemo(() => {
    const today = new Date().toISOString().slice(0, 10)

    return [...gigs]
      .filter((gig) => gig.status !== 'Cancelled' && gig.date >= today)
      .sort((left, right) => {
        const dateComparison = left.date.localeCompare(right.date)
        if (dateComparison !== 0) {
          return dateComparison
        }

        return left.title.localeCompare(right.title)
      })[0] ?? null
  }, [gigs])

  const dashboardInvoiceCandidate = useMemo(() => {
    const today = new Date().toISOString().slice(0, 10)

    return [...gigs]
      .filter(
        (gig) =>
          !gig.isInvoiced &&
          gig.status !== 'Cancelled' &&
          gig.status !== 'Draft' &&
          gig.date <= today
      )
      .sort((left, right) => {
        const dateComparison = right.date.localeCompare(left.date)
        if (dateComparison !== 0) {
          return dateComparison
        }

        const statusPriority = (gig: Gig) => (gig.status === 'Completed' ? 0 : 1)
        const statusComparison = statusPriority(left) - statusPriority(right)
        if (statusComparison !== 0) {
          return statusComparison
        }

        return left.title.localeCompare(right.title)
      })[0] ?? null
  }, [gigs])

  const dashboardSummary: DashboardSummary = useMemo(() => {
    const outstandingInvoices = invoices.filter(
      (invoice) => invoice.status !== 'Paid' && invoice.status !== 'Cancelled'
    )
    const toGigSummary = (gig: Gig) => ({
      title: gig.title,
      clientName: clientNamesById.get(gig.clientId) ?? 'Unknown client',
      dateLabel: formatDate(gig.date),
      venue: gig.venue || 'No venue set',
    })

    return {
      outstandingBalanceLabel: formatCurrency(
        outstandingInvoices.reduce((total, invoice) => total + invoice.total, 0)
      ),
      outstandingInvoiceCount: outstandingInvoices.length,
      nextGig: nextDashboardGig ? toGigSummary(nextDashboardGig) : null,
      invoiceCandidate: dashboardInvoiceCandidate
        ? {
            ...toGigSummary(dashboardInvoiceCandidate),
            feeLabel: formatCurrency(dashboardInvoiceCandidate.fee),
          }
        : null,
    }
  }, [clientNamesById, dashboardInvoiceCandidate, invoices, nextDashboardGig])

  const signIn = () => {
    const loginUrl = buildApiUrl(
      `/auth/login?returnUrl=${encodeURIComponent(buildReturnUrl())}`
    )
    window.location.assign(loginUrl)
  }

  const signOut = async () => {
    setIsLoading(true)
    closeProfileMenu()

    try {
      const response = await fetchWithSession(buildApiUrl('/auth/logout'), {
        method: 'POST',
      })

      if (!response.ok) {
        throw new Error('Unable to sign out.')
      }

      clearSession()
      resetClientsWorkspace()
      resetGigsWorkspace()
      resetGigImportsWorkspace()
      setIsGigImportsOpen(false)
      resetInvoicesWorkspace()
      resetSellerProfile()
      resetAdminWorkspace()
      setShouldCloseBrowserNotice(true)
      setStatus('Signed out successfully.')
    } catch {
      setStatus('Unable to sign out right now.')
    } finally {
      setIsLoading(false)
    }
  }

  const openSellerProfile = () => {
    openSellerProfileModal(sellerProfileNotice)
  }

  const openGigImports = () => {
    closeProfileMenu()
    if (selectedGigImportBatchId) {
      void loadGigImportBatch(selectedGigImportBatchId)
    }
    setIsGigImportsOpen(true)
  }

  const closeGigImports = () => {
    setIsGigImportsOpen(false)
  }

  const openPreviewedInvoice = () => {
    if (invoicePreviewInvoice) {
      setSelectedInvoiceId(invoicePreviewInvoice.id)
    }

    closeInvoicePreview()
    setActiveSection('invoices')
  }

  const previewInvoicePdf = async (invoice: Invoice) => {
    await openInvoicePreview(invoice)
  }

  const promptToCompleteLinkedGigs = async (invoice: Invoice) => {
    const linkedGigs = gigs.filter(
      (gig) =>
        gig.invoiceId === invoice.id &&
        gig.status !== 'Completed' &&
        gig.status !== 'Cancelled'
    )
    if (linkedGigs.length === 0) {
      return
    }

    const linkedGigLabel =
      linkedGigs.length === 1
        ? `"${linkedGigs[0].title}"`
        : `${linkedGigs.length} linked gigs`
    const shouldComplete = window.confirm(
      `Mark ${linkedGigLabel} as completed now that invoice ${invoice.invoiceNumber} is issued?`
    )
    if (!shouldComplete) {
      setGigStatus('Linked gig status left unchanged.')
      setInvoiceStatus(`Invoice ${invoice.invoiceNumber} issued; linked gig status left unchanged.`)
      return
    }

    setIsInvoiceLoading(true)
    setGigStatus(
      linkedGigs.length === 1
        ? `Marking ${linkedGigs[0].title} as completed...`
        : `Marking ${linkedGigs.length} linked gigs as completed...`
    )

    try {
      const completedGigs: Gig[] = []
      for (const gig of linkedGigs) {
        const response = await fetchWithSession(
          buildApiUrl(`/gigs/${gig.id}/status`),
          jsonRequestInit('PATCH', {
            status: 'Completed',
          })
        )

        if (isSessionExpiredResponse(response)) {
          expireSession('Your session expired. Sign in again to keep managing gigs.')
          return
        }

        if (!response.ok) {
          throw new Error(
            await getResponseErrorMessage(response, 'Unable to complete linked gig.')
          )
        }

        completedGigs.push((await response.json()) as Gig)
      }

      setGigs((current) =>
        current.map((gig) => completedGigs.find((value) => value.id === gig.id) ?? gig)
      )
      setGigStatus(
        completedGigs.length === 1
          ? `"${completedGigs[0].title}" marked as completed.`
          : `${completedGigs.length} linked gigs marked as completed.`
      )
      setInvoiceStatus(
        completedGigs.length === 1
          ? `Invoice ${invoice.invoiceNumber} issued; linked gig marked as completed.`
          : `Invoice ${invoice.invoiceNumber} issued; ${completedGigs.length} linked gigs marked as completed.`
      )
    } catch (error) {
      setGigStatus(error instanceof Error ? error.message : 'Unable to complete linked gig.')
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  const handleInvoiceStatusChangeWithGigPrompt = async (
    invoice: Invoice,
    status: InvoiceStatus
  ) => {
    const updatedInvoice = await handleInvoiceStatusChange(invoice, status)
    if (updatedInvoice?.status === 'Issued' && invoice.status !== 'Issued') {
      await promptToCompleteLinkedGigs(updatedInvoice)
    }

    return updatedInvoice
  }

  const promptToIssueDeliveredDraft = async (invoice: Invoice) => {
    if (invoice.status !== 'Draft') {
      return invoice
    }

    const shouldIssue = window.confirm(
      `Mark delivered draft invoice ${invoice.invoiceNumber} as issued?`
    )
    if (!shouldIssue) {
      setInvoiceStatus(`Invoice ${invoice.invoiceNumber} delivered and left as Draft.`)
      return invoice
    }

    return (
      (await handleInvoiceStatusChangeWithGigPrompt(invoice, 'Issued')) ?? invoice
    )
  }

  const handleSendInvoiceEmailWithIssuePrompt = async (invoice: Invoice) => {
    const deliveredInvoice = await handleSendInvoiceEmail(invoice)
    if (!deliveredInvoice) {
      return null
    }

    return promptToIssueDeliveredDraft(deliveredInvoice)
  }

  const handlePublishInvoiceGoogleDriveWithIssuePrompt = async (invoice: Invoice) => {
    const deliveredInvoice = await handlePublishInvoiceGoogleDrive(invoice)
    if (!deliveredInvoice) {
      return null
    }

    return promptToIssueDeliveredDraft(deliveredInvoice)
  }

  const handleInvoiceReissueWithPreview = async (invoice: Invoice) => {
    const updatedInvoice = await handleInvoiceReissue(invoice)
    if (updatedInvoice) {
      await openInvoicePreview(updatedInvoice)
    }

    return updatedInvoice
  }

  const handleGenerateInvoice = async (explicitGig?: Gig) => {
    const invoiceGigs = explicitGig ? [] : selectedGigs

    if (invoiceGigs.length > 0) {
      const distinctClientIds = new Set(invoiceGigs.map((gig) => gig.clientId))
      if (distinctClientIds.size > 1) {
        setGigStatus('Selected gigs must all belong to the same client.')
        return
      }

      const alreadyInvoicedGig = invoiceGigs.find((gig) => gig.isInvoiced)
      if (alreadyInvoicedGig) {
        setGigStatus(`"${alreadyInvoicedGig.title}" is already linked to an invoice.`)
        return
      }

      setIsInvoiceLoading(true)
      setGigStatus(`Generating invoice for ${invoiceGigs.length} selected gig(s)...`)

      try {
        const response = await fetchWithSession(
          buildApiUrl('/gigs/generate-invoice'),
          jsonRequestInit('POST', {
            gigIds: invoiceGigs.map((gig) => gig.id),
          })
        )

        if (response.status === 409) {
          const conflict = (await response.json()) as { message?: string }
          throw new Error(
            conflict.message ?? 'Selected gigs must all be uninvoiced before generating.'
          )
        }

        if (!response.ok) {
          throw new Error(
            await getResponseErrorMessage(response, 'Unable to generate invoice.')
          )
        }

        const generatedInvoice = (await response.json()) as Invoice
        const nowIso = new Date().toISOString()
        const selectedIds = new Set(invoiceGigs.map((gig) => gig.id))

        setInvoices((current) => [
          generatedInvoice,
          ...current.filter((invoice) => invoice.id !== generatedInvoice.id),
        ])
        setSelectedInvoiceId(generatedInvoice.id)
        setGigs((current) =>
          current.map((gig) =>
            selectedIds.has(gig.id)
              ? {
                  ...gig,
                  invoiceId: generatedInvoice.id,
                  invoicedAt: gig.invoicedAt ?? nowIso,
                  isInvoiced: true,
                }
              : gig
          )
        )
        setSelectedGigIds([])
        setGigStatus(
          `Invoice ${generatedInvoice.invoiceNumber} generated from ${invoiceGigs.length} selected gig(s).`
        )
        setInvoiceStatus(
          sellerProfile.isInvoiceReady
            ? `Invoice ${generatedInvoice.invoiceNumber} is ready for review.`
            : `Invoice ${generatedInvoice.invoiceNumber} is ready for review. ${sellerProfileNotice}`
        )
        await openInvoicePreview(generatedInvoice)
      } catch (error) {
        setGigStatus(error instanceof Error ? error.message : 'Unable to generate invoice.')
      } finally {
        setIsInvoiceLoading(false)
      }

      return
    }

    const invoiceGig = explicitGig ?? selectedGig

    if (!invoiceGig) {
      setGigStatus('Select one or more gigs first.')
      return
    }

    setIsInvoiceLoading(true)
    setGigStatus('Generating invoice and PDF...')

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/gigs/${invoiceGig.id}/generate-invoice`),
        {
          method: 'POST',
        }
      )

      if (response.status === 409) {
        const conflict = (await response.json()) as {
          message?: string
          invoiceId?: string
        }

        const existingInvoiceId = conflict.invoiceId ?? invoiceGig.invoiceId
        if (existingInvoiceId) {
          setSelectedInvoiceId(existingInvoiceId)
          setActiveSection('invoices')
        }

        setGigStatus(conflict.message ?? 'This gig has already been invoiced.')
        setInvoiceStatus('This gig already has an invoice. Reviewing the existing record.')
        return
      }

      if (!response.ok) {
        throw new Error(
          await getResponseErrorMessage(response, 'Unable to generate invoice.')
        )
      }

      const generatedInvoice = (await response.json()) as Invoice

      setInvoices((current) => [generatedInvoice, ...current.filter((invoice) => invoice.id !== generatedInvoice.id)])
      setSelectedInvoiceId(generatedInvoice.id)
      setGigs((current) =>
        current.map((gig) =>
          gig.id === invoiceGig.id
            ? {
                ...gig,
                invoiceId: generatedInvoice.id,
                invoicedAt: new Date().toISOString(),
                isInvoiced: true,
              }
            : gig
        )
      )
      setGigStatus('Invoice generated and linked to this gig.')
      setInvoiceStatus(
        sellerProfile.isInvoiceReady
          ? 'New invoice generated from the selected gig.'
          : `New invoice generated from the selected gig. ${sellerProfileNotice}`
      )
      await openInvoicePreview(generatedInvoice)
    } catch (error) {
      setGigStatus(error instanceof Error ? error.message : 'Unable to generate invoice.')
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  const openDashboardNextGig = () => {
    if (!nextDashboardGig) {
      return
    }

    setSelectedGigId(nextDashboardGig.id)
    setSelectedGigIds([])
    setGigSearchQuery('')
    setActiveSection('gigs')
  }

  const generateDashboardInvoice = () => {
    if (!dashboardInvoiceCandidate) {
      return
    }

    setSelectedGigId(dashboardInvoiceCandidate.id)
    setSelectedGigIds([])
    setGigSearchQuery('')
    setActiveSection('gigs')
    void handleGenerateInvoice(dashboardInvoiceCandidate)
  }

  const handleGenerateMonthlyInvoice = async () => {
    if (!selectedClient) {
      setMonthlyInvoiceStatus('Choose a client before running a monthly invoice.')
      return
    }

    if (!monthlyInvoiceMonth) {
      setMonthlyInvoiceStatus('Choose a month for the monthly invoice run.')
      return
    }

    const gigsToInvoice = monthlyInvoiceEligibleGigs

    if (gigsToInvoice.length === 0) {
      setMonthlyInvoiceStatus(
        `No eligible gigs found for ${selectedClient.name} in ${monthlyInvoiceMonth}.`
      )
      return
    }

    setIsInvoiceLoading(true)
    setMonthlyInvoiceStatus(
      `Creating monthly invoice for ${gigsToInvoice.length} gig(s)...`
    )

    try {
      const createInvoiceResponse = await fetchWithSession(
        buildApiUrl('/invoices'),
        jsonRequestInit('POST', {
          invoiceNumber: buildMonthlyInvoiceNumber(monthlyInvoiceMonth, invoices.length + 1),
          clientId: selectedClient.id,
          status: 'Draft',
          description: `Monthly invoice for ${monthlyInvoiceMonth}.`,
        })
      )

      if (!createInvoiceResponse.ok) {
        throw new Error(
          await getResponseErrorMessage(
            createInvoiceResponse,
            'Unable to create monthly invoice.'
          )
        )
      }

      const createdInvoice = (await createInvoiceResponse.json()) as Invoice

      for (const gig of gigsToInvoice) {
        const linkGigResponse = await fetchWithSession(
          buildApiUrl(`/gigs/${gig.id}`),
          jsonRequestInit('PUT', {
            clientId: gig.clientId,
            title: gig.title,
            date: gig.date,
            venue: gig.venue,
            fee: gig.fee,
            travelMiles: gig.travelMiles,
            passengerCount: gig.passengerCount,
            notes: gig.notes,
            wasDriving: gig.wasDriving,
            status: gig.status,
            invoiceId: createdInvoice.id,
            expenses: gig.expenses
              .slice()
              .sort((left, right) => left.sortOrder - right.sortOrder)
              .map((expense, index) => ({
                sortOrder: index + 1,
                description: expense.description,
                amount: expense.amount,
              })),
            invoicedAt: gig.invoicedAt,
          })
        )

        if (!linkGigResponse.ok) {
          throw new Error(
            await getResponseErrorMessage(
              linkGigResponse,
              `Unable to link ${gig.title} to the monthly invoice.`
            )
          )
        }
      }

      const redraftInvoiceResponse = await fetchWithSession(
        buildApiUrl(`/invoices/${createdInvoice.id}/redraft`),
        {
          method: 'POST',
        }
      )

      if (!redraftInvoiceResponse.ok) {
        throw new Error(
          await getResponseErrorMessage(
            redraftInvoiceResponse,
            'Unable to prepare the monthly invoice PDF.'
          )
        )
      }

      const redraftedInvoice = (await redraftInvoiceResponse.json()) as Invoice
      const hydratedInvoiceResponse = await fetchWithSession(
        buildApiUrl(`/invoices/${createdInvoice.id}`)
      )

      const updatedInvoice = hydratedInvoiceResponse.ok
        ? ((await hydratedInvoiceResponse.json()) as Invoice)
        : redraftedInvoice

      setInvoices((current) => [
        updatedInvoice,
        ...current.filter((invoice) => invoice.id !== updatedInvoice.id),
      ])
      setSelectedInvoiceId(updatedInvoice.id)
      setGigs((current) =>
        current.map((gig) =>
          gigsToInvoice.some((value) => value.id === gig.id)
            ? {
                ...gig,
                invoiceId: updatedInvoice.id,
                isInvoiced: true,
                invoicedAt: gig.invoicedAt ?? new Date().toISOString(),
              }
            : gig
        )
      )
      setGigStatus(
        `Monthly invoice ${updatedInvoice.invoiceNumber} created for ${gigsToInvoice.length} gig(s).`
      )
      setMonthlyInvoiceStatus(
        `Monthly invoice ${updatedInvoice.invoiceNumber} created for ${gigsToInvoice.length} gig(s).`
      )
      setInvoiceStatus(`Monthly invoice ${updatedInvoice.invoiceNumber} is ready for review.`)
      await openInvoicePreview(updatedInvoice)
    } catch (error) {
      setMonthlyInvoiceStatus(
        error instanceof Error ? error.message : 'Unable to generate monthly invoice.'
      )
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  if (isCheckingSession) {
    return <SessionCheckingScreen appMetadata={appMetadata} status={status} />
  }

  if (!isAuthenticated) {
    return (
      <SignInScreen
        appMetadata={appMetadata}
        onSignIn={signIn}
        shouldCloseBrowserNotice={shouldCloseBrowserNotice}
        status={status}
      />
    )
  }
  const currentSectionContent =
    activeSection === 'clients' ? (
      <ClientsSection
        filteredClients={filteredClients}
        form={form}
        clientSort={clientSort}
        canDeleteSelectedClient={clientDeleteEligibility.canDelete}
        clientDeleteHelperText={clientDeleteEligibility.helperText}
        isApiConnected={isApiConnected}
        isEditorOpen={isClientEditorOpen}
        isMonthlyInvoiceReady={isMonthlyInvoiceReady}
        isInvoiceLoading={isInvoiceLoading}
        isLoading={isLoading}
        monthlyInvoiceHelperText={monthlyInvoiceHelperText}
        monthlyInvoiceMonth={monthlyInvoiceMonth}
        mode={mode}
        onCloseEditor={closeClientEditor}
        onDelete={handleClientDelete}
        onGenerateMonthlyInvoice={handleGenerateMonthlyInvoice}
        onMonthlyInvoiceMonthChange={setMonthlyInvoiceMonth}
        onOpenClientSettings={openClientSettings}
        onResetForm={startCreating}
        onSearchQueryChange={setSearchQuery}
        onSelectClient={selectClient}
        onSortChange={setClientSort}
        onStartEditing={startEditing}
        onSubmit={handleSubmit}
        onUpdateAddressField={updateAddressField}
        onUpdateField={updateField}
        searchQuery={searchQuery}
        selectedClient={selectedClient}
        status={status}
      />
    ) : activeSection === 'admin' && isAdmin ? (
      <AdminSection
        adminForm={adminForm}
        isEditorOpen={isAdminEditorOpen}
        adminMode={adminMode}
        adminSearchQuery={adminSearchQuery}
        adminSort={adminSort}
        adminStatus={adminStatus}
        adminUsers={adminUsers}
        activeUsersCount={activeUsersCount}
        filteredAdminUsers={filteredAdminUsers}
        isAdminLoading={isAdminLoading}
        onCloseEditor={closeAdminEditor}
        onDeleteUser={deleteAdminUser}
        onResetForm={startAdminCreate}
        onSearchQueryChange={setAdminSearchQuery}
        onSelectUser={selectAdminUser}
        onSortChange={setAdminSort}
        onStartEditing={startAdminEdit}
        onSubmit={handleAdminSubmit}
        onUpdateField={updateAdminField}
        selectedAdminUser={selectedAdminUser}
        totalAdmins={totalAdmins}
      />
    ) : activeSection === 'gigs' ? (
      <GigsSection
        clientNamesById={clientNamesById}
        clients={clients}
        completedGigCount={completedGigCount}
        filteredGigs={filteredGigs}
        gigExpenseAmount={gigExpenseAmount}
        gigExpenseDescription={gigExpenseDescription}
        gigForm={gigForm}
        isEditorOpen={isGigEditorOpen}
        gigMode={gigMode}
        gigSearchQuery={gigSearchQuery}
        gigSort={gigSort}
        gigStatus={gigStatus}
        gigs={gigs}
        isGigLoading={isGigLoading}
        isInvoiceLoading={isInvoiceLoading}
        isMileageEstimating={isMileageEstimating}
        onAddGigExpense={handleAddGigExpense}
        onCloseEditor={closeGigEditor}
        onExpenseAmountChange={setGigExpenseAmount}
        onExpenseDescriptionChange={setGigExpenseDescription}
        onGenerateExpenseStatement={openExpenseStatement}
        onGenerateInvoice={() => void handleGenerateInvoice()}
        onEstimateMileage={estimateGigMileage}
        onDeleteGig={deleteGig}
        onDownloadExpenseAttachment={downloadExpenseAttachment}
        onCloneGig={cloneSelectedGig}
        onOpenClient={openClientShortcut}
        onOpenLinkedInvoice={openSelectedGigInvoice}
        onOpenSellerProfile={openSellerProfile}
        onUploadExpenseAttachment={uploadExpenseAttachment}
        onDeleteExpenseAttachment={deleteExpenseAttachment}
        onRemoveGigExpense={removeGigExpense}
        onResetForm={startGigCreate}
        onSearchQueryChange={setGigSearchQuery}
        onSelectGig={selectGig}
        onSortChange={setGigSort}
        onToggleGigSelection={handleToggleGigSelection}
        onStartEditing={startGigEdit}
        onSubmit={handleGigSubmit}
        onUpdateGigExpenseField={updateGigExpenseField}
        onUpdateGigField={updateGigField}
        onUpdateExpenseReimbursement={updateExpenseReimbursement}
        plannedGigCount={plannedGigCount}
        sellerProfile={sellerProfile}
        sellerProfileNotice={sellerProfileNotice}
        selectedGig={selectedGig}
        selectedGigIds={selectedGigIds}
        selectedGigs={selectedGigs}
      />
    ) : (
      <InvoicesSection
        adjustmentAmount={adjustmentAmount}
        adjustmentReason={adjustmentReason}
        clientNamesById={clientNamesById}
        draftInvoiceCount={draftInvoiceCount}
        filteredInvoices={filteredInvoices}
        isEditorOpen={isInvoiceEditorOpen}
        invoiceSearchQuery={invoiceSearchQuery}
        invoiceSort={invoiceSort}
        invoiceStatus={invoiceStatus}
        googleDrivePublishLink={
          invoiceStatus.startsWith('Uploaded ') ? googleDrivePublishLink : null
        }
        invoices={invoices}
        issuedInvoiceCount={issuedInvoiceCount}
        isGoogleDriveConnected={authUser?.isGoogleDriveConnected ?? false}
        isInvoiceLoading={isInvoiceLoading}
        isSellerProfileConfigured={sellerProfile.isConfigured}
        onAdjustmentAmountChange={setAdjustmentAmount}
        onAdjustmentReasonChange={setAdjustmentReason}
        onAddAdjustment={handleAddInvoiceAdjustment}
        onCloseEditor={closeInvoiceEditor}
        onDeleteInvoice={handleDeleteInvoice}
        onDownloadPdf={handleDownloadInvoicePdf}
        onInvoiceStatusChange={handleInvoiceStatusChangeWithGigPrompt}
        onOpenClient={openClientShortcut}
        onOpenGig={openInvoiceLineGig}
        onOpenSellerProfile={openSellerProfile}
        onPreviewPdf={previewInvoicePdf}
        onPublishGoogleDrive={handlePublishInvoiceGoogleDriveWithIssuePrompt}
        onReissue={handleInvoiceReissueWithPreview}
        onSendEmail={handleSendInvoiceEmailWithIssuePrompt}
        onSearchQueryChange={setInvoiceSearchQuery}
        onSelectInvoice={setSelectedInvoiceId}
        onSortChange={setInvoiceSort}
        onStartEditing={startInvoiceEdit}
        sellerProfileNotice={sellerProfileNotice}
        selectedInvoice={selectedInvoice}
      />
    )

  return (
    <AppShell
      activeSection={activeSection}
      appMetadata={appMetadata}
      authUser={authUser}
      currentSection={currentSection}
      currentSectionContent={currentSectionContent}
      dashboardSummary={dashboardSummary}
      isAdmin={isAdmin}
      isAdminLoading={isAdminLoading}
      isGigLoading={isGigLoading}
      isInvoiceLoading={isInvoiceLoading}
      isLoading={isLoading}
      isProfileMenuOpen={isProfileMenuOpen}
      isQuickReceiptSaving={isQuickReceiptSaving}
      isSellerProfileSaving={isSellerProfileSaving}
      isUserSettingsSaving={isUserSettingsSaving}
      navigationItems={navigationItems}
      pendingGigImportCount={pendingGigImportCount}
      onGenerateDashboardInvoice={generateDashboardInvoice}
      onOpenGigImports={openGigImports}
      onOpenNextGig={openDashboardNextGig}
      onOpenSellerProfile={openSellerProfile}
      onOpenUserSettings={openUserSettings}
      onProfileMenuToggle={toggleProfileMenu}
      onQuickReceiptFile={handleQuickReceiptFile}
      onSectionChange={setActiveSection}
      onSignOut={signOut}
      onThemePreferenceChange={setThemePreference}
      profileMenuRef={profileMenuRef}
      sellerProfile={sellerProfile}
      themePreference={themePreference}
    >
      <ExpenseStatementModal
        clientName={
          (expenseStatementGigs[0]
            ? clientNamesById.get(expenseStatementGigs[0].clientId)
            : null) ?? 'Unknown client'
        }
        expenseIds={expenseStatementExpenseIds}
        gigs={expenseStatementGigs}
        includeReceiptAppendix={includeStatementReceiptAppendix}
        includeReceiptAttachments={includeStatementReceiptAttachments}
        isOpen={isExpenseStatementOpen}
        isSaving={isExpenseStatementLoading}
        onClose={closeExpenseStatement}
        onDownload={downloadExpenseStatementPdf}
        onIncludeReceiptAppendixChange={setIncludeStatementReceiptAppendix}
        onIncludeReceiptAttachmentsChange={setIncludeStatementReceiptAttachments}
        onPreview={previewExpenseStatement}
        onToggleExpense={toggleExpenseStatementExpense}
        previewPdfUrl={expenseStatementPreviewUrl}
        receiptCount={expenseStatementReceiptCount}
        status={expenseStatementStatus}
        total={expenseStatementTotal}
      />

      <InvoiceGenerationPreviewModal
        invoice={invoicePreviewInvoice}
        isLoading={isInvoicePreviewLoading}
        isOpen={Boolean(invoicePreviewInvoice)}
        onClose={closeInvoicePreview}
        onDownload={downloadInvoicePreviewPdf}
        onOpenInvoice={openPreviewedInvoice}
        pdfUrl={invoicePreviewPdfUrl}
        status={invoicePreviewStatus}
      />

      <GigImportsModal
        batchDetail={gigImportBatchDetail}
        batches={gigImportBatches}
        clients={clients}
        gigImportStatus={gigImportStatus}
        isOpen={isGigImportsOpen}
        isGigImportLoading={isGigImportLoading}
        onClose={closeGigImports}
        onCommitDecisions={commitGigImportDecisions}
        onSelectBatch={selectGigImportBatch}
        onSetDraftStatus={(draft, draftStatus) => {
          void setGigImportDraftStatus(draft, draftStatus)
        }}
        onUpdateDraftField={updateGigImportDraftField}
        selectedBatchId={selectedGigImportBatchId}
      />

      <UserSettingsModal
        form={userSettingsForm}
        invoiceEmailSubjectPreview={buildInvoiceEmailSubjectPreview(
          userSettingsForm.invoiceEmailSubjectPattern,
          null
        )}
        invoiceFilenamePreview={buildInvoiceFilenamePreview(
          userSettingsForm.invoiceFilenamePattern,
          null
        )}
        invoiceFilenameTokens={invoiceFilenameTokens}
        googleCalendarStatus={googleCalendarStatus}
        isGoogleCalendarBusy={isGoogleCalendarBusy}
        isGoogleDriveConnected={authUser?.isGoogleDriveConnected ?? false}
        isOpen={isUserSettingsOpen}
        isSaving={isUserSettingsSaving}
        isGoogleDriveConnectDisabled={isLoading || isAdminLoading}
        onClose={closeUserSettings}
        onConnectGoogleCalendar={connectGoogleCalendar}
        onConnectGoogleDrive={connectGoogleDrive}
        onDisconnectGoogleCalendar={disconnectGoogleCalendar}
        onSubmit={handleUserSettingsSubmit}
        onUpdateField={updateUserSettingsField}
        sellerProfilePostcode={sellerProfile.postcode}
        status={userSettingsStatus}
      />

      <SellerProfileModal
        form={sellerProfileForm}
        isOpen={isSellerProfileOpen}
        isSaving={isSellerProfileSaving}
        onClose={closeSellerProfile}
        onSubmit={handleSellerProfileSubmit}
        onUpdateField={updateSellerProfileField}
        profile={sellerProfile}
        status={sellerProfileStatus}
      />

      <QuickReceiptModal
        amount={quickReceiptAmount}
        candidates={quickReceiptCandidates}
        clientNamesById={clientNamesById}
        description={quickReceiptDescription}
        draft={quickReceiptDraft}
        isSaving={isQuickReceiptSaving}
        onAmountChange={setQuickReceiptAmount}
        onClose={closeQuickReceiptPrompt}
        onDescriptionChange={setQuickReceiptDescription}
        onGoToGig={goToQuickReceiptGig}
        onSaveDetails={saveQuickReceiptDetails}
        onSaveDraft={savePendingReceiptToSelectedGig}
        onSelectedGigChange={setQuickReceiptSelectedGigId}
        pendingFile={pendingReceiptFile}
        selectedGigId={quickReceiptSelectedGigId}
        status={quickReceiptStatus}
      />

      <ClientSettingsModal
        authUser={authUser}
        form={clientSettingsForm}
        invoiceEmailSubjectPreview={buildInvoiceEmailSubjectPreview(
          clientSettingsForm.invoiceEmailSubjectPattern ||
            authUser?.invoiceEmailSubjectPattern,
          selectedClient?.name
        )}
        invoiceFilenamePreview={buildInvoiceFilenamePreview(
          clientSettingsForm.invoiceFilenamePattern || authUser?.invoiceFilenamePattern,
          selectedClient?.name
        )}
        invoiceFilenameTokens={invoiceFilenameTokens}
        isOpen={isClientSettingsOpen}
        isSaving={isClientSettingsSaving}
        onClose={closeClientSettings}
        onSubmit={handleClientSettingsSubmit}
        onUpdateField={updateClientSettingsField}
        selectedClient={selectedClient}
        status={clientSettingsStatus}
      />
    </AppShell>
  )
}

export default App
