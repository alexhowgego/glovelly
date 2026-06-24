import { useEffect, useState } from 'react'
import { buildApiUrl, fetchWithSession, getResponseErrorMessage, jsonRequestInit } from '../api'
import type {
  Gig,
  GigExternalResource,
  GigSetListImportItemDraft,
  GigSetListPreview,
  GigSetListSource,
} from '../types'

type SetListImportModalProps = {
  gig: Gig
  resource: GigExternalResource
  onClose: () => void
}

export function SetListImportModal({ gig, resource, onClose }: SetListImportModalProps) {
  const [source, setSource] = useState<GigSetListSource | null>(null)
  const [selectedWorksheetId, setSelectedWorksheetId] = useState('')
  const [preview, setPreview] = useState<GigSetListPreview | null>(null)
  const [items, setItems] = useState<GigSetListImportItemDraft[]>([])
  const [expandedItemKey, setExpandedItemKey] = useState('')
  const [status, setStatus] = useState('Loading worksheets...')
  const [isLoading, setIsLoading] = useState(false)
  const [needsSheetsConnection, setNeedsSheetsConnection] = useState(false)

  useEffect(() => {
    let isCancelled = false
    const loadSource = async () => {
      setIsLoading(true)
      try {
        const response = await fetchWithSession(
          buildApiUrl(`/gigs/${gig.id}/setlist-imports/source?resourceId=${encodeURIComponent(resource.id)}`)
        )
        if (!response.ok) {
          const message = (await getResponseErrorMessage(response, 'Unable to load Google Sheet worksheets.')) ?? 'Unable to load Google Sheet worksheets.'
          setNeedsSheetsConnection(response.status === 409 && message.toLowerCase().includes('sheets'))
          setStatus(message)
          return
        }

        const loadedSource = (await response.json()) as GigSetListSource
        if (isCancelled) {
          return
        }

        setSource(loadedSource)
        setSelectedWorksheetId(loadedSource.worksheets[0]?.sheetId ?? '')
        setStatus(
          loadedSource.worksheets.length > 1
            ? 'Choose a worksheet to preview.'
            : 'Preview the linked worksheet before saving.'
        )
      } catch (error) {
        if (!isCancelled) {
          setStatus(error instanceof Error ? error.message : 'Unable to load Google Sheet worksheets.')
        }
      } finally {
        if (!isCancelled) {
          setIsLoading(false)
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

  const previewWorksheet = async () => {
    if (!selectedWorksheet) {
      setStatus('Choose a worksheet first.')
      return
    }

    setIsLoading(true)
    setStatus('Reading worksheet...')
    try {
      const response = await fetchWithSession(
        buildApiUrl(`/gigs/${gig.id}/setlist-imports/preview`),
        jsonRequestInit('POST', {
          resourceId: resource.id,
          worksheetId: selectedWorksheet.sheetId,
          worksheetName: selectedWorksheet.title,
        })
      )
      if (!response.ok) {
        setStatus((await getResponseErrorMessage(response, 'Unable to preview setlist rows.')) ?? 'Unable to preview setlist rows.')
        return
      }

      const nextPreview = (await response.json()) as GigSetListPreview
      setPreview(nextPreview)
      setItems(nextPreview.items)
      setExpandedItemKey('')
      setStatus(`Found ${nextPreview.items.filter((item) => item.include).length} song candidate(s). Review before saving.`)
    } catch (error) {
      setStatus(error instanceof Error ? error.message : 'Unable to preview setlist rows.')
    } finally {
      setIsLoading(false)
    }
  }

  const saveImport = async (replaceActiveImport: boolean) => {
    if (!preview) {
      setStatus('Preview a worksheet before saving.')
      return
    }

    setIsLoading(true)
    setStatus('Saving reviewed setlist...')
    try {
      const response = await fetchWithSession(
        buildApiUrl(`/gigs/${gig.id}/setlist-imports`),
        jsonRequestInit('POST', {
          resourceId: resource.id,
          worksheetId: preview.worksheetId,
          worksheetName: preview.worksheetName,
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

      setStatus('Setlist import saved.')
      window.setTimeout(onClose, 500)
    } catch (error) {
      setStatus(error instanceof Error ? error.message : 'Unable to save setlist import.')
    } finally {
      setIsLoading(false)
    }
  }

  const updateItem = (
    index: number,
    patch: Partial<Pick<GigSetListImportItemDraft, 'include' | 'title' | 'padNumber' | 'key' | 'section' | 'notes'>>
  ) => {
    setItems((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, ...patch } : item))
  }

  const getItemKey = (item: GigSetListImportItemDraft, index: number) => `${item.sourceRowNumber}-${index}`

  const getItemMeta = (item: GigSetListImportItemDraft) => [
    `Row ${item.sourceRowNumber}`,
    item.kind,
    `${item.confidence} confidence`,
    item.section,
  ].filter(Boolean).join(' · ')

  return (
    <div className="settings-overlay" role="presentation">
      <section className="settings-modal panel setlist-import-modal" role="dialog" aria-modal="true" aria-labelledby="setlist-import-title">
        <div className="panel-heading">
          <div>
            <p className="section-label">Set list</p>
            <h2 id="setlist-import-title">Import from Google Sheet</h2>
          </div>
          <button className="ghost-button" onClick={onClose} type="button" disabled={isLoading}>
            Close
          </button>
        </div>

        <p className="settings-hint">{resource.title}</p>
        <p className="detail-label">{status}</p>

        {needsSheetsConnection && (
          <button className="primary-button" onClick={connectGoogleSheets} type="button">
            Connect Google Sheets
          </button>
        )}

        <div className="compact-form-grid">
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
            <button className="ghost-button" onClick={previewWorksheet} type="button" disabled={isLoading || !selectedWorksheetId}>
              Preview rows
            </button>
            <button className="primary-button" onClick={() => void saveImport(false)} type="button" disabled={isLoading || items.length === 0}>
              Save import
            </button>
          </div>
        </div>

        {items.length > 0 && (
          <div className="associated-item-list setlist-review-list">
            {items.map((item, index) => {
              const itemKey = getItemKey(item, index)
              const isExpanded = expandedItemKey === itemKey
              const isSong = item.kind === 'Song'

              return (
                <article
                  key={itemKey}
                  className={`associated-item-row setlist-review-row ${isExpanded ? 'expanded' : ''} ${!isSong ? 'muted' : ''}`}
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
                        {!isSong && <span className="resource-meta-chip">Review note</span>}
                        <span className="associated-item-expand-indicator" aria-hidden="true">
                          {isExpanded ? '−' : '+'}
                        </span>
                      </div>
                    </button>
                  </div>
                  <div className="associated-item-expansion" inert={!isExpanded}>
                    <div className="associated-item-expansion-inner setlist-review-edit">
                      <div className="compact-form-grid">
                        <label>
                          <span>Title</span>
                          <input value={item.title} onChange={(event) => updateItem(index, { title: event.target.value })} />
                        </label>
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
