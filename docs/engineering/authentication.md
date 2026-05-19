# Authentication

Glovelly uses Google OpenID Connect for authentication and its own internal user records for application access.

Google authenticates identity; Glovelly authorises access.

A valid Google login is not, by itself, sufficient to access Glovelly.

## Flow

1. A user signs in with Google.
2. Google returns identity claims to Glovelly.
3. Glovelly reads the Google subject identifier and verified email claims.
4. Glovelly maps those claims to an internal `User` record.
5. Glovelly checks that the user is active.
6. Glovelly issues an application cookie containing internal Glovelly user and role claims.
7. Application endpoints authorise work against Glovelly policies, roles, and ownership rules.

## Durable Identity

Email addresses are useful for administration and contact, but the Google subject identifier is the durable external login binding.

Email alone is not a stable identity key. It can change, be reissued, or have administrative meaning that differs from identity-provider subject identity. Once Glovelly knows a user's Google subject ID, login mapping should use that subject rather than email.

## Enrolment

The intended enrolment model is admin-friendly while converging on subject-based identity:

1. An admin provisions a Glovelly user record, usually by email address.
2. The user may start active or inactive according to admin intent.
3. `GoogleSubject` may initially be null.
4. On first successful Google login, an active provisioned user can be matched by verified email.
5. Glovelly binds the Google subject ID to that user.
6. Future logins use the Google subject ID as the canonical external login mapping.

If no matching active user exists, the login is denied and the access-request flow can collect enough information for an admin to decide what to do.

## Roles And Permissions

Roles are application-owned concepts. Google provides identity, not business permissions.

Current roles are:

- `Admin`
- `User`

Server-side policies require an authenticated Glovelly user claim and, for admin routes, the admin role. User-owned business data should also be filtered by internal ownership rules such as `WhereVisibleTo(...)`.

## Implementation Notes

Authentication registration lives in `backend/Glovelly.Api/Configuration/AuthenticationServiceCollectionExtensions.cs`.

User mapping and first-login subject binding happen during Google token validation. Cookie validation checks that the internal user still exists and is active.

Current-user access and policy names live under `backend/Glovelly.Api/Auth/`.

