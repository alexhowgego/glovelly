## ADDED Requirements

### Requirement: Administrative permission sets are additive and explicit
The system SHALL define User Administrator, License Administrator, and Super Administrator as code-defined permission sets. User Administrator SHALL grant ManageUsers, License Administrator SHALL grant ManageLicences, and Super Administrator SHALL explicitly grant ManageUsers, ManageLicences, ManageSystem, and ManagePermissions. Users SHALL hold zero or more sets and receive the union of their capabilities. New capabilities SHALL NOT automatically be granted by a wildcard.

#### Scenario: Combined independent sets
- **WHEN** a user holds both User Administrator and License Administrator
- **THEN** the user has ManageUsers and ManageLicences but not ManagePermissions or ManageSystem

#### Scenario: Ordinary user
- **WHEN** an active user has no administrative permission set
- **THEN** the user has no administrative capabilities

### Requirement: Capabilities govern administrative access
The system SHALL use ASP.NET Core authorization policies based on effective capabilities for administrative operations. User and access-request administration SHALL require ManageUsers; future licence management SHALL require ManageLicences; permission assignment SHALL require ManagePermissions. Administrative policies SHALL require an authenticated active local user and SHALL NOT grant access based solely on frontend visibility or a stale role claim.

#### Scenario: Licence administrator attempts user management
- **WHEN** an active user with only License Administrator requests the user directory or access-request review
- **THEN** the system denies access without disclosing protected information

#### Scenario: Permission revocation during a session
- **WHEN** a user's permission set is revoked while the user has an existing session
- **THEN** the next protected request uses the current effective capabilities and denies any newly unauthorized action

### Requirement: Account management cannot grant or acquire administrative authority
Users with ManageUsers SHALL be able to administer ordinary accounts but SHALL NOT assign permission sets through user creation, updates, or access-request approval. Administrative operations targeting an account with any administrative set SHALL require ManagePermissions, including identity edits, invitations, activation, deactivation and deletion. Self-service settings SHALL remain separate and SHALL NOT confer administrative authority.

#### Scenario: Attempted direct promotion
- **WHEN** a user manager without ManagePermissions submits an administrative set or legacy role through a user creation or update request
- **THEN** the system rejects the attempt and does not create or change administrative authority

#### Scenario: Attempted takeover of privileged invitation
- **WHEN** a user manager without ManagePermissions attempts to change the email or send an invitation for an unenrolled privileged account
- **THEN** the system denies the action and leaves the account unchanged

#### Scenario: User manager targets another user administrator
- **WHEN** a user manager without ManagePermissions attempts to edit or deactivate another User Administrator
- **THEN** the system denies the administrative mutation

#### Scenario: Identity change races an administrative grant
- **WHEN** an administrative account mutation and a permission grant to the same account execute concurrently
- **THEN** they serialize across instances and the mutation rechecks the target's privileges before committing

### Requirement: Permission assignment is separate and auditable
The system SHALL provide a separate administrative interface and API for authorized permission-set assignment. Each effective grant or revocation SHALL atomically record the affected user, previous and new sets, actor or operator identity, timestamp, source, and optional reason. Audit records SHALL survive account deletion. No-op changes SHALL NOT create misleading change records.

#### Scenario: Authorized grant
- **WHEN** a permission manager grants License Administrator to a user
- **THEN** the assignment and its audit record are committed together and the user receives ManageLicences

#### Scenario: Unauthorized grant
- **WHEN** a user lacking ManagePermissions attempts to grant any administrative set to themselves or another user
- **THEN** the system denies the request and makes no assignment or audit change

#### Scenario: Audit persistence
- **WHEN** a user associated with an audit record is deleted
- **THEN** the record retains sufficient actor and target identity to explain the change

### Requirement: A usable Super Administrator is protected
After a usable Super Administrator exists, the system SHALL reject a permission change, deactivation, or deletion that would leave no active enrolled Super Administrator. This invariant SHALL hold for simultaneous requests across application instances; checks and mutations SHALL be serialized transactionally.

#### Scenario: Last super demotion
- **WHEN** the only active enrolled Super Administrator attempts to remove their own Super Administrator set
- **THEN** the system rejects the change and preserves the assignment

#### Scenario: Handover to another super
- **WHEN** another active enrolled Super Administrator exists and a permission manager removes the first user's Super Administrator set
- **THEN** the system permits the change and records it

#### Scenario: Simultaneous demotions
- **WHEN** two requests concurrently try to remove the only two active enrolled Super Administrators
- **THEN** at most one succeeds and at least one active enrolled Super Administrator remains

### Requirement: Explicit operator bootstrap and recovery
The system SHALL provide an explicitly invoked operator command that grants Super Administrator to a specifically identified existing active enrolled user. The command SHALL use the same safety and audit guarantees, record an operator actor and reason, and SHALL NOT configure a permanent superuser or run automatically at startup. It SHALL support both initial bootstrap and emergency recovery.

#### Scenario: Operator bootstraps authority
- **WHEN** an operator invokes the command with an unambiguous active enrolled user
- **THEN** the system grants Super Administrator and records the operator action

#### Scenario: Invalid bootstrap target
- **WHEN** the target is missing, ambiguous, inactive, or not enrolled
- **THEN** the command fails without changing permissions

#### Scenario: Repeat bootstrap
- **WHEN** the operator grants a set the target already holds
- **THEN** the command reports no change and does not create a misleading audit record

### Requirement: Legacy administrative access migrates without elevation
Migration SHALL assign User Administrator to each existing Admin and no administrative set to each existing ordinary User. It SHALL NOT automatically grant Super Administrator or License Administrator. Development seeding and documented deployment procedures SHALL support explicit bootstrap without creating a permanent privileged identity.

#### Scenario: Existing administrator after migration
- **WHEN** an existing Admin is migrated
- **THEN** the user retains ManageUsers but does not receive ManagePermissions or ManageLicences until explicitly granted

### Requirement: Administrative UI follows effective capabilities
The authenticated session SHALL expose effective capabilities and the UI SHALL present administrative navigation and actions appropriate to them. Permission managers SHALL have a distinct assignment and audit surface. Access-request notifications and live events SHALL target active users with ManageUsers. Server authorization SHALL remain decisive when UI state is stale.

#### Scenario: Licence-only user
- **WHEN** a user has ManageLicences but not ManageUsers
- **THEN** the UI does not expose user or access-request management merely because the user holds an administrative set

#### Scenario: Access-request notification
- **WHEN** a new access request is notified
- **THEN** recipients are active users with ManageUsers rather than all users holding any administrative set
