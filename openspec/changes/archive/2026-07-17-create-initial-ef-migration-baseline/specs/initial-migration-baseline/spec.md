## ADDED Requirements

### Requirement: Baseline depends on migration infrastructure

The system SHALL create and register the initial migration baseline only after EF migration tooling, the migrations project, migration bundle packaging, and migration job execution are available.

#### Scenario: Baseline work starts before infrastructure exists
- **WHEN** the migration project or bundle infrastructure is not yet available
- **THEN** the baseline generation and live registration work SHALL NOT proceed

### Requirement: Current schemas are reconciled before baseline generation

The system SHALL compare the EF model, staging schema, and production schema before generating or registering the baseline.

#### Scenario: Schema drift is detected
- **WHEN** staging, production, or the EF model differ in unexplained tables, columns, constraints, indexes, defaults, enum representations, or provider-specific objects
- **THEN** the baseline process SHALL stop until the discrepancy is understood, resolved, or explicitly documented as expected

### Requirement: InitialBaseline represents the complete current schema

The system SHALL include a checked-in `InitialBaseline` migration that can create the complete intended current Glovelly PostgreSQL schema from an empty database.

#### Scenario: Empty PostgreSQL database is migrated
- **WHEN** the migration chain is applied to an empty PostgreSQL database
- **THEN** the database contains the complete current schema and records `InitialBaseline` in `__EFMigrationsHistory`

#### Scenario: Baseline SQL is reviewed
- **WHEN** SQL is generated for `InitialBaseline`
- **THEN** it is reviewed as evidence of the baseline schema and SHALL NOT be executed against existing staging or production databases

### Requirement: Existing databases are registered without domain changes

The system SHALL register `InitialBaseline` as already applied in existing staging and production databases by modifying only EF migration history after schema equivalence is established.

#### Scenario: Existing staging is baselined
- **WHEN** the reviewed registration operation runs against staging
- **THEN** `InitialBaseline` is recorded in `__EFMigrationsHistory` and no domain tables, columns, indexes, constraints, or data are modified

#### Scenario: Existing production is baselined
- **WHEN** the reviewed registration operation runs against production after staging verification and production backup confirmation
- **THEN** `InitialBaseline` is recorded in `__EFMigrationsHistory` and no domain tables, columns, indexes, constraints, or data are modified

### Requirement: Registration is guarded and auditable

The system SHALL use a narrowly scoped migration-history registration operation that fails loudly when the target database is not in the expected pre-baseline state.

#### Scenario: Unexpected migration history exists
- **WHEN** the target database already contains unexpected migration history rows
- **THEN** the registration operation fails without inserting or modifying the baseline row

#### Scenario: Baseline identifier is determined
- **WHEN** the registration operation is prepared
- **THEN** the migration identifier and EF product version are taken from generated EF artifacts rather than typed from memory

### Requirement: Baselined databases report no pending migrations

The system SHALL verify that the migration bundle reports no pending migrations after `InitialBaseline` is recorded in existing databases.

#### Scenario: Bundle runs against baselined database
- **WHEN** the migration bundle runs against a database whose history records `InitialBaseline` and no later migrations exist
- **THEN** the bundle exits successfully without applying schema changes

### Requirement: Baseline evidence is documented

The system SHALL document the baseline migration identifier, EF product version, schema comparison evidence, backup/restore evidence, registration procedure, and verification results.

#### Scenario: Operator reviews baseline documentation
- **WHEN** an operator reviews the database engineering documentation after baseline completion
- **THEN** it identifies how staging and production were registered and states that future schema changes must use ordinary checked-in migrations
