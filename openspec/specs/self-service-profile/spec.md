## Purpose

Allow authenticated users to view and manage their own display name without exposing administrative account controls.

## Requirements

### Requirement: Users can view and update their own display name
The system SHALL show an active authenticated user's current display name in user Settings and SHALL allow that user to save a new display name for their own account.

#### Scenario: Settings pre-populates the display name
- **WHEN** an active authenticated user opens user Settings
- **THEN** the display-name field contains the current name used by the application for that user

#### Scenario: User saves a valid display name
- **WHEN** an active authenticated user submits a nonblank display name of 200 characters or fewer
- **THEN** the system trims and persists that name on the authenticated user's account
- **AND THEN** the response returns the saved name

#### Scenario: Saved name remains available after refresh
- **WHEN** an active authenticated user has saved a display name and the application loads the current authenticated user again
- **THEN** the current-user representation contains the saved display name

### Requirement: Display-name changes update the active interface
The system SHALL update the active client-side user state with the saved display name without requiring a browser reload.

#### Scenario: Profile menu reflects a saved name
- **WHEN** a user successfully saves a new display name
- **THEN** user-facing profile menus and headers that render the current user's name show the saved name immediately

#### Scenario: Administrator user list reflects a self-service name change
- **WHEN** an administrator successfully saves a new display name and opens the Administrator user list
- **THEN** the matching cached user entry shows the saved name without a browser refresh

#### Scenario: Profile menu reflects an administrator self-edit
- **WHEN** an administrator saves a display-name update for their own account through the Administrator user list
- **THEN** the profile menu shows the saved name without a browser refresh

### Requirement: Self-service display-name changes are validated
The system SHALL reject a blank, whitespace-only, or over-200-character display name and SHALL leave the previously persisted name intact.

#### Scenario: Blank display name is rejected
- **WHEN** a user submits an empty or whitespace-only display name
- **THEN** the system returns a clear validation error
- **AND THEN** the user's previously saved display name remains unchanged

#### Scenario: Overlong display name is rejected
- **WHEN** a user submits a display name longer than 200 characters
- **THEN** the system returns a clear validation error
- **AND THEN** the user's previously saved display name remains unchanged

### Requirement: Self-service display-name changes are restricted to the authenticated user
The system SHALL derive the target account for a self-service display-name update from the authenticated identity and SHALL NOT allow a client to select another account.

#### Scenario: Standard user updates only their own name
- **WHEN** an active standard user submits a display-name update
- **THEN** only that authenticated user's display name is changed
- **AND THEN** other users' display names remain unchanged

#### Scenario: Self-service request includes another user identifier
- **WHEN** a client submits a self-service display-name update containing an identifier for another user
- **THEN** the system does not use that identifier to select the target account
- **AND THEN** only the authenticated user's account can be changed

### Requirement: Self-service profile updates exclude administrative account controls
The system SHALL NOT expose self-service changes to email address, Google subject, role, activation status, or user accounts other than the authenticated user's account.

#### Scenario: User settings do not offer administrative fields
- **WHEN** an active standard user opens user Settings
- **THEN** the interface does not provide controls for email address, Google subject, role, activation status, or another user's account

#### Scenario: Administrator user management remains separate
- **WHEN** an administrator manages users through the administrator journey
- **THEN** the existing administrator account-management controls remain available independently of self-service profile settings
