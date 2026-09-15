## 1. Deterministic Capture Coverage

- [x] 1.1 Extend the documentation browser setup to apply and verify explicit `light` and `dark` Glovelly theme preferences before each capture state.
- [x] 1.2 Capture consistently named light/dark pairs for the existing gig workspace, invoice status, and invoice email-review states.
- [x] 1.3 Add stable fixture-backed capture states for the client workspace, gig expenses/mileage, seller profile dialog, and user settings dialog, each with light/dark pairs.
- [x] 1.4 Retain settled-state waits, no-send behavior, and reset-before/reset-finally fixture lifecycle for all fourteen candidates.

## 2. Guide Images

- [ ] 2.1 Add approved paired screenshot assets under `frontend/glovelly-guide/public/screenshots/` and remove superseded unpaired assets.
- [x] 2.2 Add guide-local responsive CSS that selects exactly one paired product image from Starlight's active light/dark theme and keeps hidden variants out of the accessibility tree.
- [x] 2.3 Replace the existing gig and invoice figures with paired, accessible figures that retain one caption each.
- [x] 2.4 Add purposeful paired figures with meaningful alternative text and single captions to the Clients, Expenses, Seller profile, and Settings guide pages.

## 3. Verification And Review

- [ ] 3.1 Run the documentation capture suite against staging and review the candidate artifact plus freshness report, confirming all fourteen names are compared without blocking the pull request.
- [x] 3.2 Run `npm --prefix frontend/glovelly-guide run check` and `npm --prefix frontend/glovelly-guide run build`.
- [ ] 3.3 Manually review the seven documented pages in Starlight light and dark themes at narrow and wide viewports for correct asset selection, alt text, captions, and responsive layout.
- [x] 3.4 Compare byte-different PNG candidates by decoded pixels, suppress encoding-only drift, and include visual diffs for real pixel changes in the review artifact.
