## Why

Glovelly already has established staging and production PostgreSQL schemas, but neither database has an EF `__EFMigrationsHistory` ledger. Before ordinary EF migrations can be deployed safely, the current schema must be represented as a checked-in `InitialBaseline` migration and existing databases must be registered as already having that baseline without replaying its DDL.

## What Changes

- Depends on `introduce-ef-migration-bundles`, which establishes the migrations project, EF bundle, Cloud Run Job, and CI/deployment migration path.
- Compare the current EF model, staging schema, and production schema before generating the baseline.
- Generate a checked-in `InitialBaseline` migration in the dedicated migrations project to represent the complete current schema for fresh PostgreSQL databases.
- Verify the migration chain can create a working fresh PostgreSQL schema and that rerunning the migration bundle is harmless.
- Generate and review baseline SQL as evidence of what the baseline represents, but do not execute that DDL against existing staging or production databases.
- Prepare a guarded migration-history registration operation that creates/verifies `__EFMigrationsHistory` and inserts exactly the generated baseline row in existing databases.
- Baseline staging first, verify no domain schema/data changes, then baseline production through the same reviewed procedure after backup/restore confirmation and approval.
- Document the exact baseline identifier, EF product version, verification evidence, and operational recovery path.

## Capabilities

### New Capabilities

- `initial-migration-baseline`: One-time adoption of existing PostgreSQL databases into EF migration history without modifying existing domain schema or data.

### Modified Capabilities

## Impact

- Migrations: generates the first checked-in `InitialBaseline` migration and model snapshot in the migrations project from `introduce-ef-migration-bundles`.
- Operations: requires schema comparison, backups/restore points, guarded SQL history registration, and staging-before-production execution.
- CI/deployment: verifies fresh database creation and no-op bundle reruns using the migration infrastructure.
- Documentation: records baseline evidence and rules for future migrations.
