## Why

Glovelly's single administrator role combines user management with the authority to create more administrators. Before adding commercially sensitive licence management, the application needs enforceable separation of administrative responsibilities without allowing indirect privilege escalation.

## What Changes

- Introduce additive, code-defined administrative permission sets and capability-based ASP.NET Core authorization policies, including distinct user, licence, system, and permission-management capabilities.
- Restrict administrative changes to privileged accounts and permission-set assignments to permission managers; keep self-service settings separate.
- Provide audited permission assignment and a deliberate operator bootstrap/recovery command, with safeguards against losing the last usable Super Administrator.
- Migrate existing administrators to User Administrator, not Super Administrator; require explicit bootstrap for elevated authority.
- Update user provisioning, access-request review, notifications, session information, and administrative UI to reflect capabilities.
- **BREAKING** Replace the single Admin/User role contract in administrative and session APIs and remove role selection during ordinary account provisioning and access-request approval.
- Leave subscription and licence assignment features for #192; no custom-role editor, impersonation, or general audit platform is introduced.

## Capabilities

### New Capabilities
- `capability-based-administration`: Administrative permission sets, policy enforcement, protected account management, permission auditing, bootstrap, recovery, and lockout prevention.

### Modified Capabilities
- `admin-access-request-approval`: Restrict review to user managers and provision unprivileged users without choosing an administrative role.

## Impact

Backend authorization, user persistence and migration, access-request and admin endpoints, session responses, notifications, development seeding, operator tooling, and tests change. The authenticated React admin experience, types, and UAT journeys require corresponding updates. Deployment needs a documented one-time bootstrap and recovery procedure.
