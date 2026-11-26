## Context

Users currently have a persisted Admin/User role. A role policy protects the user directory and access-request endpoints, while approval and user editing can grant Admin. Access-request notifications select Admin users directly, and the frontend uses `role === 'Admin'`. Google account binding by invitation email makes even identity edits to an unenrolled privileged account security-sensitive.

## Goals / Non-Goals

**Goals:** Separate capabilities, prevent direct and indirect escalation, support audited additive grants, maintain safe recovery, and make #192 depend on `ManageLicences`.

**Non-Goals:** Billing or licences, configurable/custom permissions, impersonation, tenant membership, and a generic audit platform.

## Decisions

### Permission sets and policy enforcement

Persist assignments of code-defined permission sets to users with a unique user/set constraint. User Administrator grants `ManageUsers`; License Administrator grants `ManageLicences`; Super Administrator grants an explicit list of `ManageUsers`, `ManageLicences`, `ManageSystem`, and `ManagePermissions`. Effective capabilities are the union. Super is not a wildcard: new capabilities require an intentional mapping. Register standard ASP.NET Core policies and derive effective capabilities from current database state; do not build an authorization framework. Expose capabilities and assigned sets in appropriate session/admin DTOs for presentation only; backend policies remain authoritative. Define the otherwise unused `ManageSystem` capability without inventing system operations.

Alternatives: a single role cannot represent independent combinations; arbitrary user-defined capabilities or wildcard super roles add complexity and security ambiguity.

### Privileged-account boundary

Normal `ManageUsers` operations create unprivileged users and review access requests. Only `ManagePermissions` can grant/revoke sets, and all administrative mutations of an account with any administrative set require `ManagePermissions`, including invitation, email, subject, active state and deletion. Self-service profile/settings operations remain separate and cannot alter permissions or administrative identity. Apply target authorization on the server to every path, not just UI or role endpoints; reject obsolete role fields rather than silently honoring them. Explicitly protect account creation and approval against privilege injection, including case when the approval matches an existing privileged user.

Alternatives: field-level exceptions are more permissive but increase the chance of identity takeover or overlooked side effects.

### Audit and serialized safety

Store append-only permission change records including target, old and new sets, actor identity or explicit operator identity, timestamp, optional reason, and source. Commit assignment and audit together; preserve records if users are deleted by storing durable identifiers/snapshots rather than cascading deletion. Avoid recording an audit entry for a no-op. Do not log secrets.

All administrative mutations of a target account and all permission assignments must share a database-backed serialization mechanism across instances. Acquire it before reading current target privileges, recheck authorization under the lock, and hold it through the mutation and commit. This prevents a user manager's in-flight identity change from racing a permission grant. Writes that can remove the last active, enrolled Super Administrator must additionally serialize the global invariant check across targets, rechecking it in the transaction. Use a consistent lock order to avoid deadlocks. PostgreSQL transaction advisory locks or an equivalent database lock are suitable; process-local locks alone are insufficient. Define equivalent in-memory behavior for development/tests without treating in-memory tests as proof of PostgreSQL concurrency safety. Return a clear conflict and leave all state unchanged on rejection.

### Controlled operator command

Provide a deliberately invoked, documented command that connects to the target database and grants Super Administrator to an explicitly identified existing active enrolled user. It is available for first bootstrap and emergency recovery, uses the same transactional assignment/audit and locking logic, identifies the operator as an operator action rather than fabricating a user, and is idempotent. It must refuse ambiguous/missing/inactive/unenrolled targets and never runs on application startup or persists an override. Document practical Cloud Run/Neon execution, identity/environment verification, credentials, and access control; no secret values in repo.

Alternative: environment-variable super users silently restore access after revocation and undermine auditability.

### UX and revocation

Update `/auth/me` and frontend navigation/actions to use capability-based data. Provide a separate permission-management action and audit view or history in the admin experience, accessible to permission managers; a licence-only administrator need not receive user-directory access. User-management notification and live-event recipients are active users with `ManageUsers`. Resolve authorization from current local state per request, including existing sessions, so revocation applies on the next request; UI refreshes its session/capabilities and handles 403 without relying on stale visibility.

## Risks / Trade-offs

- [Migration grants or locks out the wrong person] → Map old Admin to User Administrator only; verify migration counts and explicitly bootstrap a known active enrolled operator-selected user.
- [Identity manipulation obtains privileged access] → Protect all administrative mutations to privileged targets and never grant privileges during onboarding.
- [Concurrent removal defeats last-super guard] → Shared database transaction lock and PostgreSQL integration concurrency tests.
- [Rollback restores obsolete broad Admin semantics] → Treat rollback as a security-sensitive operator procedure; preserve backup/audit and explicitly decide role mappings rather than silently regranting privileges.
- [Existing frontend sessions/clients use old role payloads] → Coordinate deployment, update the SPA and contracts together, and test stale-session refresh and denial.

## Migration Plan

1. Deploy a migration creating assignments and audit storage, mapping every legacy Admin to User Administrator and leaving ordinary users unprivileged. Preserve the legacy mapping long enough for a controlled rollback; do not let it remain an authorization source.
2. Deploy backend and frontend together, verify access-request workflows, then run the documented explicit bootstrap command for a verified active enrolled user. Until bootstrap completes, no in-app permission manager exists.
3. Verify effective permissions, audit entry, and a second Super Administrator before delegating or demoting. For recovery, use the same controlled command.
4. Roll back only with a reviewed explicit mapping and backup of assignments/audit; legacy Admin grants broader authority, so never automatically map all new permission sets to Admin.

## Open Questions

None blocking; implementation must select and document the exact operator command invocation and PostgreSQL locking mechanism consistent with deployment.
