## 1. Shared monetary calculation

- [x] 1.1 Add focused model tests for positive and negative half-penny products, non-midpoint rounding, exact penny products, unchanged quantities and unit prices, empty invoices, and sums of individually rounded lines.
- [x] 1.2 Update `InvoiceLine.LineTotal` to round the decimal product to two places with `MidpointRounding.AwayFromZero`; retain `Invoice.Total` as a sum of line totals.
- [x] 1.3 Inspect invoice API, reporting, MCP, and PDF amount consumers for arithmetic bypassing shared totals; align any equivalent invoice calculations with the line-first policy without changing unrelated monetary calculations.

## 2. Journey regression coverage

- [x] 2.1 Add a backend regression for a zero-fee gig using calculated fractional mileage, generated invoice lines, and an adjustment cancelling the returned penny-denominated total; assert exact decimal zero and unchanged source mileage/rate.
- [x] 2.2 Cover multi-line API totals and newly rendered PDF amounts using fractional-penny lines, including a zero-balance adjusted invoice.
- [x] 2.3 Update the matching invoice scenario under `docs/uat/` with the calculate-mileage → invoice → cancelling-adjustment sequence and visible zero total; retain existing notification and mobile-placement coverage. Do not run browser UAT locally.

## 3. Verification

- [x] 3.1 Run focused backend tests for invoice calculations, generation, adjustments, and PDF rendering.
- [x] 3.2 Run `dotnet test --solution glovelly.sln --max-parallel-test-modules 1` and address regressions.
- [x] 3.3 Confirm no schema migration, historical invoice updates, bulk PDF regeneration, or frontend-specific arithmetic was introduced; record verification results and leave browser UAT for Ubuntu CI.

### Verification results

- Focused invoice suite: 71 passed.
- Paid-income summary suite: 6 passed, including line-first rounding coverage.
- `git diff --check`: clean.
- Full backend suite retry: `dotnet test --solution glovelly.sln --max-parallel-test-modules 1` passed all 460 tests with zero failures or skips in 2m 03s (API: 2m 00s; matching: 1s). Earlier runs timed out or stalled; the underlying intermittent cause remains unconfirmed, but task 3.2 is now verified.
- Follow-up isolation: all 45 API test classes have passed across separate runs (71 invoice tests, 6 paid-income tests, 6 test-auth tests, 33 intake tests, and the remaining 36 classes each in a fresh process). Auth and intake also passed together. All 7 matching tests passed. No isolated class timed out; `GigEndpointsTests` was slowest at 84.2 seconds. This verifies all classes but does not establish a clean single-process full-suite run.
- The serial full-run log completed 431 tests before timeout, with one disposed-service-provider failure during intake fixture database reset. That failure did not reproduce in isolation. Shared fixture startup builds an undisposed temporary root provider; this remains an investigation lead, not a confirmed cause, and no fixture changes were made.
- No migrations, historical data updates, bulk PDF regeneration, or frontend arithmetic changes introduced. Browser UAT was not run locally; leave it to Ubuntu CI.
