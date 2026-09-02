## Context

Invoice email delivery currently uses `window.prompt` for an optional message, `window.confirm` for receipt inclusion, and a separate confirmation after a draft is delivered. The send endpoint independently resolves the client recipient, subject pattern, email body template, seller identity, PDF filename, receipt attachments, and document availability. The frontend cannot accurately reproduce that composition because it does not own all of the server-side inputs.

`invoice-document-freshness` now establishes that only a current PDF can be delivered. This change needs to expose that readiness alongside a reviewable email representation without weakening the existing guarded send path.

## Goals / Non-Goals

**Goals:**
- Replace native browser dialogs with an accessible, responsive review-and-send flow.
- Show the exact recipient, subject, safe plain-text email body, and attachment metadata that server-side delivery resolves.
- Preserve an explicit, revalidated send boundary for the current invoice PDF.
- Allow the user to request issuing a draft from the same deliberate send step.
- Make delivery success and post-delivery issue failures truthful and independently visible.

**Non-Goals:**
- Editing recipient, subject, sender identity, email templates, invoice content, or attachment files from the delivery dialog.
- Changing invoice numbering, delivery-history fields, email provider behavior, or the document-freshness lifecycle.
- Making outbound email and invoice issuance transactionally atomic; email is an external irreversible side effect.
- Redesigning Google Drive publishing or other invoice actions.

## Decisions

### Add a server-resolved delivery preparation resource

Add a visibility-scoped invoice email preparation endpoint that loads the same invoice, client, user defaults, seller profile, period date, PDF readiness, and receipt candidates used by delivery. It returns a review model containing recipient display data, resolved subject, safe rendered plain-text body, PDF filename and generation metadata, receipt attachment count, and actionable readiness or recipient validation state.

The response is preparation only: it sends no email, creates no delivery record, and makes no status change. The existing send endpoint remains authoritative and re-resolves/revalidates all delivery inputs when the user explicitly sends. A shared preparation/composition service is preferred over frontend reconstruction or duplicate endpoint logic, so the shown values follow the same subject and template rules as the sent email.

### Keep message and receipt selection as minimal send inputs

The dialog owns only the optional additional message, receipt-pack opt-in, and issue-after-send opt-in. The send request continues to carry the first two delivery inputs; recipient, subject, body template, sender identity, and PDF attachment are not client-controlled. Preparation returns a server-resolved base body plus server-provided fragments for the receipt and additional-message sections. React assembles those fragments from current local input synchronously, matching the immediate seller-profile preview interaction without making a request for each keypress.

Receipt delivery remains available rather than being silently removed with the browser confirmation. The dialog shows the number of eligible receipt attachments and uses the existing server-side attachment-size enforcement when sending.

### Make the review dialog an accessible explicit-submit boundary

Selecting Send to client opens a modal and starts preparation; it never sends email. The modal uses the established overlay and panel visual language but manages focus deliberately: focus enters a labelled control, Escape and Cancel close it with no side effects, and focus returns to the triggering Send to client control. Its message field is a textarea, so Enter creates text rather than submits. Send invoice is an explicit button and the sole operation that may initiate delivery.

The dialog remains usable within a small viewport, keeps the action controls visible, and presents preparation, validation, and delivery errors inline. While preparation or sending is pending, submission is disabled, duplicate requests are prevented, and controls that would lose in-progress input are unavailable.

### Integrate issue-after-send as a selected follow-up, not email precondition

For draft invoices, the dialog presents an unchecked Mark invoice as issued after sending checkbox. Once email delivery succeeds, the existing issue and linked-gig completion workflow runs only when selected. Email is never held back by a status transition, and the invoice is not issued before a successful send.

The frontend delivery orchestrator retains the email success result before attempting the optional follow-up. It closes the modal and reports a delivery-success Sonner notification after email succeeds. If issuing or linked-gig completion fails, it additionally reports an actionable terminal error explaining that delivery succeeded but the requested follow-up did not. This is preferred to treating an irreversible successful email as a failed send, or moving all issue behavior into the email endpoint without preserving the existing linked-gig workflow.

### Use plain text for the review surface

The review UI displays server-rendered plain text in text content or a read-only text control. It does not inject the HTML email representation into the application DOM. The mail channel can continue generating its sanitized HTML email separately.

## Risks / Trade-offs

- [Delivery settings or client data changes after preparation] -> Re-resolve all values and readiness at explicit send; return an actionable validation error rather than delivering a stale or invalid representation.
- [A PDF becomes unavailable after the dialog opens] -> The send endpoint's existing guarded PDF read rejects it, and the dialog displays the returned availability error without creating delivery history.
- [Email succeeds but issue or linked-gig completion fails] -> Preserve the email result, show delivery success, and separately identify the incomplete follow-up.
- [Receipt archive exceeds the configured attachment limit] -> Keep the dialog open with the existing attachment validation error and send no email.
- [Modal keyboard behavior regresses] -> Cover focus entry/return, Escape/Cancel, textarea Enter behavior, and explicit-send-only behavior with frontend and browser UAT coverage.

## Migration Plan

1. Deploy the preparation endpoint and dialog while retaining the guarded send endpoint as the delivery authority.
2. Replace the prompt/confirm invocation path with the dialog and integrated issue selection.
3. Update automated and documented UAT journeys to use explicit dialog interactions.
4. Rollback can restore the prior frontend invocation path; preparation endpoints are additive and do not change persisted data.

## Open Questions

- None. The integrated issue-after-send checkbox and truthful partial-outcome notification policy are agreed.
