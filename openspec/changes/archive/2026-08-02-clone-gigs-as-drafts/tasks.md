## 1. Clone Lifecycle Behaviour

- [x] 1.1 Update the Gigs workspace clone request to create the new gig with `Draft` status rather than the selected source status.
- [x] 1.2 Preserve the existing copied-field, optional-expense, invoice-clearing, selection, and editor-opening clone behaviour.

## 2. Regression Coverage

- [x] 2.1 Add parameterized Playwright UAT coverage that clones Planned, Completed, Cancelled, and Draft source gigs and verifies each opened clone is a Draft.
- [x] 2.2 Update the manual gig-cloning regression guidance to state that every clone begins as Draft.

## 3. Verification

- [x] 3.1 Run the focused browser UAT coverage in an environment configured with `GLOVELLY_UAT_SECRET`.
- [x] 3.2 Run frontend lint and build checks.
