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
6. Glovelly issues an application cookie identifying the internal user.
7. Application endpoints authorise work against current database-backed capabilities and ownership rules.

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

Google provides identity, not business permissions. Administrative permission sets are additive and code-defined:

| Permission set | Capabilities |
| --- | --- |
| User Administrator | `ManageUsers` |
| License Administrator | `ManageLicences` |
| Super Administrator | `ManageUsers`, `ManageLicences`, `ManageSystem`, `ManagePermissions` |

ASP.NET Core policies resolve current assignments from the local database for each request. New capabilities are not implicitly included in Super Administrator. The old persisted role remains for migration/rollback, not administrative authorization. Permission assignment is a separate audited action requiring `ManagePermissions`; user administrators cannot alter privileged accounts, including identity and invitation fields. User-owned business data still uses ownership rules such as `WhereVisibleTo(...)`.

## Bootstrap and recovery

The migration maps legacy Admin accounts to User Administrator only. After migration, an operator must explicitly grant the first Super Administrator to an existing active user who has already enrolled with Google. No startup setting permanently grants this authority. The same command is available for emergency recovery and is idempotent.

1. Verify the target environment, database and active enrolled user ID using an authorized read-only operator workflow. Back up the database and confirm the deployment's migration completed.
2. Run the application image as a one-off Cloud Run Job, **overriding the image entrypoint** to `dotnet` and using arguments `Glovelly.Api.dll`, `grant-super-admin`, `--user-id`, the verified GUID, `--operator`, the accountable operator identity, `--reason`, and the reason. The normal image entrypoint starts the web server and does not forward job arguments.
3. Bind `ConnectionStrings__Glovelly` to the target environment's existing database secret, use the same network access as the migration job, and restrict who can create/execute the job and access that secret. Never place the connection string in job arguments, logs, or repository files. The operator value is an accountable assertion, not independently authenticated by the command; correlate it with Cloud audit logs.
4. Check the command exit status, the `AdministrativePermissionChanges` audit entry, and the effective permissions in the application. Assign a second active, enrolled Super Administrator before a handover.

For local operator execution, set `ConnectionStrings__Glovelly` securely in the process environment, then run `dotnet run --project backend/Glovelly.Api -- grant-super-admin --user-id <guid> --operator <identity> --reason <reason>`. The command refuses missing, inactive or unenrolled targets and never applies migrations.

For the ephemeral local in-memory database, a separate operator process cannot access the web process's data. Use `./run-dev.sh --super-admin` to run the same audited bootstrap in the web process immediately after development seeding. This explicit one-run option requires `DevelopmentSeeding:AdminGoogleSubject` and rejects non-Development, staging, testing, or PostgreSQL configurations. Restart without it for the normal User Administrator fixture; the in-memory assignments and audit history do not persist across restarts.

Rollback is security-sensitive: preserve a database backup and permission audit, inspect the old `Users.Role` values, and explicitly review the resulting legacy Admin access before returning to old code. The migration down operation removes permission and audit tables; do not run it before preserving their contents. Do not automatically convert every new administrative assignment into legacy Admin.

## Implementation Notes

Authentication registration lives in `backend/Glovelly.Api/Configuration/AuthenticationServiceCollectionExtensions.cs`.

User mapping and first-login subject binding happen during Google token validation. Cookie validation checks that the internal user still exists and is active.

Current-user access and policy names live under `backend/Glovelly.Api/Auth/`.

## Optional Google Service Authorisation

The Google sign-in flow is separate from optional Google service authorisation. Optional service OAuth endpoints reuse the encrypted `GoogleConnection` record and merge granted scopes, but each journey asks for only the scope it needs:

- `/integrations/google-drive/connect` requests `https://www.googleapis.com/auth/drive.file` for invoice PDF publishing.
- `/integrations/google-sheets/connect` requests `https://www.googleapis.com/auth/spreadsheets.readonly` for setlist import.
- `/integrations/google-docs/connect` requests `https://www.googleapis.com/auth/documents.readonly` for Google Doc intake. This broad read-only permission is optional and is disclosed before connecting; resource attachment does not need it.
- `/integrations/google-calendar/connect` requests the Calendar scopes needed by the gig sync service.

Drive, Sheets and Docs can be connected or disconnected independently from the profile `Services` menu. Disconnecting one service removes that local scope and revokes the shared token record only when no Google service scopes remain; it does not revoke Google's grant.

The Sheets readonly scope may require an HTTPS-only OAuth client in Google Cloud. Local HTTP development can continue using Drive without requesting Sheets; use staging or an HTTPS tunnel when testing the Sheets flow against real Google OAuth.

Drive, Sheets, Docs and Calendar use the shared OAuth callback `/integrations/google/callback`. Before deploying this change, register the full callback URI for each local, staging and production OAuth client using its configured public API origin. Register the new URI before deployment; after all four integrations are verified, remove the old `/integrations/google-drive/callback`, `/integrations/google-sheets/callback` and `/integrations/google-calendar/callback` URIs. In-flight authorizations using the old callbacks must be restarted; saved connections are unaffected.

The current deployed callback URIs are `https://staging.glovelly.net/integrations/google/callback` and `https://menu.glovelly.net/integrations/google/callback`. Local development derives its callback origin from `App:PublicBaseUrl` or the first configured allowed CORS origin (falling back to `http://localhost:5173`); verify the actual origin and OAuth client configuration before registering it.

Before enabling Google Doc intake in production, enable the Google Docs API for the OAuth project and verify the consent-screen scope, verification status, registered shared callback URI, user-facing privacy disclosure, and the runtime account's Vertex access. Exercise connection, denial, revocation, retry and resource-only fallback on staging. Users can revoke Docs access in their Google Account; a retained batch snapshot is not removed by revocation.

## Staging UAT Authentication

Staging registers `POST /test-auth/login` so browser-based UAT tests can authenticate deterministically without exercising Google on every regression run.

The endpoint is intentionally not registered outside staging. In staging it requires the `X-Glovelly-Uat-Secret` header to match `GLOVELLY_UAT_SECRET` and signs in the seeded `regression@glovelly.net` test-only user with the normal Glovelly auth cookie.

Do not enable this endpoint in production. It exists only to make Playwright regression tests stable; Google authentication should still be covered separately by manual or focused checks.
