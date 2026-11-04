## ADDED Requirements

### Requirement: System-driven landing appearance
The landing page SHALL use a light appearance by default and SHALL follow the browser or device light/dark colour preference, including changes while the page is open. It SHALL not expose a manual theme selector, store a theme preference, or require theme-specific client-side JavaScript. Its appearance SHALL be independent of the user guide's manual theme selection.

#### Scenario: Visitor prefers a dark appearance
- **WHEN** a visitor loads the landing page with a dark system colour preference
- **THEN** the page renders its dark appearance without requiring a manual action

#### Scenario: Visitor prefers a light appearance or no preference is available
- **WHEN** a visitor loads the landing page with a light preference or without a supported colour preference
- **THEN** the page renders its light appearance

#### Scenario: System preference changes while browsing
- **WHEN** the system colour preference changes while the landing page is open
- **THEN** the page updates to the matching appearance without a reload

#### Scenario: Guide preference differs from system preference
- **WHEN** a visitor has selected a guide theme that differs from the system preference and opens the landing page
- **THEN** the landing page follows the system preference without reading or changing the guide selection

### Requirement: Accessible themed visual identity
Both landing-page appearances SHALL preserve the established warm Glovelly visual direction, remain visually consistent with the public guide's palettes, and provide accessible contrast for text, interactive elements and focus indicators. Paper-like illustrations MAY retain light surfaces in dark mode provided their content remains legible. Existing responsive layout and reduced-motion behavior SHALL remain usable in both appearances.

#### Scenario: Visitor reads or navigates in either appearance
- **WHEN** a visitor views either theme at a mobile or desktop viewport
- **THEN** text, links, controls and focus indicators remain distinguishable with accessible contrast and the content and navigation remain usable

#### Scenario: Visitor prefers reduced motion
- **WHEN** a visitor uses the landing page in either appearance with reduced motion enabled
- **THEN** existing nonessential motion remains reduced
