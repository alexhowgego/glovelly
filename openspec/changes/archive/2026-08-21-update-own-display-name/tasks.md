## 1. Self-Service API

- [x] 1.1 Extend the authenticated user-settings request and response in `AuthEndpoints` with `displayName`, validate a trimmed nonblank value against the existing 200-character limit, and persist it only on the authenticated local user.
- [x] 1.2 Ensure `/auth/me` continues to return the database-backed saved name and update all existing settings request fixtures for the required display-name contract.

## 2. Settings Interface

- [x] 2.1 Add `displayName` to `AuthUser` settings state, `UserSettingsForm`, and the form conversion/reset helpers.
- [x] 2.2 Add a pre-populated, required display-name input to `UserSettingsModal` without exposing administrative account fields.
- [x] 2.3 Include the name in `useUserSettings` save requests and apply the returned name to `AuthUser` after a successful response so the profile menu updates immediately.

## 3. Coverage And Documentation

- [x] 3.1 Add `AuthEndpointsTests` coverage for trimming, persistence, and the refreshed `/auth/me` name.
- [x] 3.2 Add validation tests for blank and overlong names that verify the previous value remains persisted.
- [x] 3.3 Add a two-user standard-access test proving a self-service request cannot select or change another user's name.
- [x] 3.4 Update the enrolment UAT checklist with self-service display-name save, immediate UI update, and refresh/sign-in persistence checks.

## 4. Verification

- [x] 4.1 Run `dotnet test glovelly.sln -m:1`.
- [x] 4.2 Run `npm --prefix frontend/glovelly-web run lint`.
- [x] 4.3 Run `npm --prefix frontend/glovelly-web run build`.

## 5. Administrator Cache Synchronization

- [x] 5.1 Synchronize the cached Administrator user entry after a successful self-service display-name save without overwriting unsaved administrator edits.
- [x] 5.2 Run frontend lint and build after the Administrator cache synchronization change.

## 6. Authenticated Profile Synchronization

- [x] 6.1 Synchronize the authenticated profile state when an administrator saves their own user record.
- [x] 6.2 Run frontend lint and build after the authenticated-profile synchronization change.
