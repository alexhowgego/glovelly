## ADDED Requirements

### Requirement: Invoice email delivery is prepared for review without side effects
The system SHALL provide an authenticated, visibility-scoped preparation operation for invoice email delivery that does not send email, create a delivery record, or change invoice status. It SHALL provide the server-resolved recipient name and address, subject, safe rendered plain-text body, invoice PDF attachment metadata, eligible receipt attachment count, and actionable delivery-readiness information.

#### Scenario: User opens review for a deliverable invoice
- **WHEN** a user selects email delivery for a visible invoice with a current PDF and a configured recipient
- **THEN** the system SHALL show the resolved recipient, subject, plain-text email content, current PDF attachment, and available receipt-pack information without sending email or changing delivery history

#### Scenario: Review identifies an unavailable recipient or document
- **WHEN** preparation finds that the invoice recipient is missing or the invoice PDF is missing, regenerating, stale, or failed
- **THEN** the system SHALL return actionable readiness information, SHALL prevent sending from the review dialog, and SHALL not create a successful delivery record

### Requirement: Users explicitly review and submit invoice email delivery
The system SHALL replace browser prompt and confirmation dialogs for invoice email delivery with a responsive, keyboard-accessible Review and send invoice dialog. The dialog SHALL show a message textarea, receipt-pack choice, and an explicit Send invoice button; only that button SHALL initiate delivery.

#### Scenario: Opening, cancelling, or editing review does not deliver email
- **WHEN** a user opens the dialog, edits the optional message, changes receipt inclusion, presses Enter in the message textarea, presses Escape, or selects Cancel
- **THEN** the system SHALL not send email, change invoice status, or create a delivery record

#### Scenario: Dialog is usable by keyboard and on a small viewport
- **WHEN** a user opens the review dialog using a keyboard or on a small screen
- **THEN** the dialog SHALL expose labelled controls, move focus into the dialog, return focus to its trigger when closed, support Escape and Cancel, and retain visible access to the explicit send action

#### Scenario: Pending delivery cannot be submitted twice
- **WHEN** a user selects Send invoice and delivery is pending
- **THEN** the system SHALL show in-progress feedback and SHALL prevent a second delivery submission until the first attempt completes

### Requirement: Reviewed delivery uses current server-side email content and documents
The system SHALL re-resolve the recipient, subject, template content, selected message, receipt attachments, and current PDF when explicitly sending. It SHALL not trust client-supplied recipient, subject, sender, body template, or attachment metadata, and SHALL display delivery validation or failure feedback in the open dialog.

#### Scenario: Explicit send delivers reviewed email successfully
- **WHEN** a user explicitly sends a prepared deliverable invoice with an optional message and receipt selection
- **THEN** the system SHALL send the invoice PDF with the resolved recipient and subject, include selected eligible receipts, update delivery history, and present a completed delivery notification

#### Scenario: Document becomes unavailable after review opens
- **WHEN** the invoice PDF is no longer current when the user explicitly sends
- **THEN** the system SHALL reject email delivery, display actionable document feedback in the dialog, and SHALL not send email or record successful delivery

#### Scenario: Untrusted message text is reviewed safely
- **WHEN** an optional message contains HTML-like or otherwise untrusted text
- **THEN** the dialog SHALL display it as text and SHALL not render it as application HTML

### Requirement: Sending can issue a draft as an explicit follow-up
The system SHALL offer an unchecked Mark invoice as issued after sending option for draft invoices in the review dialog. It SHALL send email before attempting the requested issue transition and linked-gig completion workflow.

#### Scenario: User elects to issue after a successful delivery
- **WHEN** a user selects Mark invoice as issued after sending and explicit email delivery succeeds
- **THEN** the system SHALL run the normal issue and linked-gig completion workflow without showing a separate browser confirmation

#### Scenario: Follow-up issue work fails after delivery
- **WHEN** email delivery succeeds but issuing the invoice or completing linked gigs fails
- **THEN** the system SHALL preserve and report the successful email delivery and SHALL provide an actionable terminal error for the incomplete follow-up

#### Scenario: User leaves issue option unselected
- **WHEN** a user sends a draft invoice without selecting Mark invoice as issued after sending
- **THEN** the system SHALL leave the invoice as Draft and SHALL report successful delivery without requesting a separate issue confirmation
