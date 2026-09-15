## MODIFIED Requirements

### Requirement: Captures are deterministic, real application views
The documentation capture suite SHALL produce named light and actual-dark Glovelly UI PNG candidates from real authenticated application views using a fixed browser version, viewport, locale, timezone, colour scheme, reduced-motion setting, and browser clock. It SHALL set the explicit Glovelly theme preference before the application starts, wait for the captured interface to settle, and avoid external delivery or integration side effects.

#### Scenario: Same fixture is captured twice
- **WHEN** two capture runs use the same deployed application revision, fixed fixture state, and selected Glovelly theme
- **THEN** they produce equivalent named candidates for the selected documented views

#### Scenario: Invoice delivery is documented
- **WHEN** the suite captures the invoice email-review interface
- **THEN** it captures the review state before sending and does not send an email

### Requirement: Selected workflow states have paired documentation candidates
The documentation capture suite SHALL capture light and actual-dark candidates for the client workspace, gig workspace, gig expenses and mileage, invoice status, invoice email review, seller profile, and user settings states. Candidate filenames SHALL identify both the workflow state and its light or dark variant so they correspond directly to the reviewed guide assets.

#### Scenario: Capture suite runs against the documentation fixture
- **WHEN** the documentation capture suite authenticates the isolated fixture
- **THEN** it produces one named light candidate and one named actual-dark candidate for each of the seven selected workflow states using only fixture data

#### Scenario: Documentation candidate changes
- **WHEN** a paired screenshot candidate is new or differs from its checked-in asset
- **THEN** the existing comparison workflow includes that filename in its artifact and non-blocking pull-request freshness report
