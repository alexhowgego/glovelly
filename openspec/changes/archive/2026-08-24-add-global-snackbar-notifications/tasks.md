## 1. Sonner Integration

- [x] 1.1 Add the `sonner` dependency and create a thin Glovelly notification-policy wrapper for success, information, error, dismissal, session reset, semantic deduplication, and standard durations.
- [x] 1.2 Mount and configure Sonner's root `Toaster` with three visible notifications, polite announcements, close controls, and the Glovelly desktop/mobile placement.
- [x] 1.3 Add responsive Sonner theme overrides, including reduced-motion support and safe placement above existing modal and fixed-control layers.
- [x] 1.4 Add focused frontend tests for notification-policy deduplication, error persistence configuration, dismissal, and timeout selection using the existing Vitest setup.

## 2. Shared Feedback Migration

- [x] 2.1 Document the frontend Sonner toast-versus-inline feedback policy in the notification wrapper and identify redundant terminal `status-pill` rendering that can be removed without affecting validation, progress, or durable warnings.
- [x] 2.2 Migrate client, client-settings, user-settings, seller-profile, connected-service, and admin terminal success/error outcomes to notifications, preserving contextual validation and configuration feedback.
- [x] 2.3 Migrate gig create, update, delete, clone, expense, reimbursement, mileage, receipt, and external-resource attachment terminal outcomes to notifications while preserving editing and validation feedback.
- [x] 2.4 Migrate invoice workflow, invoice generation, monthly invoice, preview, delivery, Drive, and adjustment terminal outcomes to notifications, keeping compound delivery and issue results distinct.
- [x] 2.5 Migrate quick receipt, quick attachment, expense statement, forScore library, and other applicable modal terminal outcomes to notifications without replacing modal-local progress or recovery guidance.

## 3. Error And Download Handling

- [x] 3.1 Update each migrated request path to use user-safe Problem Details messages with action-specific fallbacks, without exposing internal implementation details.
- [x] 3.2 Replace expense-receipt new-window downloads with authenticated response/blob downloads and send both receipt and external-resource attachment terminal outcomes through notifications.
- [x] 3.3 Verify missing attachment objects return the issue #207 message in a persistent error notification and preserve existing session-expiry handling.

## 4. UAT And Agent Guidance

- [x] 4.1 Update affected `docs/uat/` scenarios to assert discoverable terminal notifications, persistent errors, navigation/modal continuity, and the retained inline validation/progress behaviour.
- [x] 4.2 Add mobile UAT coverage confirming notifications do not obscure fixed quick-action or return-to-top controls.
- [x] 4.3 Update `AGENTS.md` with the Sonner configuration and Glovelly notification-wrapper locations, the toast-versus-inline policy, and UAT expectations for future frontend changes.

## 5. Verification

- [x] 5.1 Run frontend unit tests, lint, and build; resolve any migration regressions.
- [x] 5.2 Run the affected browser UAT coverage iteratively and resolve notification-related expectation or interaction failures.
- [x] 5.3 Run `./verify.sh` and perform desktop/mobile manual accessibility checks for notification announcement, keyboard dismissal, modal stacking, and reduced motion.
