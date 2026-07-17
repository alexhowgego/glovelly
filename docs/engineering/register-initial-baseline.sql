-- One-time EF migration-history registration for existing Glovelly PostgreSQL databases.
--
-- This script must be run only after the live schema has been compared with the
-- checked-in InitialBaseline migration. It records the baseline in EF history;
-- it must not run the InitialBaseline DDL against existing staging/production.
--
-- Expected preconditions:
-- - Domain schema already matches the reviewed InitialBaseline schema.
-- - __EFMigrationsHistory is either absent or present with the EF shape and zero rows.
-- - A recoverable Neon backup, restore point, or branch exists for the target database.

BEGIN;

DO $$
DECLARE
    history_exists boolean;
    history_shape_matches boolean;
    history_row_count integer;
BEGIN
    SELECT to_regclass('public."__EFMigrationsHistory"') IS NOT NULL
    INTO history_exists;

    IF history_exists THEN
        SELECT count(*) = 2
           AND bool_and(
                (column_name = 'MigrationId'
                    AND data_type = 'character varying'
                    AND character_maximum_length = 150
                    AND is_nullable = 'NO')
                OR
                (column_name = 'ProductVersion'
                    AND data_type = 'character varying'
                    AND character_maximum_length = 32
                    AND is_nullable = 'NO'))
        INTO history_shape_matches
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = '__EFMigrationsHistory';

        IF NOT coalesce(history_shape_matches, false) THEN
            RAISE EXCEPTION '__EFMigrationsHistory exists but does not match EF Core history column shape';
        END IF;

        IF NOT EXISTS (
            SELECT 1
            FROM information_schema.table_constraints
            WHERE table_schema = 'public'
              AND table_name = '__EFMigrationsHistory'
              AND constraint_name = 'PK___EFMigrationsHistory'
              AND constraint_type = 'PRIMARY KEY'
        ) THEN
            RAISE EXCEPTION '__EFMigrationsHistory exists but expected primary key PK___EFMigrationsHistory is missing';
        END IF;

        EXECUTE 'SELECT count(*) FROM public."__EFMigrationsHistory"'
        INTO history_row_count;

        IF history_row_count <> 0 THEN
            RAISE EXCEPTION '__EFMigrationsHistory must be empty before InitialBaseline registration; found % row(s)', history_row_count;
        END IF;
    ELSE
        CREATE TABLE public."__EFMigrationsHistory" (
            "MigrationId" character varying(150) NOT NULL,
            "ProductVersion" character varying(32) NOT NULL,
            CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
        );
    END IF;

    INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260717214619_InitialBaseline', '10.0.8');
END $$;

COMMIT;

-- Verification queries to run immediately after registration:
--
-- SELECT * FROM public."__EFMigrationsHistory" ORDER BY "MigrationId";
--
-- SELECT 'Users' AS table_name, count(*) FROM public."Users"
-- UNION ALL SELECT 'Clients', count(*) FROM public."Clients"
-- UNION ALL SELECT 'Gigs', count(*) FROM public."Gigs"
-- UNION ALL SELECT 'Invoices', count(*) FROM public."Invoices"
-- UNION ALL SELECT 'InvoiceLines', count(*) FROM public."InvoiceLines"
-- UNION ALL SELECT 'SellerProfiles', count(*) FROM public."SellerProfiles";
