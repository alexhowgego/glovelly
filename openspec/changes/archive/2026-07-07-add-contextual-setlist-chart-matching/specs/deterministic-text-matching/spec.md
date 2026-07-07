## ADDED Requirements

### Requirement: Text matching utilities are domain-independent
The system SHALL provide deterministic text normalization and string similarity utilities in an internal library that does not depend on Glovelly API domain models.

#### Scenario: Matching library has no API model dependency
- **WHEN** the matching library is built
- **THEN** it does not reference `Glovelly.Api`, Entity Framework, ASP.NET Core, `GigSetListItem`, or `ForScoreChart`

#### Scenario: API can use matching primitives
- **WHEN** API services need to compare set list rows with chart metadata
- **THEN** they can use the matching library's normalized text and similarity primitives without moving domain-specific matching rules into that library

### Requirement: Titles are normalized into comparable forms
The system SHALL normalize input strings into canonical, compact, and tokenized forms suitable for deterministic candidate retrieval.

#### Scenario: Punctuation-only title differences match compact form
- **WHEN** the system normalizes `L-O-V-E` and `LOVE`
- **THEN** both values produce the same compact form

#### Scenario: Ampersand and word variants are comparable
- **WHEN** the system compares `Jump Jive & Wail` with `Jump Jive And Wail`
- **THEN** the normalized forms and similarity scores treat them as a strong title match

#### Scenario: Extra descriptors do not prevent candidate retrieval
- **WHEN** the system compares `I Bet You Look Good on the Dancefloor - FULL SONG` with `I Bet You Look Good on the Dancefloor`
- **THEN** token similarity identifies the strings as strongly related while preserving the original values for display

### Requirement: String similarity returns explainable score components
The system SHALL expose similarity results as component scores rather than only a single opaque value.

#### Scenario: Similarity result includes component scores
- **WHEN** two strings are compared
- **THEN** the result includes compact-form, token-overlap, edit-distance, and best-score values

#### Scenario: Dissimilar titles score low
- **WHEN** the system compares `Valerie` with `I Wanna Dance With Somebody`
- **THEN** the similarity result indicates a weak match that should not independently drive candidate selection

### Requirement: Deterministic text matching is covered by focused tests
The system SHALL include dedicated unit tests for text normalization and string similarity edge cases used by chart matching.

#### Scenario: Real-world title variants are regression tested
- **WHEN** the matching test suite runs
- **THEN** it verifies examples including `L-O-V-E`/`LOVE`, `Jump Jive & Wail`/`Jump Jive And Wail`, and descriptor-heavy titles
