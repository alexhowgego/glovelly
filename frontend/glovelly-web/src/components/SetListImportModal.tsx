import { useEffect, useRef, useState } from 'react'
import {
  buildApiUrl,
  downloadResponseBlob,
  fetchWithSession,
  getProblemDetailsMessage,
  getResponseErrorMessage,
  jsonRequestInit,
} from '../api'
import { useWorkspaceEvents } from '../hooks/useWorkspaceEvents'
import { notifications } from '../notifications'
import type {
  Gig,
  GigExternalResource,
  GigSetListImport,
  GigSetListImportItemDraft,
  GigSetListSourceGrid,
  GigSetListSource,
  SetListChartMatchJobResponse,
  SetListChartMatchResult,
  SetListInterpretationJobResponse,
} from '../types'

type SetListImportModalProps = {
  gig: Gig
  resource: GigExternalResource
  onClose: () => void
}

type ImportPhase = 'idle' | 'loadingWorksheets' | 'interpretingSetList' | 'saving'
type ManagedSetListItem = GigSetListImportItemDraft & { id?: string; forScoreMapping?: GigSetListImport['items'][number]['forScoreMapping'] }
type ChartMatchStage = 'locate' | 'ai'
type ChartMatchRequestItem = Pick<ManagedSetListItem, 'itemId' | 'sourceRowNumber' | 'kind' | 'include' | 'title' | 'padNumber' | 'key'>
type ActiveChartMatchJob = Pick<SetListChartMatchJobResponse, 'jobId' | 'status' | 'correlationId'>
type ActiveInterpretationJob = Pick<SetListInterpretationJobResponse, 'jobId' | 'status' | 'correlationId'>
type ForScoreExportError = {
  detail?: string
  message?: string
  missingItems?: { title?: string; sourceRowNumber?: number }[]
  errors?: Record<string, string[]>
  title?: string
}

const createClientRequestId = () => {
  if ('randomUUID' in crypto) {
    return crypto.randomUUID()
  }

  return `${Date.now()}-${Math.random().toString(36).slice(2)}`
}

const getPayloadBytes = (value: unknown) => new Blob([JSON.stringify(value)]).size

async function getExportErrorMessage(response: Response) {
  const contentType = response.headers.get('content-type') ?? ''
  if (!contentType.includes('application/json') && !contentType.includes('+json')) {
    return 'Unable to export forScore set list.'
  }

  try {
    const payload = (await response.json()) as ForScoreExportError
    const problemMessage = getProblemDetailsMessage(payload)
    const missingTitles = payload.missingItems
      ?.slice(0, 3)
      .map((item) => item.title || (item.sourceRowNumber ? `row ${item.sourceRowNumber}` : null))
      .filter(Boolean)
      .join(', ')
    const missingSuffix = missingTitles ? ` Remaining rows: ${missingTitles}.` : ''
    return `${problemMessage || payload.message || 'Select forScore charts for all included song rows before exporting.'}${missingSuffix}`
  } catch {
    return 'Unable to export forScore set list.'
  }
}

const toChartMatchRequestItems = (sourceItems: ManagedSetListItem[]): ChartMatchRequestItem[] => sourceItems
  .filter((item) => item.kind === 'Song' && item.include)
  .map((item) => ({
    itemId: item.itemId,
    sourceRowNumber: item.sourceRowNumber,
    kind: item.kind,
    include: item.include,
    title: item.title,
    padNumber: item.padNumber,
    key: item.key,
  }))

const applyChartMatches = (sourceItems: ManagedSetListItem[], resultItems: SetListChartMatchResult[]) => {
  const byItemId = new Map(resultItems.map((item) => [item.itemId, item]))
  return sourceItems.map((item) => {
    const match = byItemId.get(item.itemId)
    return match ? { ...item, forScoreMatch: match, forScoreChartId: match.selectedChart?.id ?? item.forScoreChartId } : item
  })
}

const getChartMatchRowSignature = (item: ManagedSetListItem) => JSON.stringify({
  sourceRowNumber: item.sourceRowNumber,
  kind: item.kind,
  include: item.include,
  title: item.title,
  padNumber: item.padNumber,
  key: item.key,
})

const applyChartMatchesPreservingEdits = (
  sourceItems: ManagedSetListItem[],
  resultItems: SetListChartMatchResult[],
  jobStartedItemSignatures: Map<string, string>
) => {
  const byItemId = new Map(resultItems.map((item) => [item.itemId, item]))
  return sourceItems.map((item) => {
    const match = byItemId.get(item.itemId)
    if (!match || jobStartedItemSignatures.get(item.itemId) !== getChartMatchRowSignature(item)) {
      return item
    }

    return { ...item, forScoreMatch: match, forScoreChartId: match.selectedChart?.id ?? item.forScoreChartId }
  })
}

const serializeItemsForDirty = (items: ManagedSetListItem[]) => JSON.stringify(items.map((item) => ({
  itemId: item.itemId,
  id: item.id ?? null,
  sourceRowNumber: item.sourceRowNumber,
  sortOrder: item.sortOrder,
  kind: item.kind,
  include: item.include,
  section: item.section,
  padNumber: item.padNumber,
  key: item.key,
  title: item.title,
  notes: item.notes,
  rawCellsJson: item.rawCellsJson,
  sourceEvidenceJson: item.sourceEvidenceJson,
  confidence: item.confidence,
  forScoreChartId: item.forScoreChartId,
  forScoreMatch: item.forScoreMatch,
})))

const normalizeDraftItems = (draftItems: GigSetListImportItemDraft[]): ManagedSetListItem[] => draftItems.map((item) => ({
  ...item,
  // Saved imports created before draft IDs use their persistent item ID.
  itemId: item.itemId || ('id' in item && typeof item.id === 'string' ? item.id : createClientRequestId()),
  sourceEvidenceJson: item.sourceEvidenceJson ?? null,
}))

const getInterpretationResultItems = (job: SetListInterpretationJobResponse) => {
  const compatibleJob = job as SetListInterpretationJobResponse & {
    items?: GigSetListImportItemDraft[] | null
    result?: GigSetListImportItemDraft[] | { items?: GigSetListImportItemDraft[] } | null
  }
  return job.resultItems ?? compatibleJob.items ?? (Array.isArray(compatibleJob.result) ? compatibleJob.result : compatibleJob.result?.items) ?? []
}

const interpretationStorageKey = (gigId: string, resourceId: string, worksheetId: string) =>
  `glovelly:setlist-interpretation:${gigId}:${resourceId}:${worksheetId}`

export function SetListImportModal({ gig, resource, onClose }: SetListImportModalProps) {
  const [source, setSource] = useState<GigSetListSource | null>(null)
  const [activeImport, setActiveImport] = useState<GigSetListImport | null>(null)
  const [selectedWorksheetId, setSelectedWorksheetId] = useState('')
  const [draftWorksheet, setDraftWorksheet] = useState<{ id: string; name: string } | null>(null)
  const [sourceGrid, setSourceGrid] = useState<GigSetListSourceGrid | null>(null)
  const [items, setItems] = useState<ManagedSetListItem[]>([])
  const [expandedItemKey, setExpandedItemKey] = useState('')
  const [status, setStatus] = useState('Loading worksheets...')
  const [isLoading, setIsLoading] = useState(false)
  const [phase, setPhase] = useState<ImportPhase>('loadingWorksheets')
  const [needsSheetsConnection, setNeedsSheetsConnection] = useState(false)
  const [baselineItemsJson, setBaselineItemsJson] = useState('')
  const [activeChartMatchJob, setActiveChartMatchJob] = useState<ActiveChartMatchJob | null>(null)
  const [activeInterpretationJob, setActiveInterpretationJob] = useState<ActiveInterpretationJob | null>(null)
  const activeChartMatchJobIdRef = useRef<string | null>(null)
  const activeJobItemSignaturesRef = useRef<Map<string, string>>(new Map())
  const isFetchingJobStatusRef = useRef(false)
  const fetchChartMatchJobStatusRef = useRef<(jobId: string) => void>(() => {})
  const isFetchingInterpretationStatusRef = useRef(false)
  const fetchInterpretationStatusRef = useRef<(jobId: string) => void>(() => {})

  useEffect(() => {
    let isCancelled = false
    const loadSource = async () => {
      setIsLoading(true)
      setPhase('loadingWorksheets')
      try {
        let hasActiveImport = false
        let activeWorksheetId = ''
        let activeWorksheetName = ''
        const activeResponse = await fetchWithSession(buildApiUrl(`/gigs/${gig.id}/setlist-imports/active`))
        if (isCancelled) {
          return
        }

        if (activeResponse.ok) {
          const loadedImport = (await activeResponse.json()) as GigSetListImport
          if (isCancelled) {
            return
          }

          hasActiveImport = true
          activeWorksheetId = loadedImport.worksheetId ?? ''
          activeWorksheetName = loadedImport.worksheetName
          setActiveImport(loadedImport)
          const savedItems = normalizeDraftItems(loadedImport.items)
          setItems(savedItems)
          setBaselineItemsJson(serializeItemsForDirty(savedItems))
          setDraftWorksheet(null)
          setStatus(`Managing saved set list with ${loadedImport.items.filter((item) => item.include).length} included song row(s).`)
        } else if (activeResponse.status !== 404) {
          setStatus((await getResponseErrorMessage(activeResponse, 'Unable to load saved set list.')) ?? 'Unable to load saved set list.')
        }

        const response = await fetchWithSession(
          buildApiUrl(`/gigs/${gig.id}/setlist-imports/source?resourceId=${encodeURIComponent(resource.id)}`)
        )
        if (!response.ok) {
          const message = (await getResponseErrorMessage(response, 'Unable to load Google Sheet worksheets.')) ?? 'Unable to load Google Sheet worksheets.'
          setNeedsSheetsConnection(response.status === 409 && message.toLowerCase().includes('sheets'))
          if (!hasActiveImport) {
            setStatus(message)
          }
          return
        }

        const loadedSource = (await response.json()) as GigSetListSource
        if (isCancelled) {
          return
        }

        setSource(loadedSource)
        setSelectedWorksheetId(
          activeWorksheetId
          || loadedSource.worksheets.find((worksheet) => worksheet.title === activeWorksheetName)?.sheetId
          || loadedSource.worksheets[0]?.sheetId
          || ''
        )

        const worksheetId = activeWorksheetId
          || loadedSource.worksheets.find((worksheet) => worksheet.title === activeWorksheetName)?.sheetId
          || loadedSource.worksheets[0]?.sheetId
          || ''
        const savedJobId = worksheetId && sessionStorage.getItem(interpretationStorageKey(gig.id, resource.id, worksheetId))
        if (!hasActiveImport && savedJobId) {
          fetchInterpretationStatusRef.current(savedJobId)
        }

        if (hasActiveImport) {
          return
        }

        setStatus(
          loadedSource.worksheets.length > 1
            ? 'Choose a worksheet to import rows and locate chart candidates.'
            : 'Import rows from the linked worksheet to locate chart candidates before saving.'
        )
      } catch (error) {
        if (!isCancelled) {
          setStatus(error instanceof Error ? error.message : 'Unable to load Google Sheet worksheets.')
        }
      } finally {
        if (!isCancelled) {
          setIsLoading(false)
          setPhase('idle')
        }
      }
    }

    void loadSource()
    return () => {
      isCancelled = true
    }
  }, [gig.id, resource.id])

  const selectedWorksheet = source?.worksheets.find((worksheet) => worksheet.sheetId === selectedWorksheetId)

  const connectGoogleSheets = () => {
    window.location.href = buildApiUrl('/integrations/google-sheets/connect')
  }

  const requestChartMatches = async (sourceItems: ManagedSetListItem[], useAi: boolean, flowRequestId: string, stage: ChartMatchStage) => {
    const requestId = `${flowRequestId}-${stage}`
    const body = { items: toChartMatchRequestItems(sourceItems), useAi }
    const payloadBytes = getPayloadBytes(body)
    const startedAt = performance.now()
    const includedSongCount = sourceItems.filter((item) => item.kind === 'Song' && item.include).length
    console.info('Set list chart matching request started', {
      requestId,
      stage,
      useAi,
      itemCount: sourceItems.length,
      includedSongCount,
      payloadBytes,
      userAgent: navigator.userAgent,
      online: navigator.onLine,
      visibilityState: document.visibilityState,
    })

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/gigs/${gig.id}/setlist-imports/chart-matches/preview`),
        {
          ...jsonRequestInit('POST', body),
          headers: {
            'Content-Type': 'application/json',
            'X-Glovelly-Request-Id': requestId,
          },
        }
      )
      const elapsedMs = Math.round(performance.now() - startedAt)
      if (!response.ok) {
        const message = (await getResponseErrorMessage(response, 'Unable to match charts.')) ?? 'Unable to match charts.'
        console.warn('Set list chart matching request failed', { requestId, stage, status: response.status, elapsedMs, payloadBytes })
        throw new Error(`${message} (HTTP ${response.status}, request ${requestId})`)
      }

      const result = (await response.json()) as { items: SetListChartMatchResult[] }
      console.info('Set list chart matching request completed', { requestId, stage, elapsedMs, resultCount: result.items.length })
      return result.items
    } catch (error) {
      const elapsedMs = Math.round(performance.now() - startedAt)
      console.warn('Set list chart matching request errored', {
        requestId,
        stage,
        elapsedMs,
        payloadBytes,
        message: error instanceof Error ? error.message : String(error),
      })
      if (error instanceof Error && error.message.includes(`request ${requestId}`)) {
        throw error
      }

      throw new Error(`${error instanceof Error ? error.message : 'Request failed'} (request ${requestId}, stage ${stage})`, { cause: error })
    }
  }

  const startChartMatchJob = async (sourceItems: ManagedSetListItem[], flowRequestId: string) => {
    const requestId = `${flowRequestId}-ai-job`
    const body = { items: toChartMatchRequestItems(sourceItems) }
    const response = await fetchWithSession(
      buildApiUrl(`/gigs/${gig.id}/setlist-imports/chart-matches/ai-jobs`),
      {
        ...jsonRequestInit('POST', body),
        headers: {
          'Content-Type': 'application/json',
          'X-Glovelly-Request-Id': requestId,
        },
      }
    )
    if (!response.ok) {
      const message = (await getResponseErrorMessage(response, 'Unable to start AI chart matching.')) ?? 'Unable to start AI chart matching.'
      throw new Error(`${message} (HTTP ${response.status}, request ${requestId})`)
    }

    return (await response.json()) as SetListChartMatchJobResponse
  }

  const applyChartMatchJobStatus = (job: SetListChartMatchJobResponse) => {
    setActiveChartMatchJob({ jobId: job.jobId, status: job.status, correlationId: job.correlationId })
    if (job.status === 'Completed') {
      const matches = job.result ?? []
      const jobStartedItemSignatures = activeJobItemSignaturesRef.current
      setItems((current) => applyChartMatchesPreservingEdits(current, matches, jobStartedItemSignatures))
      const suggested = matches.filter((item) => item.status === 'Suggested').length
      const review = matches.filter((item) => item.status === 'NeedsReview').length
      setStatus(`AI chart matching complete: ${suggested} suggested, ${review} need review.`)
      setIsLoading(false)
      setPhase('idle')
      setActiveChartMatchJob(null)
      activeChartMatchJobIdRef.current = null
      activeJobItemSignaturesRef.current = new Map()
      return
    }

    if (job.status === 'Failed' || job.status === 'Cancelled') {
      const reference = job.correlationId ? ` Reference: ${job.correlationId}.` : ''
      setStatus(`${job.errorMessage ?? 'AI chart matching could not complete. Continue reviewing chart candidates manually.'}${reference}`)
      setIsLoading(false)
      setPhase('idle')
      setActiveChartMatchJob(null)
      activeChartMatchJobIdRef.current = null
      activeJobItemSignaturesRef.current = new Map()
      return
    }

    setStatus(job.status === 'Running' ? 'AI is choosing chart matches...' : 'AI chart matching is queued...')
  }

  const fetchChartMatchJobStatus = async (jobId: string) => {
    if (isFetchingJobStatusRef.current) {
      return
    }

    isFetchingJobStatusRef.current = true
    try {
      const response = await fetchWithSession(buildApiUrl(`/gigs/${gig.id}/setlist-imports/chart-matches/ai-jobs/${jobId}`))
      if (!response.ok) {
        const message = (await getResponseErrorMessage(response, 'Unable to check AI chart matching status.')) ?? 'Unable to check AI chart matching status.'
        setStatus(`${message} Continue reviewing chart candidates manually.`)
        if (response.status === 404) {
          setIsLoading(false)
          setPhase('idle')
          setActiveChartMatchJob(null)
          activeChartMatchJobIdRef.current = null
        }
        return
      }

      applyChartMatchJobStatus((await response.json()) as SetListChartMatchJobResponse)
    } finally {
      isFetchingJobStatusRef.current = false
    }
  }

  useEffect(() => {
    fetchChartMatchJobStatusRef.current = (jobId: string) => {
      void fetchChartMatchJobStatus(jobId)
    }
  })

  useWorkspaceEvents({
    enabled: activeChartMatchJob !== null,
    onWorkspaceChanged: (event) => {
      const activeJobId = activeChartMatchJobIdRef.current
      if (event.scope === 'setlist-chart-matching' && event.entityId === activeJobId && activeJobId) {
        fetchChartMatchJobStatusRef.current(activeJobId)
      }
    },
  })

  useEffect(() => {
    activeChartMatchJobIdRef.current = activeChartMatchJob?.jobId ?? null
  }, [activeChartMatchJob?.jobId])

  useEffect(() => {
    const jobId = activeChartMatchJob?.jobId
    if (!jobId || activeChartMatchJob.status === 'Completed' || activeChartMatchJob.status === 'Failed' || activeChartMatchJob.status === 'Cancelled') {
      return
    }

    const poll = () => fetchChartMatchJobStatusRef.current(jobId)
    const intervalId = window.setInterval(poll, 2500)
    const handleVisibility = () => {
      if (document.visibilityState === 'visible') {
        poll()
      }
    }
    window.addEventListener('focus', poll)
    window.addEventListener('online', poll)
    document.addEventListener('visibilitychange', handleVisibility)
    poll()

    return () => {
      window.clearInterval(intervalId)
      window.removeEventListener('focus', poll)
      window.removeEventListener('online', poll)
      document.removeEventListener('visibilitychange', handleVisibility)
    }
  }, [activeChartMatchJob?.jobId, activeChartMatchJob?.status])

  const applyInterpretationJobStatus = (job: SetListInterpretationJobResponse, worksheet?: { id: string; name: string }) => {
    setActiveInterpretationJob({ jobId: job.jobId, status: job.status, correlationId: job.correlationId })
    if (job.sourceGrid) {
      setSourceGrid(job.sourceGrid)
    }

    if (job.status === 'Completed') {
      const resultItems = normalizeDraftItems(getInterpretationResultItems(job))
      setItems(resultItems)
      setActiveImport(null)
      setDraftWorksheet(job.sourceGrid ? { id: job.sourceGrid.worksheetId, name: job.sourceGrid.worksheetName } : worksheet ?? draftWorksheet)
      setBaselineItemsJson('')
      setExpandedItemKey('')
      setIsLoading(false)
      setPhase('idle')
      setActiveInterpretationJob(null)
      setStatus(`AI interpretation complete: ${resultItems.filter((item) => item.kind === 'Song' && item.include).length} song candidate(s) ready for chart matching.`)
      return
    }

    if (job.status === 'Failed' || job.status === 'Cancelled') {
      const reference = job.correlationId ? ` Reference: ${job.correlationId}.` : ''
      setIsLoading(false)
      setPhase('idle')
      setStatus(`${job.errorMessage ?? 'AI interpretation could not complete.'}${reference} Retry or create a manual draft from the source grid.`)
      return
    }

    setIsLoading(true)
    setPhase('interpretingSetList')
    setStatus(job.status === 'Running' ? 'AI is interpreting the worksheet...' : 'AI interpretation is queued...')
  }

  const fetchInterpretationJobStatus = async (jobId: string) => {
    if (isFetchingInterpretationStatusRef.current) {
      return
    }

    isFetchingInterpretationStatusRef.current = true
    try {
      const response = await fetchWithSession(buildApiUrl(`/gigs/${gig.id}/setlist-imports/interpretations/${jobId}`))
      if (!response.ok) {
        const message = (await getResponseErrorMessage(response, 'Unable to check AI interpretation status.')) ?? 'Unable to check AI interpretation status.'
        setIsLoading(false)
        setPhase('idle')
        setStatus(message)
        return
      }

      applyInterpretationJobStatus((await response.json()) as SetListInterpretationJobResponse)
    } finally {
      isFetchingInterpretationStatusRef.current = false
    }
  }

  useEffect(() => {
    fetchInterpretationStatusRef.current = (jobId: string) => {
      void fetchInterpretationJobStatus(jobId)
    }
  })

  useWorkspaceEvents({
    enabled: activeInterpretationJob !== null,
    onWorkspaceChanged: (event) => {
      if (event.scope === 'setlist-interpretation' && event.entityId === activeInterpretationJob?.jobId) {
        fetchInterpretationStatusRef.current(activeInterpretationJob.jobId)
      }
    },
  })

  useEffect(() => {
    const jobId = activeInterpretationJob?.jobId
    if (!jobId || activeInterpretationJob.status === 'Completed' || activeInterpretationJob.status === 'Failed' || activeInterpretationJob.status === 'Cancelled') {
      return
    }

    const poll = () => fetchInterpretationStatusRef.current(jobId)
    const intervalId = window.setInterval(poll, 2500)
    const handleVisibility = () => {
      if (document.visibilityState === 'visible') {
        poll()
      }
    }
    window.addEventListener('focus', poll)
    window.addEventListener('online', poll)
    document.addEventListener('visibilitychange', handleVisibility)
    poll()

    return () => {
      window.clearInterval(intervalId)
      window.removeEventListener('focus', poll)
      window.removeEventListener('online', poll)
      document.removeEventListener('visibilitychange', handleVisibility)
    }
  }, [activeInterpretationJob?.jobId, activeInterpretationJob?.status])

  const startInterpretation = async () => {
    if (!selectedWorksheet) {
      setStatus('Choose a worksheet first.')
      return
    }

    if (activeImport) {
      const shouldDiscard = window.confirm('Import rows from this worksheet? This will replace the rows currently shown in this dialog. Save any set list changes you want to keep first.')
      if (!shouldDiscard) {
        setStatus('Existing set list was kept.')
        return
      }
    }

    setIsLoading(true)
    setPhase('interpretingSetList')
    setSourceGrid(null)
    setStatus('Starting AI interpretation...')
    try {
      const response = await fetchWithSession(
        buildApiUrl(`/gigs/${gig.id}/setlist-imports/interpretations`),
        jsonRequestInit('POST', {
          resourceId: resource.id,
          worksheetId: selectedWorksheet.sheetId,
          worksheetName: selectedWorksheet.title,
        })
      )
      if (!response.ok) {
        setStatus((await getResponseErrorMessage(response, 'Unable to start AI interpretation.')) ?? 'Unable to start AI interpretation.')
        setIsLoading(false)
        setPhase('idle')
        return
      }

      const job = (await response.json()) as SetListInterpretationJobResponse
      sessionStorage.setItem(interpretationStorageKey(gig.id, resource.id, selectedWorksheet.sheetId), job.jobId)
      setDraftWorksheet({ id: selectedWorksheet.sheetId, name: selectedWorksheet.title })
      setActiveImport(null)
      setItems([])
      setBaselineItemsJson('')
      setExpandedItemKey('')
      applyInterpretationJobStatus(job, { id: selectedWorksheet.sheetId, name: selectedWorksheet.title })
    } catch (error) {
      setIsLoading(false)
      setPhase('idle')
      setStatus(error instanceof Error ? error.message : 'Unable to start AI interpretation.')
    }
  }

  const createManualDraft = () => {
    if (!sourceGrid) {
      setStatus('The source grid is not available yet. Wait for the interpretation job status before starting a manual draft.')
      return
    }

    setActiveImport(null)
    setActiveInterpretationJob(null)
    setDraftWorksheet({ id: sourceGrid.worksheetId, name: sourceGrid.worksheetName })
    setItems([])
    setBaselineItemsJson('')
    setExpandedItemKey('')
    setStatus('Manual draft started. Add and order the set-list items using the source grid as reference.')
  }

  const confirmActiveSetListChange = (action: string) => {
    if (!activeImport || draftWorksheet) {
      return true
    }

    return window.confirm(`${action}? This will update the rows currently shown for the saved set list. Save any existing changes you want to keep first.`)
  }

  const matchChartsWithAi = async () => {
    if (items.length === 0) {
      setStatus('Interpret a worksheet or add a manual draft item before matching charts.')
      return
    }

    if (!items.some((item) => item.kind === 'Song' && item.include)) {
      setStatus('Include at least one song before matching charts.')
      return
    }

    if (!confirmActiveSetListChange('Ask AI to choose chart matches')) {
      setStatus('Saved set list was kept unchanged.')
      return
    }

    setIsLoading(true)
    setPhase('interpretingSetList')

    let itemsToSend = items
    const flowRequestId = createClientRequestId()
    if (!itemsToSend.some((item) => item.forScoreMatch)) {
      setStatus('Locating candidate charts...')
      try {
        const locateItems = await requestChartMatches(itemsToSend, false, flowRequestId, 'locate')
        itemsToSend = applyChartMatches(itemsToSend, locateItems)
        setItems(itemsToSend)
      } catch (error) {
        setStatus(error instanceof Error ? `Unable to locate candidates: ${error.message}` : 'Unable to locate candidate charts.')
        return
      }
    }

    setStatus('Starting AI chart matching...')
    try {
      activeJobItemSignaturesRef.current = new Map(itemsToSend.map((item) => [item.itemId, getChartMatchRowSignature(item)]))
      const job = await startChartMatchJob(itemsToSend, flowRequestId)
      activeChartMatchJobIdRef.current = job.jobId
      setActiveChartMatchJob({ jobId: job.jobId, status: job.status, correlationId: job.correlationId })
      setStatus(job.status === 'Running' ? 'AI is choosing chart matches...' : 'AI chart matching is queued...')
    } catch (error) {
      setIsLoading(false)
      setPhase('idle')
      setStatus(error instanceof Error ? `Unable to match charts: ${error.message}` : 'Unable to match charts.')
    }
  }

  const saveImport = async (replaceActiveImport: boolean) => {
    if (activeImport && !draftWorksheet) {
      await saveActiveImport()
      return
    }

    if (!draftWorksheet) {
      setStatus('Interpret a worksheet or start a manual draft before saving.')
      return
    }

    setIsLoading(true)
    setPhase('saving')
    setStatus('Saving reviewed setlist...')
    try {
      const response = await fetchWithSession(
        buildApiUrl(`/gigs/${gig.id}/setlist-imports`),
        jsonRequestInit('POST', {
          resourceId: resource.id,
          worksheetId: draftWorksheet.id,
          worksheetName: draftWorksheet.name,
          replaceActiveImport,
          items,
        })
      )

      if (response.status === 409 && !replaceActiveImport) {
        const shouldReplace = window.confirm(
          'This gig already has an active setlist import. Save this import as the new active setlist and keep the old import in history?'
        )
        if (shouldReplace) {
          await saveImport(true)
        } else {
          setStatus('Setlist import was not replaced.')
        }
        return
      }

      if (!response.ok) {
        setStatus((await getResponseErrorMessage(response, 'Unable to save setlist import.')) ?? 'Unable to save setlist import.')
        return
      }

      const savedImport = (await response.json()) as GigSetListImport
      setActiveImport(savedImport)
      setDraftWorksheet(null)
      const savedItems = normalizeDraftItems(savedImport.items)
      setItems(savedItems)
      setBaselineItemsJson(serializeItemsForDirty(savedItems))
      setStatus('Set list import saved. You can continue reviewing or match charts with AI.')
    } catch (error) {
      setStatus(error instanceof Error ? error.message : 'Unable to save setlist import.')
    } finally {
      setIsLoading(false)
      setPhase('idle')
    }
  }

  const saveActiveImport = async () => {
    if (!activeImport) {
      return
    }

    setIsLoading(true)
    setPhase('saving')
    setStatus('Saving set list changes...')
    try {
      const response = await fetchWithSession(
        buildApiUrl(`/gigs/${gig.id}/setlist-imports/${activeImport.id}`),
        jsonRequestInit('PUT', { items })
      )
      if (!response.ok) {
        setStatus((await getResponseErrorMessage(response, 'Unable to save set list changes.')) ?? 'Unable to save set list changes.')
        return
      }

      const savedImport = (await response.json()) as GigSetListImport
      setActiveImport(savedImport)
      const savedItems = normalizeDraftItems(savedImport.items)
      setItems(savedItems)
      setBaselineItemsJson(serializeItemsForDirty(savedItems))
      setDraftWorksheet(null)
      setStatus('Set list changes saved.')
    } catch (error) {
      setStatus(error instanceof Error ? error.message : 'Unable to save set list changes.')
    } finally {
      setIsLoading(false)
      setPhase('idle')
    }
  }

  const exportForScoreSetList = async () => {
    if (!canAttemptForScoreExport) {
      setStatus(unselectedSongCount > 0
        ? 'Select forScore charts for all included song rows before exporting.'
        : 'Include at least one song row before exporting to forScore.')
      return
    }

    if (!activeImport || hasUnsavedChanges) {
      const shouldSave = window.confirm('Save this set list before exporting? The forScore export uses the saved active set list.')
      if (shouldSave) {
        await saveImport(false)
      } else {
        setStatus('Save the set list before exporting to forScore.')
      }
      return
    }

    setIsLoading(true)
    setStatus('Preparing forScore export...')
    try {
      const response = await fetchWithSession(buildApiUrl(`/gigs/${gig.id}/setlist-imports/active/forscore-export`))
      if (!response.ok) {
        setStatus(await getExportErrorMessage(response))
        return
      }

      const fileName = await downloadResponseBlob(response, `${gig.title || 'setlist'}.4ss`)
      setStatus(`Downloaded ${fileName}. Open the .4ss file on your iPad and import it into forScore.`)
      notifications.success(`Downloaded ${fileName}.`, {
        dedupeKey: `gig:${gig.id}:forscore-export`,
      })
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unable to export forScore set list.'
      setStatus(message)
      notifications.error(message, { dedupeKey: `gig:${gig.id}:forscore-export` })
    } finally {
      setIsLoading(false)
    }
  }

  const updateItem = (
    index: number,
    patch: Partial<Pick<ManagedSetListItem, 'sourceRowNumber' | 'kind' | 'include' | 'title' | 'padNumber' | 'key' | 'section' | 'notes' | 'forScoreChartId'>>
  ) => {
    setItems((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, ...patch } : item))
  }

  const addManualItem = () => {
    if (!draftWorksheet) {
      return
    }

    setItems((current) => [...current, {
      itemId: createClientRequestId(),
      sourceRowNumber: sourceGrid?.cells[0]?.rowNumber ?? 1,
      sortOrder: current.length,
      kind: 'Song',
      include: true,
      section: null,
      padNumber: null,
      key: null,
      title: 'Untitled song',
      notes: null,
      rawCellsJson: '[]',
      sourceEvidenceJson: null,
      confidence: 'Low',
      forScoreChartId: null,
      forScoreMatch: null,
    }])
  }

  const moveItem = (index: number, direction: -1 | 1) => {
    setItems((current) => {
      const destination = index + direction
      if (destination < 0 || destination >= current.length) {
        return current
      }

      const next = [...current]
      const movedItem = next[index]
      next[index] = next[destination]
      next[destination] = movedItem
      return next.map((item, sortOrder) => ({ ...item, sortOrder }))
    })
  }

  const getItemKey = (item: ManagedSetListItem) => item.itemId

  const getItemMeta = (item: ManagedSetListItem) => [
    item.sourceRowNumber > 0 ? `Row ${item.sourceRowNumber}` : 'Manual item',
    item.kind,
    getMatchLabel(item),
    `${item.confidence} confidence`,
    item.section,
  ].filter(Boolean).join(' · ')

  const getMatchLabel = (item: ManagedSetListItem) => {
    if (item.kind !== 'Song' || !item.include) {
      return null
    }

    if (item.forScoreChartId) {
      const candidate = item.forScoreMatch?.candidates.find((value) => value.chart.id === item.forScoreChartId)
      return candidate ? getEvidenceLabel(candidate.evidence) : item.forScoreMapping?.chartTitle ? `Chart: ${item.forScoreMapping.chartTitle}` : 'Chart selected'
    }

    switch (item.forScoreMatch?.status) {
      case 'Suggested':
        return item.forScoreMatch.selectedChart ? getEvidenceLabel(item.forScoreMatch.candidates[0]?.evidence ?? []) : 'Suggested chart'
      case 'NeedsReview':
        return 'Choose chart'
      case 'MissingFromLatestLibrary':
        return 'Missing from latest library'
      case 'NoActiveLibrary':
        return 'No forScore library'
      default:
        if (item.forScoreMapping?.status === 'MissingFromLatestLibrary') {
          return 'Missing from latest library'
        }

        return 'No chart selected'
    }
  }

  const itemNeedsAttention = (item: ManagedSetListItem) => {
    if (item.kind !== 'Song' || !item.include) {
      return false
    }

    if (item.forScoreChartId) {
      return false
    }

    return item.forScoreMatch?.status === 'NeedsReview'
      || item.forScoreMatch?.status === 'MissingFromLatestLibrary'
      || item.forScoreMatch?.status === 'NoActiveLibrary'
      || item.forScoreMapping?.status === 'MissingFromLatestLibrary'
      || (!item.forScoreChartId && item.forScoreMatch && item.forScoreMatch.candidates.length > 0)
  }

  const includedSongs = items.filter((item) => item.kind === 'Song' && item.include)
  const attentionItems = includedSongs.filter(itemNeedsAttention)
  const selectedChartCount = includedSongs.filter((item) => item.forScoreChartId).length
  const hasUnsavedChanges = draftWorksheet !== null || (!!activeImport && baselineItemsJson !== serializeItemsForDirty(items))
  const unselectedSongCount = includedSongs.length - selectedChartCount
  const canAttemptForScoreExport = includedSongs.length > 0 && unselectedSongCount === 0
  const exportDisabledMessage = unselectedSongCount > 0
    ? `${unselectedSongCount} included song row${unselectedSongCount === 1 ? '' : 's'} still need a forScore chart before export.`
    : 'Include at least one song row before exporting to forScore.'

  const getEvidenceLabel = (evidence: string[]) => {
    if (evidence.some((value) => value.includes('chart_number'))) {
      return 'Chart number match'
    }

    if (evidence.some((value) => value.includes('title') || value.includes('file_name'))) {
      return 'Title similarity match'
    }

    return 'Chart selected'
  }

  const renderImportProgress = () => {
    if (phase === 'idle') {
      return null
    }

    const steps = [
      { key: 'loadingWorksheets', label: 'Load worksheets' },
      { key: 'interpretingSetList', label: 'Interpret worksheet' },
      { key: 'saving', label: 'Save import' },
    ] as const
    const activeIndex = steps.findIndex((step) => step.key === phase)

    return (
      <div className="setlist-import-progress" role="status" aria-live="polite">
        <div className="quick-receipt-progress" aria-hidden="true"><span /></div>
        <ol>
          {steps.map((step, index) => (
            <li key={step.key} className={index === activeIndex ? 'active' : index < activeIndex ? 'done' : ''}>
              {step.label}
            </li>
          ))}
        </ol>
      </div>
    )
  }

  return (
    <div className="settings-overlay" role="presentation">
      <section className="settings-modal panel setlist-import-modal" role="dialog" aria-modal="true" aria-labelledby="setlist-import-title">
        <div className="panel-heading">
          <div>
            <p className="section-label">Set list</p>
            <h2 id="setlist-import-title">Manage set list</h2>
          </div>
          <button className="ghost-button" onClick={onClose} type="button" disabled={isLoading}>
            Close
          </button>
        </div>

        <p className="settings-hint">{resource.title}</p>
        {(isLoading || needsSheetsConnection || /unable|failed|error|reconnect|could not|must be|not available/i.test(status)) && (
          <p className="detail-label">{status}</p>
        )}
        {renderImportProgress()}

        {items.length > 0 && (
          <div className={`setlist-attention-summary ${attentionItems.length > 0 ? 'needs-attention' : ''}`}>
            <div>
              <strong>{attentionItems.length > 0 ? `${attentionItems.length} row${attentionItems.length === 1 ? '' : 's'} need attention` : 'Set list ready for review'}</strong>
              <span>{includedSongs.length} included song row{includedSongs.length === 1 ? '' : 's'} · {selectedChartCount} chart{selectedChartCount === 1 ? '' : 's'} selected</span>
            </div>
          </div>
        )}

        {needsSheetsConnection && (
          <button className="primary-button" onClick={connectGoogleSheets} type="button">
            Connect Google Sheets
          </button>
        )}

        <div className="compact-form-grid setlist-import-controls">
          <label>
            <span>Worksheet</span>
            <select
              value={selectedWorksheetId}
              onChange={(event) => setSelectedWorksheetId(event.target.value)}
              disabled={isLoading || !source}
            >
              {(source?.worksheets ?? []).map((worksheet) => (
                <option key={worksheet.sheetId} value={worksheet.sheetId}>{worksheet.title}</option>
              ))}
            </select>
          </label>
          <div className="modal-actions inline-actions">
            <button className="ghost-button" onClick={startInterpretation} type="button" disabled={isLoading || !selectedWorksheetId}>
              {activeInterpretationJob?.status === 'Failed' || activeInterpretationJob?.status === 'Cancelled' ? 'Retry interpretation' : 'Interpret worksheet'}
            </button>
            {sourceGrid && (
              <button className="ghost-button" onClick={createManualDraft} type="button" disabled={isLoading}>
                Start manual draft
              </button>
            )}
            <button className="ghost-button ai-button" onClick={() => void matchChartsWithAi()} type="button" disabled={isLoading || items.length === 0}>
              <span aria-hidden="true">✨</span> Ask AI to choose
            </button>
            <button className="primary-button" onClick={() => void saveImport(false)} type="button" disabled={isLoading || items.length === 0 || (activeImport !== null && draftWorksheet === null && !hasUnsavedChanges)}>
              {activeImport && !draftWorksheet ? 'Save changes' : 'Save import'}
            </button>
            <span title={!canAttemptForScoreExport ? exportDisabledMessage : undefined}>
              <button className="ghost-button" onClick={() => void exportForScoreSetList()} type="button" disabled={isLoading || !canAttemptForScoreExport}>
                Export forScore .4ss
              </button>
            </span>
          </div>
        </div>
        {draftWorksheet && (
          <div className="modal-actions inline-actions">
            <button className="ghost-button" onClick={addManualItem} type="button" disabled={isLoading}>
              Add set-list item
            </button>
          </div>
        )}
        {items.length > 0 && (
          <div className="associated-item-list setlist-review-list">
            {items.map((item, index) => {
              const itemKey = getItemKey(item)
              const isExpanded = expandedItemKey === itemKey
              const isSong = item.kind === 'Song'

              return (
                <article
                  key={itemKey}
                  className={`associated-item-row setlist-review-row ${isExpanded ? 'expanded' : ''} ${!isSong ? 'muted' : ''} ${itemNeedsAttention(item) ? 'needs-attention' : ''}`}
                >
                  <div className="associated-item-summary setlist-review-summary">
                    <label className="setlist-include-toggle" title={isSong ? 'Include in saved set list' : 'Separators and comments are saved for review only'}>
                      <input
                        type="checkbox"
                        checked={item.include}
                        disabled={!isSong}
                        onChange={(event) => updateItem(index, { include: event.target.checked })}
                      />
                    </label>
                    <button
                      className="setlist-review-main-button"
                      type="button"
                      aria-expanded={isExpanded}
                      onClick={() => setExpandedItemKey((current) => current === itemKey ? '' : itemKey)}
                    >
                      <div className="associated-item-main">
                        <strong>{item.title}</strong>
                        <span>{getItemMeta(item)}</span>
                      </div>
                      <div className="associated-item-chips">
                        {item.padNumber && <span className="resource-meta-chip">Pad {item.padNumber}</span>}
                        {item.key && <span className="resource-meta-chip">Key {item.key}</span>}
                        {isSong && <span className="resource-meta-chip">{getMatchLabel(item)}</span>}
                        {!isSong && <span className="resource-meta-chip">Review note</span>}
                        <span className="associated-item-expand-indicator" aria-hidden="true">
                          {isExpanded ? '−' : '+'}
                        </span>
                      </div>
                    </button>
                    {draftWorksheet && (
                      <div className="setlist-order-actions" aria-label="Change item order">
                        <button className="setlist-order-button" type="button" onClick={() => moveItem(index, -1)} disabled={isLoading || index === 0} aria-label="Move up" title="Move up">↑</button>
                        <button className="setlist-order-button" type="button" onClick={() => moveItem(index, 1)} disabled={isLoading || index === items.length - 1} aria-label="Move down" title="Move down">↓</button>
                      </div>
                    )}
                  </div>
                  <div className="associated-item-expansion" inert={!isExpanded}>
                    <div className="associated-item-expansion-inner setlist-review-edit">
                      <div className="compact-form-grid">
                        <label>
                          <span>Title</span>
                          <input value={item.title} onChange={(event) => updateItem(index, { title: event.target.value })} />
                        </label>
                        {draftWorksheet && (
                          <label>
                            <span>Item type</span>
                            <select
                              value={item.kind}
                              onChange={(event) => {
                                const kind = event.target.value as ManagedSetListItem['kind']
                                updateItem(index, { kind, include: kind === 'Song' ? item.include : false })
                              }}
                            >
                              <option value="Song">Song</option>
                              <option value="Separator">Section</option>
                              <option value="Transition">Transition</option>
                              <option value="Comment">Comment</option>
                            </select>
                          </label>
                        )}
                        {draftWorksheet && (
                          <label>
                            <span>Source row</span>
                            <input
                              type="number"
                              min="1"
                              value={item.sourceRowNumber}
                              onChange={(event) => updateItem(index, { sourceRowNumber: Number(event.target.value) || 1 })}
                            />
                          </label>
                        )}
                        <label>
                          <span>Pad</span>
                          <input value={item.padNumber ?? ''} onChange={(event) => updateItem(index, { padNumber: event.target.value || null })} />
                        </label>
                        <label>
                          <span>Key</span>
                          <input value={item.key ?? ''} onChange={(event) => updateItem(index, { key: event.target.value || null })} />
                        </label>
                        <label>
                          <span>Section</span>
                          <input value={item.section ?? ''} onChange={(event) => updateItem(index, { section: event.target.value || null })} />
                        </label>
                      </div>
                      <label>
                        <span>Notes</span>
                        <textarea value={item.notes ?? ''} onChange={(event) => updateItem(index, { notes: event.target.value || null })} />
                      </label>
                      {isSong && item.forScoreMatch && (
                        <label>
                          <span>forScore chart</span>
                          <select value={item.forScoreChartId ?? ''} onChange={(event) => updateItem(index, { forScoreChartId: event.target.value || null })}>
                            <option value="">No chart selected</option>
                            {item.forScoreMatch.selectedChart && <option value={item.forScoreMatch.selectedChart.id}>{item.forScoreMatch.selectedChart.title}</option>}
                            {item.forScoreMatch.candidates
                              .filter((candidate) => candidate.chart.id !== item.forScoreMatch?.selectedChart?.id)
                              .map((candidate) => <option key={candidate.chart.id} value={candidate.chart.id}>{candidate.chart.title}</option>)}
                          </select>
                          <span className="settings-hint">{item.forScoreMatch.reason}</span>
                        </label>
                      )}
                    </div>
                  </div>
                </article>
              )
            })}
          </div>
        )}
      </section>
    </div>
  )
}
