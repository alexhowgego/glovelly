## Context

The current branch adds Google Sheet set-list import on top of existing gig external resources. A user links a Google Sheet attachment to a gig, previews worksheet rows, reviews parsed set-list items, and saves an active import for later review and eventual forScore generation.

The branch also adds a separate Google Sheets connection flow while the existing Google Drive invoice workflow and Google Calendar sync workflow already use a shared `GoogleConnection` record. That shared record stores one access token, one refresh token, and a merged string of granted scopes. If Drive, Sheets, and Calendar are connected independently with non-overlapping authorization requests, the stored scopes can say multiple integrations are connected while the latest token only represents the latest authorization grant.

## Goals / Non-Goals

**Goals:**
- Keep the shared Google connection state truthful for Drive, Sheets, and Calendar operations.
- Preserve separate UI affordances for connecting Drive and Sheets.
- Load worksheet metadata for the exact gig external resource the user selected.
- Convert expected token and Sheets API failures into actionable API responses.
- Add regression coverage for the reliability risks found during review.

**Non-Goals:**
- Generate forScore import files.
- Add migration automation or database schema deployment changes.
- Redesign all Google integrations into separate per-service connection tables.
- Change the persisted set-list import data model beyond what is needed for reliability.

## Decisions

### Use cumulative Google scopes for Drive, Sheets, and Calendar reconnects

When starting the Drive, Sheets, or Calendar authorization flow, the endpoint will inspect the current active `GoogleConnection`. If the user already has any other Glovelly-managed Google integration scopes, the new authorization request will include those scopes plus the scope required by the flow being started. This keeps the newly returned token material aligned with the merged `GrantedScopes` stored by `GoogleConnectionService.SaveConnectionAsync`.

Alternatives considered:
- Separate token rows per integration. This is cleaner long-term, but larger than needed for this slice and would require a broader data model and settings migration.
- One combined “Connect Google” flow. This simplifies token semantics but removes the current least-privilege/service-specific UX.
- Continue merging scopes without cumulative authorization. This preserves the bug where connection state can become false.

### Keep disconnect behavior scope-based

Drive, Sheets, and Calendar disconnect endpoints will remove only their respective integration scope from `GrantedScopes`. If other Glovelly-managed Google scopes remain, token material stays in place; if no scopes remain, the connection is revoked and token fields are cleared. Calendar disconnect will continue disabling calendar sync settings as part of disconnecting the integration.

### Make set-list source lookup resource-specific

The worksheet source endpoint will accept the selected `resourceId` and resolve metadata against that resource. Auto-selection remains useful only when no specific resource is supplied, such as direct API callers that want the primary set-list sheet. The frontend import modal will pass the resource ID when loading source metadata, matching preview and save behavior.

### Map expected Sheets import failures to API results

Set-list source and preview endpoints will catch expected failures from Google connection/token refresh and Sheets API reads. Reconnect-required conditions will return conflict-style responses that the UI can turn into a “Connect/Reconnect Google Sheets” action. Google API read failures will return a non-success result with a clear message instead of leaking implementation exceptions as 500s.

Expected failures include missing/expired/revoked connection, missing required scope, missing refresh token, failed token refresh, Google metadata read failure, Google worksheet values read failure, and missing worksheet metadata.

## Risks / Trade-offs

- Cumulative authorization still relies on one shared Google token record -> Mitigation: request all currently-connected Drive/Sheets/Calendar scopes whenever any Google integration reconnects, and test cross-service directions.
- Google may return narrower scopes than requested -> Mitigation: continue validating required scopes before saving/using the connection and surface validation errors.
- Existing users with previously inconsistent connection state may still have stale merged scopes -> Mitigation: handled token/API failures will ask the user to reconnect; reconnect requests cumulative scopes.
- Auto-selecting a primary set-list source can still surprise direct API callers -> Mitigation: frontend passes explicit resource IDs; auto-selection remains only a fallback.
