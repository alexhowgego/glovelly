import type { GoogleCalendarStatus } from '../types'

type ConnectedServicesModalProps = {
  googleCalendarStatus: GoogleCalendarStatus | null
  invoiceUploadFolderId: string
  isGoogleCalendarBusy: boolean
  isGoogleDriveBusy: boolean
  isGoogleDriveConnected: boolean
  isGoogleSheetsBusy: boolean
  isGoogleSheetsConnected: boolean
  isOpen: boolean
  isSaving: boolean
  onClose: () => void
  onConnectGoogleCalendar: () => void
  onConnectGoogleDrive: () => void
  onConnectGoogleSheets: () => void
  onDisconnectGoogleCalendar: () => void
  onDisconnectGoogleDrive: () => void
  onDisconnectGoogleSheets: () => void
  onOpenSettings: () => void
  status: string
}

export function ConnectedServicesModal({
  googleCalendarStatus,
  invoiceUploadFolderId,
  isGoogleCalendarBusy,
  isGoogleDriveBusy,
  isGoogleDriveConnected,
  isGoogleSheetsBusy,
  isGoogleSheetsConnected,
  isOpen,
  isSaving,
  onClose,
  onConnectGoogleCalendar,
  onConnectGoogleDrive,
  onConnectGoogleSheets,
  onDisconnectGoogleCalendar,
  onDisconnectGoogleDrive,
  onDisconnectGoogleSheets,
  onOpenSettings,
  status,
}: ConnectedServicesModalProps) {
  const calendarConnected = googleCalendarStatus?.isConnected ?? false
  const calendarStatusText = googleCalendarStatus
    ? calendarConnected
      ? `Calendar connected${googleCalendarStatus.pendingWorkCount > 0 ? `, ${googleCalendarStatus.pendingWorkCount} pending` : ''}`
      : googleCalendarStatus.hasRequiredScope
        ? 'Calendar not enabled'
        : 'Calendar not connected'
    : 'Calendar status loading'

  if (!isOpen) {
    return null
  }

  return (
    <div className="settings-overlay" onClick={onClose} role="presentation">
      <section
        aria-labelledby="connected-services-title"
        aria-modal="true"
        className="settings-modal panel"
        onClick={(event) => event.stopPropagation()}
        role="dialog"
      >
        <div className="panel-heading">
          <div>
            <p className="section-label">Services</p>
            <h2 id="connected-services-title">Connected services</h2>
          </div>
          <button className="ghost-button" onClick={onClose} type="button">
            Close
          </button>
        </div>

        <p className="hero-text settings-intro">
          Manage Google integrations used for publishing invoices, importing set lists and syncing gigs.
        </p>
        <p className="detail-label">{status}</p>

        <div className="connected-services-grid services-modal-grid">
          <article className="connected-service-card">
            <div className="connected-service-summary">
              <div>
                <p className="detail-label">Google Drive</p>
                <strong>{isGoogleDriveConnected ? 'Drive connected' : 'Drive not connected'}</strong>
                <span>Publish invoice PDFs to Google Drive.</span>
              </div>
              <span
                aria-hidden="true"
                className={`service-connection-indicator ${
                  isGoogleDriveConnected ? 'connected' : 'disconnected'
                }`}
              />
            </div>

            <span className="connected-service-note">
              {invoiceUploadFolderId.trim()
                ? `Invoice folder: ${invoiceUploadFolderId.trim()}`
                : "No invoice folder set. Configure it in Settings if you don't want Google's default destination."}
            </span>

            <div className="form-actions compact-actions">
              <button
                className="ghost-button"
                disabled={isGoogleDriveBusy || isSaving}
                onClick={onConnectGoogleDrive}
                type="button"
              >
                {isGoogleDriveConnected ? 'Reconnect Drive' : 'Connect Drive'}
              </button>
              <button
                className="ghost-button"
                disabled={!isGoogleDriveConnected || isGoogleDriveBusy}
                onClick={onDisconnectGoogleDrive}
                type="button"
              >
                Disconnect
              </button>
              <button className="ghost-button" onClick={onOpenSettings} type="button">
                Folder settings
              </button>
            </div>
          </article>

          <article className="connected-service-card">
            <div className="connected-service-summary">
              <div>
                <p className="detail-label">Google Sheets</p>
                <strong>{isGoogleSheetsConnected ? 'Sheets connected' : 'Sheets not connected'}</strong>
                <span>Read linked set list spreadsheets for gig setlist imports.</span>
              </div>
              <span
                aria-hidden="true"
                className={`service-connection-indicator ${
                  isGoogleSheetsConnected ? 'connected' : 'disconnected'
                }`}
              />
            </div>

            <span className="connected-service-note">
              This asks for read-only access to Google Sheets and is separate from Drive publishing.
            </span>

            <div className="form-actions compact-actions">
              <button
                className="ghost-button"
                disabled={isGoogleSheetsBusy}
                onClick={onConnectGoogleSheets}
                type="button"
              >
                {isGoogleSheetsConnected ? 'Reconnect Sheets' : 'Connect Sheets'}
              </button>
              <button
                className="ghost-button"
                disabled={!isGoogleSheetsConnected || isGoogleSheetsBusy}
                onClick={onDisconnectGoogleSheets}
                type="button"
              >
                Disconnect
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
                    ? `Last synced ${new Date(googleCalendarStatus.lastSuccessfulSyncAtUtc).toLocaleString()}`
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
              <span className="connected-service-error">{googleCalendarStatus.lastError}</span>
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
    </div>
  )
}
