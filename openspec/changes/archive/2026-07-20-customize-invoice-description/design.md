## Context

Invoices already persist a document-level `Description`, which is displayed as "In respect of" in the invoice detail and rendered in the PDF's Description block. The Line items pane currently supports manual adjustments but has no control for that document-level value. The broad invoice update endpoint accepts a complete invoice payload, making it unsuitable for a narrowly scoped editor.

PDF redraft and reissue operations render the saved invoice entity and its saved lines. They do not rebuild the invoice description from linked gigs, so persistence is sufficient for regeneration to retain a custom description.

## Goals / Non-Goals

**Goals:**
- Let an authenticated owner edit a Draft invoice's non-empty document-level description from the Line items pane.
- Persist only the description through a focused API operation and return the refreshed invoice for client state replacement.
- Keep a saved description when the Draft invoice is redrafted and its PDF is regenerated.
- Prevent mutations to a non-Draft invoice's description.

**Non-Goals:**
- Editing individual generated or adjustment line descriptions.
- Automatically regenerating a PDF when the description is saved.
- Changing an issued PDF or permitting post-issue document-content changes.
- Changing the generated default description or gig data.

## Decisions

### Use an invoice-level, description-only endpoint

Add a dedicated update route beneath the invoice resource that accepts a small request containing `description`. The handler will load the invoice through the existing user-visibility scope, reject anything other than `Draft`, trim and validate a non-empty value, stamp the update, save, and return the invoice with lines.

This avoids exposing a broad full-invoice update from a small inline field and keeps status and document-integrity rules at the API boundary.

Alternative considered: reuse `PUT /invoices/{id}`. Rejected because the frontend would have to submit and maintain unrelated invoice fields, increasing the risk of overwriting dates, client, number, or status.

### Put the editor in the existing Line items pane

The pane will display a labelled text field initialized from the selected invoice description and a deliberate Save action. It will be editable only while the selected invoice is Draft; all other statuses present the description as read-only in that pane. The existing invoice detail display remains unchanged.

Alternative considered: place editing in the invoice detail summary. Rejected for now because the Line items pane is the established location for draft-level invoice adjustments and the requested discovery point.

### Do not regenerate on save

Saving changes only the persistent invoice description. The normal Redraft action generates a replacement draft PDF using that saved value. This separates text editing from document generation and retains the existing user-confirmed regeneration workflow.

## Risks / Trade-offs

- [A user may expect an existing PDF download to update immediately] -> The UI will communicate a successful save without claiming regeneration; the existing Redraft action remains the explicit document refresh.
- [Client state can show stale input after selecting another invoice] -> The editor state will be synchronized when the selected invoice changes and replaced from the successful API response.
- [A non-Draft client can attempt the route directly] -> The backend independently enforces the Draft-only constraint and returns a validation response.
- [Blank descriptions could produce a poor document] -> The API rejects blank or whitespace-only values after trimming.
