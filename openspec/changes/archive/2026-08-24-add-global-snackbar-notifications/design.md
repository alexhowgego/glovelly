## Context

Glovelly has no shared notification system. Workspace hooks and `App.tsx` maintain local mutable status strings that components render as `status-pill` elements. This is useful for validation, progress, and modal workflows, but terminal results are easily missed after navigation or modal dismissal and can be overwritten by another action.

The change spans the React application shell, workspace hooks, presentational components, shared API error handling, browser UAT scenarios, and agent orientation guidance. The application uses plain React and CSS with no component library or state-management dependency. Existing modal overlays use `z-index: 80`; mobile fixed controls occupy the bottom of the viewport.

## Goals / Non-Goals

**Goals:**

- Provide a single in-session notification mechanism for applicable terminal success, information, and error outcomes.
- Migrate the full current set of applicable terminal outcomes across clients, gigs, invoices, quick capture, settings, seller profile, integrations, and admin workflows.
- Keep errors visible until explicitly dismissed and make routine success/information messages self-clearing.
- Preserve contextual inline validation, progress, multi-step workflow, and durable health/configuration feedback.
- Make notifications accessible, mobile-safe, and available across workspace navigation and modal closure.
- Update browser UAT documentation and agent orientation so future work applies the feedback policy consistently.

**Non-Goals:**

- Replacing form validation, progress displays, or persistent configuration/health warnings with toasts.
- Persisting notifications through a browser reload, sign-out, or new session.
- Introducing a general-purpose component library or state-management library beyond the focused Sonner notification dependency.
- Retrofitting every existing API endpoint or adding an application-wide error boundary.
- Adding retry behaviour where an existing operation does not already have a safe retry path.

## Decisions

### Use Sonner with a thin Glovelly policy wrapper

Add the MIT-licensed, React 19-compatible `sonner` package. Mount its `Toaster` once at the frontend root beside `App`, configured for the global desktop and mobile placement, three visible notifications, a close button, and the Glovelly visual theme.

Create a small Glovelly notification-policy wrapper around Sonner's `toast` API. It exposes success, information, error, dismissal, and session-reset operations to workspace hooks and components. The wrapper applies standard durations, uses `Infinity` for errors, and maps caller-provided semantic deduplication keys to Sonner toast IDs.

Sonner supplies the presentation, visible-item limit, timeout handling, hover/focus pause, dismissal controls, interaction handling, and mobile offsets. The wrapper keeps Glovelly policy and library-specific calls out of business-workflow code, avoiding a bespoke context, queue, timer, portal, and event-store implementation.

### Separate notification policy from local workflow state

Use notifications for terminal outcomes only:

- Success: completed create, save, delete, upload, download, delivery, or integration action.
- Information: an important terminal outcome that is neither success nor failure.
- Error: unexpected request failure or an error whose initiating view may close or change.

Keep field validation, eligibility guidance, in-progress work, multi-step modal outcomes, and durable warnings in their current contextual UI. A migrated terminal outcome must not be announced both as a new toast and a redundant local status pill. Some operations can retain a local result where it remains materially useful, but the notification is the primary completion feedback.

This is deliberately a policy-driven migration rather than a mechanical replacement of every `set*Status` call.

### Queueing, deduplication, and lifetime

Sonner is configured to show at most three notifications in its collapsed viewport while retaining additional notifications. Errors are never silently discarded.

Callers can provide semantic deduplication keys such as `gig:<id>:attachment-download`. A matching notification replaces the prior notification and resets its timeout, preventing repeated operation feedback from stacking.

Success notifications auto-dismiss after approximately five seconds; information notifications after approximately six seconds. Errors have no timeout and require explicit dismissal. Timed notifications pause while hovered or keyboard-focused. Notification state is cleared for a new unauthenticated session and is never stored in browser persistence.

### Accessible, non-modal presentation

Sonner provides a fixed non-modal notification viewport with a polite live region for all notification types. Persistent error notifications provide durable visual feedback without interrupting the user's current screen-reader task. Notifications never take focus; each has a keyboard-accessible dismiss button. The visible notification content is announced once rather than duplicated in a hidden live region.

Desktop placement is top-right. Mobile placement is top-centre with safe-area insets and sufficient top offset to avoid the app header; bottom placement is avoided because of the existing fixed quick-action and return-to-top controls. The viewport itself does not intercept pointer events outside its notification cards. Motion respects `prefers-reduced-motion`.

### Normalize error messages as each action is migrated

Migrated actions use the existing authenticated fetch and Problem Details helpers where applicable. They show a user-safe server `detail`, validation message, or title when available, with an action-specific fallback. This includes converting expense-receipt downloads from a new-window navigation to the existing authenticated blob-download pattern, allowing the explicit missing-attachment response from issue #207 to reach the notification system.

Avoid a broad unrelated API-error refactor. Correct error-message handling only at action paths included in the migration.

### Treat UAT and agent orientation as delivery work

Update the relevant `docs/uat/` scenarios to cover terminal notifications, persistent errors, contextual feedback that remains inline, modal/navigation continuity, and mobile placement. Update `AGENTS.md` with the Sonner configuration and Glovelly notification-wrapper locations and the toast-versus-inline policy so future agents add feedback consistently.

## Risks / Trade-offs

- [Notification fatigue from full migration] -> Limit messages to terminal outcomes, deduplicate repeated operations, and retain local feedback for guidance and progress.
- [Duplicate or conflicting feedback during migration] -> Review each status call by category and remove redundant terminal status-pill rendering only after its toast path is in place.
- [Screen-reader announcement noise] -> Use Sonner's single polite live region, announce visible content once, and retain important recovery guidance in contextual UI.
- [Toasts obscure modal or mobile controls] -> Use a dedicated layer above overlays, a top-based responsive layout, safe-area spacing, and manual browser UAT verification.
- [Concurrent operations overwrite feedback] -> Queue independent notifications rather than reuse one mutable workspace status string.
- [Generic errors hide useful server detail] -> Migrate calls through the shared Problem Details helpers and test representative failure paths.
- [UAT churn] -> Update scenarios alongside each workflow migration and run the browser suite iteratively rather than treating documentation as a final cleanup.

## Migration Plan

1. Add and verify Sonner, the Glovelly notification-policy wrapper, root toaster configuration, styles, accessibility, and focused wrapper tests.
2. Migrate terminal feedback by workflow domain, removing only redundant status pills while retaining local progress, validation, and durable warnings.
3. Convert browser attachment downloads to authenticated response handling and notify missing-file failures using the backend response from issue #207.
4. Update UAT scenarios and `AGENTS.md`, then run frontend lint/build and the affected browser UAT coverage.
5. Rollback is limited to reverting the frontend change; no server schema, persisted state, or API contract is introduced. Local contextual status remains available during incremental migration, reducing rollback risk.

## Open Questions

- None. The initial policy is to migrate all currently applicable terminal outcomes and extend the system for future suitable actions.
