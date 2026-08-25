## ADDED Requirements

### Requirement: Global terminal notifications
The application SHALL provide an in-session global notification viewport for applicable terminal outcomes from authenticated workspace actions. The viewport SHALL remain available when the user changes workspace section or closes the modal that initiated the action.

#### Scenario: Completed action after navigation
- **WHEN** an applicable workspace action completes successfully and the user changes section before viewing its original status area
- **THEN** the application SHALL show the completion notification in the global viewport

#### Scenario: Completed action after modal closure
- **WHEN** an applicable action initiated from a modal completes and the modal is closed
- **THEN** the application SHALL retain the notification in the global viewport for its configured lifetime

### Requirement: Notification severity and lifetime
The application SHALL classify applicable terminal outcomes as success, information, or error. Success and information notifications SHALL auto-dismiss after a finite timeout. Error notifications SHALL remain visible until the user dismisses them.

#### Scenario: Successful upload
- **WHEN** an attachment upload completes successfully
- **THEN** the application SHALL show a success notification that auto-dismisses

#### Scenario: Failed download
- **WHEN** an attachment download fails
- **THEN** the application SHALL show an error notification that remains visible until dismissed

### Requirement: Notification visibility and deduplication
The application SHALL show no more than three notifications in its collapsed global viewport. It SHALL NOT silently discard an error notification when additional notifications exist. The application SHALL replace a prior notification when an action supplies the same semantic deduplication key.

#### Scenario: Collapsed viewport receives an error
- **WHEN** three notifications are visible and an action produces an error notification
- **THEN** the application SHALL retain the error notification without silently discarding it

#### Scenario: Repeated action feedback
- **WHEN** an action emits a notification with the same semantic deduplication key as a visible notification
- **THEN** the application SHALL update the existing notification instead of adding a duplicate

### Requirement: Contextual feedback remains local
The application SHALL retain contextual UI feedback for field validation, action progress, multi-step modal workflows, and durable configuration or health warnings. It SHALL NOT use a global notification as the sole feedback for those states.

#### Scenario: Invalid form submission
- **WHEN** a user submits a form with invalid or missing required values
- **THEN** the application SHALL show the validation feedback in the relevant form context

#### Scenario: Long-running modal workflow
- **WHEN** a multi-step modal workflow is in progress
- **THEN** the application SHALL show progress in the modal context

### Requirement: Accessible and responsive notifications
The application SHALL announce notifications through a polite live region. Notifications SHALL NOT move keyboard focus, SHALL provide an accessible dismiss control, SHALL respect reduced-motion preferences, and SHALL avoid obscuring fixed mobile controls.

#### Scenario: Keyboard dismissal
- **WHEN** a keyboard user reaches a visible notification
- **THEN** the user SHALL be able to dismiss it through an accessible dismiss control without focus having been moved automatically

#### Scenario: Mobile notification placement
- **WHEN** the application is viewed on a mobile-width viewport
- **THEN** the notification viewport SHALL be positioned so it does not obscure the fixed quick-action or return-to-top controls

### Requirement: User-safe action failures
For each migrated action, the application SHALL show the user-safe Problem Details message when the server provides one, otherwise it SHALL show an action-specific fallback. It SHALL NOT expose storage keys, internal exception details, or other implementation-sensitive information in a notification.

#### Scenario: Missing attachment object
- **WHEN** a user downloads an attachment whose metadata exists but whose stored object is unavailable
- **THEN** the application SHALL show the server-provided missing-attachment message as an error notification

#### Scenario: Generic request failure
- **WHEN** a migrated action fails without a parseable user-safe server message
- **THEN** the application SHALL show that action's fallback error notification
