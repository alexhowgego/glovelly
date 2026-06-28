## 1. Test Infrastructure And Helpers

- [x] 1.1 Review existing `tests/Glovelly.Uat.Tests` helpers and identify reusable setup/navigation methods for the new browser journeys.
- [x] 1.2 Add small shared helpers for run-specific clients, gigs, invoices, manual adjustment lines, selected-record assertions, and stale search setup where duplication would otherwise make tests brittle.
- [x] 1.3 Add or reuse tiny upload fixtures for receipt and attachment browser tests, ensuring downloads can be asserted as non-empty.
- [x] 1.4 Add helper patterns for scoped browser dialog handling that can explicitly accept or decline prompts around one action.

## 2. Cross-Workspace Navigation Coverage

- [x] 2.1 Add a Playwright UAT test that verifies gig and invoice client shortcuts open Clients with the intended client selected despite stale search filters.
- [x] 2.2 Add a Playwright UAT test that verifies generated invoice line shortcuts open Gigs with the intended linked gig selected.
- [x] 2.3 Add an assertion that manual adjustment invoice lines do not expose gig navigation shortcuts.
- [x] 2.4 Update the cross-workspace navigation automation status in `docs/uat/pre-merge-regression.md`.

## 3. Dirty Editor Guard Coverage

- [x] 3.1 Add client editor UAT coverage for declining and accepting discard prompts when selecting another client.
- [x] 3.2 Add gig editor UAT coverage for declining and accepting discard prompts when selecting another gig after a changed field or unsaved expense draft.
- [x] 3.3 Verify discarded edits are not persisted to the original record after accepted navigation.
- [x] 3.4 Update the editor navigation regression automation status in `docs/uat/pre-merge-regression.md`.

## 4. Imported Gig Review Coverage

- [x] 4.1 Determine the safest authenticated setup path for staging imported gig batches for the UAT user without relying on a real MCP client.
- [x] 4.2 Add browser UAT coverage for profile imported-gig notification state and opening the Imported gigs modal.
- [x] 4.3 Add browser UAT coverage for row autosave persistence after closing/reopening the modal or switching batches.
- [x] 4.4 Add browser UAT coverage for accepting, rejecting, committing, and confirming accepted rows become gigs while rejected and pending rows behave correctly.
- [x] 4.5 Update `docs/uat/gig-imports.md` and the pre-merge imported-gig status line to reflect the new coverage and remaining manual MCP/client checks.

## 5. Upload And Quick Capture Coverage

- [x] 5.1 Add browser UAT coverage for uploading, downloading, and deleting a receipt on a gig expense while preserving reimbursement state.
- [x] 5.2 Add browser UAT coverage for creating a file-only gig attachment, uploading/downloading/deleting the attached file, and preserving the attachment shell.
- [x] 5.3 Add mobile-sized viewport browser UAT coverage for the quick attachment floating action and target gig review/change behavior.
- [x] 5.4 Update `docs/uat/expenses.md`, `docs/uat/gig-external-resources.md`, and the pre-merge attachment status line for the new coverage.

## 6. Expense Statement Variant Coverage

- [x] 6.1 Expand expense statement UAT coverage so reimbursed expenses are visually distinct and excluded from totals by default.
- [x] 6.2 Add coverage for explicitly including a reimbursed expense in statement preview or download output.
- [x] 6.3 Add coverage for mixed-client selection blocking before statement generation.
- [x] 6.4 Add coverage proving expense statement preview/download for an invoiced gig does not create invoices or mutate gig invoice links.
- [x] 6.5 Update `docs/uat/expenses.md` to name the expanded UAT coverage and remaining manual variants.

## 7. Invoice Prompt Choice Coverage

- [x] 7.1 Add browser UAT coverage for issuing a linked draft invoice and accepting linked-gig completion.
- [x] 7.2 Add browser UAT coverage for issuing a linked draft invoice and declining linked-gig completion.
- [x] 7.3 Add browser UAT coverage for declining linked draft regeneration after an invoice-relevant gig edit and verifying invoice lines remain unchanged.
- [x] 7.4 Update `docs/uat/invoices.md` and `docs/uat/pre-merge-regression.md` for the newly automated prompt choices.

## 8. Verification

- [x] 8.1 Run the relevant UAT test subset locally or against the configured UAT environment and capture any environment prerequisites.
- [x] 8.2 Run backend tests or focused API tests if implementation requires setup helpers or production bug fixes.
- [x] 8.3 Run frontend lint/build only if frontend code changes are made to support testability or fix defects.
- [x] 8.4 Review all changed `docs/uat` automation labels for consistency with `docs/uat/index.md`.
