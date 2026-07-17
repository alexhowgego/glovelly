#!/usr/bin/env bash
set -euo pipefail

project="backend/Glovelly.Migrations/Glovelly.Migrations.csproj"
startup_project="$project"
context="AppDbContext"
snapshot_count=$(find backend/Glovelly.Migrations -name '*ModelSnapshot.cs' -print | wc -l | tr -d ' ')

dotnet tool restore

if [[ "$snapshot_count" == "0" ]]; then
  printf 'No EF model snapshot found in Glovelly.Migrations; skipping pending-model and PostgreSQL migration-chain validation until InitialBaseline is generated.\n'
  exit 0
fi

connection_string="${GLOVELLY_MIGRATIONS_VALIDATION_CONNECTION:-}"
if [[ -z "$connection_string" ]]; then
  printf 'Missing required environment variable: GLOVELLY_MIGRATIONS_VALIDATION_CONNECTION\n' >&2
  exit 1
fi

dotnet tool run dotnet-ef migrations has-pending-model-changes \
  --project "$project" \
  --startup-project "$startup_project" \
  --context "$context"

dotnet tool run dotnet-ef database update \
  --project "$project" \
  --startup-project "$startup_project" \
  --context "$context" \
  --connection "$connection_string"

dotnet tool run dotnet-ef database update \
  --project "$project" \
  --startup-project "$startup_project" \
  --context "$context" \
  --connection "$connection_string"

printf 'EF migration validation completed successfully.\n'
