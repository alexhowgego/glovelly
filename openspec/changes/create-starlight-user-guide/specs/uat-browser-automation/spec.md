## ADDED Requirements

### Requirement: Documentation capture automation is isolated from regression UAT
The browser automation project SHALL support a separately selectable documentation-capture suite that uses the dedicated documentation fixture and its own artifact output. It SHALL preserve the existing UAT regression account, diagnostics behavior, and normal test coverage.

#### Scenario: Documentation capture is selected
- **WHEN** CI or a developer selects the documentation-capture suite
- **THEN** it authenticates and operates only through the documentation fixture and writes named capture candidates to the configured documentation artifact directory

#### Scenario: Normal UAT is selected
- **WHEN** CI or a developer runs the ordinary UAT suite without the documentation-capture selector
- **THEN** its existing regression fixture and failure-diagnostic behavior remain unchanged
