## 1. Prerequisite Confirmation

- [x] 1.1 Confirm `introduce-ef-migration-bundles` is complete enough to provide `Glovelly.Migrations`, design-time `AppDbContext`, EF bundle packaging, and PostgreSQL migration validation.
- [x] 1.2 Confirm no ordinary feature schema change, including `GigType`, has been folded into the baseline scope.

## 2. Schema Reconciliation

- [x] 2.1 Capture the schema represented by the current EF model and Npgsql provider configuration.
- [x] 2.2 Compare the EF model schema with staging and document differences.
- [x] 2.3 Compare staging and production schemas and document differences.
- [x] 2.4 Resolve or explicitly document all expected differences before baseline generation proceeds.

## 3. Baseline Generation And Review

- [x] 3.1 Generate `InitialBaseline` in the `Glovelly.Migrations` project.
- [x] 3.2 Review the generated migration source and model snapshot for complete current schema coverage.
- [x] 3.3 Generate SQL for `InitialBaseline` and review it as evidence of the baseline DDL.
- [x] 3.4 Confirm the generated baseline SQL is not wired into any path that executes against existing staging or production databases.

## 4. Fresh Database Verification

- [x] 4.1 Apply the migration chain to a temporary empty PostgreSQL database.
- [x] 4.2 Verify the resulting schema matches the intended current Glovelly schema.
- [x] 4.3 Verify the application can start against the freshly migrated database.
- [x] 4.4 Verify representative backend checks or UAT seeding can run against the freshly migrated database where practical.
- [x] 4.5 Verify `__EFMigrationsHistory` contains `InitialBaseline` with the expected identifier and EF product version.
- [x] 4.6 Verify rerunning the migration bundle reports no pending migrations and makes no schema changes.

## 5. Guarded Registration Operation

- [x] 5.1 Determine the exact `InitialBaseline` migration identifier and EF product version from generated EF artifacts.
- [x] 5.2 Prepare guarded SQL that creates/verifies `__EFMigrationsHistory` shape only if needed.
- [x] 5.3 Ensure the guarded SQL fails if unexpected migration history already exists.
- [x] 5.4 Ensure the guarded SQL inserts exactly the `InitialBaseline` row and does not modify domain objects.
- [x] 5.5 Review the registration SQL and verification queries before live execution.

## 6. Staging Baseline

- [x] 6.1 Confirm a recoverable staging backup or restore point exists.
- [x] 6.2 Capture staging schema fingerprint and row-count sanity checks before registration.
- [x] 6.3 Execute only the reviewed migration-history registration operation against staging.
- [x] 6.4 Verify no staging domain schema or data changed.
- [x] 6.5 Verify staging records `InitialBaseline` with the expected identifier and EF product version.
- [x] 6.6 Verify the migration bundle is a no-op against baselined staging.
- [x] 6.7 Run staging UAT or the agreed staging verification suite.

## 7. Production Baseline

- [x] 7.1 Confirm a recoverable production backup, Neon restore point, or branch exists.
- [x] 7.2 Capture production schema fingerprint and row-count sanity checks before registration.
- [x] 7.3 Execute the same reviewed migration-history registration procedure against production through the protected path.
- [x] 7.4 Verify no production domain schema or data changed.
- [x] 7.5 Verify production records `InitialBaseline` with the expected identifier and EF product version.
- [x] 7.6 Verify the migration bundle is a no-op against baselined production.
- [x] 7.7 Run production smoke tests.

## 8. Documentation And Handoff

- [x] 8.1 Update database documentation with the baseline identifier, EF product version, schema comparison evidence, registration procedure, and recovery notes.
- [x] 8.2 Update deployment documentation to state that `InitialBaseline` is used for fresh database creation and is registered, not replayed, on existing databases.
- [x] 8.3 Record that future schema changes must be represented by ordinary checked-in EF migrations.
- [x] 8.4 Hand off `GigType` or other feature schema work to post-baseline ordinary migrations.
