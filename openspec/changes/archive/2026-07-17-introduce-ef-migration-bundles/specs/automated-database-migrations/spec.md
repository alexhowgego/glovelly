## ADDED Requirements

### Requirement: Migration source is versioned separately from web startup

The system SHALL store EF Core migration source files and the model snapshot in a dedicated migrations project that references the existing application data model.

#### Scenario: Developer adds a migration
- **WHEN** a developer generates a new EF Core migration for `AppDbContext`
- **THEN** the migration source and model snapshot are written under the migrations project rather than the web application startup surface

#### Scenario: Web application starts with PostgreSQL configured
- **WHEN** the web application starts with `ConnectionStrings:Glovelly` configured
- **THEN** startup SHALL NOT call `EnsureCreated`, `Migrate`, `MigrateAsync`, or equivalent schema-mutating database APIs

### Requirement: Migration bundle is included in the deployable image

The system SHALL build a self-contained Linux EF Core migration bundle from the migrations project and include it in the final application image.

#### Scenario: Container image is built
- **WHEN** CI builds the deployable container image
- **THEN** the final image contains a stable migration executable produced from the checked-in migrations

#### Scenario: Migration bundle fails
- **WHEN** the migration executable cannot connect to the target database or a migration fails
- **THEN** the executable SHALL return a non-zero exit code

### Requirement: Migrations run as an explicit Cloud Run Job

The system SHALL execute database migrations through a dedicated one-shot Cloud Run Job using the same image artifact that will be deployed to the application service.

#### Scenario: Staging deployment starts
- **WHEN** a staging deployment is eligible to deploy a new image
- **THEN** the pipeline runs the migration job against staging before deploying the staging service revision

#### Scenario: Production deployment starts
- **WHEN** staging UAT has passed and the production environment gate has approved the release
- **THEN** the pipeline runs the exact same migration artifact against production before deploying the production service revision

#### Scenario: Migration job fails
- **WHEN** the migration job exits unsuccessfully
- **THEN** the corresponding service deployment SHALL stop and expose migration failure logs in GitHub Actions

### Requirement: Migration execution is safe to rerun

The system SHALL rely on EF Core migration history so that rerunning the same bundle applies only migrations missing from `__EFMigrationsHistory`.

#### Scenario: Bundle runs after all migrations are applied
- **WHEN** the migration bundle runs against a database whose `__EFMigrationsHistory` already records all migrations in the bundle
- **THEN** no migration is reapplied and the command exits successfully

### Requirement: CI validates migration consistency

The system SHALL fail CI when the EF model has pending schema changes without a checked-in migration and SHALL verify that the migration chain can create a fresh PostgreSQL schema.

#### Scenario: Model changes without migration
- **WHEN** a pull request changes the EF model without updating migrations
- **THEN** CI fails with an actionable pending-model-changes error

#### Scenario: Migration chain creates fresh schema
- **WHEN** CI applies the checked-in migration chain to an empty PostgreSQL database
- **THEN** the schema is created successfully and a second migration run is harmless

### Requirement: Migration workflow is documented

The system SHALL document the migration generation, review, CI validation, deployment ordering, rerun behavior, and rollback expectations.

#### Scenario: Developer reviews database docs
- **WHEN** a developer needs to make a schema change
- **THEN** the database documentation explains how to generate and review EF migrations and why destructive same-release changes require explicit care
