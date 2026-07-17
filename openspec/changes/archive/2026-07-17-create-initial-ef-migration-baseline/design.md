## Context

The `introduce-ef-migration-bundles` change establishes EF Core migration tooling, a dedicated `Glovelly.Migrations` project, migration bundle packaging, Cloud Run Job execution, and CI validation. This baseline change depends on that infrastructure.

Staging and production already contain Glovelly PostgreSQL schemas created through prior manual SQL operations. Those databases must not receive the generated baseline DDL because their domain objects already exist. Instead, after verifying schema equivalence, they should receive only the EF migration-history row that records `InitialBaseline` as already applied.

The generated `InitialBaseline` still matters: it is the authoritative checked-in representation of the current schema and must be able to create a fresh PostgreSQL database from scratch.

## Goals / Non-Goals

**Goals:**

- Reconcile the EF model, staging schema, and production schema before baseline generation.
- Generate a checked-in `InitialBaseline` migration that represents the complete intended current schema.
- Prove the migration chain can create a fresh working PostgreSQL schema.
- Register existing staging and production databases as having already applied `InitialBaseline` without modifying domain tables, columns, indexes, constraints, or data.
- Use guarded, reviewed history-registration SQL that fails loudly if the database is not in the expected pre-baseline state.
- Document the exact migration identifier, EF product version, comparison evidence, backup/restore evidence, and verification results.

**Non-Goals:**

- Execute `InitialBaseline` DDL against existing staging or production databases.
- Reconstruct historical manual schema scripts as separate migrations.
- Introduce feature schema changes such as `GigType`; those belong in later ordinary migrations.
- Squash or replace migration history after ordinary migrations begin.
- Automatically baseline arbitrary databases without schema verification.
- Roll back failed production migration adoption by running EF `Down` methods.

## Decisions

### Generate a full-schema `InitialBaseline`

The baseline migration should contain the complete current schema represented by `AppDbContext` and Npgsql provider configuration. It should create tables, keys, relationships, indexes, constraints, column types, defaults, and other provider-specific objects represented by EF.

Rationale: fresh PostgreSQL databases must be creatable from checked-in migrations. A metadata-only migration would simplify existing database adoption but would fail the fresh-database requirement.

Alternative considered: create an empty baseline migration. Rejected because it would not establish a complete schema history and would require separate schema bootstrap steps.

### Register existing databases by migration history only

For existing staging and production databases, execute only a narrow SQL operation that creates/verifies `__EFMigrationsHistory` and inserts the exact generated `InitialBaseline` row after schema equivalence is confirmed.

Rationale: replaying baseline DDL against existing schemas would fail or risk destructive behavior. The existing databases already have the domain schema; they need only the EF ledger entry.

Alternative considered: make the generated migration idempotent against existing schemas. Rejected because it would complicate the baseline and could hide drift that should be understood before adoption.

### Use guarded registration rather than broad idempotency

The registration SQL should refuse unexpected states: existing migration records, mismatched history shape, missing expected preconditions, or multiple baseline rows. It should not be a loose `INSERT IF NOT EXISTS`.

Rationale: this is a one-time controlled operation. Silent success in an unexpected state is more dangerous than a loud failure.

### Stage before production with evidence retention

Run the baseline registration first against staging after backup/restore confirmation and schema fingerprint capture. Promote the same reviewed procedure to production only after staging verification, UAT, and production backup/approval.

Rationale: production adoption changes release safety posture and should be auditable. Staging validates both the procedure and the migration bundle's no-op behavior against a baselined existing database.

## Risks / Trade-offs

- Live schema differs from EF model → Stop and resolve drift before generating or registering the baseline.
- Baseline migration accidentally includes feature changes → Generate only from the current model before post-baseline schema work such as `GigType`.
- Wrong migration identifier or product version is registered → Extract values from generated EF artifacts and reviewed SQL, not from memory.
- Registration SQL is too permissive → Make it intentionally guarded and fail on unexpected existing history.
- Production backup is unavailable or unverified → Do not baseline production until recovery evidence exists.
- Fresh database verification misses provider-specific differences → Use a real PostgreSQL database for verification, not EF in-memory.

## Migration Plan

1. Complete `introduce-ef-migration-bundles` so migration project, bundle, and CI/deployment paths exist.
2. Compare EF model, staging, and production schemas and document any drift or expected differences.
3. Generate `InitialBaseline` in `Glovelly.Migrations` and review migration source, snapshot, and generated SQL.
4. Apply the migration chain to a temporary empty PostgreSQL database and verify app compatibility plus no-op rerun behavior.
5. Prepare and review guarded migration-history registration SQL for existing databases.
6. Confirm staging backup/restore point, execute registration against staging, verify no domain schema/data changes, verify bundle no-op, and run staging UAT.
7. Confirm production backup/restore point and approval, execute the same registration procedure against production, verify no domain schema/data changes, verify bundle no-op, and run smoke tests.
8. Update documentation with migration identifier, EF product version, evidence, and the rule that future schema changes use ordinary checked-in migrations.

Rollback for a failed registration before domain schema changes is database restore from the confirmed backup/restore point or manual removal of the narrowly inserted history row only if the failure mode is fully understood and reviewed. Automatic EF `Down` execution is not part of this procedure.
