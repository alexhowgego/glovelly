## 1. Migration Project And Tooling

- [x] 1.1 Add EF Core design-time tooling and package versions through central package management.
- [x] 1.2 Add `backend/Glovelly.Migrations` to own EF migration files, the model snapshot, and design-time `AppDbContext` creation.
- [x] 1.3 Reference `Glovelly.Api` from `Glovelly.Migrations` and configure the migrations assembly for Npgsql design-time operations.
- [x] 1.4 Add the migrations project to `glovelly.sln` and verify `dotnet restore glovelly.sln` succeeds.

## 2. Startup Boundary

- [x] 2.1 Remove PostgreSQL schema creation from web startup so configured Postgres deployments do not call `EnsureCreated`, `Migrate`, or `MigrateAsync`.
- [x] 2.2 Preserve in-memory local development and test database initialization/seeding behavior where no Glovelly connection string is configured.
- [x] 2.3 Add or update tests that protect the startup boundary where practical.

## 3. Migration Bundle Image Packaging

- [x] 3.1 Update the Docker build to restore any required EF tooling and build a self-contained `linux-x64` migration bundle from `Glovelly.Migrations`.
- [x] 3.2 Copy the migration bundle into the final image at a stable path such as `/app/efbundle`.
- [x] 3.3 Verify the final image keeps the existing web entrypoint and worker binaries unchanged.

## 4. Cloud Run Migration Job

- [x] 4.1 Add a migration job deployment/execution script using the same image URI, runtime service account, and `ConnectionStrings__Glovelly` Secret Manager binding.
- [x] 4.2 Configure the migration job as a one-shot task with bounded timeout and no automatic retries.
- [x] 4.3 Ensure GitHub Actions captures useful job output and failure details when migration execution fails.

## 5. Pipeline Integration

- [x] 5.1 Add a staging migration job that runs after image build and before staging service deployment.
- [x] 5.2 Keep staging UAT after staging service deployment.
- [x] 5.3 Add a production migration job that runs after staging UAT and the production environment gate but before production service deployment.
- [x] 5.4 Ensure both staging and production use the exact migration bundle contained in the promoted image.

## 6. CI Validation

- [x] 6.1 Add CI validation for pending EF model changes without checked-in migrations.
- [x] 6.2 Add PostgreSQL-backed CI validation that applies the complete migration chain to an empty database.
- [x] 6.3 Validate that running the migration chain or bundle twice is harmless where practical.

## 7. Documentation And Verification

- [x] 7.1 Update database engineering documentation with migration generation, review, and operational expectations.
- [x] 7.2 Update deployment pipeline documentation with migration job ordering and failure behavior.
- [x] 7.3 Run `dotnet test glovelly.sln -m:1` and relevant frontend checks if the workflow or Docker changes require them.
- [x] 7.4 Verify the change is ready for the dependent `create-initial-ef-migration-baseline` work.
