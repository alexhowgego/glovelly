## 1. Draft Description API

- [x] 1.1 Add an owner-scoped invoice description update endpoint with a focused request contract, trimming and rejecting blank values.
- [x] 1.2 Enforce Draft-only updates, stamp the invoice update, and return the invoice with its line items.
- [x] 1.3 Add integration coverage for successful trimmed updates, blank validation, non-Draft rejection, and user visibility.

## 2. Invoice Workspace

- [x] 2.1 Add Line items pane state and a save handler that calls the dedicated description endpoint and replaces the updated invoice in workspace state.
- [x] 2.2 Render a Description editor and Save action for Draft invoices, and a read-only description for non-Draft invoices.
- [x] 2.3 Synchronize the editor value on invoice selection and report saving and validation outcomes through the existing invoice status feedback.

## 3. Regression Coverage

- [x] 3.1 Extend invoice redraft coverage to verify that a saved custom description is retained in the regenerated PDF.
- [x] 3.2 Update the relevant invoice user-acceptance scenario to cover changing a Draft invoice description and redrafting it.
- [x] 3.3 Run the targeted backend invoice tests, frontend lint, and frontend build.
