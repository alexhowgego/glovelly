## Context

`InvoiceLine.LineTotal` currently returns the exact decimal product of quantity and unit price. `Invoice.Total` sums those products. Mileage calculated to one decimal place and charged at £0.45 can produce half-penny amounts; currency formatting hides that precision. For example, 118.9 × £0.45 produces £53.505, displayed as £53.51, and a -£53.51 adjustment leaves -£0.005.

The shared model already feeds API responses and PDF rendering, while the frontend formats backend totals. This is a monetary policy issue, not a binary floating-point calculation defect.

## Goals / Non-Goals

**Goals:**
- Make displayed invoice lines add up to the displayed invoice total.
- Make a penny-denominated adjustment cancelling that total yield exact zero.
- Apply one policy to generated and manually maintained lines of all types.

**Non-Goals:**
- Historical invoice reconciliation, persisted total snapshots, migrations, or bulk PDF regeneration.
- Changes to mileage estimation, quantities, unit prices, API shapes, or frontend monetary calculations.
- Changes to unrelated expense or gig fee calculations.

## Decisions

### Round at the shared line-total boundary

Calculate `InvoiceLine.LineTotal` using decimal rounding to two places with explicit `MidpointRounding.AwayFromZero`. Preserve `Quantity` and `UnitPrice` unchanged. Keep `Invoice.Total` as the sum of line totals, including exact zero for an empty invoice.

Rounding only the final invoice total would still allow displayed lines to disagree with it. Rounding only mileage generation would miss manual fractional quantities and other generated types. Frontend-only rounding would leave API and document consumers inconsistent.

### Use symmetric midpoint rounding

Positive £53.505 becomes £53.51 and negative -£53.505 becomes -£53.51. Specify the midpoint mode explicitly rather than relying on the default ties-to-even behavior, so credits and charges behave symmetrically and match the intended currency presentation.

### Validate the shared policy and user journey

Add focused calculation coverage for midpoint signs, values either side of the midpoint, exact penny values, unchanged operands, empty invoices, and multiple fractional lines. Add endpoint regression coverage for generated fractional-mileage lines and an adjustment equal to the negative returned total. Verify API and newly generated PDF amounts consume rounded totals, and update the existing invoice UAT journey with the reported sequence.

## Risks / Trade-offs

- [Existing calculated totals can change by pennies while stored PDFs retain old amounts] → Accepted explicitly; no historical remediation or bulk regeneration. Normal future rendering uses the new policy.
- [Aggregated reporting or MCP projections could bypass model totals] → Inspect invoice amount consumers during implementation and ensure any equivalent projections follow the same line-first rounding rule without unrelated refactoring.
- [A formatting-only assertion could conceal a residual amount] → Assert exact decimal zero and exact API totals, not just formatted text.

## Migration Plan

Deploy as a normal backend change without database migrations or data updates. Existing quantities and rates remain intact. Rollback consists of redeploying the previous application version; PDFs already generated under either version are not automatically rewritten.

## Open Questions

None; historical penny differences are accepted by the user.
