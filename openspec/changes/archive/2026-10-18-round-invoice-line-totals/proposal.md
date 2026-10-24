## Why

Issue #300 exposes hidden fractional pennies: mileage invoice lines retain more precision than their displayed amounts, so an adjustment cancelling the displayed total can leave a displayed -£0.01 balance. Readers must be able to add the visible line amounts and obtain the invoice total.

## What Changes

- Round each invoice line's quantity-times-unit-price result to two decimal places, with midpoint rounding away from zero.
- Sum rounded line totals to calculate invoice totals, retaining original quantities and unit prices.
- Add regression coverage for fractional mileage, negative amounts, multi-line totals, and exact-zero adjustments.
- Do not migrate historical invoices or bulk-regenerate existing PDFs; small historical differences are accepted.

## Capabilities

### New Capabilities

- `invoice-monetary-rounding`: Defines line-level penny rounding and invoice totals derived from rounded lines.

### Modified Capabilities

None.

## Impact

- Shared backend invoice model calculations, API totals, frontend displays, and newly rendered PDFs consuming those calculations.
- Backend regression tests and the relevant invoice UAT journey.
- No API shape changes, database migrations, new dependencies, or frontend-specific arithmetic.
