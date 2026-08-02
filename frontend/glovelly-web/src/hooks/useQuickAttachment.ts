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
  GigExternalResourcePurpose,
  GigExternalResourceType,
  QuickExternalResourceDraftResponse,
  QuickExternalResourceDraftUpdateResponse,
  QuickGigCandidate,
} from '../types'

type UseQuickAttachmentOptions = {
  getGigById: (gigId: string) => Gig | undefined
  getQuickCaptureCandidates: () => QuickGigCandidate[]
  onMergeSavedGig: (gig: Gig) => void
  onOpenAttachmentDraft: (gig: Gig, scrollToGig?: boolean) => void
  onSelectGig: (gigId: string) => void
  onSessionExpired: (message: string) => void
  setGigStatus: (status: string) => void
}

export type QuickAttachmentMode = 'choose' | 'file' | 'link'

export function useQuickAttachment({
  getGigById,
  getQuickCaptureCandidates,
  onMergeSavedGig,
  onOpenAttachmentDraft,
  onSelectGig,
  onSessionExpired,
  setGigStatus,
}: UseQuickAttachmentOptions) {
  const [quickAttachmentMode, setQuickAttachmentMode] = useState<QuickAttachmentMode>('choose')
  const [pendingAttachmentFile, setPendingAttachmentFile] = useState<File | null>(null)
  const [quickAttachmentDraft, setQuickAttachmentDraft] =
    useState<QuickExternalResourceDraftResponse | null>(null)
  const [quickAttachmentCandidates, setQuickAttachmentCandidates] =
    useState<QuickGigCandidate[]>([])
  const [quickAttachmentSelectedGigId, setQuickAttachmentSelectedGigId] = useState('')
  const [quickAttachmentTitle, setQuickAttachmentTitle] = useState('')
  const [quickAttachmentUrl, setQuickAttachmentUrl] = useState('')
  const [quickAttachmentResourceType, setQuickAttachmentResourceType] =
    useState<GigExternalResourceType>('File')
  const [quickAttachmentPurpose, setQuickAttachmentPurpose] =
    useState<GigExternalResourcePurpose>('Other')
  const [quickAttachmentNotes, setQuickAttachmentNotes] = useState('')
  const [quickAttachmentIsPrimary, setQuickAttachmentIsPrimary] = useState(false)
  const [quickAttachmentStatus, setQuickAttachmentStatus] = useState('')
  const [isQuickAttachmentSaving, setIsQuickAttachmentSaving] = useState(false)

  const syncDraftFields = (draft: QuickExternalResourceDraftResponse) => {
    const resource = draft.gig.externalResources.find((item) => item.id === draft.resourceId)
    setQuickAttachmentTitle(resource?.title ?? '')
    setQuickAttachmentUrl(resource?.url ?? '')
    setQuickAttachmentResourceType(resource?.resourceType ?? (draft.attachmentId ? 'File' : 'Url'))
    setQuickAttachmentPurpose(resource?.purpose ?? 'Other')
    setQuickAttachmentNotes(resource?.notes ?? '')
    setQuickAttachmentIsPrimary(resource?.isPrimary ?? false)
  }

  const inferResourceType = (url: string): GigExternalResourceType => {
    try {
      const parsed = new URL(url)
      if (parsed.hostname.toLowerCase() === 'docs.google.com') {
        if (parsed.pathname.toLowerCase().startsWith('/spreadsheets/')) {
          return 'GoogleSheet'
        }
        if (parsed.pathname.toLowerCase().startsWith('/document/')) {
          return 'GoogleDoc'
        }
      }
    } catch {
      return 'Url'
    }

    return 'Url'
  }

  const promptForAttachmentGig = (
    mode: QuickAttachmentMode,
    candidates: QuickGigCandidate[],
    message: string,
    file?: File
  ) => {
    setQuickAttachmentMode(mode)
    setPendingAttachmentFile(file ?? null)
    setQuickAttachmentDraft(null)
    setQuickAttachmentCandidates(candidates)
    setQuickAttachmentSelectedGigId(candidates[0]?.id ?? '')
    setQuickAttachmentStatus(message)
  }

  const clearQuickAttachmentDialog = useCallback(() => {
    setQuickAttachmentMode('choose')
    setPendingAttachmentFile(null)
    setQuickAttachmentDraft(null)
    setQuickAttachmentCandidates([])
    setQuickAttachmentSelectedGigId('')
    setQuickAttachmentTitle('')
    setQuickAttachmentUrl('')
    setQuickAttachmentResourceType('File')
    setQuickAttachmentPurpose('Other')
    setQuickAttachmentNotes('')
    setQuickAttachmentIsPrimary(false)
    setQuickAttachmentStatus('')
  }, [])

  const openQuickAttachmentDialog = () => {
    clearQuickAttachmentDialog()
    setQuickAttachmentStatus('Choose how to add this attachment.')
  }

  const uploadQuickAttachmentFileDraft = async (file: File, gigId?: string) => {
    const formData = new FormData()
    formData.append('file', file)
    if (gigId) {
      formData.append('gigId', gigId)
    }

    setIsQuickAttachmentSaving(true)
    setQuickAttachmentMode('file')
    setQuickAttachmentStatus('Saving attachment draft...')
    setPendingAttachmentFile(file)
    setQuickAttachmentDraft(null)
    if (!gigId) {
      setQuickAttachmentCandidates([])
      setQuickAttachmentSelectedGigId('')
    }

    try {
      const response = await fetchWithSession(buildApiUrl('/gigs/external-resource-drafts/file'), {
        method: 'POST',
        body: formData,
      })

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to add attachments.'
        )
      ) {
        return
      }

      if (response.status === 409) {
        const conflict = (await response.json()) as {
          message?: string
          candidates?: QuickGigCandidate[]
        }
        promptForAttachmentGig(
          'file',
          conflict.candidates ?? [],
          conflict.message ?? 'Choose a gig before saving this attachment draft.',
          file
        )
        return
      }

      if (!response.ok) {
        throw new Error(
          await getResponseErrorMessage(response, 'Unable to save attachment draft.')
        )
      }

      const draft = (await response.json()) as QuickExternalResourceDraftResponse
      onMergeSavedGig(draft.gig)
      onOpenAttachmentDraft(draft.gig)
      setPendingAttachmentFile(null)
      setQuickAttachmentDraft(draft)
      setQuickAttachmentCandidates(draft.candidates)
      setQuickAttachmentSelectedGigId(draft.gig.id)
      syncDraftFields(draft)
      setQuickAttachmentStatus(
        draft.hasNearbyCandidates
          ? 'Attachment saved. There are other nearby gigs, so please check the selected gig.'
          : 'Attachment saved. Add details now or come back later.'
      )
      setGigStatus(
        draft.inferredGig
          ? 'Attachment draft saved to the nearest matching gig.'
          : 'Attachment draft saved.'
      )
    } catch (error) {
      setQuickAttachmentStatus(
        error instanceof Error ? error.message : 'Unable to save attachment draft.'
      )
    } finally {
      setIsQuickAttachmentSaving(false)
    }
  }

  const saveQuickAttachmentLinkDraft = async (gigId?: string) => {
    const url = quickAttachmentUrl.trim()
    if (!url) {
      setQuickAttachmentStatus('Paste a link before saving this attachment.')
      return
    }

    setIsQuickAttachmentSaving(true)
    setQuickAttachmentMode('link')
    setQuickAttachmentStatus('Saving attachment draft...')
    setQuickAttachmentDraft(null)
    if (!gigId) {
      setQuickAttachmentCandidates([])
      setQuickAttachmentSelectedGigId('')
    }

    try {
      const response = await fetchWithSession(
        buildApiUrl('/gigs/external-resource-drafts/link'),
        jsonRequestInit('POST', {
          gigId: gigId || undefined,
          url,
          title: quickAttachmentTitle.trim() || undefined,
          resourceType: quickAttachmentResourceType,
          purpose: quickAttachmentPurpose,
          notes: quickAttachmentNotes.trim() || undefined,
          isPrimary: quickAttachmentIsPrimary,
        })
      )

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to add attachments.'
        )
      ) {
        return
      }

      if (response.status === 409) {
        const conflict = (await response.json()) as {
          message?: string
          candidates?: QuickGigCandidate[]
        }
        promptForAttachmentGig(
          'link',
          conflict.candidates ?? [],
          conflict.message ?? 'Choose a gig before saving this attachment draft.'
        )
        return
      }

      if (!response.ok) {
        throw new Error(
          await getResponseErrorMessage(response, 'Unable to save attachment draft.')
        )
      }

      const draft = (await response.json()) as QuickExternalResourceDraftResponse
      onMergeSavedGig(draft.gig)
      onOpenAttachmentDraft(draft.gig)
      setQuickAttachmentDraft(draft)
      setQuickAttachmentCandidates(draft.candidates)
      setQuickAttachmentSelectedGigId(draft.gig.id)
      syncDraftFields(draft)
      setQuickAttachmentStatus(
        draft.hasNearbyCandidates
          ? 'Attachment saved. There are other nearby gigs, so please check the selected gig.'
          : 'Attachment saved. Add details now or come back later.'
      )
      setGigStatus(
        draft.inferredGig
          ? 'Attachment draft saved to the nearest matching gig.'
          : 'Attachment draft saved.'
      )
    } catch (error) {
      setQuickAttachmentStatus(
        error instanceof Error ? error.message : 'Unable to save attachment draft.'
      )
    } finally {
      setIsQuickAttachmentSaving(false)
    }
  }

  const handleQuickAttachmentFile = (file: File) => {
    void uploadQuickAttachmentFileDraft(file)
  }

  const savePendingAttachmentToSelectedGig = () => {
    if (quickAttachmentMode === 'file') {
      if (!pendingAttachmentFile || !quickAttachmentSelectedGigId) {
        setQuickAttachmentStatus('Choose a gig before saving this attachment draft.')
        return
      }

      void uploadQuickAttachmentFileDraft(pendingAttachmentFile, quickAttachmentSelectedGigId)
      return
    }

    if (!quickAttachmentSelectedGigId) {
      setQuickAttachmentStatus('Choose a gig before saving this attachment draft.')
      return
    }

    void saveQuickAttachmentLinkDraft(quickAttachmentSelectedGigId)
  }

  const saveQuickAttachmentDetails = async () => {
    if (!quickAttachmentDraft || !quickAttachmentSelectedGigId) {
      setQuickAttachmentStatus('Choose a gig before saving this attachment draft.')
      return
    }

    const title = quickAttachmentTitle.trim()
    if (!title) {
      setQuickAttachmentStatus('Add a title before saving attachment details.')
      return
    }

    setIsQuickAttachmentSaving(true)
    setQuickAttachmentStatus('Saving attachment details...')

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/gigs/external-resource-drafts/${quickAttachmentDraft.resourceId}`),
        jsonRequestInit('PATCH', {
          gigId: quickAttachmentSelectedGigId,
          resourceType: quickAttachmentResourceType,
          purpose: quickAttachmentPurpose,
          title,
          url: quickAttachmentUrl.trim() || null,
          notes: quickAttachmentNotes.trim() || null,
          isPrimary: quickAttachmentIsPrimary,
        })
      )

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to add attachments.'
        )
      ) {
        return
      }

      if (!response.ok) {
        throw new Error(
          await getResponseErrorMessage(response, 'Unable to save attachment details.')
        )
      }

      const update = (await response.json()) as QuickExternalResourceDraftUpdateResponse
      onMergeSavedGig(update.gig)
      if (update.previousGig) {
        onMergeSavedGig(update.previousGig)
      }

      setQuickAttachmentDraft((current) =>
        current
          ? {
              ...current,
              gig: update.gig,
              resourceId: update.resourceId,
              inferredGig: false,
            }
          : current
      )
      onSelectGig(update.gig.id)
      setQuickAttachmentSelectedGigId(update.gig.id)
      setGigStatus('Attachment details saved.')
      setQuickAttachmentStatus(
        update.moved
          ? 'Attachment moved and details saved.'
          : 'Attachment details saved.'
      )
    } catch (error) {
      setQuickAttachmentStatus(
        error instanceof Error ? error.message : 'Unable to save attachment details.'
      )
    } finally {
      setIsQuickAttachmentSaving(false)
    }
  }

  const goToQuickAttachmentGig = () => {
    const targetGig =
      getGigById(quickAttachmentSelectedGigId) ?? quickAttachmentDraft?.gig ?? null
    if (!targetGig) {
      return
    }

    onOpenAttachmentDraft(targetGig, true)
    clearQuickAttachmentDialog()
  }

  const closeQuickAttachmentPrompt = () => {
    if (isQuickAttachmentSaving) {
      return
    }

    clearQuickAttachmentDialog()
  }

  const updateQuickAttachmentUrl = (url: string) => {
    setQuickAttachmentUrl(url)
    if (!quickAttachmentDraft) {
      setQuickAttachmentResourceType(inferResourceType(url))
    }
  }

  const startQuickAttachmentLinkMode = () => {
    const candidates = getQuickCaptureCandidates()
    setQuickAttachmentMode('link')
    setQuickAttachmentResourceType(inferResourceType(quickAttachmentUrl))
    setQuickAttachmentPurpose('Other')
    setQuickAttachmentCandidates(candidates)
    setQuickAttachmentSelectedGigId(candidates[0]?.id ?? '')
    setQuickAttachmentStatus(
      candidates.length > 0
        ? 'Paste a link and choose the destination gig.'
        : 'No nearby gigs are available. Create or update a gig near this attachment date, then try again.'
    )
  }

  const startQuickAttachmentFileMode = () => {
    setQuickAttachmentMode('file')
    setQuickAttachmentResourceType('File')
    setQuickAttachmentPurpose('Other')
    setQuickAttachmentStatus('Choose a file to add as an attachment.')
  }

  return {
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
    saveQuickAttachmentLinkDraft: () => void saveQuickAttachmentLinkDraft(),
    setQuickAttachmentIsPrimary,
    setQuickAttachmentMode,
    setQuickAttachmentNotes,
    setQuickAttachmentPurpose,
    setQuickAttachmentResourceType,
    setQuickAttachmentSelectedGigId,
    setQuickAttachmentTitle,
    startQuickAttachmentFileMode,
    startQuickAttachmentLinkMode,
    updateQuickAttachmentUrl,
  }
}
