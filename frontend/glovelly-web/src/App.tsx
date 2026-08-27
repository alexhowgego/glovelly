import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  AdminSection,
  AccessRequestsModal,
  AppShell,
  ClientSettingsModal,
  ConnectedServicesModal,
  ClientsSection,
  ExpenseStatementModal,
  GigImportsModal,
  GigsSection,
  InvoiceGenerationPreviewModal,
  InvoicesSection,
  QuickAttachmentModal,
  QuickReceiptModal,
  SellerProfileModal,
  SessionCheckingScreen,
  SignInScreen,
  UserSettingsModal,
} from './AppSections'
import type { AppNavigationItem } from './AppSections'
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
  buildInvoiceEmailBodyPreview,
  buildInvoiceEmailSubjectPreview,
  buildInvoiceFilenamePreview,
  invoiceEmailBodyTokens,
  invoiceFilenameTokens,
} from './invoicePreview'
import { getDashboardCards } from './dashboardCards'
import { useAdminWorkspace } from './hooks/useAdminWorkspace'
import { useAccessRequestsWorkspace } from './hooks/useAccessRequestsWorkspace'
import { useClientsWorkspace } from './hooks/useClientsWorkspace'
import { useGigsWorkspace } from './hooks/useGigsWorkspace'
import { useGigImportsWorkspace } from './hooks/useGigImportsWorkspace'
import { useInvoicePreview } from './hooks/useInvoicePreview'
import { useInvoicesWorkspace } from './hooks/useInvoicesWorkspace'
import { useProfileMenu } from './hooks/useProfileMenu'
import { useQuickAttachment } from './hooks/useQuickAttachment'
import { useQuickReceipt } from './hooks/useQuickReceipt'
import { useSellerProfile } from './hooks/useSellerProfile'
import { useThemePreference } from './hooks/useThemePreference'
import { useUserSettings } from './hooks/useUserSettings'
import { useWorkspaceEvents } from './hooks/useWorkspaceEvents'
import { notifications } from './notifications'
import type {
  AppMetadata,
  AppSection,
  AdminUser,
  AuthUser,
  Client,
  ForScoreLibrarySnapshot,
  ForScoreLibraryImportResponse,
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

function getAccessRequestDeepLinkId(pathname: string) {
  const match = pathname.match(
    /^\/access-requests\/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\/?$/i
  )

  return match?.[1] ?? null
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
  const lastWorkspaceRefreshAtRef = useRef(0)
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
  const [invoiceListScrollRequest, setInvoiceListScrollRequest] = useState(0)
  const [isGigImportsOpen, setIsGigImportsOpen] = useState(false)
  const [isAccessRequestsOpen, setIsAccessRequestsOpen] = useState(false)
  const [forScoreSnapshot, setForScoreSnapshot] = useState<ForScoreLibrarySnapshot | null>(null)
  const [forScoreLibraryStatus, setForScoreLibraryStatus] = useState('No forScore library snapshot imported yet.')
  const [isForScoreLibraryUploading, setIsForScoreLibraryUploading] = useState(false)

  const isAdmin = authUser?.role === 'Admin'
  const accessRequestDeepLinkId = getAccessRequestDeepLinkId(window.location.pathname)
  const clearSession = useCallback(() => {
    notifications.resetSession()
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
  const syncCurrentUserFromAdmin = useCallback((savedUser: AdminUser) => {
    setAuthUser((current) =>
      current?.userId === savedUser.id
        ? {
            ...current,
            email: savedUser.email,
            name: savedUser.displayName ?? savedUser.email,
            role: savedUser.role,
          }
        : current
    )
  }, [])

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
    updateAdminUserDisplayName,
    updateAdminField,
  } = useAdminWorkspace({
    onAdminUserSaved: syncCurrentUserFromAdmin,
    onSessionExpired: expireSession,
  })
  const {
    accessRequestStatus,
    accessRequests,
    approveAccessRequest,
    declineAccessRequest,
    isAccessRequestLoading,
    loadAccessRequests,
    reportAccessRequestStatus,
    resetAccessRequestsWorkspace,
    selectAccessRequest,
    selectedAccessRequest,
  } = useAccessRequestsWorkspace({
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
    cancelExternalResourceEdit,
    cloneSelectedGig,
    closeGigEditor,
    closeExpenseStatement,
    completedGigCount,
    deleteExternalResource,
    deleteExternalResourceAttachment,
    deleteGig,
    deleteExpenseAttachment,
    downloadExpenseAttachment,
    downloadExternalResourceAttachment,
    downloadExpenseStatementPdf,
    expenseStatementExpenseIds,
    expenseStatementGigs,
    expenseStatementPreviewUrl,
    expenseStatementReceiptCount,
    expenseStatementStatus,
    expenseStatementTotal,
    externalResourceForm,
    externalResourceMode,
    filteredGigs,
    gigForm,
    gigOverviewScrollRequest,
    gigMode,
    gigQuickFilter,
    gigTypeFilter,
    gigSearchQuery,
    gigSort,
    gigStatus,
    gigs,
    gigsById,
    deleteExpenseDraft,
    estimateGigMileage,
    handleGigSubmit,
    handleToggleGigSelection,
    includeStatementReceiptAppendix,
    includeStatementReceiptAttachments,
    isExpenseStatementLoading,
    isExpenseStatementOpen,
    isExternalResourceEditorOpen,
    isGigEditorOpen,
    isGigLoading,
    isMileageEstimating,
    mergeSavedGig,
    openExpenseStatement,
    openGigReceiptDraft,
    plannedGigCount,
    previewExpenseStatement,
    resetGigsWorkspace,
    saveExpenseDraft,
    selectedGig,
    selectedGigIds,
    selectedGigs,
    showPastGigs,
    selectGig,
    setGigs,
    setGigQuickFilter,
    setGigTypeFilter,
    setGigSearchQuery,
    setGigSort,
    setGigStatus,
    setIncludeStatementReceiptAppendix,
    setIncludeStatementReceiptAttachments,
    setSelectedGigIds,
    setShowPastGigs,
    startExternalResourceCreate,
    startExternalResourceEdit,
    startGigCreate,
    startGigEdit,
    submitExternalResource,
    updateExternalResourceField,
    updateGigField,
    updateExpenseReimbursement,
    uploadExpenseAttachment,
    uploadExternalResourceAttachment,
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
      notifications.info(message, { dedupeKey: `invoice:${invoice.id}:linked-gig` })
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
    onGigsCommitted: (committedGigs) => {
      applyGigs(committedGigs)
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
    handleDeleteInvoiceAdjustment,
    handleDeleteInvoice,
    handleDownloadInvoicePdf,
    handleInvoiceReissue,
    handleInvoiceDescriptionSave,
    handleInvoiceStatusChange,
    handlePublishInvoiceGoogleDrive,
    handleSendInvoiceEmail,
    invoices,
    invoiceQuickFilter,
    invoiceDescription,
    invoiceSearchQuery,
    invoiceSort,
    invoiceStatus,
    isInvoiceEditorOpen,
    issuedInvoiceCount,
    isInvoiceLoading,
    loadPaidIncomeSummary,
    paidIncomeSummary,
    resetInvoicesWorkspace,
    selectedInvoice,
    setAdjustmentAmount,
    setAdjustmentReason,
    setInvoiceDescription,
    setInvoices,
    setInvoiceStatus,
    setIsInvoiceLoading,
    setInvoiceQuickFilter,
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
    onOpenReceiptDraft: (gig, scrollToGig) => openGigReceiptDraft(gig, scrollToGig),
    onSelectGig: selectGig,
    onSessionExpired: expireSession,
    setGigStatus,
  })
  const getQuickCaptureCandidates = useCallback(() => {
    const today = new Date()
    today.setHours(0, 0, 0, 0)

    const sortedCandidates = gigs
      .filter((gig) => gig.status !== 'Cancelled')
      .map((gig) => {
        const gigDate = new Date(`${gig.date}T00:00:00`)
        gigDate.setHours(0, 0, 0, 0)
        const daysFromToday = Number.isNaN(gigDate.getTime())
          ? 0
          : Math.abs(Math.round((gigDate.getTime() - today.getTime()) / 86_400_000))

        return {
          id: gig.id,
          clientId: gig.clientId,
          title: gig.title,
          date: gig.date,
          venue: gig.venue,
          type: gig.type,
          status: gig.status,
          daysFromToday,
          isSelected: false,
        }
      })
      .filter((candidate) => candidate.daysFromToday <= 30)
      .sort((left, right) =>
        left.daysFromToday - right.daysFromToday ||
        left.date.localeCompare(right.date) ||
        left.title.localeCompare(right.title)
      )

    const cutoff = sortedCandidates[4]?.daysFromToday
    return sortedCandidates
      .filter((candidate, index) => cutoff === undefined || index < 5 || candidate.daysFromToday === cutoff)
      .map((candidate, index) => ({
        ...candidate,
        isSelected: index === 0,
      }))
  }, [gigs])
  const {
    clearQuickAttachmentDialog,
    closeQuickAttachmentPrompt,
    goToQuickAttachmentGig,
    handleQuickAttachmentFile,
    isQuickAttachmentSaving,
    openQuickAttachmentDialog,
    pendingAttachmentFile,
    quickAttachmentCandidates,
    quickAttachmentDraft,
    quickAttachmentIsPrimary,
    quickAttachmentMode,
    quickAttachmentNotes,
    quickAttachmentPurpose,
    quickAttachmentResourceType,
    quickAttachmentSelectedGigId,
    quickAttachmentStatus,
    quickAttachmentTitle,
    quickAttachmentUrl,
    savePendingAttachmentToSelectedGig,
    saveQuickAttachmentDetails,
    saveQuickAttachmentLinkDraft,
    setQuickAttachmentIsPrimary,
    setQuickAttachmentNotes,
    setQuickAttachmentPurpose,
    setQuickAttachmentResourceType,
    setQuickAttachmentSelectedGigId,
    setQuickAttachmentTitle,
    startQuickAttachmentLinkMode,
    updateQuickAttachmentUrl,
  } = useQuickAttachment({
    getGigById: (gigId) => gigsById.get(gigId),
    getQuickCaptureCandidates,
    onMergeSavedGig: (gig) => mergeSavedGig(gig),
    onOpenAttachmentDraft: (gig, scrollToGig) => openGigReceiptDraft(gig, scrollToGig),
    onSelectGig: selectGig,
    onSessionExpired: expireSession,
    setGigStatus,
  })
  const {
    closeConnectedServices,
    closeUserSettings,
    connectGoogleCalendar,
    connectGoogleDrive,
    connectGoogleSheets,
    disconnectGoogleCalendar,
    disconnectGoogleDrive,
    disconnectGoogleSheets,
    googleCalendarStatus,
    handleUserSettingsSubmit,
    isGoogleCalendarBusy,
    isGoogleDriveBusy,
    isGoogleSheetsBusy,
    isConnectedServicesOpen,
    isUserSettingsOpen,
    isUserSettingsSaving,
    openConnectedServices,
    openSettingsFromServices,
    openUserSettings,
    resetUserSettings,
    updateUserSettingsField,
    userSettingsForm,
    userSettingsStatus,
  } = useUserSettings({
    authUser,
    onCloseProfileMenu: closeProfileMenu,
    onDisplayNameSaved: updateAdminUserDisplayName,
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

  const refreshGigsFromServer = useCallback(async (reason: 'focus' | 'realtime') => {
    if (!isAuthenticated) {
      return
    }

    const now = Date.now()
    if (reason === 'focus' && now - lastWorkspaceRefreshAtRef.current < 5000) {
      return
    }

    lastWorkspaceRefreshAtRef.current = now

    try {
      const response = await fetchWithSession(buildApiUrl('/gigs'))
      if (isSessionExpiredResponse(response)) {
        expireSession('Your session expired. Sign in again to keep working.')
        return
      }

      if (!response.ok) {
        throw new Error('Unable to refresh gigs.')
      }

      setGigs((await response.json()) as Gig[])
    } catch {
      if (reason === 'focus') {
        setStatus('We could not refresh your workspace right now.')
      }
    }
  }, [expireSession, isAuthenticated, setGigs])

  const refreshInvoicesFromServer = useCallback(async (reason: 'focus' | 'realtime') => {
    if (!isAuthenticated) {
      return
    }

    try {
      const response = await fetchWithSession(buildApiUrl('/invoices'))
      if (isSessionExpiredResponse(response)) {
        expireSession('Your session expired. Sign in again to keep working.')
        return
      }

      if (!response.ok) {
        throw new Error('Unable to refresh invoices.')
      }

      setInvoices((await response.json()) as Invoice[])
    } catch {
      if (reason === 'focus') {
        setStatus('We could not refresh your invoices right now.')
      }
    }
  }, [expireSession, isAuthenticated, setInvoices])

  useEffect(() => {
    if (isAuthenticated) {
      void loadPaidIncomeSummary()
    }
  }, [isAuthenticated, loadPaidIncomeSummary])

  useWorkspaceEvents({
    enabled: isAuthenticated,
    onWorkspaceChanged: (event) => {
      if (event.scope === 'gigs') {
        void refreshGigsFromServer('realtime')
      }

      if (event.scope === 'gig-imports') {
        void loadGigImportBatches(true)
        if (selectedGigImportBatchId) {
          void loadGigImportBatch(selectedGigImportBatchId)
        }
      }

      if (event.scope === 'access-requests' && isAdmin) {
        void loadAccessRequests(selectedAccessRequest?.id)
      }
    },
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
      resetAccessRequestsWorkspace()
      setIsAccessRequestsOpen(false)
      setMonthlyInvoiceMonth(getCurrentMonthValue())
      setMonthlyInvoiceStatus('')
      resetInvoicesWorkspace()
      clearQuickReceiptDialog()
      clearQuickAttachmentDialog()
      resetUserSettings()
      resetSellerProfile()
      resetAdminWorkspace()
      setForScoreSnapshot(null)
      setForScoreLibraryStatus('No forScore library snapshot imported yet.')
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

        const [clientsResponse, gigsResponse, gigImportsResponse, invoicesResponse, sellerProfileResponse, forScoreSnapshotResponse] = await Promise.all([
          fetchWithSession(buildApiUrl('/clients')),
          fetchWithSession(buildApiUrl('/gigs')),
          fetchWithSession(buildApiUrl('/gig-imports')),
          fetchWithSession(buildApiUrl('/invoices')),
          fetchWithSession(buildApiUrl('/seller-profile')),
          fetchWithSession(buildApiUrl('/forscore-library/active')),
        ])

        if (
          isSessionExpiredResponse(clientsResponse) ||
          isSessionExpiredResponse(gigsResponse) ||
          isSessionExpiredResponse(gigImportsResponse) ||
          isSessionExpiredResponse(invoicesResponse) ||
          isSessionExpiredResponse(sellerProfileResponse) ||
          isSessionExpiredResponse(forScoreSnapshotResponse)
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

        if (!forScoreSnapshotResponse.ok && forScoreSnapshotResponse.status !== 404) {
          throw new Error('Unable to load forScore library snapshot.')
        }

        const data = (await clientsResponse.json()) as Client[]
        const gigData = (await gigsResponse.json()) as Gig[]
        const gigImportData = (await gigImportsResponse.json()) as GigImportBatchSummary[]
        const invoiceData = (await invoicesResponse.json()) as Invoice[]
        const sellerProfileData = (await sellerProfileResponse.json()) as SellerProfile
        const loadedForScoreSnapshot = forScoreSnapshotResponse.ok
          ? ((await forScoreSnapshotResponse.json()) as ForScoreLibrarySnapshot)
          : null
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
        setForScoreSnapshot(loadedForScoreSnapshot)
        setForScoreLibraryStatus(
          loadedForScoreSnapshot
            ? `Active forScore library: ${loadedForScoreSnapshot.chartCount} chart(s) imported from ${loadedForScoreSnapshot.originalFileName}.`
            : 'No forScore library snapshot imported yet.'
        )
        setIsApiConnected(true)
        setShouldCloseBrowserNotice(false)
        setStatus(
          data.length > 0
            ? `Signed in as ${user.email}.`
            : `Signed in as ${user.email}. No clients yet.`
        )

        if (user.role === 'Admin') {
          try {
            await Promise.all([
              loadAdminUsers(),
              loadAccessRequests(accessRequestDeepLinkId ?? undefined),
            ])
            if (accessRequestDeepLinkId) {
              setIsAccessRequestsOpen(true)
            }
          } catch {
            if (!ignore) {
              markAdminLoadFailed()
            }
          }
        } else if (accessRequestDeepLinkId) {
          setStatus('Administrator access is required to review access requests.')
          reportAccessRequestStatus('Administrator access is required to review access requests.')
          setIsAccessRequestsOpen(true)
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
    clearQuickAttachmentDialog,
    clearQuickReceiptDialog,
    expireSession,
    loadAdminUsers,
    loadAccessRequests,
    reportAccessRequestStatus,
    loadGigImportBatch,
    markAdminLoadFailed,
    resetClientsWorkspace,
    resetAdminWorkspace,
    resetAccessRequestsWorkspace,
    resetGigImportsWorkspace,
    resetGigsWorkspace,
    resetInvoicesWorkspace,
    resetSellerProfile,
    resetUserSettings,
    accessRequestDeepLinkId,
  ])

  const uploadForScoreLibrary = async (file: File) => {
    setIsForScoreLibraryUploading(true)
    setForScoreLibraryStatus('Importing forScore library snapshot...')

    try {
      const formData = new FormData()
      formData.append('file', file)
      const response = await fetchWithSession(buildApiUrl('/forscore-library/imports'), {
        method: 'POST',
        body: formData,
      })

      if (isSessionExpiredResponse(response)) {
        expireSession('Your session expired. Sign in again to keep importing forScore libraries.')
        return
      }

      if (!response.ok) {
        throw new Error(
          await getResponseErrorMessage(response, 'Unable to import forScore library snapshot.')
        )
      }

      const importResult = (await response.json()) as ForScoreLibraryImportResponse
      const { snapshot, impact } = importResult
      setForScoreSnapshot(snapshot)
      setForScoreLibraryStatus(
        impact.needsReviewItemCount > 0
          ? `Imported ${snapshot.chartCount} chart(s). ${impact.affectedSetListCount} set list(s) have chart links that need review.`
          : impact.autoRelinkedItemCount > 0
            ? `Imported ${snapshot.chartCount} chart(s). ${impact.autoRelinkedItemCount} chart link(s) were updated automatically.`
            : `Imported ${snapshot.chartCount} chart(s) from ${snapshot.originalFileName}.`
      )
      notifications.success(
        `Imported ${snapshot.chartCount} chart(s) from ${snapshot.originalFileName}.`,
        { dedupeKey: 'forscore-library:import' }
      )
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unable to import forScore library snapshot.'
      setForScoreLibraryStatus(message)
      notifications.error(message, { dedupeKey: 'forscore-library:import' })
    } finally {
      setIsForScoreLibraryUploading(false)
    }
  }

  useEffect(() => {
    if (!isAuthenticated) {
      return
    }

    const refreshWhenVisible = () => {
      if (document.visibilityState === 'visible') {
        void refreshGigsFromServer('focus')
        void refreshInvoicesFromServer('focus')
      }
    }

    window.addEventListener('focus', refreshWhenVisible)
    document.addEventListener('visibilitychange', refreshWhenVisible)

    return () => {
      window.removeEventListener('focus', refreshWhenVisible)
      document.removeEventListener('visibilitychange', refreshWhenVisible)
    }
  }, [isAuthenticated, refreshGigsFromServer, refreshInvoicesFromServer])

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

  const openSelectedGigInvoice = async () => {
    if (!selectedGig?.invoiceId) {
      return
    }

    const invoiceId = selectedGig.invoiceId
    try {
      const response = await fetchWithSession(buildApiUrl(`/invoices/${invoiceId}`))
      if (isSessionExpiredResponse(response)) {
        expireSession('Your session expired. Sign in again to keep working.')
        return
      }

      if (!response.ok) {
        throw new Error('Unable to open linked invoice.')
      }

      const invoice = (await response.json()) as Invoice
      setInvoices((current) => [
        invoice,
        ...current.filter((value) => value.id !== invoice.id),
      ])
    } catch (error) {
      notifications.error(
        error instanceof Error ? error.message : 'Unable to open linked invoice.',
        { dedupeKey: `gig:${selectedGig.id}:linked-invoice` }
      )
      return
    }

    setSelectedInvoiceId(invoiceId)
    setActiveSection('invoices')
  }

  const openInvoiceLineGig = (gigId: string) => {
    if (!selectGig(gigId)) {
      return
    }

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
  const pendingAccessRequestCount = accessRequests.length
  const dashboardCards = useMemo(
    () =>
      getDashboardCards({
        activeSection,
        clients,
        gigs,
        invoices,
        isWorkspaceLoading: isLoading,
        paidIncomeSummary,
        today: new Date().toISOString().slice(0, 10),
      }),
    [activeSection, clients, gigs, invoices, isLoading, paidIncomeSummary]
  )

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
      resetAccessRequestsWorkspace()
      setIsAccessRequestsOpen(false)
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

  const openAccessRequests = () => {
    closeProfileMenu()
    setIsAccessRequestsOpen(true)
    void loadAccessRequests()
  }

  const closeAccessRequests = () => {
    setIsAccessRequestsOpen(false)
  }

  const openPreviewedInvoice = () => {
    if (invoicePreviewInvoice) {
      setInvoices((current) => [
        invoicePreviewInvoice,
        ...current.filter((invoice) => invoice.id !== invoicePreviewInvoice.id),
      ])
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
      notifications.info(
        `Invoice ${invoice.invoiceNumber} issued; linked gig status left unchanged.`,
        { dedupeKey: `invoice:${invoice.id}:linked-gig-status` }
      )
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
      notifications.success(
        completedGigs.length === 1
          ? `Invoice ${invoice.invoiceNumber} issued; linked gig marked as completed.`
          : `Invoice ${invoice.invoiceNumber} issued; ${completedGigs.length} linked gigs marked as completed.`,
        { dedupeKey: `invoice:${invoice.id}:linked-gig-status` }
      )
    } catch (error) {
      notifications.error(
        error instanceof Error ? error.message : 'Unable to complete linked gig.',
        { dedupeKey: `invoice:${invoice.id}:linked-gig-status` }
      )
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
      notifications.info(`Invoice ${invoice.invoiceNumber} delivered and left as Draft.`, {
        dedupeKey: `invoice:${invoice.id}:delivery-status`,
      })
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
        notifications.success(
          sellerProfile.isInvoiceReady
            ? `Invoice ${generatedInvoice.invoiceNumber} is ready for review.`
            : `Invoice ${generatedInvoice.invoiceNumber} is ready for review. ${sellerProfileNotice}`,
          { dedupeKey: `invoice:${generatedInvoice.id}:generation` }
        )
        await openInvoicePreview(generatedInvoice)
      } catch (error) {
        notifications.error(error instanceof Error ? error.message : 'Unable to generate invoice.', {
          dedupeKey: 'invoice:generation',
        })
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

        notifications.info(conflict.message ?? 'This gig has already been invoiced.', {
          dedupeKey: `gig:${invoiceGig.id}:invoice-generation`,
        })
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
      notifications.success(
        sellerProfile.isInvoiceReady
          ? `Invoice ${generatedInvoice.invoiceNumber} is ready for review.`
          : `Invoice ${generatedInvoice.invoiceNumber} is ready for review. ${sellerProfileNotice}`,
        { dedupeKey: `invoice:${generatedInvoice.id}:generation` }
      )
      await openInvoicePreview(generatedInvoice)
    } catch (error) {
      notifications.error(error instanceof Error ? error.message : 'Unable to generate invoice.', {
        dedupeKey: `gig:${invoiceGig.id}:invoice-generation`,
      })
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  const handleDashboardCardAction = (action: 'invoices-outstanding' | 'invoices-overdue' | 'invoices-income') => {
    setActiveSection('invoices')
    setInvoiceSearchQuery('')
    setInvoiceQuickFilter(
      action === 'invoices-income'
        ? 'income-this-financial-year'
        : action === 'invoices-overdue'
          ? 'overdue'
          : 'outstanding'
    )
    setInvoiceListScrollRequest((request) => request + 1)
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
      notifications.success(
        `Monthly invoice ${updatedInvoice.invoiceNumber} created for ${gigsToInvoice.length} gig(s) and ready for review.`,
        { dedupeKey: `invoice:${updatedInvoice.id}:monthly-generation` }
      )
      await openInvoicePreview(updatedInvoice)
    } catch (error) {
      notifications.error(error instanceof Error ? error.message : 'Unable to generate monthly invoice.', {
        dedupeKey: 'invoice:monthly-generation',
      })
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
        externalResourceForm={externalResourceForm}
        externalResourceMode={externalResourceMode}
        gigForm={gigForm}
        scrollToGigOverviewRequest={gigOverviewScrollRequest}
        isEditorOpen={isGigEditorOpen}
        gigMode={gigMode}
        gigQuickFilter={gigQuickFilter}
        gigTypeFilter={gigTypeFilter}
        gigSearchQuery={gigSearchQuery}
        gigSort={gigSort}
        gigStatus={gigStatus}
        gigs={gigs}
        isGigLoading={isGigLoading}
        isInvoiceLoading={isInvoiceLoading}
        isMileageEstimating={isMileageEstimating}
        isExternalResourceEditorOpen={isExternalResourceEditorOpen}
        onCancelExternalResourceEdit={cancelExternalResourceEdit}
        onCloseEditor={closeGigEditor}
        onDeleteExpenseDraft={deleteExpenseDraft}
        onDeleteExternalResource={deleteExternalResource}
        onDeleteExternalResourceAttachment={deleteExternalResourceAttachment}
        onGenerateExpenseStatement={openExpenseStatement}
        onGenerateInvoice={() => void handleGenerateInvoice()}
        onEstimateMileage={estimateGigMileage}
        onDeleteGig={deleteGig}
        onDownloadExpenseAttachment={downloadExpenseAttachment}
        onDownloadExternalResourceAttachment={downloadExternalResourceAttachment}
        onCloneGig={cloneSelectedGig}
        onOpenClient={openClientShortcut}
        onOpenLinkedInvoice={openSelectedGigInvoice}
        onSessionExpired={expireSession}
        onUploadExpenseAttachment={uploadExpenseAttachment}
        onUploadExternalResourceAttachment={uploadExternalResourceAttachment}
        onDeleteExpenseAttachment={deleteExpenseAttachment}
        onResetForm={startGigCreate}
        onQuickFilterChange={setGigQuickFilter}
        onGigTypeFilterChange={setGigTypeFilter}
        onSearchQueryChange={setGigSearchQuery}
        onSelectGig={selectGig}
        onShowPastGigsChange={setShowPastGigs}
        onSortChange={setGigSort}
        onToggleGigSelection={handleToggleGigSelection}
        onStartEditing={startGigEdit}
        onStartExternalResourceCreate={startExternalResourceCreate}
        onStartExternalResourceEdit={startExternalResourceEdit}
        onSaveExpenseDraft={saveExpenseDraft}
        onSubmit={handleGigSubmit}
        onSubmitExternalResource={submitExternalResource}
        onUpdateExternalResourceField={updateExternalResourceField}
        onUpdateGigField={updateGigField}
        onUpdateExpenseReimbursement={updateExpenseReimbursement}
        plannedGigCount={plannedGigCount}
        selectedGig={selectedGig}
        selectedGigIds={selectedGigIds}
        selectedGigs={selectedGigs}
        showPastGigs={showPastGigs}
      />
    ) : (
      <InvoicesSection
        adjustmentAmount={adjustmentAmount}
        adjustmentReason={adjustmentReason}
        clientNamesById={clientNamesById}
        draftInvoiceCount={draftInvoiceCount}
        filteredInvoices={filteredInvoices}
        isEditorOpen={isInvoiceEditorOpen}
        invoiceQuickFilter={invoiceQuickFilter}
        invoiceDescription={invoiceDescription}
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
        onDeleteAdjustment={handleDeleteInvoiceAdjustment}
        onDeleteInvoice={handleDeleteInvoice}
        onDownloadPdf={handleDownloadInvoicePdf}
        onInvoiceDescriptionChange={setInvoiceDescription}
        onInvoiceDescriptionSave={handleInvoiceDescriptionSave}
        onInvoiceStatusChange={handleInvoiceStatusChangeWithGigPrompt}
        onOpenClient={openClientShortcut}
        onOpenGig={openInvoiceLineGig}
        onOpenSellerProfile={openSellerProfile}
        onPreviewPdf={previewInvoicePdf}
        onPublishGoogleDrive={handlePublishInvoiceGoogleDriveWithIssuePrompt}
        onReissue={handleInvoiceReissueWithPreview}
        onSendEmail={handleSendInvoiceEmailWithIssuePrompt}
        onQuickFilterChange={setInvoiceQuickFilter}
        onSearchQueryChange={setInvoiceSearchQuery}
        onSelectInvoice={setSelectedInvoiceId}
        onSortChange={setInvoiceSort}
        onStartEditing={startInvoiceEdit}
        scrollToListRequest={invoiceListScrollRequest}
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
      dashboardCards={dashboardCards}
      isAdmin={isAdmin}
      isAdminLoading={isAdminLoading}
      isGigLoading={isGigLoading}
      isLoading={isLoading}
      isProfileMenuOpen={isProfileMenuOpen}
      isQuickAttachmentSaving={isQuickAttachmentSaving}
      isQuickReceiptSaving={isQuickReceiptSaving}
      isSellerProfileSaving={isSellerProfileSaving}
      isUserSettingsSaving={isUserSettingsSaving}
      navigationItems={navigationItems}
      pendingGigImportCount={pendingGigImportCount}
      pendingAccessRequestCount={pendingAccessRequestCount}
      onDashboardCardAction={handleDashboardCardAction}
      onOpenGigImports={openGigImports}
      onOpenAccessRequests={openAccessRequests}
      onOpenSellerProfile={openSellerProfile}
      onOpenConnectedServices={openConnectedServices}
      onOpenUserSettings={openUserSettings}
      onProfileMenuToggle={toggleProfileMenu}
      onQuickAttachmentOpen={openQuickAttachmentDialog}
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

      <AccessRequestsModal
        accessRequestStatus={accessRequestStatus}
        accessRequests={accessRequests}
        isLoading={isAccessRequestLoading}
        isOpen={isAccessRequestsOpen}
        onApprove={(approval) => {
          void approveAccessRequest(approval)
        }}
        onClose={closeAccessRequests}
        onDecline={(decisionNote) => {
          void declineAccessRequest(decisionNote)
        }}
        onRefresh={() => {
          void loadAccessRequests(selectedAccessRequest?.id)
        }}
        onSelect={selectAccessRequest}
        selectedAccessRequest={selectedAccessRequest}
      />

      <UserSettingsModal
        form={userSettingsForm}
        invoiceEmailBodyPreview={buildInvoiceEmailBodyPreview(
          userSettingsForm.invoiceEmailBodyTemplate,
          sellerProfile.sellerName
        )}
        invoiceEmailBodyTokens={invoiceEmailBodyTokens}
        invoiceEmailSubjectPreview={buildInvoiceEmailSubjectPreview(
          userSettingsForm.invoiceEmailSubjectPattern,
          null
        )}
        invoiceFilenamePreview={buildInvoiceFilenamePreview(
          userSettingsForm.invoiceFilenamePattern,
          null
        )}
        invoiceFilenameTokens={invoiceFilenameTokens}
        isOpen={isUserSettingsOpen}
        isSaving={isUserSettingsSaving}
        onClose={closeUserSettings}
        onSubmit={handleUserSettingsSubmit}
        onUpdateField={updateUserSettingsField}
        sellerProfilePostcode={sellerProfile.postcode}
        status={userSettingsStatus}
      />

      <ConnectedServicesModal
        forScoreLibrarySnapshot={forScoreSnapshot}
        forScoreLibraryStatus={forScoreLibraryStatus}
        googleCalendarStatus={googleCalendarStatus}
        invoiceUploadFolderId={authUser?.invoiceUploadFolderId ?? userSettingsForm.invoiceUploadFolderId}
        isGoogleCalendarBusy={isGoogleCalendarBusy}
        isGoogleDriveBusy={isGoogleDriveBusy}
        isGoogleDriveConnected={authUser?.isGoogleDriveConnected ?? false}
        isGoogleSheetsBusy={isGoogleSheetsBusy}
        isGoogleSheetsConnected={authUser?.isGoogleSheetsConnected ?? false}
        isForScoreLibraryUploading={isForScoreLibraryUploading}
        isOpen={isConnectedServicesOpen}
        isSaving={isUserSettingsSaving}
        onClose={closeConnectedServices}
        onConnectGoogleCalendar={connectGoogleCalendar}
        onConnectGoogleDrive={connectGoogleDrive}
        onConnectGoogleSheets={connectGoogleSheets}
        onDisconnectGoogleCalendar={disconnectGoogleCalendar}
        onDisconnectGoogleDrive={disconnectGoogleDrive}
        onDisconnectGoogleSheets={disconnectGoogleSheets}
        onForScoreLibraryFile={uploadForScoreLibrary}
        onOpenSettings={openSettingsFromServices}
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

      <QuickAttachmentModal
        candidates={quickAttachmentCandidates}
        clientNamesById={clientNamesById}
        draft={quickAttachmentDraft}
        isPrimary={quickAttachmentIsPrimary}
        isSaving={isQuickAttachmentSaving}
        mode={quickAttachmentMode}
        notes={quickAttachmentNotes}
        onClose={closeQuickAttachmentPrompt}
        onFileChange={handleQuickAttachmentFile}
        onGoToGig={goToQuickAttachmentGig}
        onIsPrimaryChange={setQuickAttachmentIsPrimary}
        onModeLink={startQuickAttachmentLinkMode}
        onNotesChange={setQuickAttachmentNotes}
        onPurposeChange={setQuickAttachmentPurpose}
        onResourceTypeChange={setQuickAttachmentResourceType}
        onSaveDetails={saveQuickAttachmentDetails}
        onSaveDraft={savePendingAttachmentToSelectedGig}
        onSaveLink={saveQuickAttachmentLinkDraft}
        onSelectedGigChange={setQuickAttachmentSelectedGigId}
        onTitleChange={setQuickAttachmentTitle}
        onUrlChange={updateQuickAttachmentUrl}
        pendingFile={pendingAttachmentFile}
        purpose={quickAttachmentPurpose}
        resourceType={quickAttachmentResourceType}
        selectedGigId={quickAttachmentSelectedGigId}
        status={quickAttachmentStatus}
        title={quickAttachmentTitle}
        url={quickAttachmentUrl}
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
        onSessionExpired={expireSession}
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
