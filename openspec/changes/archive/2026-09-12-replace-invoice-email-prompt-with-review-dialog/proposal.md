## Why

Invoice email delivery currently collects its optional message and receipt choice through browser dialogs. On mobile, Enter can accept the message prompt and send a partially written email, while neither prompt provides a reliable review of the final recipient, subject, body, or attachments before an irreversible delivery.

The document-freshness lifecycle now guarantees whether an invoice PDF is deliverable. This change can use that contract to make email delivery a deliberate, inspectable terminal stage with clear outcome feedback.

## What Changes

- Replace browser prompt and confirmation dialogs in invoice email delivery with an accessible, responsive Review and send invoice dialog.
- Show the exact server-resolved recipient, subject, plain-text email content, current invoice PDF attachment, and optional receipt-pack choice before sending.
- Provide an optional, unchecked Mark invoice as issued after sending choice in the dialog, replacing the post-delivery issue confirmation.
- Use explicit Send invoice submission only; Enter in message editing must not send, and pending delivery must prevent duplicate submission.
- Preserve truthful delivery outcomes: report email success even when the requested subsequent invoice issue or linked-gig completion cannot finish.
- Use inline dialog feedback for preparation, validation, and in-progress delivery; use Sonner notifications for completed delivery and terminal follow-up failures.

## Capabilities

### New Capabilities
- `invoice-email-review-delivery`: Review, validate, explicitly send, and report invoice email delivery from an accessible dialog.

### Modified Capabilities

- None.

## Impact

- Frontend invoice workspace state, invoice actions, a new delivery-review modal, and invoice UAT/Playwright coverage.
- A visibility-scoped invoice email preparation API and the existing invoice email-delivery endpoint/request contract.
- Invoice email composition, delivery status handling, and linked-gig completion orchestration; no new external dependencies are expected.
