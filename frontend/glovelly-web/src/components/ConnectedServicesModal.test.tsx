// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { ConnectedServicesModal } from './ConnectedServicesModal'

afterEach(cleanup)

it('offers Google Docs connection independently of Sheets and Drive', () => {
  const connect = vi.fn()
  const disconnect = vi.fn()
  const { container } = render(<ConnectedServicesModal
    forScoreLibrarySnapshot={null} forScoreLibraryStatus="" googleCalendarStatus={null} invoiceUploadFolderId=""
    isForScoreLibraryUploading={false} isGoogleCalendarBusy={false} isGoogleDriveBusy={false} isGoogleDriveConnected={true}
    isGoogleSheetsBusy={false} isGoogleSheetsConnected={true} isGoogleDocsBusy={false} isGoogleDocsConnected={false}
    isOpen isSaving={false} onClose={vi.fn()} onConnectGoogleCalendar={vi.fn()} onConnectGoogleDrive={vi.fn()}
    onConnectGoogleSheets={vi.fn()} onConnectGoogleDocs={connect} onDisconnectGoogleCalendar={vi.fn()}
    onDisconnectGoogleDrive={vi.fn()} onDisconnectGoogleSheets={vi.fn()} onDisconnectGoogleDocs={disconnect}
    onForScoreLibraryFile={vi.fn()} onOpenSettings={vi.fn()} status=""
  />)
  expect(Array.from(container.querySelectorAll('.connected-service-card .detail-label')).map(element => element.textContent)).toEqual(['Google Drive', 'Google Docs', 'Google Sheets', 'Google Calendar', 'forScore library'])
  expect(container.querySelectorAll('.connected-service-icon')).toHaveLength(5)
  const cards = Array.from(container.querySelectorAll('.connected-service-card'))
  expect(cards.map(card => card.querySelector('.connected-service-status')?.textContent)).toEqual(['Connected', 'Not connected', 'Connected', 'Status loading', 'No library snapshot'])
  for (const card of cards) {
    const indicator = card.querySelector('.connected-service-status .service-connection-indicator')
    expect(indicator?.getAttribute('aria-hidden')).toBe('true')
  }
  const disclosure = screen.getByText(/read-only access to your Google Docs/i)
  expect(disclosure).toBeTruthy()
  fireEvent.click(disclosure)
  expect(screen.getByText(/not just a single document/)).toBeTruthy()
  fireEvent.click(screen.getByRole('button', { name: 'Connect Docs' }))
  expect(connect).toHaveBeenCalledOnce()
  expect(disconnect).not.toHaveBeenCalled()
})
