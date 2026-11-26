import { useEffect, useId, useRef, useState } from 'react'
import type { InvoiceActionDecision, InvoiceActionRequest } from '../hooks/useInvoiceActionConfirmation'
import type { EntitlementSnapshot } from '../types'
import { InvoiceAllowanceNotice } from './InvoiceAllowanceNotice'

export function InvoiceActionConfirmation({ request, onFinish, entitlements, busy = false, error = '' }: {
  request: InvoiceActionRequest
  onFinish: (decision: InvoiceActionDecision | null) => void
  entitlements?: EntitlementSnapshot | null
  busy?: boolean
  error?: string
}) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const titleId = useId()
  const descriptionId = useId()
  const [completeLinkedGigs, setCompleteLinkedGigs] = useState(false)
  const publishing = request.action === 'publish'
  const willIssue = request.invoice.status === 'Draft'

  useEffect(() => {
    const trigger = document.activeElement instanceof HTMLElement ? document.activeElement : null
    const dialog = dialogRef.current!
    dialog.showModal()
    return () => {
      dialog.close()
      if (trigger?.isConnected) trigger.focus({ preventScroll: true })
    }
  }, [])

  return <dialog ref={dialogRef} className="panel invoice-issue-confirmation"
    aria-labelledby={titleId} aria-describedby={descriptionId} data-testid="invoice-issue-confirmation"
    onCancel={event => { event.preventDefault(); if (!busy) onFinish(null) }}>
    <h2 id={titleId}>{publishing && !willIssue ? 'Publish' : 'Issue'} {request.invoice.invoiceNumber}{publishing && willIssue ? ' and publish?' : '?'}</h2>
    <p id={descriptionId}>{publishing
      ? willIssue
        ? 'This invoice will be issued to remove DRAFT, then published to your connected Google Drive.'
        : 'Publish this invoice PDF to your connected Google Drive.'
      : 'This removes the DRAFT watermark. It does not send the invoice.'}</p>
    {willIssue && <InvoiceAllowanceNotice invoice={request.invoice} entitlements={entitlements} />}
    {error && <p role="alert">{error}</p>}
    {willIssue && request.linkedGigCount > 0 && <label className="checkbox-field">
      <input type="checkbox" disabled={busy} checked={completeLinkedGigs} onChange={event => setCompleteLinkedGigs(event.target.checked)} />
      <span>Also mark {request.linkedGigCount === 1 ? 'the linked gig' : `${request.linkedGigCount} linked gigs`} as completed</span>
    </label>}
    <div className="actions">
      <button type="button" className="ghost-button" disabled={busy} autoFocus onClick={() => onFinish(null)}>Cancel</button>
      <button type="button" className="primary-button" data-testid="invoice-issue-confirm-button"
        disabled={busy || (willIssue && !request.invoice.hasBeenIssued && !request.invoice.firstIssuedUtc && entitlements?.invoiceIssuance.remaining === 0)}
        onClick={() => onFinish({ completeLinkedGigs })}>{publishing ? willIssue ? 'Issue and publish' : 'Publish invoice' : 'Issue invoice'}</button>
    </div>
  </dialog>
}
