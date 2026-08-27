## Why

Glovelly currently reports many completed actions and failures through local, mutable status pills. Those messages can be difficult to find after navigation or a modal closes, and later operations can overwrite them. A global snackbar system will make applicable feedback consistently visible while retaining local feedback where the user needs context to act.

## What Changes

- Add a global, accessible snackbar notification system for in-session success, information, and error feedback using Sonner.
- Show terminal outcomes for applicable create, save, delete, upload, download, delivery, and integration actions as snackbars across clients, gigs, invoices, quick capture, settings, seller profile, and admin workflows.
- Keep errors visible until dismissed; auto-dismiss success and informational notifications.
- Retain contextual inline feedback for validation, progress, multi-step modal workflows, and durable configuration or health warnings.
- Standardize migrated API-error handling so user-safe Problem Details messages are shown when available.
- Update browser UAT scenarios for the notification behaviour and update agent orientation guidance so future changes use the system consistently.

## Capabilities

### New Capabilities

- `global-snackbar-notifications`: Provides accessible global notifications for applicable terminal user-action outcomes while preserving contextual feedback where required.

### Modified Capabilities

- None.

## Impact

- Frontend React application: Sonner integration, a Glovelly notification-policy wrapper, styling, and workspace action feedback.
- Frontend dependency graph: add the MIT-licensed, React 19-compatible `sonner` package.
- Frontend browser download handling for attachment failures, including missing attachment objects addressed by issue #207.
- Existing frontend API error-message call sites as they are migrated.
- UAT documentation and agent orientation files, especially `AGENTS.md`.
