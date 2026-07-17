# Deployment Pipeline

Glovelly deploys as a single container image to Google Cloud Run. GitHub Actions builds, tests, publishes, and deploys the image.

## Container Build

The root `Dockerfile` uses a multi-stage build:

1. Build the Vite frontend with Node.
2. Restore and publish the ASP.NET Core backend and worker with the .NET SDK.
3. Copy the frontend `dist` output into the backend publish output under `wwwroot`.
4. Build a self-contained Linux EF Core migration bundle from `backend/Glovelly.Migrations` and copy it into the final image as `/app/efbundle`.
5. Run the final image on the ASP.NET Core runtime image.

The final image exposes port `8080` and starts `Glovelly.Api.dll`, respecting Cloud Run's `PORT` environment variable. It also includes `Glovelly.Worker.dll` under `/app/worker` so Cloud Run Jobs can run non-interactive commands from the same image, and `/app/efbundle` so database migrations can run from the same immutable artifact.

## Database Migrations

Database migrations run as an explicit Cloud Run Job, not during web application startup. The deployment job uses the same image URI that will be deployed to the Cloud Run service and binds `ConnectionStrings__Glovelly` from Secret Manager. The job runs `/app/efbundle` with the target connection string and fails the deployment if the bundle exits unsuccessfully.

The migration job is one-shot, single-task, non-retrying, and bounded by a task timeout. Re-running the same bundle is safe because EF applies only migrations missing from `__EFMigrationsHistory`.

The initial migration baseline is a special adoption step. `20260717214619_InitialBaseline` creates the full schema for fresh databases, but existing staging and production databases must be registered as already having applied it with the guarded SQL in `docs/engineering/register-initial-baseline.sql`. Do not run the baseline DDL against existing databases. After registration, normal deployments use the migration job and bundle path below.

The release order is:

1. Build and test the application image and migration bundle.
2. Execute pending migrations against staging.
3. Deploy the staging service revision.
4. Run staging UAT.
5. Pass the production environment approval/gate.
6. Execute the exact same migration artifact against production.
7. Deploy the production service revision.
8. Run production smoke tests.

If migration execution fails, the corresponding service revision is not deployed. The workflow prints recent Cloud Run Job logs to make the failure actionable.

## Worker Commands

Local Calendar sync queue draining can be run with:

```bash
dotnet run --project backend/Glovelly.Worker -- calendar-sync drain
```

Useful options are:

```bash
dotnet run --project backend/Glovelly.Worker -- calendar-sync drain --max-items 100 --max-duration-seconds 55
```

The deployed container can run the same command via the published worker DLL:

```bash
dotnet worker/Glovelly.Worker.dll calendar-sync drain
```

Business lifecycle advancement, including automatic gig completion and invoice overdue transitions, can be run with:

```bash
dotnet run --project backend/Glovelly.Worker -- business-lifecycle advance
```

The deployed container can run the same command via the published worker DLL:

```bash
dotnet worker/Glovelly.Worker.dll business-lifecycle advance
```

The CI deployment creates or updates Cloud Run Jobs for these commands and Cloud Scheduler HTTP triggers that invoke the jobs. Each job uses the same image, runtime service account, environment variables, and Secret Manager bindings as the Cloud Run service.

The default job name is `<cloud-run-service>-calendar-sync`; the default scheduler name is `<job-name>-schedule`; the default schedule is every five minutes. These can be overridden per GitHub Environment with `GCP_CALENDAR_SYNC_JOB_NAME`, `GCP_CALENDAR_SYNC_SCHEDULER_NAME`, `CALENDAR_SYNC_SCHEDULE`, and `GCP_SCHEDULER_LOCATION` variables.

The default Business lifecycle job name is `<cloud-run-service>-business-lifecycle`; the default scheduler name is `<job-name>-schedule`; the default schedule is every fifteen minutes. These can be overridden per GitHub Environment with `GCP_BUSINESS_LIFECYCLE_JOB_NAME`, `GCP_BUSINESS_LIFECYCLE_SCHEDULER_NAME`, `BUSINESS_LIFECYCLE_SCHEDULE`, and `GCP_SCHEDULER_LOCATION` variables.

## GitHub Actions

The main workflow is `.github/workflows/main.yml`.

It currently:

1. checks out the repository
2. sets up .NET
3. restores dependencies
4. validates EF migrations against a disposable PostgreSQL database once a baseline snapshot exists
5. runs backend tests with `dotnet test glovelly.sln --no-restore -m:1`
6. authenticates to Google Cloud through Workload Identity Federation
7. sets up Docker Buildx
8. builds and optionally pushes the image
9. pushes images to Artifact Registry
10. runs the database migration job before eligible Cloud Run service deployments
11. deploys eligible builds to Cloud Run
12. deploys the Calendar sync and Business lifecycle Cloud Run Jobs and Scheduler triggers
13. comments a staging preview URL on same-repository pull requests

Workload Identity Federation is preferred because it avoids storing long-lived Google service account JSON keys in GitHub.

## Environments

GitHub Environments are used for environment-specific deployment metadata and visibility. Pull requests use `staging`; main-branch deployments use `production`.

Important distinction:

- GitHub environment variables/secrets are for CI/CD deployment-time configuration.
- Google Secret Manager and Cloud Run configuration are the source for runtime secrets and production application configuration where possible.

Avoid duplicating secret ownership unnecessarily. Runtime secrets should generally live with the runtime platform.

## Runtime Configuration

Cloud Run receives non-secret environment variables for values such as deployment name, bucket names, email mode/display names, MCP issuer/resource URLs, client display/scopes, and optional set list chart ranking provider settings.

The app uses SignalR for authenticated workspace invalidation events at `/workspace-events`. Cloud Run supports these WebSocket connections, but deployments should use a timeout long enough for active browser sessions and reconnects. While the default in-memory SignalR hub has no distributed backplane, keep the service on a single instance or treat events as best-effort with the frontend focus/visibility refresh as a fallback.

Secrets are bound from Secret Manager for values such as:

- Google OAuth client ID and client secret
- database connection string
- Resend API key
- email sender addresses
- MCP OAuth client ID and client secret
- MCP OAuth redirect URI values

Set list chart matching defaults to deterministic ranking. To enable Gemini-backed ranking in a GitHub Environment, set `SET_LIST_CHART_RANKING_PROVIDER` to `VertexAi`. Optional override variables are `SET_LIST_CHART_RANKING_VERTEX_AI_PROJECT_ID`, `SET_LIST_CHART_RANKING_VERTEX_AI_LOCATION`, and `SET_LIST_CHART_RANKING_VERTEX_AI_MODEL`; the workflow otherwise uses the deployment project, deployment region, and `gemini-2.5-flash`. The Cloud Run runtime service account must have permission to call Vertex AI, for example `roles/aiplatform.user`.

Do not commit secret values, OAuth client secrets, Resend API keys, database connection strings, or user-specific credential material.

## GCP Resources

The deployment depends on:

- Cloud Run service hosting the app
- Cloud Run Job that executes the EF migration bundle before service deployment
- Cloud Run Job that drains the Calendar sync queue
- Cloud Scheduler trigger that invokes the Calendar sync job
- Artifact Registry repository for the container image
- Workload Identity Federation pool/provider for GitHub Actions
- Google service accounts for deployment and runtime
- Google Secret Manager secrets for production runtime values
- Google Cloud Storage bucket for blob-backed features where configured
- Vertex AI API and runtime service account permissions when Gemini-backed chart ranking is enabled
- custom domain mapping for `glovelly.net`
- custom domain mapping for `staging.glovelly.net`
- GitHub Pages custom domain mapping for `docs.glovelly.net`

## Pull Requests

Pull requests from the same repository can build, push, deploy to the staging service, and receive a staging preview comment. External fork pull requests do not receive the same privileged GCP authentication path.

## Handbook Publishing

The Glovelly Handbook is built with DocFX from the Markdown files under `docs/`.

- `.github/workflows/docs-pr.yml` validates the handbook build on pull requests that touch documentation.
- `.github/workflows/docs.yml` builds the handbook from `main` and publishes `docs/_site` to GitHub Pages.

The public handbook URL is `https://docs.glovelly.net`.
