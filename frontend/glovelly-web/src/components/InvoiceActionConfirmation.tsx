import { useEffect, useId, useRef, useState } from 'react'
import type { InvoiceActionDecision, InvoiceActionRequest } from '../hooks/useInvoiceActionConfirmation'

export function InvoiceActionConfirmation({ request, onFinish }: {
  request: InvoiceActionRequest
  onFinish: (decision: InvoiceActionDecision | null) => void
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
    onCancel={event => { event.preventDefault(); onFinish(null) }}>
    <h2 id={titleId}>{publishing && !willIssue ? 'Publish' : 'Issue'} {request.invoice.invoiceNumber}{publishing && willIssue ? ' and publish?' : '?'}</h2>
    <p id={descriptionId}>{publishing
      ? willIssue
        ? 'This invoice will be issued to remove DRAFT, then published to your connected Google Drive.'
        : 'Publish this invoice PDF to your connected Google Drive.'
      : 'This removes the DRAFT watermark. It does not send the invoice.'}</p>
    {willIssue && request.linkedGigCount > 0 && <label className="checkbox-field">
      <input type="checkbox" checked={completeLinkedGigs} onChange={event => setCompleteLinkedGigs(event.target.checked)} />
      <span>Also mark {request.linkedGigCount === 1 ? 'the linked gig' : `${request.linkedGigCount} linked gigs`} as completed</span>
    </label>}
    <div className="actions">
      <button type="button" className="ghost-button" autoFocus onClick={() => onFinish(null)}>Cancel</button>
      <button type="button" className="primary-button" data-testid="invoice-issue-confirm-button"
        onClick={() => onFinish({ completeLinkedGigs })}>{publishing ? willIssue ? 'Issue and publish' : 'Publish invoice' : 'Issue invoice'}</button>
    </div>
  </dialog>
}
