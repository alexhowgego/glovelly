# Deployment Pipeline

Glovelly deploys as a single container image to Google Cloud Run. GitHub Actions builds, tests, publishes, and deploys the image.

## Container Build

The root `Dockerfile` uses a multi-stage build:

1. Build the Vite frontend with Node.
2. Restore and publish the ASP.NET Core backend and worker with the .NET SDK.
3. Copy the frontend `dist` output into the backend publish output under `wwwroot`.
4. Run the final image on the ASP.NET Core runtime image.

The final image exposes port `8080` and starts `Glovelly.Api.dll`, respecting Cloud Run's `PORT` environment variable. It also includes `Glovelly.Worker.dll` under `/app/worker` so Cloud Run Jobs can run non-interactive commands from the same image.

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

The CI deployment creates or updates a Cloud Run Job for this command and a Cloud Scheduler HTTP trigger that invokes the job. The job uses the same image, runtime service account, environment variables, and Secret Manager bindings as the Cloud Run service.

The default job name is `<cloud-run-service>-calendar-sync`; the default scheduler name is `<job-name>-schedule`; the default schedule is every five minutes. These can be overridden per GitHub Environment with `GCP_CALENDAR_SYNC_JOB_NAME`, `GCP_CALENDAR_SYNC_SCHEDULER_NAME`, `CALENDAR_SYNC_SCHEDULE`, and `GCP_SCHEDULER_LOCATION` variables.

## GitHub Actions

The main workflow is `.github/workflows/main.yml`.

It currently:

1. checks out the repository
2. sets up .NET
3. restores dependencies
4. runs backend tests with `dotnet test glovelly.sln --no-restore -m:1`
5. authenticates to Google Cloud through Workload Identity Federation
6. sets up Docker Buildx
7. builds and optionally pushes the image
8. pushes images to Artifact Registry
9. deploys eligible builds to Cloud Run
10. deploys the Calendar sync Cloud Run Job and Scheduler trigger
11. comments a staging preview URL on same-repository pull requests

Workload Identity Federation is preferred because it avoids storing long-lived Google service account JSON keys in GitHub.

## Environments

GitHub Environments are used for environment-specific deployment metadata and visibility. Pull requests use `staging`; main-branch deployments use `production`.

Important distinction:

- GitHub environment variables/secrets are for CI/CD deployment-time configuration.
- Google Secret Manager and Cloud Run configuration are the source for runtime secrets and production application configuration where possible.

Avoid duplicating secret ownership unnecessarily. Runtime secrets should generally live with the runtime platform.

## Runtime Configuration

Cloud Run receives non-secret environment variables for values such as deployment name, bucket names, email mode/display names, MCP issuer/resource URLs, and client display/scopes.

The app uses SignalR for authenticated workspace invalidation events at `/workspace-events`. Cloud Run supports these WebSocket connections, but deployments should use a timeout long enough for active browser sessions and reconnects. While the default in-memory SignalR hub has no distributed backplane, keep the service on a single instance or treat events as best-effort with the frontend focus/visibility refresh as a fallback.

Secrets are bound from Secret Manager for values such as:

- Google OAuth client ID and client secret
- database connection string
- Resend API key
- email sender addresses
- MCP OAuth client ID and client secret
- MCP OAuth redirect URI values

Do not commit secret values, OAuth client secrets, Resend API keys, database connection strings, or user-specific credential material.

## GCP Resources

The deployment depends on:

- Cloud Run service hosting the app
- Cloud Run Job that drains the Calendar sync queue
- Cloud Scheduler trigger that invokes the Calendar sync job
- Artifact Registry repository for the container image
- Workload Identity Federation pool/provider for GitHub Actions
- Google service accounts for deployment and runtime
- Google Secret Manager secrets for production runtime values
- Google Cloud Storage bucket for blob-backed features where configured
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
