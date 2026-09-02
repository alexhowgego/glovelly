## 1. Document State Foundation

- [x] 1.1 Add invoice document-state and revision persistence fields, API serialization, and an EF Core migration that safely initializes existing invoices with or without stored PDFs.
- [x] 1.2 Extend frontend invoice types and shared invoice mapping to represent current, missing, regenerating, and failed document states with safe user-facing status detail.
- [x] 1.3 Add a shared invoice document-readiness service/result that identifies whether a stored PDF is readable and represents the invoice's latest document revision.

## 2. Regeneration Workflow

- [x] 2.1 Update manual adjustment add and removal handling to invalidate the current document, persist the changed invoice revision, and regenerate the PDF from the latest invoice data.
- [x] 2.2 Ensure completed generation replaces PDF metadata only when it represents the latest invoice revision; record a failed state and safe recovery detail when rendering or storage fails.
- [x] 2.3 Add an authenticated, visibility-scoped PDF regeneration retry endpoint that regenerates the latest revision without creating an adjustment, reissue audit entry, or delivery audit record.

## 3. Delivery Boundary Enforcement

- [x] 3.1 Route invoice PDF download through the shared readiness guard and return actionable unavailable-document validation for non-current documents.
- [x] 3.2 Route email delivery, including its attachment open path, through the shared readiness guard so a non-current document cannot send or update delivery history.
- [x] 3.3 Route Google Drive publishing through the shared readiness guard so a non-current document cannot upload.

## 4. Invoice Workspace Feedback

- [x] 4.1 Display invoice document state and disable preview, download, email, and Drive publishing actions with the same visible explanation when the document is unavailable.
- [x] 4.2 Report adjustment success only when its replacement PDF is current; otherwise explain that the adjustment was saved and present a transient inline regeneration retry.
- [x] 4.3 Add the inline PDF regeneration retry and refresh invoice state after it succeeds or fails without adding a persistent invoice-toolbar action.

## 5. Verification

- [x] 5.1 Add backend integration tests for successful adjustment add/removal regeneration, current PDF content and metadata, and repeated adjustments retaining only the latest representation.
- [x] 5.2 Add backend integration tests for regeneration failure, retry recovery, and preservation of invoice number and delivery history.
- [x] 5.3 Add backend integration tests proving missing, stale, regenerating, and failed PDFs cannot be downloaded, emailed, or published, and cannot create successful delivery history.
- [x] 5.4 Add frontend tests for current and unavailable document feedback, disabled document actions, and regeneration retry outcomes.
- [x] 5.5 Run `dotnet test --solution glovelly.sln --max-parallel-test-modules 1`, `npm --prefix frontend/glovelly-web run lint`, and `npm --prefix frontend/glovelly-web run build`.
