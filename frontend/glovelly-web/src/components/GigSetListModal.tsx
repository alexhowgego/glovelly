import { useEffect, useState } from 'react'
import { buildApiUrl, fetchWithSession, getResponseErrorMessage, jsonRequestInit } from '../api'
import type { Gig, GigSetListImport, GigSetListImportItemDraft } from '../types'

type SavedSetListItem = GigSetListImportItemDraft & { id: string }

type GigSetListModalProps = {
  gig: Gig
  onClose: () => void
}

export function GigSetListModal({ gig, onClose }: GigSetListModalProps) {
  const [setListImport, setSetListImport] = useState<GigSetListImport | null>(null)
  const [items, setItems] = useState<SavedSetListItem[]>([])
  const [expandedItemKey, setExpandedItemKey] = useState('')
  const [status, setStatus] = useState('Loading imported set list...')
  const [isLoading, setIsLoading] = useState(false)

  useEffect(() => {
    let isCancelled = false
    const loadSetList = async () => {
      setIsLoading(true)
      try {
        const response = await fetchWithSession(
          buildApiUrl(`/gigs/${gig.id}/setlist-imports/active`)
        )
        if (!response.ok) {
          const message = response.status === 404
            ? 'No imported set list has been saved for this gig yet.'
            : (await getResponseErrorMessage(response, 'Unable to load imported set list.')) ?? 'Unable to load imported set list.'
          setStatus(message)
          return
        }

        const loadedImport = (await response.json()) as GigSetListImport
        if (isCancelled) {
          return
        }

        setSetListImport(loadedImport)
        setItems(loadedImport.items)
        setStatus(`Reviewing ${loadedImport.items.filter((item) => item.include).length} included set list item(s).`)
      } catch (error) {
        if (!isCancelled) {
          setStatus(error instanceof Error ? error.message : 'Unable to load imported set list.')
        }
      } finally {
        if (!isCancelled) {
          setIsLoading(false)
        }
      }
    }

    void loadSetList()
    return () => {
      isCancelled = true
    }
  }, [gig.id])

  const updateItem = (
    index: number,
    patch: Partial<Pick<SavedSetListItem, 'include' | 'title' | 'padNumber' | 'key' | 'section' | 'notes'>>
  ) => {
    setItems((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, ...patch } : item))
  }

  const saveSetList = async () => {
    if (!setListImport) {
      return
    }

    setIsLoading(true)
    setStatus('Saving set list changes...')
    try {
      const response = await fetchWithSession(
        buildApiUrl(`/gigs/${gig.id}/setlist-imports/${setListImport.id}`),
        jsonRequestInit('PUT', { items })
      )
      if (!response.ok) {
        setStatus((await getResponseErrorMessage(response, 'Unable to save set list changes.')) ?? 'Unable to save set list changes.')
        return
      }

      const savedImport = (await response.json()) as GigSetListImport
      setSetListImport(savedImport)
      setItems(savedImport.items)
      setStatus('Set list changes saved.')
    } catch (error) {
      setStatus(error instanceof Error ? error.message : 'Unable to save set list changes.')
    } finally {
      setIsLoading(false)
    }
  }

  const getItemKey = (item: SavedSetListItem, index: number) => `${item.id}-${index}`

  const getItemMeta = (item: SavedSetListItem) => [
    `Row ${item.sourceRowNumber}`,
    item.kind,
    `${item.confidence} confidence`,
    item.section,
  ].filter(Boolean).join(' · ')

  return (
    <div className="settings-overlay" role="presentation">
      <section className="settings-modal panel setlist-import-modal" role="dialog" aria-modal="true" aria-labelledby="gig-setlist-title">
        <div className="panel-heading">
          <div>
            <p className="section-label">Set list</p>
            <h2 id="gig-setlist-title">Review imported set list</h2>
          </div>
          <button className="ghost-button" onClick={onClose} type="button" disabled={isLoading}>
            Close
          </button>
        </div>

        <p className="settings-hint">{gig.title}</p>
        <p className="detail-label">{status}</p>

        <div className="modal-actions inline-actions">
          <button className="primary-button" onClick={() => void saveSetList()} type="button" disabled={isLoading || !setListImport || items.length === 0}>
            Save changes
          </button>
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
                    <label className="setlist-include-toggle" title={isSong ? 'Include in set list' : 'Separators and comments are saved for review only'}>
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
