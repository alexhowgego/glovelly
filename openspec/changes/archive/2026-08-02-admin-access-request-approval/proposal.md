## Why

Access-request notifications currently require an administrator to manually re-enter the requester's email when provisioning them. That duplicates trusted request data, is error-prone, and makes a routine approval unnecessarily slow.

## What Changes

- Add an authenticated administrator review workflow for recorded access requests, with pending, provisioned, declined, and expired outcomes.
- Let an administrator approve a request by choosing the user's role, active state, and whether to send an invitation email; provision the user from the stored request identity without editable email entry.
- Add an administrator-only access-request review modal that opens from a badge-counted profile-menu item and can preselect a request from an email deep link.
- Update access-request notifications with a clear review action that navigates to the request but never mutates state.
- Record review, decision, and provisioning audit metadata; safely handle concurrent decisions, duplicate requests, expired requests, and users already provisioned by another path.

## Capabilities

### New Capabilities
- `admin-access-request-approval`: Secure administrator review, approval, decline, audit, and deep-link access to pending access requests.

### Modified Capabilities
- None.

## Impact

- Backend access-request model, EF configuration and migration, workflow service, notification email, and admin-only endpoints.
- React profile menu, access-request modal/workspace state, deep-link handling, API types, and service-worker/Vite API configuration if a new API prefix is introduced.
- Backend integration tests and enrolment UAT coverage.
