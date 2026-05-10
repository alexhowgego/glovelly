import { useState } from 'react'
import type { FormEvent } from 'react'
import type { AuthUser, Client, ClientSettingsForm } from '../appShared'
import { formatRate } from '../appShared'

type ClientSettingsModalProps = {
  authUser: AuthUser | null
  form: ClientSettingsForm
  invoiceEmailSubjectPreview: string
  invoiceFilenamePreview: string
  invoiceFilenameTokens: string[]
  isOpen: boolean
  isSaving: boolean
  onClose: () => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onUpdateField: (field: keyof ClientSettingsForm, value: string) => void
  selectedClient: Client | null
  status: string
}

export function ClientSettingsModal({
  authUser,
  form,
  invoiceEmailSubjectPreview,
  invoiceFilenamePreview,
  invoiceFilenameTokens,
  isOpen,
  isSaving,
  onClose,
  onSubmit,
  onUpdateField,
  selectedClient,
  status,
}: ClientSettingsModalProps) {
  const [focusedField, setFocusedField] = useState<keyof ClientSettingsForm | null>(null)

  if (!isOpen || !selectedClient) {
    return null
  }

  const isOverriding = (value: string) => value.trim().length > 0
  const fieldClassName = (value: string) =>
    `override-field ${isOverriding(value) ? 'is-overriding' : 'is-inherited'}`
  const mileageOverride = isOverriding(form.mileageRate)
  const passengerOverride = isOverriding(form.passengerMileageRate)
  const filenameOverride = isOverriding(form.invoiceFilenamePattern)
  const subjectOverride = isOverriding(form.invoiceEmailSubjectPattern)
  const formatFormRate = (value: string, fallback: number | null | undefined) => {
    if (!isOverriding(value)) {
      return formatRate(fallback ?? null)
    }

    const parsed = Number(value.trim())
    return Number.isFinite(parsed) ? formatRate(parsed) : value.trim()
  }
  const settingsNote =
    focusedField === 'mileageRate'
      ? 'Leave blank to inherit your personal mileage default.'
      : focusedField === 'passengerMileageRate'
        ? 'Leave blank to inherit your personal passenger mileage default.'
        : focusedField === 'invoiceFilenamePattern'
          ? `Leave blank to inherit the default filename pattern. Filename tokens: ${invoiceFilenameTokens.join(', ')}.`
          : focusedField === 'invoiceEmailSubjectPattern'
            ? `Leave blank to inherit the default email subject. Subject tokens: ${invoiceFilenameTokens.join(', ')}.`
            : 'Choose a setting to see override details here.'
  const handleFocus = (field: keyof ClientSettingsForm) => {
    setFocusedField(field)
  }

  return (
    <div className="settings-overlay" onClick={onClose} role="presentation">
      <section
        aria-labelledby="client-settings-title"
        aria-modal="true"
        className="settings-modal panel"
        onClick={(event) => event.stopPropagation()}
        role="dialog"
      >
        <div className="panel-heading">
          <div>
            <p className="section-label">Client settings</p>
            <h2 id="client-settings-title">{selectedClient.name}</h2>
          </div>
          <button className="ghost-button" onClick={onClose} type="button">
            Close
          </button>
        </div>

        <p className="hero-text settings-intro">
          Leave a field blank to inherit the default from your own user settings.
          Add a value here only when this client needs special handling.
        </p>

        <form className="settings-form" onSubmit={onSubmit}>
          <div className="form-grid">
            <label className={fieldClassName(form.mileageRate)}>
              <span>Mileage rate override</span>
              <input
                inputMode="decimal"
                placeholder={
                  authUser?.mileageRate === null || authUser?.mileageRate === undefined
                    ? 'Use default'
                    : `Default ${authUser.mileageRate}`
                }
                type="text"
                value={form.mileageRate}
                onFocus={() => handleFocus('mileageRate')}
                onChange={(event) => onUpdateField('mileageRate', event.target.value)}
              />
            </label>

            <label className={fieldClassName(form.passengerMileageRate)}>
              <span>Passenger rate override</span>
              <input
                inputMode="decimal"
                placeholder={
                  authUser?.passengerMileageRate === null ||
                  authUser?.passengerMileageRate === undefined
                    ? 'Use default'
                    : `Default ${authUser.passengerMileageRate}`
                }
                type="text"
                value={form.passengerMileageRate}
                onFocus={() => handleFocus('passengerMileageRate')}
                onChange={(event) =>
                  onUpdateField('passengerMileageRate', event.target.value)
                }
              />
            </label>

            <label className={fieldClassName(form.invoiceFilenamePattern)}>
              <span>Invoice filename pattern</span>
              <input
                placeholder="{InvoiceNumber}"
                type="text"
                value={form.invoiceFilenamePattern}
                onFocus={() => handleFocus('invoiceFilenamePattern')}
                onChange={(event) =>
                  onUpdateField('invoiceFilenamePattern', event.target.value)
                }
              />
            </label>

            <label className={fieldClassName(form.invoiceEmailSubjectPattern)}>
              <span>Invoice email subject</span>
              <input
                placeholder={
                  authUser?.invoiceEmailSubjectPattern ??
                  'Invoice {InvoiceNumber} from Glovelly'
                }
                type="text"
                value={form.invoiceEmailSubjectPattern}
                onFocus={() => handleFocus('invoiceEmailSubjectPattern')}
                onChange={(event) =>
                  onUpdateField('invoiceEmailSubjectPattern', event.target.value)
                }
              />
            </label>

            {focusedField === 'mileageRate' ? (
              <article
                className={`setting-card ${
                  mileageOverride ? 'override' : 'inherited'
                } invoice-filename-preview`}
              >
                <p className="detail-label">Mileage rule</p>
                <strong>{formatFormRate(form.mileageRate, authUser?.mileageRate)}</strong>
                <span>
                  {mileageOverride
                    ? 'Custom rate for this client'
                    : 'Inherited from your user settings'}
                </span>
              </article>
            ) : null}

            {focusedField === 'passengerMileageRate' ? (
              <article
                className={`setting-card ${
                  passengerOverride ? 'override' : 'inherited'
                } invoice-filename-preview`}
              >
                <p className="detail-label">Passenger mileage rule</p>
                <strong>
                  {formatFormRate(
                    form.passengerMileageRate,
                    authUser?.passengerMileageRate
                  )}
                </strong>
                <span>
                  {passengerOverride
                    ? 'Custom passenger rate for this client'
                    : 'Inherited from your user settings'}
                </span>
              </article>
            ) : null}

            {focusedField === 'invoiceFilenamePattern' ? (
              <article
                className={`setting-card ${
                  filenameOverride ? 'override' : 'inherited'
                } invoice-filename-preview`}
              >
                <p className="detail-label">Filename preview</p>
                <strong>{invoiceFilenamePreview}</strong>
                <span>
                  {filenameOverride
                    ? 'Custom pattern for this client'
                    : 'Inherited from your user settings'}
                </span>
              </article>
            ) : null}

            {focusedField === 'invoiceEmailSubjectPattern' ? (
              <article
                className={`setting-card ${
                  subjectOverride ? 'override' : 'inherited'
                } invoice-filename-preview`}
              >
                <p className="detail-label">Email subject preview</p>
                <strong>{invoiceEmailSubjectPreview}</strong>
                <span>
                  {subjectOverride
                    ? 'Custom subject for this client'
                    : 'Inherited from your user settings'}
                </span>
              </article>
            ) : null}
          </div>

          <div className="settings-note">{settingsNote}</div>

          <div className="form-actions">
            <button className="primary-button" type="submit" disabled={isSaving}>
              {isSaving ? 'Saving…' : 'Save client settings'}
            </button>
            <span className="status-pill">{status}</span>
          </div>
        </form>
      </section>
    </div>
  )
}
