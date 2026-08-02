## 1. Access-Request Lifecycle

- [x] 1.1 Add access-request lifecycle status and review/provisioning audit fields, including the configurable 30-day approval window.
- [x] 1.2 Configure the new fields and indexes as needed, create the EF migration, and preserve the existing 180-day retention cleanup behaviour.
- [x] 1.3 Extend the access-request workflow service to expire stale pending requests and make terminal decisions concurrency-safe and idempotent.

## 2. Administrator Review APIs And Notifications

- [x] 2.1 Add active-admin-authorized endpoints to list pending requests, read a selected request, approve it with role/active/invitation options, and decline it.
- [x] 2.2 Provision users from immutable stored request identity, preserve normal Google first-login binding, and report existing-user and invitation-delivery outcomes safely.
- [x] 2.3 Add an access-request review deep link to HTML and plain-text administrator notification emails without mutating state on GET.
- [x] 2.4 Update Vite proxy and service-worker API bypass configuration if the review endpoints use a new frontend API prefix.

## 3. Access-Request Review Interface

- [x] 3.1 Add frontend access-request types and a stateful workspace hook for loading, selecting, approving, declining, and refreshing pending requests.
- [x] 3.2 Add an accessible access-request review modal with immutable requester identity, role and active-state choices, checked-by-default invitation option, approval feedback, and confirmed immediate decline.
- [x] 3.3 Add the admin-only, badge-counted Access requests profile-menu entry and notification indicator using the existing Imported gigs interaction pattern.
- [x] 3.4 Parse access-request deep-link paths so sign-in return navigation opens the modal with the addressed request selected, and handle missing, expired, terminal, and unauthorized requests clearly.

## 4. Coverage And Documentation

- [x] 4.1 Add backend integration coverage for authorization, lifecycle/expiry, deep-link email rendering, approval, decline, duplicate/existing-user, concurrency/idempotency, and invitation-delivery outcomes.
- [x] 4.2 Update enrolment UAT with the administrator notification-to-review-to-provisioning journey, invitation option, decline confirmation, and normal requester first sign-in.
- [x] 4.3 Run relevant backend tests, frontend lint/build checks, and the broader verification required by the change.
