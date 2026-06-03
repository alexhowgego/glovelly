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
  QuickReceiptCandidate,
  QuickReceiptDraftResponse,
  QuickReceiptDraftUpdateResponse,
} from '../types'

type UseQuickReceiptOptions = {
  getGigById: (gigId: string) => Gig | undefined
  onMergeSavedGig: (gig: Gig) => void
  onOpenReceiptDraft: (gig: Gig) => void
  onSelectGig: (gigId: string) => void
  onSessionExpired: (message: string) => void
  setGigStatus: (status: string) => void
}

export function useQuickReceipt({
  getGigById,
  onMergeSavedGig,
  onOpenReceiptDraft,
  onSelectGig,
  onSessionExpired,
  setGigStatus,
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

  const promptForReceiptGig = (
    file: File,
    candidates: QuickReceiptCandidate[],
    message: string
  ) => {
    setPendingReceiptFile(file)
    setQuickReceiptDraft(null)
    setQuickReceiptCandidates(candidates)
    setQuickReceiptSelectedGigId(candidates[0]?.id ?? '')
    setQuickReceiptAmount('')
    setQuickReceiptDescription('Receipt draft')
    setQuickReceiptStatus(message)
  }

  const clearQuickReceiptDialog = useCallback(() => {
    setPendingReceiptFile(null)
    setQuickReceiptDraft(null)
    setQuickReceiptCandidates([])
    setQuickReceiptSelectedGigId('')
    setQuickReceiptAmount('')
    setQuickReceiptDescription('')
    setQuickReceiptStatus('')
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
        }
        promptForReceiptGig(
          file,
          conflict.candidates ?? [],
          conflict.message ?? 'Choose a gig before saving this receipt draft.'
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
      setQuickReceiptSelectedGigId(receiptDraft.gig.id)
      setQuickReceiptAmount('')
      setQuickReceiptDescription('Receipt draft')
      setQuickReceiptStatus(
        receiptDraft.hasNearbyCandidates
          ? 'Receipt saved. There are other nearby gigs, so please check the selected gig.'
          : 'Receipt saved. Add details now or come back later.'
      )
      setGigStatus(
        receiptDraft.inferredGig
          ? 'Receipt draft saved to the nearest matching gig.'
          : 'Receipt draft saved. Add the amount and description when you are ready.'
      )
    } catch (error) {
      setQuickReceiptStatus(
        error instanceof Error ? error.message : 'Unable to save receipt draft.'
      )
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

  const saveQuickReceiptDetails = async () => {
    if (!quickReceiptDraft || !quickReceiptSelectedGigId) {
      setQuickReceiptStatus('Choose a gig before saving this receipt draft.')
      return
    }

    const description = quickReceiptDescription.trim()
    const amount = Number(quickReceiptAmount || '0')

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
      setGigStatus('Receipt details saved.')
      setQuickReceiptStatus(
        update.moved
          ? 'Receipt moved and details saved.'
          : 'Receipt details saved.'
      )
    } catch (error) {
      setQuickReceiptStatus(
        error instanceof Error ? error.message : 'Unable to save receipt details.'
      )
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

    onOpenReceiptDraft(targetGig)
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
  }
}
