## 1. Permission model and persistence

- [x] 1.1 Define fixed administrative capabilities and additive permission-set mappings, including explicit Super Administrator capabilities.
- [x] 1.2 Add user permission-set assignments and durable permission-change audit entities, constraints, and EF configurations.
- [x] 1.3 Create and test a migration mapping legacy Admin users to User Administrator only, preserving a controlled rollback path.
- [x] 1.4 Update development/test fixtures and seeding without introducing automatic permanent superuser grants.

## 2. Authorization and safety

- [x] 2.1 Implement current-state capability resolution and ASP.NET Core authorization policies for active authenticated users.
- [x] 2.2 Implement transactional permission assignment and audit recording with operator and user actors, no-op handling, and durable identity snapshots.
- [x] 2.3 Implement cross-instance serialization of account mutations and permission grants with target privilege rechecks, plus the active-enrolled-last-super invariant; provide appropriate in-memory behavior.
- [x] 2.4 Add the explicit bootstrap/recovery command using the same audited assignment service and validate target identity, enrollment, and activity.

## 3. Administrative workflows

- [x] 3.1 Migrate user and access-request endpoints to ManageUsers, remove provisioning role selection, and reject privilege-injection fields.
- [x] 3.2 Enforce ManagePermissions on every administrative mutation of privileged accounts, including invitation and identity changes, and safely handle access requests matching existing privileged accounts.
- [x] 3.3 Add protected permission-set assignment and audit history APIs, including clear conflict responses for lockout prevention.
- [x] 3.4 Update session DTOs, access-request notification recipients, workspace events, and existing role-dependent authentication and authorization paths.

## 4. Frontend and documentation

- [x] 4.1 Replace role-based frontend types, session handling, navigation, and access-request affordances with effective capability checks and handle revocation/stale-session failures.
- [x] 4.2 Remove role selection from ordinary provisioning and provide protected permission assignment and audit history UI with last-super feedback.
- [x] 4.3 Document the capability model, operator command, secure Cloud Run/Neon bootstrap and recovery, migration verification, and rollback in the engineering handbook.
- [x] 4.4 Update affected admin and access-request UAT journeys, including privilege boundaries, notifications, and mobile feedback.

## 5. Verification

- [x] 5.1 Add backend tests for additive permissions, migration mapping, active-user enforcement, existing-session revocation, notification recipients, and no implicit superuser.
- [x] 5.2 Add authorization tests for self/other escalation, privileged identity and invitation takeover, access-request injection, audit atomicity, and audit retention.
- [x] 5.3 Test bootstrap/recovery, invalid and duplicate targets, handover, last-super deactivation/deletion/demotion, and no-op audit behavior.
- [x] 5.4 Add PostgreSQL-backed cross-instance concurrency tests for competing last-super mutations and account mutation racing a permission grant.
- [x] 5.5 Add frontend tests for capability-specific navigation, assignment/audit UI, access-request workflows, and revoked permissions; run appropriate backend, frontend lint/build, and unit checks.
