## Why

Glovelly currently evolves production PostgreSQL schemas through manually reviewed one-off SQL scripts that are executed manually against staging and production. As the schema and production dataset grow, this creates avoidable release risk around ordering, omission, duplication, environment drift, and operator error.

Database migrations should become an explicit, observable deployment stage that uses the same immutable image promoted through staging and production, without allowing the web application to mutate schema during ordinary startup.

## What Changes

- Add EF Core migration tooling and a dedicated `Glovelly.Migrations` project that owns migration source files, the model snapshot, and design-time `AppDbContext` creation while referencing the existing `Glovelly.Api` model.
- Build a self-contained Linux EF migration bundle from `Glovelly.Migrations` and include it in the final application image as the stable migration executable.
- Add a dedicated one-shot Cloud Run Job path that runs the bundle against the selected environment using the existing Secret Manager connection string binding.
- Reorder CI/CD so migrations run before each corresponding Cloud Run service deployment and failures stop the release.
- Add CI validation that checked-in migrations match the EF model and can create a fresh PostgreSQL schema.
- Remove PostgreSQL schema creation from web application startup while preserving local/test in-memory behavior.
- Document the new database migration and deployment workflow.

## Capabilities

### New Capabilities

- `automated-database-migrations`: Versioned EF Core migration history, migration bundle packaging, Cloud Run Job execution, CI validation, and release-pipeline ordering for PostgreSQL schema changes.

### Modified Capabilities

## Impact

- Backend projects: `Glovelly.Api`, new `Glovelly.Migrations`, solution/project references, central package versions, design-time EF configuration.
- Container build: root `Dockerfile` must build and copy the EF migration bundle into the final image.
- Deployment: `.github/workflows/main.yml` and a new Cloud Run Job deployment/execution script.
- Runtime startup: PostgreSQL deployments must no longer call `EnsureCreated`, `Migrate`, or equivalent schema-mutating startup code.
- CI: migration consistency checks and PostgreSQL-backed migration-chain validation.
- Documentation: database and deployment engineering handbook pages.
