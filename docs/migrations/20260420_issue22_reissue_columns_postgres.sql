-- One-time migration for Issue #22 (invoice re-issue metadata)
-- Target: PostgreSQL
-- Safe to run once in production before deploying the API changes.

BEGIN;

ALTER TABLE "Invoices"
    ADD COLUMN IF NOT EXISTS "ReissueCount" integer NOT NULL DEFAULT 0;

ALTER TABLE "Invoices"
    ADD COLUMN IF NOT EXISTS "LastReissuedUtc" timestamp with time zone NULL;

ALTER TABLE "Invoices"
    ADD COLUMN IF NOT EXISTS "LastReissuedByUserId" uuid NULL;

COMMIT;
