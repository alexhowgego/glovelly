#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${script_dir}/lib/cloud-run-jobs.sh"

require_env GCP_PROJECT_ID
require_env GCP_REGION
require_env IMAGE_URI
require_env DEPLOYMENT_NAME
require_env DEPLOYMENT_URL
require_env GCP_BUCKET_NAME
require_env GCP_CONNECTION_STRING_SECRET_ID
require_env GCP_CLOUD_RUN_SERVICE

job_name="${BUSINESS_LIFECYCLE_JOB_NAME:-${GCP_CLOUD_RUN_SERVICE}-business-lifecycle}"
scheduler_name="${BUSINESS_LIFECYCLE_SCHEDULER_NAME:-${job_name}-schedule}"
scheduler_location="${GCP_SCHEDULER_LOCATION:-${GCP_REGION}}"
schedule="${BUSINESS_LIFECYCLE_SCHEDULE:-*/15 * * * *}"
time_zone="${BUSINESS_LIFECYCLE_TIME_ZONE:-Etc/UTC}"
task_timeout="${BUSINESS_LIFECYCLE_TASK_TIMEOUT:-300s}"
runtime_service_account="${BUSINESS_LIFECYCLE_SERVICE_ACCOUNT:-$(default_runtime_service_account)}"

env_vars="$(common_worker_env_vars)"
secrets="$(common_worker_secrets)"

deploy_cloud_run_job \
  "$job_name" \
  dotnet \
  "worker/Glovelly.Worker.dll,business-lifecycle,advance" \
  "$runtime_service_account" \
  "$task_timeout" \
  "$env_vars" \
  "$secrets"

upsert_scheduler_http_trigger \
  "$scheduler_name" \
  "$scheduler_location" \
  "$schedule" \
  "$time_zone" \
  "$job_name" \
  "$runtime_service_account"

printf 'Deployed Business lifecycle job %s and scheduler %s.\n' "$job_name" "$scheduler_name"
