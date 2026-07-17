## Context

Glovelly is deployed as a single immutable Cloud Run image containing the ASP.NET Core API, bundled Vite frontend, and worker binaries. The production database is PostgreSQL, with the connection string supplied through Secret Manager as `ConnectionStrings__Glovelly`. When no connection string is configured, local development and tests use EF Core's in-memory provider.

The current web startup calls `InitializeDatabaseAsync`, which seeds local development data or calls `EnsureCreatedAsync`. That startup behavior is incompatible with managed EF migrations for PostgreSQL because schema mutation should become an explicit deployment concern with logs, permissions, ordering, and a clear failure boundary.

Issue #198 will handle the one-time `InitialBaseline` adoption for existing staging and production databases. This change creates the migration machinery that #198 depends on.

## Goals / Non-Goals

**Goals:**

- Establish checked-in EF Core migrations as Glovelly's authoritative PostgreSQL schema history.
- Keep migration source files and model snapshot outside `Glovelly.Api` in a dedicated `Glovelly.Migrations` project.
- Build a self-contained Linux EF migration bundle into the same final container image as the web app.
- Run migrations as a dedicated one-shot Cloud Run Job before each environment's service deployment.
- Fail deployments on migration failure and expose actionable logs.
- Validate in CI that migrations are consistent with the EF model and can create a fresh PostgreSQL schema.
- Preserve the current in-memory local/test database path where no PostgreSQL connection string exists.

**Non-Goals:**

- Create a custom migration host or wrapper CLI around EF migration bundles.
- Extract `AppDbContext` and entity configuration into a new `Glovelly.Data` project.
- Run migrations automatically from the web application or worker startup path.
- Reconstruct historical manual SQL scripts as separate migrations.
- Perform the `InitialBaseline` generation or existing database history registration; that belongs to the dependent baseline change.
- Automatically roll back failed production migrations through EF `Down` methods.

## Decisions

### Use a dedicated migrations project without extracting a data project

Create `backend/Glovelly.Migrations` to own migration files, `AppDbContextModelSnapshot`, and a design-time factory. The project references `Glovelly.Api`, where `AppDbContext`, models, and entity configuration remain for now.

Rationale: this separates migration artifacts from the web app while avoiding a larger data-layer extraction. It gives the release pipeline a clear migration surface without moving the existing model during an infrastructure change.

Alternative considered: store migrations directly in `Glovelly.Api`. This is simpler but makes the web app assembly the migration surface and blurs the deployment boundary this change is trying to establish.

Alternative considered: create `Glovelly.Data` now. This is cleaner architecturally but adds a broader refactor to an already high-risk migration adoption. Defer until API/data coupling becomes painful.

### Use EF migration bundles directly

Build the migration executable with `dotnet ef migrations bundle --self-contained --runtime linux-x64` from the migrations project and copy it into the final image, for example as `/app/efbundle`.

Rationale: EF bundles already provide the executable, history-table behavior, idempotent pending-migration application, logging, and non-zero failure status needed here.

Alternative considered: a custom migration host in `Glovelly.Migrations`. This would duplicate EF bundle behavior and create another operational surface without a current need.

### Keep web startup non-mutating for PostgreSQL

PostgreSQL deployments must not call `EnsureCreated`, `Migrate`, or migration APIs at web startup. Local/test in-memory behavior may continue to create and seed data as needed.

Rationale: schema changes need explicit ordering before service deployment. Startup-time mutation can race across instances and hides migration failures inside ordinary application startup.

### Reuse the Cloud Run Job pattern

Add a dedicated migration job script following the existing worker-job deployment style: same image URI, Secret Manager binding for `ConnectionStrings__Glovelly`, runtime service account, bounded timeout, zero retries, and explicit execution from GitHub Actions.

Rationale: Glovelly already uses Cloud Run Jobs for non-interactive worker commands from the same image. Migration execution is also non-interactive and benefits from the same operational model.

### Gate service deployment on migration success

The pipeline order becomes build, migrate staging, deploy staging, staging UAT, production gate, migrate production, deploy production, production smoke.

Rationale: a migration failure must stop the release before an application revision that expects the new schema receives traffic.

## Risks / Trade-offs

- Dedicated migrations project still references `Glovelly.Api` → Accept for now; defer `Glovelly.Data` extraction unless design-time coupling becomes problematic.
- Initial migration adoption can damage existing environments if executed incorrectly → Keep baseline generation and history registration in the dependent #198 change with explicit safeguards.
- CI PostgreSQL migration tests add build time and infrastructure complexity → Use a service container or equivalent disposable database because in-memory tests cannot validate provider-specific schema.
- Cloud Run Job execution could be triggered concurrently by separate workflow runs → Use GitHub workflow concurrency and configure the migration job for one task with no retries; rely on EF/PostgreSQL migration locking as an additional safeguard.
- Bundle build may require EF tooling in the Docker SDK stage → Install or restore tooling explicitly and keep versions aligned with central EF package versions.

## Migration Plan

1. Add migration tooling and `Glovelly.Migrations` without creating ordinary post-baseline migrations yet.
2. Build the EF bundle into the container image and verify the executable is present.
3. Add CI checks against disposable PostgreSQL databases.
4. Add migration Cloud Run Job deployment/execution steps before service deployment.
5. Remove PostgreSQL schema creation from web startup.
6. Document the workflow and hand off to the dependent baseline change for `InitialBaseline` creation and existing database registration.

Rollback for this infrastructure change is to stop invoking the migration job and deploy a previous image/workflow. Once ordinary migrations are used, database rollback remains an operational restore or forward-fix process, not automatic `Down` execution.
