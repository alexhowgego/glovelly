# Glovelly

[![Glovelly CI/CD](https://github.com/alexhowgego/glovelly/actions/workflows/main.yml/badge.svg)](https://github.com/alexhowgego/glovelly/actions/workflows/main.yml)
[![Glovelly UAT](https://github.com/alexhowgego/glovelly/actions/workflows/uat.yml/badge.svg)](https://github.com/alexhowgego/glovelly/actions/workflows/uat.yml)

Glovelly is a personal business platform for managing my self-employed music work.

## Public URLs

- Production site: [https://glovelly.net](https://glovelly.net)
- Staging site: [https://staging.glovelly.net](https://staging.glovelly.net)
- Docs site: [https://docs.glovelly.net](https://docs.glovelly.net)

## Handbook

The repo-owned Glovelly Handbook starts at [docs/index.md](docs/index.md). Use the [UAT and regression section](docs/uat/index.md) for manual pre-merge testing journeys.

The handbook can also be built as a local DocFX site:

```bash
dotnet tool restore
dotnet tool run docfx docs/docfx.json --serve
```

Changes pushed to `main` are published to GitHub Pages at `https://docs.glovelly.net`.

### MCP contract snapshots and generated docs

The MCP tool catalog is treated as a public integration contract. Intentional tool contract changes should update both the test snapshot and generated public docs in the same pull request.

When you intentionally change MCP tool names, descriptions, safety metadata, input schemas, or output schemas, run:

```bash
UPDATE_MCP_SNAPSHOT=1 UPDATE_MCP_DOCS=1 dotnet test --project backend/Glovelly.Api.Tests/Glovelly.Api.Tests.csproj -- --filter-class '*McpToolCatalog*'
```

This refreshes:

- `backend/Glovelly.Api.Tests/Contracts/mcp-tools.snapshot.json`: the checked-in test contract snapshot.
- `docs/mcp-tools.md`: human-readable MCP tool documentation.
- `docs/mcp-tools.json`: machine-readable MCP capability manifest.

After regenerating, review the diff carefully. These files should only change when the MCP public contract has intentionally changed. Then run the MCP-focused tests:

```bash
dotnet test --project backend/Glovelly.Api.Tests/Glovelly.Api.Tests.csproj -- --filter-class '*Mcp*'
```

## Current Scope (v1)
- Client and gig tracking
- Gig expenses and receipt attachments
- Invoice generation, issue, reissue, PDF download, email delivery, and Google Drive publishing
- Seller profile, personal invoice/default settings, and connected-service management
- Google Sheets set list import from linked gig attachments, with review, audit, and re-import history
- Google Calendar sync for confirmed and completed gigs
- Admin access management and access request workflow
- MCP read-only business tools and staged imported-gig review

## Tech Stack
- Frontend: React + Vite
- Backend: ASP.NET Core Web API
- Database: PostgreSQL in deployed environments; EF in-memory fallback for local development/tests when no connection string is configured
- Background work: `Glovelly.Worker` run via Cloud Run Jobs and Cloud Scheduler

## Local Development

Run both the frontend and backend together from the repo root:

```bash
./run-dev.sh
```

This starts:
- Frontend: `http://localhost:5173`
- Backend API: `http://localhost:5153`
- Swagger UI: `http://localhost:5153/swagger`

If you want the in-memory local development database to auto-seed your own Glovelly admin user, create a git-ignored local config file from the example:

```bash
cp .glovelly.dev.local.example .glovelly.dev.local
```

Then set at least your Google subject claim in `.glovelly.dev.local`:

```bash
export DevelopmentSeeding__AdminGoogleSubject="your-google-subject-claim"
export DevelopmentSeeding__AdminEmail="you@example.com"
export DevelopmentSeeding__AdminDisplayName="Your Name"
export Email__Mode="Resend"
export Email__AccessRequests__FromAddress="access@example.com"
export Email__AccessRequests__FromDisplayName="Glovelly Access"
export Email__Invoices__FromAddress="invoices@example.com"
export Email__Invoices__FromDisplayName="Glovelly Invoices"
```

`run-dev.sh` sources this file automatically before starting the backend. The admin user is only seeded when no `ConnectionStrings:Glovelly` value is configured and Glovelly is using the in-memory development database.

Keep secrets such as `Email__Resend__ApiKey` and `Mileage__GoogleRoutes__ApiKey` out of `.glovelly.dev.local`. Store those with `dotnet user-secrets` instead.

#### Finding your Google subject claim locally

If you do not already know your Google subject claim (`sub`), Glovelly exposes a development-only helper endpoint:

```text
http://localhost:5153/auth/debug/google-claims
```

Open that URL locally, sign in with the Google account you want to enrol, and Glovelly will return the raw Google claims as JSON before local user matching runs. Copy the `sub` value into `.glovelly.dev.local` as `DevelopmentSeeding__AdminGoogleSubject`.

There is also a second development-only endpoint at `http://localhost:5153/auth/debug/claims` which shows the claims on the current Glovelly application session after a user has been successfully matched and signed in.

### Google OIDC setup

Glovelly now requires Google sign-in before the SPA can call the API.

1. Create a Google OAuth client in Google Cloud as a `Web application`.
2. Add these authorised redirect URIs:
   - `http://localhost:5153/signin-oidc`
   - `https://localhost:7087/signin-oidc`
   - Your production callback, for example `https://your-domain/signin-oidc`
3. For local development, store the Google credentials with `dotnet user-secrets` from [backend/Glovelly.Api/Glovelly.Api.csproj](backend/Glovelly.Api/Glovelly.Api.csproj):

```bash
cd backend/Glovelly.Api
dotnet user-secrets set "Authentication:Google:ClientId" "your-client-id"
dotnet user-secrets set "Authentication:Google:ClientSecret" "your-client-secret"
dotnet user-secrets set "ConnectionStrings:Glovelly" "your-postgresql-connection-string"
dotnet user-secrets set "Email:Resend:ApiKey" "your-resend-api-key"
dotnet user-secrets set "Mileage:GoogleRoutes:ApiKey" "your-google-routes-api-key"
```

4. To make outbound email work locally with Resend, also add these non-secret values to `.glovelly.dev.local`:

```bash
export Email__Mode="Resend"
export Email__AccessRequests__FromAddress="access@example.com"
export Email__AccessRequests__FromDisplayName="Glovelly Access"
export Email__Invoices__FromAddress="invoices@example.com"
export Email__Invoices__FromDisplayName="Glovelly Invoices"
```

5. As an alternative, you can provide the same values via environment variables:
   - `Authentication__Google__ClientId`
   - `Authentication__Google__ClientSecret`
   - `ConnectionStrings__Glovelly`
   - `Email__Resend__ApiKey`
   - `Mileage__GoogleRoutes__ApiKey`
   - `Email__Mode`
   - `Email__AccessRequests__FromAddress`
   - `Email__AccessRequests__FromDisplayName`
   - `Email__Invoices__FromAddress`
   - `Email__Invoices__FromDisplayName`
   - `SetListChartRanking__Provider`
   - `SetListChartRanking__VertexAiProjectId`
   - `SetListChartRanking__VertexAiLocation`
   - `SetListChartRanking__VertexAiModel`
   - `ReceiptAnalysis__Enabled`
   - `ReceiptAnalysis__VertexAiProjectId`
   - `ReceiptAnalysis__VertexAiLocation`
   - `ReceiptAnalysis__VertexAiModel`

The frontend signs users in through `/auth/login`, the backend completes the Google OpenID Connect flow, and the app stores the session in a secure cookie before allowing access to `/clients`, `/gigs`, `/invoices`, and `/invoice-lines`.

### Vertex AI set list chart ranking

Set list chart matching uses deterministic ranking by default. To enable Gemini-backed contextual ranking locally, authenticate with Google Application Default Credentials and set the ranking provider values:

```bash
gcloud auth application-default login
cd backend/Glovelly.Api
dotnet user-secrets set "SetListChartRanking:Provider" "VertexAi"
dotnet user-secrets set "SetListChartRanking:VertexAiProjectId" "your-gcp-project-id"
dotnet user-secrets set "SetListChartRanking:VertexAiLocation" "eu"
dotnet user-secrets set "SetListChartRanking:VertexAiModel" "gemini-3.1-flash-lite"
```

The configured principal must be able to call Vertex AI, for example with `roles/aiplatform.user`. If any required Vertex AI setting is missing or `Provider` remains `Deterministic`, Glovelly uses the deterministic fallback and does not call Gemini.

### Vertex AI receipt analysis

Receipt analysis is disabled unless `ReceiptAnalysis:Enabled` is `true` and its Vertex project, location, and model are configured. The Cloud Run service account needs `roles/aiplatform.user`. Receipt analysis defaults to Gemini 3.5 Flash, while set-list chart matching remains on Gemini 3.1 Flash Lite. A user explicitly starts analysis from a stored expense receipt; Glovelly sends supported receipt media up to the configured analysis limit to Vertex AI and presents validated suggestions for review. Analysis does not automatically change an expense.

To enable it locally, authenticate with Application Default Credentials and configure the receipt-analysis options with user secrets:

```bash
gcloud auth application-default login
cd backend/Glovelly.Api
dotnet user-secrets set "ReceiptAnalysis:Enabled" "true"
dotnet user-secrets set "ReceiptAnalysis:VertexAiProjectId" "your-gcp-project-id"
dotnet user-secrets set "ReceiptAnalysis:VertexAiLocation" "eu"
dotnet user-secrets set "ReceiptAnalysis:VertexAiModel" "gemini-3.1-flash-lite"
```

Optional limits can be configured with `ReceiptAnalysis:MaxFileSizeBytes`, `ReceiptAnalysis:Timeout`, `ReceiptAnalysis:PerUserPermitLimit`, and `ReceiptAnalysis:PerUserWindow`.

Do not enable provider request/response logging for receipt analysis. Operational logs contain only identifiers, configured model, outcome, elapsed time, and failure classification. Monitor Vertex usage and cost from the Google Cloud billing console before widening access or enabling automatic/background analysis.

### Google Routes mileage estimates

Glovelly can estimate gig mileage from the seller profile postcode to the gig location using Google Routes API. The API key is a secret and should be stored with `dotnet user-secrets` locally:

```bash
cd backend/Glovelly.Api
dotnet user-secrets set "Mileage:GoogleRoutes:ApiKey" "your-google-routes-api-key"
```

Non-secret local defaults are shown in `appsettings.Development.json`. In deployed environments, provide the key through Secret Manager or an equivalent secret-backed environment variable named `Mileage__GoogleRoutes__ApiKey`.

### Google Drive, Sheets and Calendar services

Authenticated users can connect optional Google services from the profile menu under `Services`:

- Google Drive requests `https://www.googleapis.com/auth/drive.file` and is used for publishing invoice PDFs.
- Google Sheets requests `https://www.googleapis.com/auth/spreadsheets.readonly` and is used for importing gig set lists from linked Google Sheet attachments.
- Google Calendar uses the app-created/calendar scopes documented in the handbook and syncs confirmed/completed gigs.

Drive and Sheets are separate OAuth journeys even though they reuse the same encrypted `GoogleConnection` token record internally. This keeps the more sensitive Sheets readonly scope out of the Drive publishing flow and makes local/staging service enablement easier to reason about. Google may require HTTPS-only OAuth clients for the Sheets readonly scope; local HTTP development can still use Drive without requesting Sheets.

Press `Ctrl+C` to stop both services.

## Docker

The repository includes a single multi-stage [Dockerfile](Dockerfile) that builds both services into one container image.

The container build:
- Builds the React frontend with Vite
- Publishes the ASP.NET Core backend and worker
- Copies the frontend build output into the backend `wwwroot`
- Serves both the API and frontend from the same ASP.NET Core process
- Includes the worker under `/app/worker` for Cloud Run Job command overrides

Build the image from the repo root:

```bash
docker build -t glovelly .
```

Run the container locally:

```bash
docker run --rm -p 8080:8080 glovelly
```

The app will be available at `http://localhost:8080`, with the frontend served from `/` and the API endpoints served from the same origin.

## CI

GitHub Actions runs the `Glovelly CI/CD` workflow on pushes to `main`, on pull requests, and on manual dispatch.

The workflow:
- Builds the React frontend, ASP.NET Core backend, and worker into a single Docker image
- Pushes the image to Google Artifact Registry for main and internal pull request runs
- Tags images with `latest` on the default branch and with a commit SHA tag for each build
- Injects Google Secret Manager secrets into Cloud Run, including `Authentication__Google__ClientId`, `Authentication__Google__ClientSecret`, and `ConnectionStrings__Glovelly`
- Injects the Resend API key into Cloud Run as `Email__Resend__ApiKey`
- Passes optional set list chart ranking variables for Vertex AI/Gemini; environments remain deterministic unless `SET_LIST_CHART_RANKING_PROVIDER` is set to `VertexAi`
- Deploys `main` to the `glovelly` Cloud Run service
- Deploys each internal pull request to the shared `glovelly-staging` Cloud Run service and comments the preview URL on the PR
- Deploys the Calendar sync and Business lifecycle Cloud Run Jobs and Cloud Scheduler triggers for eligible environments

The image is published to the Google Artifact Registry image configured in `.github/workflows/main.yml`.

You can view workflow runs from the [Actions tab](https://github.com/alexhowgego/glovelly/actions/workflows/main.yml).

The handbook has separate GitHub Actions workflows:

- `Handbook validation` builds the DocFX site for pull requests that touch `docs/**`.
- `Publish handbook` builds the site from `main` and deploys `docs/_site` to GitHub Pages.
