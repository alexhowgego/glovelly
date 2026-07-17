#!/usr/bin/env bash

require_env() {
  local name="$1"
  if [[ -z "${!name:-}" ]]; then
    printf 'Missing required environment variable: %s\n' "$name" >&2
    exit 1
  fi
}

default_runtime_service_account() {
  printf 'glovelly-runner@%s.iam.gserviceaccount.com' "$GCP_PROJECT_ID"
}

append_csv_value() {
  local current="$1"
  local value="$2"

  if [[ -z "$current" ]]; then
    printf '%s' "$value"
  else
    printf '%s,%s' "$current" "$value"
  fi
}

common_worker_env_vars() {
  printf 'App__DeploymentName=%s,BlobStorage__BucketName=%s,Email__Mode=Resend,Email__AccessRequests__FromDisplayName=Glovelly,Email__Invoices__FromDisplayName=Glovelly,ExpenseAttachments__BucketName=%s,Mcp__OAuth__Issuer=%s,Mcp__OAuth__Resource=%s/mcp,Mcp__OAuth__Clients__0__DisplayName=ChatGPT,Mcp__OAuth__Clients__0__Scopes__0=mcp:read' \
    "$DEPLOYMENT_NAME" \
    "$GCP_BUCKET_NAME" \
    "$GCP_BUCKET_NAME" \
    "$DEPLOYMENT_URL" \
    "$DEPLOYMENT_URL"
}

common_worker_secrets() {
  local secrets="Authentication__Google__ClientId=google-client-id:latest,Authentication__Google__ClientSecret=google-client-secret:latest,ConnectionStrings__Glovelly=${GCP_CONNECTION_STRING_SECRET_ID}:latest,Email__Resend__ApiKey=glovelly-resend-api-key:latest,Email__AccessRequests__FromAddress=glovelly-access-requests-from-address:latest,Email__Invoices__FromAddress=glovelly-invoices-from-address:latest,Mileage__GoogleRoutes__ApiKey=glovelly-routes-api-key:latest,Mcp__OAuth__Clients__0__ClientId=chatgpt-oauth-client-id:latest,Mcp__OAuth__Clients__0__ClientSecret=chatgpt-oauth-client-secret:latest"

  if [[ -n "${GCP_CHATGPT_REDIRECT_SECRET_ID:-}" ]]; then
    secrets="$(append_csv_value "$secrets" "Mcp__OAuth__Clients__0__RedirectUris__0=${GCP_CHATGPT_REDIRECT_SECRET_ID}:latest")"
  fi
  if [[ -n "${GCP_UAT_SECRET_ID:-}" ]]; then
    secrets="$(append_csv_value "$secrets" "GLOVELLY_UAT_SECRET=${GCP_UAT_SECRET_ID}:latest")"
  fi

  printf '%s' "$secrets"
}

deploy_cloud_run_job() {
  local job_name="$1"
  local command="$2"
  local args="$3"
  local service_account="$4"
  local task_timeout="$5"
  local env_vars="$6"
  local secrets="$7"
  shift 7

  local gcloud_args=(
    run jobs deploy "$job_name"
    --project "$GCP_PROJECT_ID"
    --region "$GCP_REGION"
    --image "$IMAGE_URI"
    --command "$command"
    --args "$args"
    --service-account "$service_account"
    --max-retries 0
    --task-timeout "$task_timeout"
  )

  if [[ -n "$env_vars" ]]; then
    gcloud_args+=(--set-env-vars "$env_vars")
  fi
  if [[ -n "$secrets" ]]; then
    gcloud_args+=(--set-secrets "$secrets")
  fi

  gcloud_args+=("$@")

  gcloud "${gcloud_args[@]}"
}

upsert_scheduler_http_trigger() {
  local scheduler_name="$1"
  local scheduler_location="$2"
  local schedule="$3"
  local time_zone="$4"
  local job_name="$5"
  local oauth_service_account="$6"

  local scheduler_uri="https://${GCP_REGION}-run.googleapis.com/apis/run.googleapis.com/v1/namespaces/${GCP_PROJECT_ID}/jobs/${job_name}:run"

  if gcloud scheduler jobs describe "$scheduler_name" --project "$GCP_PROJECT_ID" --location "$scheduler_location" >/dev/null 2>&1; then
    gcloud scheduler jobs update http "$scheduler_name" \
      --project "$GCP_PROJECT_ID" \
      --location "$scheduler_location" \
      --schedule "$schedule" \
      --time-zone "$time_zone" \
      --uri "$scheduler_uri" \
      --http-method POST \
      --oauth-service-account-email "$oauth_service_account" \
      --oauth-token-scope "https://www.googleapis.com/auth/cloud-platform"
  else
    gcloud scheduler jobs create http "$scheduler_name" \
      --project "$GCP_PROJECT_ID" \
      --location "$scheduler_location" \
      --schedule "$schedule" \
      --time-zone "$time_zone" \
      --uri "$scheduler_uri" \
      --http-method POST \
      --oauth-service-account-email "$oauth_service_account" \
      --oauth-token-scope "https://www.googleapis.com/auth/cloud-platform"
  fi
}

print_recent_job_logs() {
  local job_name="$1"

  printf 'Recent Cloud Run Job logs for %s:\n' "$job_name" >&2
  gcloud logging read \
    "resource.type=cloud_run_job AND resource.labels.job_name=\"${job_name}\"" \
    --project "$GCP_PROJECT_ID" \
    --limit 100 \
    --format 'value(timestamp,severity,textPayload,jsonPayload.message)' >&2 || true
}

execute_cloud_run_job() {
  local job_name="$1"

  gcloud run jobs execute "$job_name" \
    --project "$GCP_PROJECT_ID" \
    --region "$GCP_REGION" \
    --wait
}
