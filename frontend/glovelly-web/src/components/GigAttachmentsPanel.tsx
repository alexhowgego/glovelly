import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import type {
  Gig,
  GigExternalResource,
  GigExternalResourceAttachment,
  GigExternalResourceForm,
  GigExternalResourcePurpose,
  GigExternalResourceType,
} from '../types'
import { SetListImportModal } from './SetListImportModal'
import { GigSetListModal } from './GigSetListModal'
import { TrashIcon } from './TrashIcon'

type GigAttachmentsPanelProps = {
  selectedGig: Gig | null
  externalResourceForm: GigExternalResourceForm
  externalResourceMode: 'create' | 'edit'
  gigStatus: string
  isGigLoading: boolean
  isExternalResourceEditorOpen: boolean
  onCancelExternalResourceEdit: () => void
  onDeleteExternalResource: (resource: GigExternalResource) => void
  onDeleteExternalResourceAttachment: (
    resource: GigExternalResource,
    attachment: GigExternalResourceAttachment
  ) => void
  onDownloadExternalResourceAttachment: (
    resource: GigExternalResource,
    attachment: GigExternalResourceAttachment
  ) => void
  onStartExternalResourceCreate: () => void
  onStartExternalResourceEdit: (resource: GigExternalResource) => void
  onSubmitExternalResource: (event: FormEvent<HTMLFormElement>) => void
  onUpdateExternalResourceField: (
    field: keyof GigExternalResourceForm,
    value: string | boolean
  ) => void
  onUploadExternalResourceAttachment: (resource: GigExternalResource, file: File) => void
}

const resourceTypeOptions: { value: GigExternalResourceType; label: string }[] = [
  { value: 'GoogleSheet', label: 'Google Sheet' },
  { value: 'GoogleDoc', label: 'Google Doc' },
  { value: 'Url', label: 'URL' },
  { value: 'Email', label: 'Email' },
  { value: 'File', label: 'File' },
  { value: 'Other', label: 'Other' },
]

const resourcePurposeOptions: { value: GigExternalResourcePurpose; label: string }[] = [
  { value: 'SetList', label: 'Set list' },
  { value: 'GigPlan', label: 'Gig plan' },
  { value: 'Contract', label: 'Contract' },
  { value: 'Travel', label: 'Travel' },
  { value: 'Other', label: 'Other' },
]

export function GigAttachmentsPanel({
  selectedGig,
  externalResourceForm,
  externalResourceMode,
  gigStatus,
  isGigLoading,
  isExternalResourceEditorOpen,
  onCancelExternalResourceEdit,
  onDeleteExternalResource,
  onDeleteExternalResourceAttachment,
  onDownloadExternalResourceAttachment,
  onStartExternalResourceCreate,
  onStartExternalResourceEdit,
  onSubmitExternalResource,
  onUpdateExternalResourceField,
  onUploadExternalResourceAttachment,
}: GigAttachmentsPanelProps) {
  const [expandedResourceId, setExpandedResourceId] = useState<string>('')
  const [setListImportResource, setSetListImportResource] = useState<GigExternalResource | null>(null)
  const [isSetListModalOpen, setIsSetListModalOpen] = useState(false)
  const externalResourceEditorTitle =
    externalResourceMode === 'edit' ? 'Edit attachment' : 'Add attachment'
  const formatResourceType = (value: GigExternalResourceType) =>
    resourceTypeOptions.find((option) => option.value === value)?.label ?? value
  const formatResourcePurpose = (value: GigExternalResourcePurpose) =>
    resourcePurposeOptions.find((option) => option.value === value)?.label ?? value
  const sortedExternalResources = (selectedGig?.externalResources ?? [])
    .slice()
    .sort((left, right) => {
      if (left.isPrimary !== right.isPrimary) {
        return left.isPrimary ? -1 : 1
      }

      return left.title.localeCompare(right.title)
    })

  useEffect(() => {
    setExpandedResourceId('')
  }, [selectedGig?.id])

  return (
    <>
      <div className="gig-timeline-note">
        <div className="associated-items-heading">
          <div>
            <p className="detail-label">Attachments</p>
            <span>Links, documents, and files attached to this gig.</span>
          </div>
          <button
            className="ghost-button"
            data-testid="add-gig-attachment-button"
            onClick={onStartExternalResourceCreate}
            type="button"
            disabled={isGigLoading}
          >
            Add attachment
          </button>
        </div>

        {sortedExternalResources.length > 0 ? (
          <div className="associated-item-list external-resource-list">
            {sortedExternalResources.map((resource) => {
              const isExpanded = expandedResourceId === resource.id
              const purposeLabel = formatResourcePurpose(resource.purpose)
              const typeLabel = formatResourceType(resource.resourceType)
              const fileCount = resource.attachments.length

              return (
                <article
                  key={resource.id}
                  className={`associated-item-row external-resource-item ${isExpanded ? 'expanded' : ''}`}
                  data-testid="gig-attachment-item"
                >
                  <button
                    className="associated-item-summary"
                    type="button"
                    aria-expanded={isExpanded}
                    onClick={() =>
                      setExpandedResourceId((current) =>
                        current === resource.id ? '' : resource.id
                      )
                    }
                  >
                    <div className="associated-item-main">
                      <strong>{resource.title}</strong>
                      <span>{purposeLabel} · {typeLabel}</span>
                    </div>
                    <div className="associated-item-chips">
                      {resource.isPrimary && (
                        <span className="resource-primary-badge">
                          Primary {purposeLabel.toLowerCase()}
                        </span>
                      )}
                      {resource.url && <span className="resource-meta-chip">Link</span>}
                      <span className="resource-meta-chip">
                        {fileCount} file{fileCount === 1 ? '' : 's'}
                      </span>
                      <span className="associated-item-expand-indicator" aria-hidden="true">
                        {isExpanded ? '−' : '+'}
                      </span>
                    </div>
                  </button>

                  <div className="associated-item-expansion" inert={!isExpanded}>
                    <div className="associated-item-expansion-inner">
                      {resource.notes?.trim() && <p>{resource.notes}</p>}
                      <div className="associated-item-actions external-resource-actions">
                        {resource.url && (
                          <a
                            className="ghost-button"
                            href={resource.url}
                            target="_blank"
                            rel="noreferrer"
                          >
                            Open
                          </a>
                        )}
                        {selectedGig && resource.resourceType === 'GoogleSheet' && resource.purpose === 'SetList' && (
                          <>
                            <button
                              className="ghost-button"
                              onClick={() => setSetListImportResource(resource)}
                              type="button"
                              disabled={isGigLoading}
                            >
                              Import set list
                            </button>
                            <button
                              className="ghost-button"
                              onClick={() => setIsSetListModalOpen(true)}
                              type="button"
                              disabled={isGigLoading}
                            >
                              Review set list
                            </button>
                          </>
                        )}
                        <button
                          className="ghost-button"
                          onClick={() => onStartExternalResourceEdit(resource)}
                          type="button"
                          disabled={isGigLoading}
                        >
                          Edit
                        </button>
                        <button
                          aria-label={`Delete attachment ${resource.title || 'Untitled attachment'}`}
                          className="icon-delete-button"
                          onClick={() => onDeleteExternalResource(resource)}
                          type="button"
                          disabled={isGigLoading}
                          title="Delete attachment"
                        >
                          <TrashIcon />
                        </button>
                      </div>
                      <div className="resource-attachments">
                        <div className="expense-attachment-header">
                          <span>Files</span>
                          <label className="ghost-button file-upload-button">
                            Upload
                            <input
                              data-testid="gig-attachment-file-input"
                              type="file"
                              onChange={(event) => {
                                const file = event.target.files?.[0]
                                if (file) {
                                  onUploadExternalResourceAttachment(resource, file)
                                }
                                event.currentTarget.value = ''
                              }}
                              disabled={isGigLoading}
                            />
                          </label>
                        </div>
                        {resource.attachments.length > 0 ? (
                          <div className="expense-attachment-list">
                            {resource.attachments.map((attachment) => (
                              <div key={attachment.id} className="expense-attachment-item">
                                <span>{attachment.fileName}</span>
                                <div className="expense-attachment-actions">
                                  <button
                                    className="ghost-button"
                                    onClick={() =>
                                      onDownloadExternalResourceAttachment(resource, attachment)
                                    }
                                    type="button"
                                    disabled={isGigLoading}
                                  >
                                    Download
                                  </button>
                                  <button
                                    aria-label={`Delete file ${attachment.fileName}`}
                                    className="icon-delete-button"
                                    onClick={() =>
                                      onDeleteExternalResourceAttachment(resource, attachment)
                                    }
                                    type="button"
                                    disabled={isGigLoading}
                                    title="Delete file"
                                  >
                                    <TrashIcon />
                                  </button>
                                </div>
                              </div>
                            ))}
                          </div>
                        ) : (
                          <span>No files attached.</span>
                        )}
                      </div>
                    </div>
                  </div>
                </article>
              )
            })}
          </div>
        ) : (
          <span>No attachments added yet.</span>
        )}
      </div>

      {isExternalResourceEditorOpen && (
        <div className="settings-overlay" role="presentation">
          <section
            aria-labelledby="external-resource-editor-title"
            className="settings-modal external-resource-modal panel"
            role="dialog"
            aria-modal="true"
          >
            <div className="panel-heading">
              <div>
                <p className="section-label">Attachments</p>
                <h2 id="external-resource-editor-title">{externalResourceEditorTitle}</h2>
              </div>
              <button
                className="ghost-button"
                onClick={onCancelExternalResourceEdit}
                type="button"
                disabled={isGigLoading}
              >
                Close
              </button>
            </div>

            <form className="external-resource-form" onSubmit={onSubmitExternalResource}>
              <div className="compact-form-grid">
                <label>
                  <span>Type</span>
                  <select
                    data-testid="gig-attachment-type-select"
                    value={externalResourceForm.resourceType}
                    onChange={(event) =>
                      onUpdateExternalResourceField(
                        'resourceType',
                        event.target.value as GigExternalResourceType
                      )
                    }
                  >
                    {resourceTypeOptions.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  <span>Purpose</span>
                  <select
                    value={externalResourceForm.purpose}
                    onChange={(event) =>
                      onUpdateExternalResourceField(
                        'purpose',
                        event.target.value as GigExternalResourcePurpose
                      )
                    }
                  >
                    {resourcePurposeOptions.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  <span>Title</span>
                  <input
                    data-testid="gig-attachment-title-input"
                    required
                    value={externalResourceForm.title}
                    onChange={(event) =>
                      onUpdateExternalResourceField('title', event.target.value)
                    }
                  />
                </label>
                <label>
                  <span>URL</span>
                  <input
                    data-testid="gig-attachment-url-input"
                    type="url"
                    placeholder="Optional link"
                    value={externalResourceForm.url}
                    onChange={(event) =>
                      onUpdateExternalResourceField('url', event.target.value)
                    }
                  />
                </label>
              </div>
              <label>
                <span>Notes</span>
                <textarea
                  rows={3}
                  value={externalResourceForm.notes}
                  onChange={(event) =>
                    onUpdateExternalResourceField('notes', event.target.value)
                  }
                />
              </label>
              <label className="checkbox-field resource-primary-toggle">
                  <input
                  type="checkbox"
                  checked={externalResourceForm.isPrimary}
                  onChange={(event) =>
                    onUpdateExternalResourceField('isPrimary', event.target.checked)
                  }
                />
                <span>Make this the primary attachment for its purpose</span>
              </label>
              <div className="form-actions">
                <button className="primary-button" type="submit" disabled={isGigLoading}>
                  <span data-testid="gig-attachment-submit-label">
                  {externalResourceMode === 'edit' ? 'Update attachment' : 'Add attachment'}
                  </span>
                </button>
                <button
                  className="ghost-button"
                  onClick={onCancelExternalResourceEdit}
                  type="button"
                  disabled={isGigLoading}
                >
                  Cancel
                </button>
                <span className="status-pill">{gigStatus}</span>
              </div>
            </form>
          </section>
        </div>
      )}

      {selectedGig && setListImportResource && (
        <SetListImportModal
          gig={selectedGig}
          resource={setListImportResource}
          onClose={() => setSetListImportResource(null)}
        />
      )}

      {selectedGig && isSetListModalOpen && (
        <GigSetListModal
          gig={selectedGig}
          onClose={() => setIsSetListModalOpen(false)}
        />
      )}
    </>
  )
}
