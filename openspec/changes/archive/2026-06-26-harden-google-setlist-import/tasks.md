## 1. Google Connection Semantics

- [x] 1.1 Add tests proving Sheets connect after Drive requests both Drive and Sheets scopes.
- [x] 1.2 Add tests proving Drive connect after Sheets requests both Sheets and Drive scopes.
- [x] 1.3 Add tests proving Calendar connect after Drive or Sheets requests Calendar plus existing Google service scopes.
- [x] 1.4 Add tests proving Drive or Sheets connect after Calendar requests the Calendar scope plus the newly required service scope.
- [x] 1.5 Update Drive, Sheets, and Calendar connect endpoints to build authorization scopes from the required service scope plus any currently connected Glovelly-managed Google scopes.
- [x] 1.6 Keep disconnect behavior scope-specific for Drive, Sheets, and Calendar, and verify token material is only cleared when no Google scopes remain.

## 2. Resource-Specific Set-List Source Loading

- [x] 2.1 Add a backend test with multiple Google Sheet set-list resources where source metadata resolves for the explicitly selected resource.
- [x] 2.2 Update the set-list source endpoint to accept and validate an optional `resourceId`.
- [x] 2.3 Update the set-list import modal to pass the selected resource ID when loading worksheet metadata.

## 3. Actionable Sheets Import Errors

- [x] 3.1 Add backend tests for reconnect-required failures during set-list source or preview loading.
- [x] 3.2 Add backend tests for Google Sheets metadata/value read failures returning user-facing non-500 responses.
- [x] 3.3 Add backend test coverage for empty worksheet metadata returning a user-facing error.
- [x] 3.4 Update set-list source and preview endpoints to catch expected token and Sheets API failures and map them to explicit problem responses.
- [x] 3.5 Ensure the frontend import modal still detects reconnect-required responses and presents the Google Sheets connect action.

## 4. Verification

- [x] 4.1 Run `dotnet test glovelly.sln -m:1`.
- [x] 4.2 Run `npm --prefix frontend/glovelly-web run lint`.
- [x] 4.3 Run `npm --prefix frontend/glovelly-web run build`.
