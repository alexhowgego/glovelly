## Purpose

Define the invitation email and secure first-login experience for pre-provisioned Glovelly users.

## Requirements

### Requirement: Invitation email presents a clear acceptance action
When an administrator sends an invitation to an active provisioned user, the system SHALL send an HTML email whose primary CTA explicitly communicates that the recipient is accepting the invitation and signing in to Glovelly.

#### Scenario: Recipient receives an HTML invitation
- **WHEN** an administrator sends an invitation to an active user
- **THEN** the HTML email identifies the invited Google email address and contains a primary invitation-acceptance CTA linking to Glovelly's login route

### Requirement: Invitation acceptance preserves secure first-login enrolment
The system SHALL direct invitation recipients through the existing Google sign-in flow and SHALL grant access only after Google authentication provides a verified email address that matches an active pre-provisioned user.

#### Scenario: Provisioned recipient accepts invitation with matching Google account
- **WHEN** the recipient follows the invitation CTA and authenticates with Google using the provisioned verified email address
- **THEN** the system binds the Google subject to the pre-provisioned user and grants access according to that user's existing role

#### Scenario: Invitation link is opened without a matching provisioned Google identity
- **WHEN** a recipient follows the invitation CTA but Google authentication does not produce a verified email matching an active pre-provisioned user
- **THEN** the system SHALL not grant access based on the email link alone

### Requirement: Invitation email has an equivalent plain-text path
The system SHALL include plain-text invitation guidance that identifies the Google email address to use and provides the Glovelly login URL as a fallback to the HTML CTA.

#### Scenario: Recipient uses a plain-text email client
- **WHEN** an invitation is rendered without HTML support
- **THEN** the recipient can identify the intended Google email address and open the Glovelly login URL to begin the same acceptance flow
