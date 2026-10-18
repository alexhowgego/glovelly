## 1. Shared overview and dialog foundations

- [x] 1.1 Inventory current overview actions, warnings, detail fields, and existing dialog/focus utilities; map each field/action to the design's workspace content table and retain existing business restrictions.
- [x] 1.2 Add or reuse a small accessible detail-dialog shell with bounded scrolling, initial focus, keyboard containment, Escape/close behavior, body-scroll locking, and trigger/fallback focus restoration.
- [x] 1.3 Implement section-level record-ID-bound dialog state and context invalidation with busy/dirty guards; ensure authoritative updates and stale asynchronous completions cannot retarget another record.
- [x] 1.4 Coordinate existing parent/child modal layers so only the top layer is interactive, parent drafts/scroll context survive child operations, and Escape/body locks restore correctly.

## 2. Gig overview and focused management dialogs

- [x] 2.1 Replace the full gig detail stack with compact booking facts, notes preview, resource count, expense count/total, and explicit full-text/collection entry points, including zero-item states.
- [x] 2.2 Present complete gig notes, identity, location, and invoice linkage inline, retaining note editing in the existing editor and scrolling for unusually long content.
- [x] 2.3 Move resource management into its focused dialog using existing components and callbacks; preserve creation/editing, primary resources, files, downloads, deletion, and set-list import without parallel inline implementations.
- [x] 2.4 Move expense and receipt management into its focused dialog; preserve expense editing, reimbursement, receipt upload/download/delete, and analysis through existing state/mutation owners.
- [x] 2.5 Preserve gig invoice/statement generation, linked-invoice navigation, cloning, and deletion with compact action placement, stable-ID operations, and unchanged eligibility/confirmation behavior.

## 3. Other record overviews

- [x] 3.1 Show complete Client identity/contact/billing details inline, with monthly invoicing, settings, existing edit actions, and long-content scrolling rather than a read-only details dialog.
- [x] 3.2 Compact Invoices overview with status, dates, total, description/document state, delivery summary, and complete metadata/workflow detail access; retain all delivery/PDF/reissue/delete rules and durable warning actions.
- [x] 3.3 Preserve the existing invoice line-items/adjustments/description editor and gig-line navigation; adjust action grouping and email-review trigger/focus restoration if needed.
- [x] 3.4 Compact Admin Users overview with role/access/enrolment and last-login facts plus full identity/account metadata access; retain existing access-edit/delete restrictions and summary-strip behavior.

## 4. Layout, navigation, and feedback integration

- [x] 4.1 Apply fitted overview CSS for all four workspaces at 1280×800 and 1440×900 with editors both closed/open, preserving readable controls and independently scrollable lists/editors/dialogs.
- [x] 4.2 Keep mobile, short-window, enlarged-text, durable-warning, and zoom fallbacks accessible; provide full text for bounded previews without tooltip-only recovery or hiding required content.
- [x] 4.3 Preserve capture-to-gig and cross-workspace selection/reveal behavior, list scroll position on dialog close, and sensible focus fallback when records or triggers disappear.
- [x] 4.4 Audit relocated operations against notification policy: still-open-dialog feedback inline, outliving terminal feedback through the notification wrapper, no automatic close/navigation on attachment success, and notifications below modal controls.

## 5. Regression coverage and user documentation

- [x] 5.1 Add component tests for overview facts/counts/totals, empty states, full-text access, record action restrictions, and dialog open/close/focus behavior across all four workspaces.
- [x] 5.2 Add tests for parent/child modal coordination, dirty/busy dismissal, selection/workspace changes, deletion, and in-flight completion identity; verify original-record saves never populate another record's dialog.
- [x] 5.3 Update browser UAT helpers and journeys that previously accessed inline resources/expenses, retaining expense/receipt/resource/set-list workflows and invoice navigation/delivery coverage.
- [x] 5.4 Extend browser geometry tests at both desktop targets to assert bounded documents/panes, independently scrolling long standard details, scroll reset on record changes, and reachable collection-dialog/editor contents using long-text and large-collection fixtures.
- [x] 5.5 Add browser keyboard/mobile/short-window/effective-zoom coverage, including child modal return, menus, focus restoration, durable warnings, and notification placement.
- [x] 5.6 Update relevant docs/uat journeys and public guide instructions for deliberate drill-down; update documentation-capture steps to open the intended dialog when capturing resources/expenses.

## 6. Validation and capture review

- [x] 6.1 Run frontend tests, lint, and production build; compile the browser test project without running Playwright locally.
- [x] 6.2 Run ./verify.sh and affected guide checks/build; record actual results and blockers separately from browser validation.
- [x] 6.3 Run browser UAT in Ubuntu CI and review geometry plus end-to-end workflow results; do not assume the preceding change's waived checks prove this layout. **Resolved by explicit user waiver at archive, not by a successful browser validation run.**
- [x] 6.4 Generate staging documentation-capture candidates in Ubuntu CI, review light/dark pairs for changed workspace/dialog subjects, and replace checked-in screenshots only after reviewing real captures; never manufacture substitutes locally. **Resolved by explicit user waiver at archive; capture generation/review was not performed and no screenshots were replaced.**

## Implementation and validation notes

- Latest review refinement: standard details are now fully inline and left-aligned, including gig notes, client billing details, invoice metadata, and all Admin account fields. Removed redundant read-only detail dialogs/buttons. The middle region uses normal available space and scrolls for anomalous content; it resets on record changes. Only collection/task controls retain a dock. The user explicitly relaxed the absolute no-detail-scrolling requirement, and the proposal/design/specs and regression expectations now reflect that decision. Real-browser geometry was not verified; the remaining check was explicitly waived at archive.
- Confirmed with the user during implementation: external record/selection/workspace invalidation closes stale management dialogs with an explicit possible-discard notice. Manual dismissal continues to use busy/dirty guards. The design and dialog spec capture this distinction.
- Reused existing editor areas and domain management components. Detail dialogs use the native browser modal layer, with child-layer inertness, parent scroll preservation, and focus restoration; no component library or API changes were introduced.
- Guarded delayed saves/merges against replacing another selected gig's form; detail refreshes do not resurrect deleted records, and overlapping operations retain the busy guard until all have finished.
- ./verify.sh passed: 464 backend tests, 66 frontend tests, lint, and production build. The existing large-bundle advisory remains.
- Browser test project builds successfully. Guide check and build passed; the guide build retains existing i18n/404 content warnings.
- Tasks 6.3 and 6.4 were resolved by a separate explicit user waiver for this change at archive, not by successful CI validation. No Playwright/browser capture tests were run locally and no checked-in screenshots were replaced. Geometry and real-browser modal behavior remain unverified.
- No git commits or pushes were created.
