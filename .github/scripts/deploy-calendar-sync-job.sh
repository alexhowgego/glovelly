#!/usr/bin/env bash
set -euo pipefail

require_env() {
  local name="$1"
  if [[ -z "${!name:-}" ]]; then
    printf 'Missing required environment variable: %s\n' "$name" >&2
    exit 1
  fi
}

require_env GCP_PROJECT_ID
require_env GCP_REGION
require_env IMAGE_URI
require_env DEPLOYMENT_NAME
require_env DEPLOYMENT_URL
require_env GCP_BUCKET_NAME
require_env GCP_CONNECTION_STRING_SECRET_ID
require_env GCP_CLOUD_RUN_SERVICE

job_name="${CALENDAR_SYNC_JOB_NAME:-${GCP_CLOUD_RUN_SERVICE}-calendar-sync}"
scheduler_name="${CALENDAR_SYNC_SCHEDULER_NAME:-${job_name}-schedule}"
scheduler_location="${GCP_SCHEDULER_LOCATION:-${GCP_REGION}}"
schedule="${CALENDAR_SYNC_SCHEDULE:-*/5 * * * *}"
time_zone="${CALENDAR_SYNC_TIME_ZONE:-Etc/UTC}"
max_items="${CALENDAR_SYNC_MAX_ITEMS:-100}"
max_duration_seconds="${CALENDAR_SYNC_MAX_DURATION_SECONDS:-55}"
task_timeout="${CALENDAR_SYNC_TASK_TIMEOUT:-300s}"
runtime_service_account="${CALENDAR_SYNC_SERVICE_ACCOUNT:-glovelly-runner@${GCP_PROJECT_ID}.iam.gserviceaccount.com}"

env_vars="App__DeploymentName=${DEPLOYMENT_NAME},BlobStorage__BucketName=${GCP_BUCKET_NAME},Email__Mode=Resend,Email__AccessRequests__FromDisplayName=Glovelly,Email__Invoices__FromDisplayName=Glovelly,ExpenseAttachments__BucketName=${GCP_BUCKET_NAME},Mcp__OAuth__Issuer=${DEPLOYMENT_URL},Mcp__OAuth__Resource=${DEPLOYMENT_URL}/mcp,Mcp__OAuth__Clients__0__DisplayName=ChatGPT,Mcp__OAuth__Clients__0__Scopes__0=mcp:read"
secrets="Authentication__Google__ClientId=google-client-id:latest,Authentication__Google__ClientSecret=google-client-secret:latest,ConnectionStrings__Glovelly=${GCP_CONNECTION_STRING_SECRET_ID}:latest,Email__Resend__ApiKey=glovelly-resend-api-key:latest,Email__AccessRequests__FromAddress=glovelly-access-requests-from-address:latest,Email__Invoices__FromAddress=glovelly-invoices-from-address:latest,Mileage__GoogleRoutes__ApiKey=glovelly-routes-api-key:latest,Mcp__OAuth__Clients__0__ClientId=chatgpt-oauth-client-id:latest,Mcp__OAuth__Clients__0__ClientSecret=chatgpt-oauth-client-secret:latest"
if [[ -n "${GCP_CHATGPT_REDIRECT_SECRET_ID:-}" ]]; then
  secrets="${secrets},Mcp__OAuth__Clients__0__RedirectUris__0=${GCP_CHATGPT_REDIRECT_SECRET_ID}:latest"
fi
if [[ -n "${GCP_UAT_SECRET_ID:-}" ]]; then
  secrets="${secrets},GLOVELLY_UAT_SECRET=${GCP_UAT_SECRET_ID}:latest"
fi

gcloud run jobs deploy "$job_name" \
  --project "$GCP_PROJECT_ID" \
  --region "$GCP_REGION" \
  --image "$IMAGE_URI" \
  --command dotnet \
  --args "worker/Glovelly.Worker.dll,calendar-sync,drain,--max-items,${max_items},--max-duration-seconds,${max_duration_seconds}" \
  --service-account "$runtime_service_account" \
  --set-env-vars "$env_vars" \
  --set-secrets "$secrets" \
  --max-retries 0 \
  --task-timeout "$task_timeout"

scheduler_uri="https://${GCP_REGION}-run.googleapis.com/apis/run.googleapis.com/v1/namespaces/${GCP_PROJECT_ID}/jobs/${job_name}:run"

if gcloud scheduler jobs describe "$scheduler_name" --project "$GCP_PROJECT_ID" --location "$scheduler_location" >/dev/null 2>&1; then
  gcloud scheduler jobs update http "$scheduler_name" \
    --project "$GCP_PROJECT_ID" \
    --location "$scheduler_location" \
    --schedule "$schedule" \
    --time-zone "$time_zone" \
    --uri "$scheduler_uri" \
    --http-method POST \
    --oauth-service-account-email "$runtime_service_account" \
    --oauth-token-scope "https://www.googleapis.com/auth/cloud-platform"
else
  gcloud scheduler jobs create http "$scheduler_name" \
    --project "$GCP_PROJECT_ID" \
    --location "$scheduler_location" \
    --schedule "$schedule" \
    --time-zone "$time_zone" \
    --uri "$scheduler_uri" \
    --http-method POST \
    --oauth-service-account-email "$runtime_service_account" \
    --oauth-token-scope "https://www.googleapis.com/auth/cloud-platform"
fi

printf 'Deployed Calendar sync job %s and scheduler %s.\n' "$job_name" "$scheduler_name"
