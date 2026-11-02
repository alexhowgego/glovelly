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

job_name="${GCP_CLOUD_RUN_SERVICE}-intake-preparation"
scheduler_name="${job_name}-schedule"
runtime_service_account="$(default_runtime_service_account)"
env_vars="$(common_worker_env_vars),ReceiptAnalysis__Enabled=${RECEIPT_ANALYSIS_ENABLED:-false},ReceiptAnalysis__VertexAiProjectId=${RECEIPT_ANALYSIS_VERTEX_AI_PROJECT_ID:-$GCP_PROJECT_ID},ReceiptAnalysis__VertexAiLocation=${RECEIPT_ANALYSIS_VERTEX_AI_LOCATION:-eu},ReceiptAnalysis__VertexAiModel=${RECEIPT_ANALYSIS_VERTEX_AI_MODEL:-gemini-3.1-flash-lite}"

deploy_cloud_run_job "$job_name" dotnet "worker/Glovelly.Worker.dll,intake,drain" "$runtime_service_account" "300s" "$env_vars" "$(common_worker_secrets)"
upsert_scheduler_http_trigger "$scheduler_name" "${GCP_SCHEDULER_LOCATION:-$GCP_REGION}" '*/5 * * * *' 'Etc/UTC' "$job_name" "$runtime_service_account"
printf 'Deployed intake preparation job %s and scheduler %s.\n' "$job_name" "$scheduler_name"
