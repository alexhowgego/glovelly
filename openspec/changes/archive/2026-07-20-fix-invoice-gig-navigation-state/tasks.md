## 1. Safe Gig Navigation

- [x] 1.1 Route invoice-line and other App-level gig-opening shortcuts through the canonical gig workspace selection behavior rather than directly setting the selected gig ID.
- [x] 1.2 Confirm navigating after a saved gig or expense edit rehydrates the target gig form while navigation with unsaved edits retains the existing discard-confirmation behavior.

## 2. Regression Coverage

- [x] 2.1 Extend the cross-workspace Playwright UAT journey with two distinct, combined-invoice gigs and the saved-edit, regenerate, invoice-line-to-second-gig circuit from issue #206.
- [x] 2.2 Assert the second gig editor displays its own persisted fields and expenses, then save a deliberate second-gig change and verify neither gig is overwritten by the other.
- [x] 2.3 Update the cross-workspace UAT documentation to name the expanded automated regression coverage and its state-isolation guarantee.

## 3. Verification

- [x] 3.1 Run the focused cross-workspace UAT test and confirm the regression journey passes.
- [x] 3.2 Run frontend lint and build checks.
- [x] 3.3 Run `./verify.sh` before handover.
