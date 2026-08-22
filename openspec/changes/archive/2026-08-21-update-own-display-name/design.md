## Context

`User.DisplayName` is already persisted and constrained to 200 characters. Administrators can edit it through `/admin/users/{id}`, while authenticated users manage personal defaults through the protected `/auth/me/settings` endpoint. That endpoint identifies the record from the authenticated principal and is the appropriate self-service boundary.

The frontend keeps the current `/auth/me` response in `AuthUser`. `AppShell` renders the profile name and initials from `AuthUser.name`; `useUserSettings` owns the modal form, settings request, and authenticated-user updates.

## Goals / Non-Goals

**Goals:**
- Let active users view, validate, save, and immediately see their own display name.
- Maintain server-side ownership by deriving the target user only from authentication.
- Reuse the existing settings endpoint, error presentation, form lifecycle, and data model.
- Preserve a saved name across `/auth/me` refreshes and later authenticated sessions.

**Non-Goals:**
- Editing email addresses, Google subjects, roles, activation status, or any other user's profile.
- Changing the administrator user-management API or UI.
- Adding profile images, public profiles, audit history, or a database migration.

## Decisions

### Extend the existing self settings contract

Add `displayName` to `PUT /auth/me/settings`, the saved settings response, and the `/auth/me` representation. The server will trim and validate the supplied value, then assign it to the `User` record found with the authenticated user ID.

This keeps all personal account preferences in one user-facing dialog and retains the established ownership mechanism. A separate profile endpoint would add a second request and validation path without a different authorization model.

### Require a nonblank display name within the existing persistence bound

The self-service request will reject blank or whitespace-only names and values longer than 200 characters before saving. The UI will present the API's standard problem-details message and retain the previously saved state after rejection.

The existing nullable storage remains unchanged to accommodate historical or externally provisioned records; the self-service operation itself produces a nonblank value.

### Update the client session model from the response

On a successful settings request, `useUserSettings` will put the returned name into `AuthUser.name` alongside the saved defaults. `AppShell` already derives the visible profile summary and initials from that state, so no reload or separate user fetch is needed.

The administrator workspace holds a separate cached list of `AdminUser` records. Expose a narrow cache-update action from that workspace and call it after a successful settings save. It will update only the matching user row and will synchronize an open self-editor only when it has no unsaved administrative changes.

Conversely, an administrator save returns the persisted `AdminUser`. The workspace will notify `App` after that save; `App` updates `AuthUser` only when the response belongs to the current user, using the saved display name and email. This keeps the avatar dropdown synchronized without giving the workspace ownership of session state.

### Cover ownership using two persisted users

Endpoint tests will seed an active standard user, send a request authenticated as that user, and assert the standard user's name changes while the original user's name remains unchanged. The request payload will not offer a supported target-user field; a stray client-supplied identifier must not alter server-side target selection.

## Risks / Trade-offs

- [Existing API callers omit the new field] → Update the frontend and affected integration fixtures together; validate that all settings calls include the pre-populated value.
- [An initial user has no display name] → Use the current `/auth/me` fallback name (email) to pre-populate the form, allowing the user to establish an explicit display name.
- [A stale authentication cookie has an old name claim] → The app renders the database-backed `/auth/me` value, and claims transformation reloads the local user on authenticated requests.
- [Self-service scope broadens accidentally] → Use a narrow request shape containing only display name and existing personal defaults; do not accept account identifiers or administrative fields.

## Migration Plan

No schema migration is required. Deploy the API and frontend together so the Settings form sends the new field and applies the returned name. Rollback is safe because the database column and prior admin workflow already support display names; saved values remain valid if the frontend is reverted.

## Open Questions

None.
