## MODIFIED Requirements

### Requirement: Administrative UI follows effective capabilities
The authenticated session SHALL expose effective capabilities and the UI SHALL present administrative navigation and actions appropriate to them. Permission managers SHALL have a distinct assignment and audit surface. Licence management SHALL have a standalone workspace reachable with ManageLicences alone and SHALL NOT depend on access to the ManageUsers directory. Product-plan assignment SHALL NOT confer administrative authority, and administrative permission sets SHALL NOT imply an unlimited product plan. Access-request notifications and live events SHALL target active users with ManageUsers. Server authorization SHALL remain decisive when UI state is stale.

#### Scenario: Licence-only user
- **WHEN** a user has ManageLicences but not ManageUsers
- **THEN** the UI SHALL expose the dedicated licence assignment/audit workspace
- **AND** it SHALL NOT expose user or access-request management merely because the user holds an administrative set

#### Scenario: Access-request notification
- **WHEN** a new access request is notified
- **THEN** recipients are active users with ManageUsers rather than all users holding an administrative set

#### Scenario: Product upgrade does not promote an administrator
- **WHEN** a user receives Pro through licensing
- **THEN** their administrative permission sets SHALL remain unchanged
