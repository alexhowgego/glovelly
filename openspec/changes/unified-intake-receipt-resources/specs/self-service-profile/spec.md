## ADDED Requirements

### Requirement: Users can choose automatic receipt application
The system SHALL show Automatic receipt application in Settings with Manual only, High confidence, and Medium confidence options, defaulting to High confidence. It SHALL return the saved preference in settings responses and current-user state. Legacy Very high confidence values SHALL behave and display as High confidence. Help SHALL explain permission to attach to the nearest nearby gig and later review/reassignment through focus help and a hover tooltip without adding a multi-line row inside the field label or disrupting control alignment.

#### Scenario: Settings shows the default
- **WHEN** an authenticated user without a saved preference opens Settings
- **THEN** High confidence SHALL be selected

#### Scenario: User saves a preference
- **WHEN** a user selects a supported option and saves Settings
- **THEN** the system SHALL persist and return it and retain the selected option in active frontend state

#### Scenario: User refreshes after saving
- **WHEN** the application reloads current-user state
- **THEN** it SHALL include the persisted automatic application preference

#### Scenario: Legacy option is loaded
- **WHEN** the user has a previously stored Very high confidence preference
- **THEN** Settings SHALL display High confidence and automatic application SHALL use the High confidence rule

#### Scenario: User seeks help
- **WHEN** a user focuses the control or hovers its label
- **THEN** the explanation SHALL be available without altering the aligned label/select layout

### Requirement: Automatic receipt application preference is restricted to the authenticated user
The system SHALL derive the target account from authenticated identity and SHALL NOT allow a client to select another account for preference updates.

#### Scenario: Standard user updates their preference
- **WHEN** an active standard user saves an automatic application preference
- **THEN** only that user's preference SHALL change and other accounts SHALL remain unchanged
