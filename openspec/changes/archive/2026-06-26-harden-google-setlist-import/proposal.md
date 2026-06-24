## Why

Set-list import now depends on linked Google Sheets, but Glovelly stores Drive, Sheets, and Calendar Google authorization state in one shared connection. The current branch can report integrations as connected when the stored Google token may not actually satisfy every recorded Google scope, can load worksheet metadata for a different linked resource than the user selected, and can expose Google/API failures as generic server errors. These issues would make the new import journey unreliable before it becomes the foundation for forScore set-list generation.

## What Changes

- Keep Google integration connection state aligned with the token material used for Drive, Sheets, and Calendar operations.
- Ensure set-list import source metadata is resolved for the specific gig attachment/resource the user selected.
- Return actionable set-list import errors for reconnect-required and Google Sheets read failures instead of generic 500 responses.
- Add regression coverage for multi-resource import selection, combined Google scopes across Drive/Sheets/Calendar, and handled Sheets/token failure paths.

## Capabilities

### New Capabilities
- `google-setlist-import-reliability`: Reliable Google Sheet set-list import behavior, including selected-resource resolution, shared Google authorization state, and user-facing import errors.

### Modified Capabilities

## Impact

- Backend endpoints under `/gigs/{gigId}/setlist-imports`.
- Google Drive, Google Sheets, and Google Calendar integration connect/disconnect flows under `/integrations/google-drive`, `/integrations/google-sheets`, and `/integrations/google-calendar`.
- Shared Google connection scope/token handling.
- Frontend set-list import modal source loading.
- Backend integration tests for Google scopes and set-list import endpoints.
