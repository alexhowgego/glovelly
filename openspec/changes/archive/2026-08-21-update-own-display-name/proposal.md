## Why

Active standard users can currently see their account name but cannot correct or maintain it without an administrator. They need a safe self-service profile setting that does not expose account-administration controls.

## What Changes

- Add a display-name field to the signed-in user's Settings dialog, pre-populated with the current value.
- Allow an active user to save a validated display name for only their own account.
- Update the in-memory authenticated-user state after a successful save so name-bearing UI updates immediately.
- Synchronize an administrator's cached user-list entry after a self-service name change.
- Synchronize the signed-in user's profile state after an administrator edits their own account.
- Preserve the existing administrator user-management journey and prevent self-service changes to email, role, activation status, or another user.
- Add authorization, validation, persistence, and manual UAT coverage.

## Capabilities

### New Capabilities
- `self-service-profile`: Active users can maintain their own display name through the authenticated profile settings surface.

### Modified Capabilities

None.

## Impact

- Backend: `/auth/me` and `/auth/me/settings` contracts and validation in `AuthEndpoints`.
- Frontend: authenticated-user and settings form types, settings hook, settings modal, and profile menu state.
- Tests: `AuthEndpointsTests` self-service ownership, validation, and persistence coverage.
- Documentation: enrolment/settings UAT checklist.
