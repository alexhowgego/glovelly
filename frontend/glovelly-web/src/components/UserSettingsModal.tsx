import { useState } from 'react'
import type { FormEvent } from 'react'
import type { GoogleCalendarStatus, UserSettingsForm } from '../types'

type UserSettingsModalProps = {
  form: UserSettingsForm
  invoiceEmailSubjectPreview: string
  invoiceFilenamePreview: string
  invoiceFilenameTokens: string[]
  isGoogleDriveConnected: boolean
  googleCalendarStatus: GoogleCalendarStatus | null
  isGoogleCalendarBusy: boolean
  isOpen: boolean
  isSaving: boolean
  isGoogleDriveConnectDisabled: boolean
  onClose: () => void
  onConnectGoogleCalendar: () => void
  onConnectGoogleDrive: () => void
  onDisconnectGoogleCalendar: () => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onUpdateField: (field: keyof UserSettingsForm, value: string) => void
  sellerProfilePostcode: string | null
  status: string
}

export function UserSettingsModal({
  form,
  invoiceEmailSubjectPreview,
  invoiceFilenamePreview,
  invoiceFilenameTokens,
  isGoogleDriveConnected,
  googleCalendarStatus,
  isGoogleCalendarBusy,
  isOpen,
  isSaving,
  isGoogleDriveConnectDisabled,
  onClose,
  onConnectGoogleCalendar,
  onConnectGoogleDrive,
  onDisconnectGoogleCalendar,
  onSubmit,
  onUpdateField,
  sellerProfilePostcode,
  status,
}: UserSettingsModalProps) {
  const [focusedField, setFocusedField] = useState<keyof UserSettingsForm | null>(null)
  const travelOriginPlaceholder = sellerProfilePostcode?.trim() || 'BS1 1AA'
  const calendarConnected = googleCalendarStatus?.isConnected ?? false
  const calendarStatusText = googleCalendarStatus
    ? calendarConnected
      ? `Calendar connected${googleCalendarStatus.pendingWorkCount > 0 ? `, ${googleCalendarStatus.pendingWorkCount} pending` : ''}`
      : googleCalendarStatus.hasRequiredScope
        ? 'Calendar not enabled'
        : 'Calendar not connected'
    : 'Calendar status loading'
  const settingsNote =
    focusedField === 'mileageRate'
      ? 'Leave blank if you do not want a personal mileage default.'
      : focusedField === 'passengerMileageRate'
        ? 'Leave blank if you do not want a personal passenger mileage default.'
        : focusedField === 'travelOriginPostcode'
          ? 'Used for mileage estimates before falling back to your seller profile postcode.'
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
            <button className="ghost-button" onClick={onClose} type="button">
              Close
            </button>
          </div>
        </div>

        <p className="hero-text settings-intro">
          Set your personal defaults for rates, payment timing, invoice files, and connected services.
        </p>

        <form className="settings-form" onSubmit={onSubmit}>
          <section className="settings-section">
            <div className="settings-section-heading">
              <p className="section-label">Connected services</p>
              <h3>Google integrations</h3>
            </div>

            <div className="connected-services-grid">
              <article className="connected-service-card">
                <div className="connected-service-summary">
                  <div>
                    <p className="detail-label">Google Drive</p>
                    <strong>
                      {isGoogleDriveConnected ? 'Drive connected' : 'Drive not connected'}
                    </strong>
                    <span>Publish invoice PDFs to Google Drive.</span>
                  </div>
                  <span
                    aria-hidden="true"
                    className={`service-connection-indicator ${
                      isGoogleDriveConnected ? 'connected' : 'disconnected'
                    }`}
                  />
                </div>

                <label className="compact-service-field">
                  <span>Invoice folder ID</span>
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

                <div className="form-actions compact-actions">
                  <button
                    className="ghost-button"
                    disabled={isGoogleDriveConnectDisabled}
                    onClick={onConnectGoogleDrive}
                    type="button"
                  >
                    {isGoogleDriveConnected ? 'Reconnect Drive' : 'Connect Drive'}
                  </button>
                </div>
              </article>

              <article className="connected-service-card">
                <div className="connected-service-summary">
                  <div>
                    <p className="detail-label">Google Calendar</p>
                    <strong>{calendarStatusText}</strong>
                    <span>
                      {googleCalendarStatus?.lastSuccessfulSyncAtUtc
                        ? `Last synced ${new Date(
                            googleCalendarStatus.lastSuccessfulSyncAtUtc
                          ).toLocaleString()}`
                        : 'Confirmed and completed gigs sync to a dedicated Glovelly Gigs calendar.'}
                    </span>
                  </div>
                  <span
                    aria-hidden="true"
                    className={`service-connection-indicator ${
                      calendarConnected ? 'connected' : 'disconnected'
                    }`}
                  />
                </div>

                {googleCalendarStatus?.lastError ? (
                  <span className="connected-service-error">
                    {googleCalendarStatus.lastError}
                  </span>
                ) : null}

                <span className="connected-service-note">
                  Draft and cancelled gigs are skipped. Changes may take a few minutes to appear.
                </span>

                <div className="form-actions compact-actions">
                  <button
                    className="ghost-button"
                    disabled={isGoogleCalendarBusy}
                    onClick={onConnectGoogleCalendar}
                    type="button"
                  >
                    {calendarConnected ? 'Reconnect Calendar' : 'Connect Calendar'}
                  </button>
                  <button
                    className="ghost-button"
                    disabled={!calendarConnected || isGoogleCalendarBusy}
                    onClick={onDisconnectGoogleCalendar}
                    type="button"
                  >
                    Disconnect
                  </button>
                </div>
              </article>
            </div>
          </section>

          <section className="settings-section">
            <div className="settings-section-heading">
              <p className="section-label">Personal defaults</p>
              <h3>Rates and payment timing</h3>
            </div>

            <div className="form-grid">
              <label>
                <span>Mileage rate</span>
                <input
                  data-testid="user-settings-mileage-rate-input"
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
                  data-testid="user-settings-passenger-mileage-rate-input"
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
                <span>Travel origin postcode</span>
                <input
                  autoComplete="postal-code"
                  data-testid="user-settings-travel-origin-postcode-input"
                  placeholder={travelOriginPlaceholder}
                  type="text"
                  value={form.travelOriginPostcode}
                  onFocus={() => handleFocus('travelOriginPostcode')}
                  onChange={(event) =>
                    onUpdateField('travelOriginPostcode', event.target.value)
                  }
                />
              </label>
            </div>
          </section>

          <section className="settings-section">
            <div className="settings-section-heading">
              <p className="section-label">Invoice defaults</p>
              <h3>Messages and filenames</h3>
            </div>

            <div className="form-grid">
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
          </section>

          <div className="settings-note">{settingsNote}</div>

          <div className="form-actions">
            <button className="primary-button" data-testid="user-settings-save-button" type="submit" disabled={isSaving}>
              {isSaving ? 'Saving…' : 'Save settings'}
            </button>
            <span className="status-pill" data-testid="user-settings-status">{status}</span>
          </div>
        </form>
      </section>
    </div>
  )
}
