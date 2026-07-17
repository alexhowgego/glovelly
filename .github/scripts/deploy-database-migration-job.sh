#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${script_dir}/lib/cloud-run-jobs.sh"

require_env GCP_PROJECT_ID
require_env GCP_REGION
require_env IMAGE_URI
require_env GCP_CONNECTION_STRING_SECRET_ID
require_env GCP_CLOUD_RUN_SERVICE

job_name="${DATABASE_MIGRATION_JOB_NAME:-${GCP_CLOUD_RUN_SERVICE}-database-migration}"
task_timeout="${DATABASE_MIGRATION_TASK_TIMEOUT:-600s}"
runtime_service_account="${DATABASE_MIGRATION_SERVICE_ACCOUNT:-$(default_runtime_service_account)}"
secrets="ConnectionStrings__Glovelly=${GCP_CONNECTION_STRING_SECRET_ID}:latest"

deploy_cloud_run_job \
  "$job_name" \
  sh \
  '-c,/app/efbundle --connection "$ConnectionStrings__Glovelly"' \
  "$runtime_service_account" \
  "$task_timeout" \
  "" \
  "$secrets" \
  --tasks 1 \
  --parallelism 1

printf 'Executing database migration job %s with image %s.\n' "$job_name" "$IMAGE_URI"
if ! execute_cloud_run_job "$job_name"; then
  print_recent_job_logs "$job_name"
  exit 1
fi

printf 'Database migration job %s completed successfully.\n' "$job_name"
