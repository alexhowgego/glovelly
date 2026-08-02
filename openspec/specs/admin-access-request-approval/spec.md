## Purpose

TBD - Define administrator review and approval of access requests.

## Requirements

### Requirement: Administrators can review pending access requests
The system SHALL allow an authenticated active Glovelly administrator to list and view access requests that are pending review. Each review record SHALL present the requester identity captured at request time, request time, current lifecycle status, and recorded decision metadata where applicable.

#### Scenario: Administrator opens pending access requests
- **WHEN** an authenticated active administrator opens the access-request review surface
- **THEN** the system SHALL show pending requests and allow the administrator to select one for review

#### Scenario: Non-administrator attempts to view a request
- **WHEN** a user who is not an authenticated active administrator requests access-request review data
- **THEN** the system SHALL deny access and SHALL not disclose requester details

### Requirement: Notification links navigate to authenticated review without granting authority
The system SHALL include a review URL for the recorded request in each administrator access-request notification. Following that URL SHALL not change request state or grant access based on possession of the link.

#### Scenario: Unauthenticated administrator follows a notification link
- **WHEN** an administrator follows a request review URL without an active session
- **THEN** the system SHALL require Google sign-in and return the administrator to the selected request review URL after successful authentication

#### Scenario: Authenticated administrator follows a notification link
- **WHEN** an authenticated active administrator follows a request review URL
- **THEN** the system SHALL open the access-request review surface with that request selected

#### Scenario: Request review URL is opened by a non-administrator
- **WHEN** a user without active administrator access follows a request review URL
- **THEN** the system SHALL not disclose the request or mutate its state

### Requirement: Administrators can provision a user from an approved request
The system SHALL allow an authenticated active administrator to approve a pending, unexpired request by selecting a user role, active state, and whether to send an invitation email. The system SHALL create the user from the request's stored email and display name without requiring or allowing the administrator to edit that identity in the approval action.

#### Scenario: Administrator approves a pending request
- **WHEN** an authenticated active administrator approves a pending unexpired request with a role and active state
- **THEN** the system SHALL create one user with the request's stored email and display name, record the request as provisioned with reviewer and provisioning metadata, and preserve normal Google first-login subject binding

#### Scenario: Administrator requests an invitation during approval
- **WHEN** an administrator approves a request with invitation delivery selected
- **THEN** the system SHALL attempt to send the existing user invitation email after provisioning and SHALL report invitation delivery failure without reversing successful provisioning

#### Scenario: Administrator does not request an invitation during approval
- **WHEN** an administrator approves a request with invitation delivery unselected
- **THEN** the system SHALL provision the user without sending an invitation email

### Requirement: Access-request decisions are safe and idempotent
The system SHALL allow only one terminal decision for a pending request and SHALL safely handle repeat, concurrent, already-provisioned, duplicate, and expired decision attempts without creating duplicate users or granting access based on a link.

#### Scenario: Two administrators approve the same request
- **WHEN** two authenticated active administrators attempt to approve the same pending request concurrently
- **THEN** the system SHALL create at most one user and SHALL return the recorded terminal outcome to the later decision attempt

#### Scenario: Requester was already provisioned by another path
- **WHEN** an administrator approves a request whose stored email already belongs to a user
- **THEN** the system SHALL not create a duplicate user and SHALL return an outcome that identifies the existing provisioning condition

#### Scenario: Administrator decides an expired request
- **WHEN** an administrator attempts to approve or decline a pending request after its configured approval window has elapsed
- **THEN** the system SHALL record or return the request as expired and SHALL not provision a user

### Requirement: Administrators can decline a pending request
The system SHALL allow an authenticated active administrator to decline a pending unexpired request after an explicit confirmation. Declining SHALL not provision a user and SHALL record reviewer and decision metadata.

#### Scenario: Administrator confirms decline
- **WHEN** an authenticated active administrator confirms decline of a pending unexpired request
- **THEN** the system SHALL record the request as declined and SHALL not create or modify a user

#### Scenario: Administrator cancels decline confirmation
- **WHEN** an administrator cancels the decline confirmation
- **THEN** the system SHALL leave the request pending and SHALL not record a decision

### Requirement: Pending access requests are discoverable from the administrator profile menu
The system SHALL expose an Access requests entry to authenticated active administrators in the profile menu, including the current count of pending requests and a visual pending indicator when the count is nonzero.

#### Scenario: Administrator has pending requests
- **WHEN** an authenticated active administrator has one or more pending access requests
- **THEN** the profile menu SHALL show the Access requests entry with the pending count and visual pending indicator

#### Scenario: Standard user opens the profile menu
- **WHEN** an authenticated user without administrator access opens the profile menu
- **THEN** the Access requests entry SHALL not be shown
