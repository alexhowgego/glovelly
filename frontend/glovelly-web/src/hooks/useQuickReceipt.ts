import { useCallback, useState } from 'react'
import {
  buildApiUrl,
  fetchWithSession,
  getResponseErrorMessage,
  handleSessionExpired,
  jsonRequestInit,
} from '../api'
import type {
  Gig,
  Invoice,
  QuickReceiptCandidate,
  QuickReceiptDraftResponse,
  QuickReceiptDraftUpdateResponse,
} from '../types'
import { getQuickCaptureCandidatePage } from '../quickCaptureCandidates'

type UseQuickReceiptOptions = {
  getGigById: (gigId: string) => Gig | undefined
  onMergeInvoices: (invoices: Invoice[]) => void
  onMergeSavedGig: (gig: Gig) => void
  onOpenReceiptDraft: (gig: Gig, scrollToGig?: boolean) => void
  onSelectGig: (gigId: string) => void
  onSessionExpired: (message: string) => void
  setGigStatus: (status: string) => void
}

export function useQuickReceipt({
  getGigById,
  onMergeInvoices,
  onMergeSavedGig,
  onOpenReceiptDraft,
  onSelectGig,
  onSessionExpired,
}: UseQuickReceiptOptions) {
  const [pendingReceiptFile, setPendingReceiptFile] = useState<File | null>(null)
  const [quickReceiptDraft, setQuickReceiptDraft] =
    useState<QuickReceiptDraftResponse | null>(null)
  const [quickReceiptCandidates, setQuickReceiptCandidates] =
    useState<QuickReceiptCandidate[]>([])
  const [quickReceiptSelectedGigId, setQuickReceiptSelectedGigId] = useState('')
  const [quickReceiptAmount, setQuickReceiptAmount] = useState('')
  const [quickReceiptDescription, setQuickReceiptDescription] = useState('')
  const [quickReceiptStatus, setQuickReceiptStatus] = useState('')
  const [isQuickReceiptSaving, setIsQuickReceiptSaving] = useState(false)
  const [quickReceiptContinuation, setQuickReceiptContinuation] = useState<string | null>(null)
  const [quickReceiptHasMoreCandidates, setQuickReceiptHasMoreCandidates] = useState(false)
  const [isQuickReceiptLoadingCandidates, setIsQuickReceiptLoadingCandidates] = useState(false)
  const [quickReceiptCandidateLoadError, setQuickReceiptCandidateLoadError] = useState('')

  const setCandidatePage = (page: { hasMore: boolean; continuation: string | null }) => {
    setQuickReceiptHasMoreCandidates(page.hasMore)
    setQuickReceiptContinuation(page.continuation)
    setQuickReceiptCandidateLoadError('')
  }

  const promptForReceiptGig = (
    file: File,
    candidates: QuickReceiptCandidate[],
    message: string,
    page?: { hasMoreCandidates?: boolean; candidateContinuation?: string | null }
  ) => {
    setPendingReceiptFile(file)
    setQuickReceiptDraft(null)
    setQuickReceiptCandidates(candidates)
    setQuickReceiptSelectedGigId(candidates[0]?.id ?? '')
    setQuickReceiptAmount('')
    setQuickReceiptDescription('Receipt draft')
    setQuickReceiptStatus(message)
    setQuickReceiptHasMoreCandidates(page?.hasMoreCandidates ?? false)
    setQuickReceiptContinuation(page?.candidateContinuation ?? null)
    setQuickReceiptCandidateLoadError('')
  }

  const clearQuickReceiptDialog = useCallback(() => {
    setPendingReceiptFile(null)
    setQuickReceiptDraft(null)
    setQuickReceiptCandidates([])
    setQuickReceiptSelectedGigId('')
    setQuickReceiptAmount('')
    setQuickReceiptDescription('')
    setQuickReceiptStatus('')
    setQuickReceiptContinuation(null)
    setQuickReceiptHasMoreCandidates(false)
    setIsQuickReceiptLoadingCandidates(false)
    setQuickReceiptCandidateLoadError('')
  }, [])

  const uploadQuickReceiptDraft = async (file: File, gigId?: string) => {
    const formData = new FormData()
    formData.append('file', file)
    if (gigId) {
      formData.append('gigId', gigId)
    }

    setIsQuickReceiptSaving(true)
    setQuickReceiptStatus('Saving receipt draft...')
    setPendingReceiptFile(file)
    setQuickReceiptDraft(null)
    if (!gigId) {
      setQuickReceiptCandidates([])
      setQuickReceiptSelectedGigId('')
      setQuickReceiptAmount('')
      setQuickReceiptDescription('')
    }

    try {
      const response = await fetchWithSession(buildApiUrl('/gigs/receipt-drafts'), {
        method: 'POST',
        body: formData,
      })

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to add receipts.'
        )
      ) {
        return
      }

      if (response.status === 409) {
        const conflict = (await response.json()) as {
          message?: string
          candidates?: QuickReceiptCandidate[]
          hasMoreCandidates?: boolean
          candidateContinuation?: string | null
        }
        promptForReceiptGig(
          file,
          conflict.candidates ?? [],
          conflict.message ?? 'Choose a gig before saving this receipt draft.',
          conflict
        )
        return
      }

      if (!response.ok) {
        throw new Error(
          await getResponseErrorMessage(response, 'Unable to save receipt draft.')
        )
      }

      const receiptDraft = (await response.json()) as QuickReceiptDraftResponse
      onOpenReceiptDraft(receiptDraft.gig)
      setPendingReceiptFile(null)
      setQuickReceiptDraft(receiptDraft)
        setQuickReceiptCandidates(receiptDraft.candidates)
        setCandidatePage({ hasMore: receiptDraft.hasMoreCandidates, continuation: receiptDraft.candidateContinuation })
      setQuickReceiptSelectedGigId(receiptDraft.gig.id)
      setQuickReceiptAmount('')
      setQuickReceiptDescription('Receipt draft')
      setQuickReceiptStatus(
        receiptDraft.hasNearbyCandidates
          ? 'Receipt saved. There are other nearby gigs, so please check the selected gig.'
          : 'Receipt saved. Add details now or come back later.'
      )
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unable to save receipt draft.'
      setQuickReceiptStatus(message)
    } finally {
      setIsQuickReceiptSaving(false)
    }
  }

  const handleQuickReceiptFile = (file: File) => {
    void uploadQuickReceiptDraft(file)
  }

  const savePendingReceiptToSelectedGig = () => {
    if (!pendingReceiptFile || !quickReceiptSelectedGigId) {
      setQuickReceiptStatus('Choose a gig before saving this receipt draft.')
      return
    }

    void uploadQuickReceiptDraft(pendingReceiptFile, quickReceiptSelectedGigId)
  }

  const loadMoreQuickReceiptCandidates = async () => {
    if (!quickReceiptHasMoreCandidates || !quickReceiptContinuation || isQuickReceiptLoadingCandidates) {
      return
    }

    setIsQuickReceiptLoadingCandidates(true)
    setQuickReceiptCandidateLoadError('')
    try {
      const { response, page } = await getQuickCaptureCandidatePage(quickReceiptContinuation)
      if (handleSessionExpired(response, onSessionExpired, 'Your session expired. Sign in again to load gigs.')) {
        return
      }
      if (!response.ok || !page) {
        throw new Error(await getResponseErrorMessage(response, 'Unable to load more gigs.'))
      }

      setQuickReceiptCandidates((current) => {
        const knownIds = new Set(current.map((candidate) => candidate.id))
        return [...current, ...page.candidates.filter((candidate) => !knownIds.has(candidate.id))]
      })
      setCandidatePage(page)
    } catch (error) {
      setQuickReceiptCandidateLoadError(
        error instanceof Error ? error.message : 'Unable to load more gigs. Try again.'
      )
    } finally {
      setIsQuickReceiptLoadingCandidates(false)
    }
  }

  const saveQuickReceiptDetails = async (details?: { description: string; amount: string }) => {
    if (!quickReceiptDraft || !quickReceiptSelectedGigId) {
      setQuickReceiptStatus('Choose a gig before saving this receipt draft.')
      return
    }

    const description = (details?.description ?? quickReceiptDescription).trim()
    const amount = Number((details?.amount ?? quickReceiptAmount) || '0')

    if (!description) {
      setQuickReceiptStatus('Add a description before saving receipt details.')
      return
    }

    if (!Number.isFinite(amount) || amount < 0) {
      setQuickReceiptStatus('Receipt amount must be a valid non-negative number.')
      return
    }

    setIsQuickReceiptSaving(true)
    setQuickReceiptStatus('Saving receipt details...')

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/gigs/receipt-drafts/${quickReceiptDraft.expenseId}`),
        jsonRequestInit('PATCH', {
            gigId: quickReceiptSelectedGigId,
            description,
            amount,
          })
      )

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to add receipts.'
        )
      ) {
        return
      }

      if (!response.ok) {
        throw new Error(
          await getResponseErrorMessage(response, 'Unable to save receipt details.')
        )
      }

      const update = (await response.json()) as QuickReceiptDraftUpdateResponse
      onMergeSavedGig(update.gig)
      if (update.previousGig) {
        onMergeSavedGig(update.previousGig)
      }
      onMergeInvoices(update.invoices)

      setQuickReceiptDraft((current) =>
        current
          ? {
              ...current,
              gig: update.gig,
              expenseId: update.expenseId,
              inferredGig: false,
            }
          : current
      )
      onSelectGig(update.gig.id)
      setQuickReceiptSelectedGigId(update.gig.id)
      const failedInvoice = update.invoices.find((invoice) => invoice.documentState !== 'Current')
      if (failedInvoice) {
        setQuickReceiptStatus(
          `Receipt details saved, but ${failedInvoice.invoiceNumber} PDF is unavailable. Retry it from Invoices.`
        )
      } else if (update.invoices.length > 0) {
        setQuickReceiptStatus(
          update.moved
            ? 'Receipt moved, details saved, and linked draft invoices refreshed.'
            : 'Receipt details saved and linked draft invoices refreshed.'
        )
      } else {
        setQuickReceiptStatus(
          update.moved
            ? 'Receipt moved and details saved. You can continue editing or go to the gig.'
            : 'Receipt details saved. You can continue editing or go to the gig.'
        )
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unable to save receipt details.'
      setQuickReceiptStatus(message)
    } finally {
      setIsQuickReceiptSaving(false)
    }
  }

  const goToQuickReceiptGig = () => {
    const targetGig =
      getGigById(quickReceiptSelectedGigId) ?? quickReceiptDraft?.gig ?? null
    if (!targetGig) {
      return
    }

    onOpenReceiptDraft(targetGig, true)
    clearQuickReceiptDialog()
  }

  const closeQuickReceiptPrompt = () => {
    if (isQuickReceiptSaving) {
      return
    }

    clearQuickReceiptDialog()
  }

  return {
    clearQuickReceiptDialog,
    closeQuickReceiptPrompt,
    goToQuickReceiptGig,
    handleQuickReceiptFile,
    isQuickReceiptSaving,
    isQuickReceiptLoadingCandidates,
    pendingReceiptFile,
    quickReceiptAmount,
    quickReceiptCandidates,
    quickReceiptCandidateLoadError,
    quickReceiptDescription,
    quickReceiptHasMoreCandidates,
    quickReceiptDraft,
    quickReceiptSelectedGigId,
    quickReceiptStatus,
    savePendingReceiptToSelectedGig,
    loadMoreQuickReceiptCandidates,
    saveQuickReceiptDetails,
    setQuickReceiptAmount,
    setQuickReceiptDescription,
    setQuickReceiptSelectedGigId,
  }
}
