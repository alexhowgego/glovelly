import { useState } from 'react'
import type { FormEvent } from 'react'
import type { UserSettingsForm } from '../types'

type UserSettingsModalProps = {
  form: UserSettingsForm
  invoiceEmailSubjectPreview: string
  invoiceFilenamePreview: string
  invoiceFilenameTokens: string[]
  isGoogleDriveConnected: boolean
  isOpen: boolean
  isSaving: boolean
  isGoogleDriveConnectDisabled: boolean
  onClose: () => void
  onConnectGoogleDrive: () => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onUpdateField: (field: keyof UserSettingsForm, value: string) => void
  status: string
}

export function UserSettingsModal({
  form,
  invoiceEmailSubjectPreview,
  invoiceFilenamePreview,
  invoiceFilenameTokens,
  isGoogleDriveConnected,
  isOpen,
  isSaving,
  isGoogleDriveConnectDisabled,
  onClose,
  onConnectGoogleDrive,
  onSubmit,
  onUpdateField,
  status,
}: UserSettingsModalProps) {
  const [focusedField, setFocusedField] = useState<keyof UserSettingsForm | null>(null)
  const settingsNote =
    focusedField === 'mileageRate'
      ? 'Leave blank if you do not want a personal mileage default.'
      : focusedField === 'passengerMileageRate'
        ? 'Leave blank if you do not want a personal passenger mileage default.'
        : focusedField === 'defaultPaymentWindowDays'
          ? 'Leave blank to keep the standard 14 day payment window.'
          : focusedField === 'invoiceFilenamePattern'
            ? `Available filename tokens: ${invoiceFilenameTokens.join(', ')}.`
            : focusedField === 'invoiceEmailSubjectPattern'
              ? `Available subject tokens: ${invoiceFilenameTokens.join(', ')}.`
              : focusedField === 'invoiceReplyToEmail'
                ? 'Leave blank if replies should not be directed to a personal mailbox.'
                : focusedField === 'invoiceUploadFolderId'
                  ? "Leave blank to use Google Drive's default upload destination."
                  : 'Choose a setting to see a short note here.'
  const handleFocus = (field: keyof UserSettingsForm) => {
    setFocusedField(field)
  }

  if (!isOpen) {
    return null
  }

  return (
    <div className="settings-overlay" onClick={onClose} role="presentation">
      <section
        aria-labelledby="user-settings-title"
        aria-modal="true"
        className="settings-modal panel"
        onClick={(event) => event.stopPropagation()}
        role="dialog"
      >
        <div className="panel-heading">
          <div>
            <p className="section-label">User settings</p>
            <h2 id="user-settings-title">Your settings</h2>
          </div>
          <div className="settings-header-actions">
            <div className="drive-connection-state">
              <span
                aria-hidden="true"
                className={`drive-connection-indicator ${
                  isGoogleDriveConnected ? 'connected' : 'disconnected'
                }`}
              />
              <span>
                {isGoogleDriveConnected ? 'Drive connected' : 'Drive not connected'}
              </span>
            </div>
            <button
              className="ghost-button"
              disabled={isGoogleDriveConnectDisabled}
              onClick={onConnectGoogleDrive}
              type="button"
            >
              {isGoogleDriveConnected ? 'Reconnect Drive' : 'Connect Drive'}
            </button>
            <button className="ghost-button" onClick={onClose} type="button">
              Close
            </button>
          </div>
        </div>

        <p className="hero-text settings-intro">
          Set your personal defaults for rates, payment timing, invoice files, and connected services.
        </p>

        <form className="settings-form" onSubmit={onSubmit}>
          <div className="form-grid">
            <label>
              <span>Mileage rate</span>
              <input
                inputMode="decimal"
                placeholder="0.45"
                type="text"
                value={form.mileageRate}
                onFocus={() => handleFocus('mileageRate')}
                onChange={(event) => onUpdateField('mileageRate', event.target.value)}
              />
            </label>

            <label>
              <span>Passenger mileage rate</span>
              <input
                inputMode="decimal"
                placeholder="0.10"
                type="text"
                value={form.passengerMileageRate}
                onFocus={() => handleFocus('passengerMileageRate')}
                onChange={(event) =>
                  onUpdateField('passengerMileageRate', event.target.value)
                }
              />
            </label>

            <label>
              <span>Default payment window</span>
              <input
                inputMode="numeric"
                min="0"
                placeholder="14"
                type="number"
                value={form.defaultPaymentWindowDays}
                onFocus={() => handleFocus('defaultPaymentWindowDays')}
                onChange={(event) =>
                  onUpdateField('defaultPaymentWindowDays', event.target.value)
                }
              />
            </label>

            <label>
              <span>Invoice reply-to email</span>
              <input
                placeholder="you@example.com"
                type="email"
                value={form.invoiceReplyToEmail}
                onFocus={() => handleFocus('invoiceReplyToEmail')}
                onChange={(event) =>
                  onUpdateField('invoiceReplyToEmail', event.target.value)
                }
              />
            </label>

            <label>
              <span>Default invoice filename pattern</span>
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

            <label>
              <span>Default invoice email subject</span>
              <input
                placeholder="Invoice {InvoiceNumber} from Glovelly"
                type="text"
                value={form.invoiceEmailSubjectPattern}
                onFocus={() => handleFocus('invoiceEmailSubjectPattern')}
                onChange={(event) =>
                  onUpdateField('invoiceEmailSubjectPattern', event.target.value)
                }
              />
            </label>

            <label>
              <span>Google Drive invoice folder ID</span>
              <input
                placeholder="Drive folder ID"
                type="text"
                value={form.invoiceUploadFolderId}
                onFocus={() => handleFocus('invoiceUploadFolderId')}
                onChange={(event) =>
                  onUpdateField('invoiceUploadFolderId', event.target.value)
                }
              />
            </label>

            {focusedField === 'invoiceFilenamePattern' ? (
              <article className="setting-card override invoice-filename-preview">
                <p className="detail-label">Preview</p>
                <strong>{invoiceFilenamePreview}</strong>
                <span>Using today's date and a sample invoice number.</span>
              </article>
            ) : null}

            {focusedField === 'invoiceEmailSubjectPattern' ? (
              <article className="setting-card override invoice-filename-preview">
                <p className="detail-label">Preview</p>
                <strong>{invoiceEmailSubjectPreview}</strong>
                <span>Using today's date and a sample invoice number.</span>
              </article>
            ) : null}
          </div>

          <div className="settings-note">{settingsNote}</div>

          <div className="form-actions">
            <button className="primary-button" type="submit" disabled={isSaving}>
              {isSaving ? 'Saving…' : 'Save settings'}
            </button>
            <span className="status-pill">{status}</span>
          </div>
        </form>
      </section>
    </div>
  )
}
