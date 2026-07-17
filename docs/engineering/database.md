# Database

Neon Postgres is the production database and primary system of record.

Local development and tests can use EF Core's in-memory provider when no Glovelly connection string is configured.

## System Of Record

The database stores:

- Glovelly users
- roles and access metadata
- clients and contacts
- gigs
- imported gig setlists and reviewed setlist item rows
- gig import batches and draft rows
- gig expenses and receipt attachment metadata
- invoices and invoice lines
- seller profiles
- reusable Google connection metadata plus Drive, Sheets, and Calendar integration settings/status
- Google Calendar sync state and durable Calendar sync queue work items
- MCP OAuth state/tokens
- ASP.NET Core data protection keys in Postgres-backed deployments
- future domain entities

Generated binary content such as receipt files and invoice PDFs is stored through blob storage abstractions, with database records holding metadata and storage keys.

Gig import batches and drafts are staging records. They can contain incomplete AI-extracted data and are not treated as real gigs until the user commits accepted rows. Rejected draft rows are deleted when import decisions are committed.

Setlist imports are reviewed snapshots of linked Google Sheet worksheet rows. `GigSetListImports` stores source metadata and active/history state; `GigSetListItems` stores ordered reviewed rows, source row numbers, song/separator/comment kind, and raw source cells for auditability. Re-import creates a new import rather than overwriting prior history.

## EF Core

`AppDbContext` is the EF Core boundary. Entity configuration lives under `backend/Glovelly.Api/Data/Configuration/`.

Production uses Npgsql when `ConnectionStrings:Glovelly` is configured. Without that connection string, the app uses an in-memory database and seeds development data outside the testing environment.

Checked-in EF Core migrations are the authoritative schema history for PostgreSQL deployments. Migration files and the model snapshot live in `backend/Glovelly.Migrations`, which references the existing `Glovelly.Api` data model and owns design-time `AppDbContext` creation.

Generate migrations from the repo root with the migrations project as both project and startup project:

```bash
dotnet tool restore
dotnet tool run dotnet-ef migrations add <MigrationName> \
  --project backend/Glovelly.Migrations/Glovelly.Migrations.csproj \
  --startup-project backend/Glovelly.Migrations/Glovelly.Migrations.csproj \
  --context AppDbContext
```

Review generated migrations before merge. Pay particular attention to destructive operations, renames that EF may model as drop/create, data transformations, defaults for existing rows, indexes, constraints, and provider-specific PostgreSQL details. Prefer expand-and-contract changes for production-safe releases.

The web application must not apply PostgreSQL schema changes during startup. When `ConnectionStrings:Glovelly` is configured, schema creation and migration execution are deployment concerns handled by the migration bundle and Cloud Run Job. Local development and tests may still use EF Core's in-memory provider when no Glovelly connection string is configured.

CI validates EF migration consistency once an initial model snapshot exists. It checks for pending model changes without a migration, applies the migration chain to a disposable PostgreSQL database, and runs the update path twice to verify no-op reruns.

The `InitialBaseline` migration and live database registration are handled by the dedicated baseline procedure. The baseline migration is for fresh database creation; existing staging and production databases are registered as already having applied it only after schema equivalence has been verified.

### InitialBaseline Adoption

`InitialBaseline` is `20260717214619_InitialBaseline`, generated with EF Core product version `10.0.8` in `backend/Glovelly.Migrations`.

Schema evidence captured before registration:

- Neon staging and production schema exports were compared before baseline generation.
- Staging and production domain schemas matched.
- Staging already contained an empty, correctly shaped `__EFMigrationsHistory` table from migration-pipeline execution.
- Production did not yet contain `__EFMigrationsHistory` before registration.
- The only staging/production schema differences were expected EF history presence, column-order/export formatting, and equivalent check-constraint formatting.
- EF baseline SQL was compared against the production schema export. Column shape matched after explicitly modelling existing nullable seller address columns, existing defaults, and `InvoiceLines.CalculationNotes` as `text`.
- Index shape matched apart from primary-key indexes emitted differently by Neon export and the EF convention support index `IX_SetListChartMatchJobs_GigId`, which appears only in fresh databases created by the baseline and is not a domain constraint.

Pre-registration table-count sanity checks were captured for `Users`, `Clients`, `Gigs`, `Invoices`, `InvoiceLines`, and `SellerProfiles` in both staging and production. These counts must remain unchanged after registration.

Existing databases must be registered with `docs/engineering/register-initial-baseline.sql`. That script modifies only `__EFMigrationsHistory`, allows either an absent history table or an existing empty EF-shaped history table, and fails if unexpected history rows or shape differences exist. Do not execute generated `InitialBaseline` DDL against staging or production.

After registration, verify:

- `__EFMigrationsHistory` contains exactly `20260717214619_InitialBaseline` with product version `10.0.8`.
- The table-count sanity query returns the same values captured before registration.
- The migration bundle exits successfully with no pending migrations.

Staging registration was completed first. The guarded SQL inserted the expected `20260717214619_InitialBaseline` history row, row-count sanity checks were unchanged, staging UAT passed, and the staging Cloud Run migration job reported `No migrations were applied. The database is already up to date.`

Production adoption follows the same reviewed registration procedure through the protected production rollout. Confirm the production restore point/branch before registration, verify the same history row and unchanged table counts, then allow the production migration job and smoke tests to complete as part of the merge-gated deployment.

Future schema changes, including any `GigType` work, must be added as ordinary post-baseline EF migrations. Do not edit or replace `InitialBaseline` after it has been registered in staging or production.

Rollback for a failed registration before later domain schema changes is Neon restore from the confirmed restore point or, only after review of the failure mode, removal of the narrowly inserted history row. Do not use EF `Down` methods for this adoption step.

## Ownership And Access

Use one application database at this stage. Do not introduce separate databases or schemas per user without a clear product/operational reason.

Business data should relate to internal Glovelly users/domain entities, not directly to raw provider claims.

User-owned entities generally carry internal `CreatedByUserId` and `UpdatedByUserId` values. Endpoints and query services should apply existing visibility helpers such as `WhereVisibleTo(...)` or equivalent owner checks before returning user-scoped data.

External identity is not domain ownership. Google subject IDs and emails help Glovelly authenticate and enrol a user, but domain records should reference internal Glovelly user IDs or future account/tenant constructs.

## Data Protection Keys

When Postgres is configured, ASP.NET Core data protection keys are persisted through `AppDbContext`. This supports stable cookie/token protection across Cloud Run instances and deploys.

## Operational Notes

The database connection string is a runtime secret and should be supplied through secure configuration, currently via Cloud Run/Secret Manager binding.

Deployment runs migrations explicitly before deploying each corresponding Cloud Run service revision. Migration failures stop the release; rollback normally means restoring a database backup or deploying a forward-fix migration, not automatically running EF `Down` methods.

Backup, restore, retention, and operational alerting for Neon data are important follow-up topics for the operations handbook.
