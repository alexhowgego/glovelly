## 1. Delivery Preparation Contract

- [x] 1.1 Extract shared server-side invoice email composition and receipt-discovery logic so preparation and explicit delivery resolve the same recipient, subject, rendered plain text, attachment metadata, and current-PDF readiness.
- [x] 1.2 Add an authenticated, visibility-scoped invoice email preparation endpoint that has no delivery or status side effects and returns deliverability, recipient validation, PDF metadata, receipt count, and rendered review content.
- [x] 1.3 Retain send-time current-PDF, recipient, receipt-size, and composition validation without trusting client-supplied delivery metadata; return actionable validation errors for the review dialog.
- [x] 1.4 Add backend integration tests for preparation success, missing recipient, non-current PDF, receipt availability, explicit send after preparation, and no unintended delivery/status records.

## 2. Review And Send Dialog

- [x] 2.1 Add frontend delivery-preparation types and workspace state/actions to open, load, refresh, close, and submit invoice email review without native browser dialogs.
- [x] 2.2 Create a responsive Review and send invoice modal that shows recipient, subject, safe plain-text body, PDF attachment metadata, optional message textarea, receipt-pack choice, and inline preparation/validation/progress feedback.
- [x] 2.3 Implement keyboard and focus behavior: labelled controls, focus entry and return, Escape/Cancel with no side effects, textarea Enter behavior, and disabled duplicate submission while pending.
- [x] 2.4 Update invoice action wiring to open the review dialog only for current documents and remove the prompt/confirmation-based email send flow.

## 3. Issue-After-Send Outcomes

- [x] 3.1 Add the unchecked Mark invoice as issued after sending option for draft invoices and preserve the normal issue and linked-gig completion workflow after successful delivery.
- [x] 3.2 Orchestrate delivery and optional follow-up so a successful email closes the dialog and produces a Sonner delivery-success notification, while issue or linked-gig failures produce a separate actionable terminal notification.
- [x] 3.3 Preserve Draft status when the issue option is not selected and remove the post-delivery browser confirmation.

## 4. Verification And Documentation

- [x] 4.1 Add frontend tests for review rendering, cancellation, textarea Enter safety, disabled pending submit, unsafe-text display, readiness failures, and delivery/follow-up notifications.
- [x] 4.2 Update backend delivery tests for the preparation contract, stale-document revalidation, receipt selection, and delivery-history integrity.
- [x] 4.3 Update `docs/uat/invoices.md` and the Playwright invoice delivery journey for explicit review, cancel, mobile/keyboard behavior, receipt selection, issue-after-send, and partial follow-up failure coverage.
- [x] 4.4 Run `dotnet test --solution glovelly.sln --max-parallel-test-modules 1`, `npm --prefix frontend/glovelly-web run lint`, `npm --prefix frontend/glovelly-web run build`, and the relevant Playwright UAT coverage where configured.
