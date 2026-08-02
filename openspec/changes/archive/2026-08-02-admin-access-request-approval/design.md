## Context

Unauthorised Google-authenticated users can create an `AccessRequest`; active administrators receive an email with requester details, then manually create a matching `User` in the Admin workspace. The request stores verified-email-derived identity data and notification metadata, but no review state. Existing Google enrolment binds a user's Google subject only after normal sign-in with a verified email matching an active provisioned user.

The application is a React SPA served by the ASP.NET Core API. Its profile menu already exposes a badge-counted Imported gigs review modal, and its sign-in flow preserves the current URL as the safe return location. Admin user creation and invitation delivery already exist under the active-admin authorization policy.

## Goals / Non-Goals

**Goals:**
- Replace manual email transcription with an authenticated request-review and provisioning journey.
- Make a notification email open the same review modal available from an admin's profile menu.
- Preserve email-link non-authority and existing Google verified-email/subject binding semantics.
- Make approvals and declines auditable, idempotent, and safe under concurrent administration.

**Non-Goals:**
- Granting access, provisioning a user, or changing a request from an email-link GET.
- Changing recipient invitation acceptance wording or the Google first-login protocol.
- Building a general-purpose audit-log system or an administrator request-management page outside the modal.
- Editing the requester's stored email or display name during review.

## Decisions

### Persist request lifecycle and review metadata on `AccessRequest`

Add a status enum with `Pending`, `Provisioned`, `Declined`, and `Expired`, together with decision time, reviewing administrator ID, provisioned-user ID, and an optional decision note. Preserve the identity and request metadata already recorded.

The status is the source of truth, rather than inferring an outcome from matching users, so the application can show a definitive audit trail and safely return terminal results. User IDs are retained as nullable scalar references to avoid complicating the existing `User` navigation model.

### Use a configurable approval window distinct from retention

Pending requests become `Expired` once they exceed an approval window, initially 30 days. The existing 180-day retention policy continues to delete old request records, including historical decisions, to minimise personal-data retention.

Thirty days gives an administrator time to act without allowing notification links to remain actionable for the full retention period. Expiry is evaluated in list, detail, and decision operations and persisted when observed, so a request cannot be approved after expiry.

### Make the email link a deep link, not a credential

The notification includes a URL such as `/access-requests/{requestId}`. The SPA reads that path, opens the access-request modal, and preselects the request. An unauthenticated visitor uses the existing `/auth/login?returnUrl=...` path and returns to the same URL after authentication.

The identifier only targets the UI. Request APIs independently require an authenticated, active administrator, and the link GET never performs a state mutation. A signed review token was rejected because it would add expiry and token-leakage management without replacing authorization.

### Reuse the profile-menu review pattern

Add an admin-only **Access requests** profile-menu item with a pending count and notification dot, matching Imported gigs. It opens a modal with a pending-request list and selected request detail.

The selected request exposes non-editable identity details, role and active-state choices, a checked-by-default invitation-email choice, an approve action, and an immediate decline action protected by a confirmation dialog. This keeps all entry points in one consistent surface and avoids adding an inbox to the Admin section.

### Provision from the stored request, then preserve first-login binding

Approval creates a `User` using the request's normalized email and display name, plus the reviewer's chosen role and active state. It does not copy the request's Google subject; the existing normal first-login flow remains responsible for binding the subject after Google provides a verified matching email.

If the request email already belongs to a user, approval does not create a second record. The backend returns the terminal/request state and either records the existing user as provisioned when it is an appropriate match or reports a conflict that requires the admin to resolve the user through existing Admin tools. Atomic status transition and unique-email handling make repeat/concurrent approvals safe.

### Make invitation delivery an explicit post-provisioning option

Approval accepts `sendInvitationEmail`. Provisioning succeeds even if the selected email cannot be sent; the response must distinguish provisioning success from invitation delivery failure so the administrator can retry through the existing user-invitation facility.

This supports the agreed optional checkbox without turning notification delivery into a reason to roll back an approved access decision.

## Risks / Trade-offs

- [Concurrent approval can race with manual provisioning] -> Use a transaction/concurrency-safe conditional pending-to-terminal transition and re-check the unique user email before creation.
- [A request ID leaks through an email forward or browser history] -> Treat it as untrusted routing data; do not expose request content or mutate state without active-admin authorization.
- [A failed invitation could make approval appear to have failed] -> Return and display a distinct provisioned-with-email-failure state with a retry path.
- [A modal-only surface may become unwieldy with many requests] -> Start with pending requests and preselection; retain API pagination/filtering options if volume grows.
- [Retention removes audit history] -> Retain the existing 180-day privacy policy; introduce a durable audit system only if governance requirements later demand it.

## Migration Plan

1. Add nullable lifecycle/audit columns and a status value defaulting existing rows to `Pending` through an EF migration.
2. Deploy the backend APIs and notification deep link before or together with the SPA, so older clients retain their existing access-request behaviour.
3. Deploy the modal and profile-menu entry. Existing records remain reviewable until their approval window expires.
4. Roll back application code if necessary; the additive columns remain harmless and existing access-request submission continues to work.

## Open Questions

- None. The initial 30-day approval window is configurable if operational experience suggests a different duration.
