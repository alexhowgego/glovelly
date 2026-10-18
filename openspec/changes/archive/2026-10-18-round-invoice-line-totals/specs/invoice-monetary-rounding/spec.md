## ADDED Requirements

### Requirement: Invoice line totals are rounded to pennies

The system SHALL calculate each invoice line total by multiplying its decimal quantity and unit price and rounding the product to two decimal places with midpoint rounding away from zero. This policy SHALL apply to all line types, whether generated or manually maintained, without changing the quantity or unit price.

#### Scenario: Mileage produces a positive half-penny
- **WHEN** a mileage line has quantity 118.9 and unit price £0.45
- **THEN** its line total SHALL be £53.51
- **AND** its quantity SHALL remain 118.9 and its unit price SHALL remain £0.45

#### Scenario: Negative midpoint rounds symmetrically
- **WHEN** a line has quantity 118.9 and unit price -£0.45
- **THEN** its line total SHALL be -£53.51

#### Scenario: Non-midpoint values round to the nearest penny
- **WHEN** line products are £1.234 and £1.236 respectively
- **THEN** their line totals SHALL be £1.23 and £1.24 respectively

#### Scenario: Exact penny amounts are preserved
- **WHEN** a line has quantity 2.5 and unit price £120.00
- **THEN** its line total SHALL be £300.00

### Requirement: Invoice totals sum rounded lines

The system SHALL calculate an invoice total as the sum of its rounded line totals, rather than rounding the sum of unrounded products. API amounts, frontend invoice amounts, and newly rendered PDF amounts SHALL use these shared line and invoice totals.

#### Scenario: Multiple fractional lines add up visibly
- **WHEN** an invoice contains two lines each with quantity 118.9 and unit price £0.45
- **THEN** each line total SHALL be £53.51
- **AND** the invoice total SHALL be £107.02
- **AND** API responses and newly rendered invoice PDFs SHALL reflect these amounts

#### Scenario: Adjustment cancels the displayed mileage total
- **WHEN** a zero-performance-fee gig with calculated mileage of 118.9 miles at £0.45 per mile is invoiced
- **AND** an adjustment of -£53.51 is added to cancel its £53.51 total
- **THEN** the invoice total SHALL equal exactly zero
- **AND** its frontend display and newly rendered PDF SHALL show a zero balance, not -£0.01

#### Scenario: Empty invoice totals zero
- **WHEN** an invoice has no lines
- **THEN** its total SHALL equal exactly zero
