import { useCallback, useEffect, useRef, useState } from 'react'
import {
  buildApiUrl,
  fetchWithSession,
  getProblemDetailsMessage,
  handleSessionExpired,
  jsonRequestInit,
  parseProblemDetails,
} from '../api'
import type {
  Gig,
  GigImportBatchDetail,
  GigImportBatchSummary,
  GigImportCommitResult,
  GigImportDraft,
  GigImportDraftConfidence,
  GigImportDraftStatus,
} from '../types'

type UseGigImportsWorkspaceOptions = {
  onGigsCommitted: (gigs: Gig[], message: string) => void
  onSessionExpired: (message: string) => void
}

export type GigImportDraftField =
  | 'proposedClientId'
  | 'clientName'
  | 'title'
  | 'date'
  | 'venueName'
  | 'venueAddress'
  | 'postcode'
  | 'fee'
  | 'perDiem'
  | 'notes'
  | 'accommodationNotes'
  | 'travelNotes'
  | 'sourceReference'
  | 'confidence'
  | 'status'

function problemMessage(problem: Awaited<ReturnType<typeof parseProblemDetails>>) {
  return getProblemDetailsMessage(problem)
}

function toDraftPayload(draft: GigImportDraft) {
  return {
    proposedClientId: draft.proposedClientId || null,
    clientName: draft.clientName || null,
    contactName: draft.contactName || null,
    contactEmail: draft.contactEmail || null,
    projectName: draft.projectName || null,
    title: draft.title || null,
    date: draft.date || null,
    arrivalTime: draft.arrivalTime || null,
    rehearsalStartTime: draft.rehearsalStartTime || null,
    rehearsalEndTime: draft.rehearsalEndTime || null,
    showStartTime: draft.showStartTime || null,
    showEndTime: draft.showEndTime || null,
    venueName: draft.venueName || null,
    venueAddress: draft.venueAddress || null,
    postcode: draft.postcode || null,
    fee: draft.fee ?? null,
    perDiem: draft.perDiem ?? null,
    notes: draft.notes || null,
    accommodationNotes: draft.accommodationNotes || null,
    travelNotes: draft.travelNotes || null,
    sourceReference: draft.sourceReference || null,
    confidence: draft.confidence,
    warnings: draft.warnings,
    status: draft.status,
  }
}

function summarizeBatch(detail: GigImportBatchDetail): GigImportBatchSummary {
  return {
    ...detail.batch,
    draftCount: detail.drafts.length,
    pendingCount: detail.drafts.filter((draft) => draft.status === 'Pending').length,
    acceptedCount: detail.drafts.filter((draft) => draft.status === 'Accepted').length,
    rejectedCount: detail.drafts.filter((draft) => draft.status === 'Rejected').length,
    committedCount: detail.drafts.filter((draft) => draft.status === 'Committed').length,
    lowConfidenceCount: detail.drafts.filter((draft) => draft.confidence === 'Low').length,
    mediumConfidenceCount: detail.drafts.filter((draft) => draft.confidence === 'Medium').length,
    highConfidenceCount: detail.drafts.filter((draft) => draft.confidence === 'High').length,
  }
}

function orderDrafts(
  drafts: GigImportDraft[],
  batchStatus: GigImportBatchSummary['status'],
  areRejectedRowsCommitted = false
) {
  return [...drafts].sort((left, right) => {
    const leftHandled =
      left.status === 'Committed' ||
      ((batchStatus !== 'Draft' || areRejectedRowsCommitted) && left.status === 'Rejected')
    const rightHandled =
      right.status === 'Committed' ||
      ((batchStatus !== 'Draft' || areRejectedRowsCommitted) && right.status === 'Rejected')
    if (leftHandled !== rightHandled) {
      return leftHandled ? 1 : -1
    }

    const dateComparison = (left.date ?? '9999-12-31').localeCompare(right.date ?? '9999-12-31')
    if (dateComparison !== 0) {
      return dateComparison
    }

    return (left.title ?? '').localeCompare(right.title ?? '')
  })
}

export function useGigImportsWorkspace({
  onGigsCommitted,
  onSessionExpired,
}: UseGigImportsWorkspaceOptions) {
  const [batches, setBatches] = useState<GigImportBatchSummary[]>([])
  const [selectedBatchId, setSelectedBatchId] = useState('')
  const [batchDetail, setBatchDetail] = useState<GigImportBatchDetail | null>(null)
  const [gigImportStatus, setGigImportStatus] = useState('')
  const [isGigImportLoading, setIsGigImportLoading] = useState(false)
  const autoSaveTimersRef = useRef<Map<string, number>>(new Map())
  const committedDecisionBatchIdsRef = useRef<Set<string>>(new Set())

  const selectedBatch =
    batches.find((batch) => batch.batchId === selectedBatchId) ?? batches[0] ?? null

  useEffect(() => {
    const timers = autoSaveTimersRef.current
    return () => {
      timers.forEach((timerId) => window.clearTimeout(timerId))
      timers.clear()
    }
  }, [])

  const applyGigImportBatches = useCallback((nextBatches: GigImportBatchSummary[]) => {
    setBatches(nextBatches)
    setSelectedBatchId((current) =>
      current && nextBatches.some((batch) => batch.batchId === current)
        ? current
        : nextBatches[0]?.batchId ?? ''
    )
    setGigImportStatus(
      nextBatches.length > 0
        ? 'Review staged rows before creating gigs.'
        : 'No staged gig imports yet.'
    )
  }, [])

  const loadGigImportBatches = useCallback(async (silent = false) => {
    if (!silent) {
      setIsGigImportLoading(true)
    }

    try {
      const response = await fetchWithSession(buildApiUrl('/gig-imports'))
      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to review gig imports.'
        )
      ) {
        return
      }

      if (!response.ok) {
        throw new Error('Unable to load gig imports.')
      }

      const loadedBatches = (await response.json()) as GigImportBatchSummary[]
      setBatches(loadedBatches)
      setSelectedBatchId((current) => current || loadedBatches[0]?.batchId || '')
      if (!silent) {
        setGigImportStatus(
          loadedBatches.length > 0
            ? 'Review staged rows before creating gigs.'
            : 'No staged gig imports yet.'
        )
      }
    } catch (error) {
      if (!silent) {
        setGigImportStatus(error instanceof Error ? error.message : 'Unable to load gig imports.')
      }
    } finally {
      if (!silent) {
        setIsGigImportLoading(false)
      }
    }
  }, [onSessionExpired])

  const loadGigImportBatch = useCallback(
    async (batchId: string) => {
      if (!batchId) {
        setBatchDetail(null)
        return
      }

      setIsGigImportLoading(true)

      try {
        const response = await fetchWithSession(buildApiUrl(`/gig-imports/${batchId}`))
        if (
          handleSessionExpired(
            response,
            onSessionExpired,
            'Your session expired. Sign in again to review gig imports.'
          )
        ) {
          return
        }

        if (!response.ok) {
          throw new Error('Unable to load this import batch.')
        }

        const detail = (await response.json()) as GigImportBatchDetail
        setBatchDetail({
          ...detail,
          drafts: orderDrafts(
            detail.drafts,
            detail.batch.status,
            committedDecisionBatchIdsRef.current.has(detail.batch.batchId)
          ),
        })
        setGigImportStatus('Review staged rows before creating gigs.')
      } catch (error) {
        setGigImportStatus(
          error instanceof Error ? error.message : 'Unable to load this import batch.'
        )
      } finally {
        setIsGigImportLoading(false)
      }
    },
    [onSessionExpired]
  )

  const applyBatchDetail = (detail: GigImportBatchDetail, options?: { decisionsCommitted?: boolean }) => {
    if (options?.decisionsCommitted) {
      committedDecisionBatchIdsRef.current.add(detail.batch.batchId)
    }

    setBatchDetail({
      ...detail,
      drafts: orderDrafts(
        detail.drafts,
        detail.batch.status,
        committedDecisionBatchIdsRef.current.has(detail.batch.batchId)
      ),
    })
    setBatches((current) =>
      current.map((batch) => (batch.batchId === detail.batch.batchId ? detail.batch : batch))
    )
  }

  const selectGigImportBatch = (batchId: string) => {
    setSelectedBatchId(batchId)
    void loadGigImportBatch(batchId)
  }

  const saveGigImportDraft = async (
    draft: GigImportDraft,
    status?: GigImportDraftStatus,
    options?: { silent?: boolean }
  ) => {
    const pendingTimer = autoSaveTimersRef.current.get(draft.draftId)
    if (pendingTimer) {
      window.clearTimeout(pendingTimer)
      autoSaveTimersRef.current.delete(draft.draftId)
    }

    if (!options?.silent) {
      setIsGigImportLoading(true)
    }

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/gig-imports/${draft.batchId}/drafts/${draft.draftId}`),
        jsonRequestInit('PUT', {
            ...toDraftPayload(draft),
            status: status ?? draft.status,
          })
      )

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to review gig imports.'
        )
      ) {
        return null
      }

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        throw new Error(problemMessage(problem) || 'Unable to save import row.')
      }

      const savedDraft = (await response.json()) as GigImportDraft
      setBatchDetail((current) => {
        if (!current) {
          return current
        }

        const nextDetail = {
          ...current,
          drafts: orderDrafts(
            current.drafts.map((value) =>
              value.draftId === savedDraft.draftId ? savedDraft : value
            ),
            current.batch.status
          ),
        }
        const nextSummary = summarizeBatch(nextDetail)
        setBatches((currentBatches) =>
          currentBatches.map((batch) =>
            batch.batchId === nextSummary.batchId ? nextSummary : batch
          )
        )
        return {
          ...nextDetail,
          batch: nextSummary,
        }
      })
      if (!options?.silent) {
        setGigImportStatus('Import row saved.')
      }
      return savedDraft
    } catch (error) {
      setGigImportStatus(error instanceof Error ? error.message : 'Unable to save import row.')
      return null
    } finally {
      if (!options?.silent) {
        setIsGigImportLoading(false)
      }
    }
  }

  const scheduleGigImportDraftSave = (draft: GigImportDraft) => {
    const pendingTimer = autoSaveTimersRef.current.get(draft.draftId)
    if (pendingTimer) {
      window.clearTimeout(pendingTimer)
    }

    const nextTimer = window.setTimeout(() => {
      autoSaveTimersRef.current.delete(draft.draftId)
      void saveGigImportDraft(draft, undefined, { silent: true })
    }, 700)
    autoSaveTimersRef.current.set(draft.draftId, nextTimer)
  }

  const updateGigImportDraftField = (
    draftId: string,
    field: GigImportDraftField,
    value: string
  ) => {
    let draftToSave: GigImportDraft | null = null

    setBatchDetail((current) => {
      if (!current) {
        return current
      }

      return {
        ...current,
        drafts: current.drafts.map((draft) => {
          if (draft.draftId !== draftId) {
            return draft
          }

          draftToSave = {
            ...draft,
            [field]:
              field === 'fee' || field === 'perDiem'
                ? value.trim() === ''
                  ? null
                  : Number(value)
                : value,
          }

          return draftToSave
        }),
      }
    })

    if (draftToSave) {
      scheduleGigImportDraftSave(draftToSave)
    }
  }

  const setGigImportDraftStatus = async (
    draft: GigImportDraft,
    status: GigImportDraftStatus
  ) => {
    const savedDraft = await saveGigImportDraft(draft, status)
    if (savedDraft) {
      setGigImportStatus(`Import row marked ${status.toLowerCase()}.`)
    }
  }

  const commitGigImportDecisions = async () => {
    if (!batchDetail) {
      setGigImportStatus('Select an import batch before committing rows.')
      return
    }

    const acceptedCount = batchDetail.drafts.filter((draft) => draft.status === 'Accepted').length
    const rejectedCount = batchDetail.drafts.filter((draft) => draft.status === 'Rejected').length

    if (acceptedCount + rejectedCount === 0) {
      setGigImportStatus('Accept or reject at least one row before committing decisions.')
      return
    }

    setIsGigImportLoading(true)

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/gig-imports/${batchDetail.batch.batchId}/commit`),
        jsonRequestInit('POST', {
            draftIds: [],
            commitAccepted: true,
          })
      )

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to review gig imports.'
        )
      ) {
        return
      }

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        throw new Error(problemMessage(problem) || 'Unable to commit import rows.')
      }

      const result = (await response.json()) as GigImportCommitResult
      applyBatchDetail(result.batch, { decisionsCommitted: true })

      const gigsResponse = await fetchWithSession(buildApiUrl('/gigs'))
      if (gigsResponse.ok) {
        const gigs = (await gigsResponse.json()) as Gig[]
        onGigsCommitted(
          gigs,
          `${result.createdCount} gig${result.createdCount === 1 ? '' : 's'} created from import.`
        )
      }

      setGigImportStatus(
        `${result.createdCount} gig${result.createdCount === 1 ? '' : 's'} created from import.`
      )
    } catch (error) {
      setGigImportStatus(
        error instanceof Error ? error.message : 'Unable to commit import rows.'
      )
    } finally {
      setIsGigImportLoading(false)
    }
  }

  const resetGigImportsWorkspace = useCallback(() => {
    setBatches([])
    setSelectedBatchId('')
    setBatchDetail(null)
    setGigImportStatus('')
    setIsGigImportLoading(false)
  }, [])

  return {
    batchDetail,
    applyGigImportBatches,
    batches,
    commitGigImportDecisions,
    isGigImportLoading,
    loadGigImportBatch,
    loadGigImportBatches,
    resetGigImportsWorkspace,
    saveGigImportDraft,
    selectedBatch,
    selectedBatchId,
    selectGigImportBatch,
    setGigImportDraftStatus,
    gigImportStatus,
    updateGigImportDraftField,
  }
}

export type { GigImportDraftConfidence }
