import type { ForScoreLibrarySnapshot, GoogleCalendarStatus } from '../types'

function ServiceStatus({ connected, children }: { connected: boolean; children: string }) {
  return <strong className="connected-service-status"><span aria-hidden="true" className={`service-connection-indicator ${connected ? 'connected' : 'disconnected'}`} />{children}</strong>
}

type ConnectedServicesModalProps = {
  forScoreLibrarySnapshot: ForScoreLibrarySnapshot | null
  forScoreLibraryStatus: string
  googleCalendarStatus: GoogleCalendarStatus | null
  invoiceUploadFolderId: string
  isForScoreLibraryUploading: boolean
  isGoogleCalendarBusy: boolean
  isGoogleDriveBusy: boolean
  isGoogleDriveConnected: boolean
  isGoogleSheetsBusy: boolean
  isGoogleSheetsConnected: boolean
  isGoogleDocsBusy: boolean
  isGoogleDocsConnected: boolean
  isOpen: boolean
  isSaving: boolean
  onClose: () => void
  onConnectGoogleCalendar: () => void
  onConnectGoogleDrive: () => void
  onConnectGoogleSheets: () => void
  onConnectGoogleDocs: () => void
  onDisconnectGoogleCalendar: () => void
  onDisconnectGoogleDrive: () => void
  onDisconnectGoogleSheets: () => void
  onDisconnectGoogleDocs: () => void
  onForScoreLibraryFile: (file: File) => void
  onOpenSettings: () => void
  status: string
}

export function ConnectedServicesModal({
  forScoreLibrarySnapshot,
  forScoreLibraryStatus,
  googleCalendarStatus,
  invoiceUploadFolderId,
  isForScoreLibraryUploading,
  isGoogleCalendarBusy,
  isGoogleDriveBusy,
  isGoogleDriveConnected,
  isGoogleSheetsBusy,
  isGoogleSheetsConnected,
  isGoogleDocsBusy,
  isGoogleDocsConnected,
  isOpen,
  isSaving,
  onClose,
  onConnectGoogleCalendar,
  onConnectGoogleDrive,
  onConnectGoogleSheets,
  onConnectGoogleDocs,
  onDisconnectGoogleCalendar,
  onDisconnectGoogleDrive,
  onDisconnectGoogleSheets,
  onDisconnectGoogleDocs,
  onForScoreLibraryFile,
  onOpenSettings,
  status,
}: ConnectedServicesModalProps) {
  const calendarConnected = googleCalendarStatus?.isConnected ?? false
  const calendarStatusText = googleCalendarStatus
    ? googleCalendarStatus.requiresReconnection
      ? 'Reconnect required'
      : calendarConnected
      ? `Connected${googleCalendarStatus.pendingWorkCount > 0 ? `, ${googleCalendarStatus.pendingWorkCount} pending` : ''}`
      : googleCalendarStatus.hasRequiredScope
        ? 'Not enabled'
        : 'Not connected'
    : 'Status loading'

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

        {status && <p className="detail-label">{status}</p>}

        <div className="connected-services-grid services-modal-grid">
          <article className="connected-service-card">
            <div className="connected-service-summary">
              <img className="connected-service-icon" src="/service-icons/google-drive.svg" alt="" />
              <div>
                <p className="detail-label">Google Drive</p>
                <ServiceStatus connected={isGoogleDriveConnected}>{isGoogleDriveConnected ? 'Connected' : 'Not connected'}</ServiceStatus>
              </div>
            </div>

            <details className="connected-service-details"><summary>Details</summary><span className="connected-service-note">Publish invoice PDFs to Google Drive.{' '}
              {invoiceUploadFolderId.trim()
                ? `Invoice folder: ${invoiceUploadFolderId.trim()}`
                : "No invoice folder set. Configure it in Settings if you don't want Google's default destination."}
            </span></details>

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
            <div className="connected-service-summary"><img className="connected-service-icon" src="/service-icons/google-docs.svg" alt="" /><div><p className="detail-label">Google Docs</p><ServiceStatus connected={isGoogleDocsConnected}>{isGoogleDocsConnected ? 'Connected' : 'Not connected'}</ServiceStatus></div></div>
            <details className="connected-service-details"><summary>Read-only access to your Google Docs · Details</summary><span className="connected-service-note">Read submitted Google Docs to prepare gig imports. This requests access to your Google Docs, not just a single document. Attaching a link does not require this connection. Disconnecting here removes Glovelly's local access; revoke the app in your Google Account to withdraw Google's grant.</span></details>
            <div className="form-actions compact-actions"><button className="ghost-button" disabled={isGoogleDocsBusy} onClick={onConnectGoogleDocs} type="button">{isGoogleDocsConnected ? 'Reconnect Docs' : 'Connect Docs'}</button><button className="ghost-button" disabled={!isGoogleDocsConnected || isGoogleDocsBusy} onClick={onDisconnectGoogleDocs} type="button">Disconnect</button></div>
          </article>

          <article className="connected-service-card">
            <div className="connected-service-summary">
              <img className="connected-service-icon" src="/service-icons/google-sheets.svg" alt="" />
              <div>
                <p className="detail-label">Google Sheets</p>
                <ServiceStatus connected={isGoogleSheetsConnected}>{isGoogleSheetsConnected ? 'Connected' : 'Not connected'}</ServiceStatus>
              </div>
            </div>

            <details className="connected-service-details"><summary>Details</summary><span className="connected-service-note">Read linked set list spreadsheets for gig setlist imports. This asks for read-only access to Google Sheets and is separate from Drive publishing.</span></details>

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
              <img className="connected-service-icon" src="/service-icons/google-calendar.svg" alt="" />
              <div>
                <p className="detail-label">Google Calendar</p>
                <ServiceStatus connected={calendarConnected && !googleCalendarStatus?.requiresReconnection}>{calendarStatusText}</ServiceStatus>
              </div>
            </div>

            {googleCalendarStatus?.requiresReconnection ? (
              <span className="connected-service-error">
                Glovelly no longer has permission to add events to this Google Calendar. Reconnect your calendar to grant the required access.
              </span>
            ) : googleCalendarStatus?.lastError ? (
              <span className="connected-service-error">{googleCalendarStatus.lastError}</span>
            ) : null}

            <details className="connected-service-details"><summary>Details</summary><span className="connected-service-note">{googleCalendarStatus?.lastSuccessfulSyncAtUtc ? `Last synced ${new Date(googleCalendarStatus.lastSuccessfulSyncAtUtc).toLocaleString()}. ` : ''}Confirmed and completed gigs sync to a dedicated Glovelly Gigs calendar. Draft and cancelled gigs are skipped. Changes may take a few minutes to appear.</span></details>

            <div className="form-actions compact-actions">
              <button
                className="ghost-button"
                disabled={isGoogleCalendarBusy}
                onClick={onConnectGoogleCalendar}
                type="button"
              >
                {calendarConnected || googleCalendarStatus?.requiresReconnection
                  ? 'Reconnect Calendar'
                  : 'Connect Calendar'}
              </button>
              <button
                className="ghost-button"
                disabled={(!calendarConnected && !googleCalendarStatus?.requiresReconnection) || isGoogleCalendarBusy}
                onClick={onDisconnectGoogleCalendar}
                type="button"
              >
                Disconnect
              </button>
            </div>
          </article>

          <article className="connected-service-card">
            <div className="connected-service-summary">
              <img className="connected-service-icon" src="/service-icons/forscore.png" alt="" />
              <div>
                <p className="detail-label">forScore library</p>
                <ServiceStatus connected={!!forScoreLibrarySnapshot}>
                  {forScoreLibrarySnapshot
                    ? `${forScoreLibrarySnapshot.chartCount} chart(s) imported`
                    : 'No library snapshot'}
                </ServiceStatus>
              </div>
            </div>

            <details className="connected-service-details"><summary>Details</summary><span className="connected-service-note">Upload a forScore .4sb library export for set list chart matching.{' '}
              {forScoreLibrarySnapshot
                ? `Active snapshot: ${forScoreLibrarySnapshot.originalFileName}, imported ${new Date(forScoreLibrarySnapshot.importedAtUtc).toLocaleString()}. Existing forScore set lists are ignored.`
                : 'Glovelly only stores chart metadata from the backup. Existing forScore set lists, annotations and settings are ignored.'}
            </span></details>
            <span className="connected-service-note">{forScoreLibraryStatus}</span>
            {forScoreLibraryStatus.includes('need review') && (
              <span className="connected-service-note">
                Open each affected gig's reviewed set list and choose Check forScore matches to fix chart links.
              </span>
            )}
            {forScoreLibrarySnapshot?.warnings.length ? (
              <span className="connected-service-error">
                {forScoreLibrarySnapshot.warnings.slice(0, 2).join(' ')}
              </span>
            ) : null}

            <div className="form-actions compact-actions">
              <label className="ghost-button file-action-label">
                {isForScoreLibraryUploading ? 'Importing...' : 'Upload .4sb'}
                <input
                  accept=".4sb"
                  disabled={isForScoreLibraryUploading}
                  onChange={(event) => {
                    const file = event.currentTarget.files?.[0]
                    event.currentTarget.value = ''
                    if (file) {
                      onForScoreLibraryFile(file)
                    }
                  }}
                  type="file"
                />
              </label>
            </div>
          </article>
        </div>
      </section>
    </div>
  )
}
