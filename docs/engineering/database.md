# Database

Neon Postgres is the production database and primary system of record.

Local development and tests can use EF Core's in-memory provider when no Glovelly connection string is configured.

## System Of Record

The database stores:

- Glovelly users
- roles and access metadata
- clients and contacts
- gigs
- gig import batches and draft rows
- gig expenses and receipt attachment metadata
- invoices and invoice lines
- seller profiles
- Google Drive connection metadata
- MCP OAuth state/tokens
- ASP.NET Core data protection keys in Postgres-backed deployments
- future domain entities

Generated binary content such as receipt files and invoice PDFs is stored through blob storage abstractions, with database records holding metadata and storage keys.

Gig import batches and drafts are staging records. They can contain incomplete AI-extracted data and are not treated as real gigs until the user commits accepted rows. Rejected draft rows are deleted when import decisions are committed.

## EF Core

`AppDbContext` is the EF Core boundary. Entity configuration lives under `backend/Glovelly.Api/Data/Configuration/`.

Production uses Npgsql when `ConnectionStrings:Glovelly` is configured. Without that connection string, the app uses an in-memory database and seeds development data outside the testing environment.

Schema evolution should use EF Core migrations. Migrations should describe deliberate product/data changes rather than incidental local experiments.

## Ownership And Access

Use one application database at this stage. Do not introduce separate databases or schemas per user without a clear product/operational reason.

Business data should relate to internal Glovelly users/domain entities, not directly to raw provider claims.

User-owned entities generally carry internal `CreatedByUserId` and `UpdatedByUserId` values. Endpoints and query services should apply existing visibility helpers such as `WhereVisibleTo(...)` or equivalent owner checks before returning user-scoped data.

External identity is not domain ownership. Google subject IDs and emails help Glovelly authenticate and enrol a user, but domain records should reference internal Glovelly user IDs or future account/tenant constructs.

## Data Protection Keys

When Postgres is configured, ASP.NET Core data protection keys are persisted through `AppDbContext`. This supports stable cookie/token protection across Cloud Run instances and deploys.

## Operational Notes

The database connection string is a runtime secret and should be supplied through secure configuration, currently via Cloud Run/Secret Manager binding.

Backup, restore, retention, and operational alerting for Neon data are important follow-up topics for the operations handbook.
