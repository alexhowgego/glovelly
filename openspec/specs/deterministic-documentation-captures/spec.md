# Deterministic Documentation Captures Specification

## Purpose
Define isolated, repeatable browser captures for public user-guide imagery.

## Requirements

### Requirement: Documentation screenshots are captured from an isolated fixture
The system SHALL provide a staging-only `Glovelly Docs` fixture account separate from the UAT regression account. It SHALL reset the documentation account's owned data before capture, seed a fixed scenario, and authenticate the documentation capture suite only as that predefined account.

#### Scenario: Capture run begins
- **WHEN** the documentation screenshot suite starts against staging
- **THEN** it removes prior documentation-account data, seeds the fixed scenario, and does not alter UAT or non-documentation user data

#### Scenario: Capture run is repeated
- **WHEN** the documentation screenshot suite runs after a prior successful, failed, or cancelled capture
- **THEN** its initial reset produces the same owned fixture state before capture

### Requirement: Documentation fixture cleanup is ownership and storage aware
The documentation fixture reset SHALL remove the documentation account's owned relational data and associated storage-backed attachments, and SHALL reset profile/default state that affects captured UI. The capture suite SHALL attempt final cleanup regardless of capture success.

#### Scenario: Capture run finishes
- **WHEN** a documentation screenshot capture succeeds or fails after seeding data
- **THEN** the suite attempts to restore the documentation account to its known blank state without deleting the account identity or other users' data

### Requirement: Captures are deterministic, real application views
The documentation capture suite SHALL produce named PNG candidates from real authenticated application views using a fixed browser version, viewport, locale, timezone, colour scheme, reduced-motion setting, and browser clock. It SHALL wait for the captured interface to settle and avoid external delivery or integration side effects.

#### Scenario: Same fixture is captured twice
- **WHEN** two capture runs use the same deployed application revision and fixed fixture state
- **THEN** they produce equivalent named candidates for the selected documented views

#### Scenario: Invoice delivery is documented
- **WHEN** the suite captures the invoice email-review interface
- **THEN** it captures the review state before sending and does not send an email

### Requirement: Screenshot drift is reviewable but non-blocking
For same-repository pull requests, the staging CI pipeline SHALL run the documentation capture path after normal staging UAT, compare candidates with the checked-in guide images, upload candidates and comparison output as an artifact, and update one marker-based PR comment with the result and artifact location. Initial screenshot drift SHALL not fail the pipeline, and capture/comment jobs SHALL not receive staging credentials for forked pull requests.

#### Scenario: A documented view changes
- **WHEN** a same-repository pull request changes a captured view relative to its checked-in image
- **THEN** CI reports the affected image in its artifact and PR comment without blocking the pull request

#### Scenario: Forked pull request runs CI
- **WHEN** CI runs for a pull request from a fork
- **THEN** the documentation capture/comment path does not expose staging secrets or write a PR comment
