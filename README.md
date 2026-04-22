# Glovelly

[![Build and Push Container](https://github.com/alexhowgego/glovelly/actions/workflows/main.yml/badge.svg)](https://github.com/alexhowgego/glovelly/actions/workflows/main.yml)

Glovelly is a personal business platform for managing my self-employed music work.

## Current Scope (v1)
- Gig tracking
- Invoice generation from gigs
- Basic invoice viewing

## First Milestone
Create a gig and generate an invoice draft from it.

## Tech Stack
- Frontend: React (PWA planned)
- Backend: ASP.NET Core Web API
- Database: PostgreSQL (SQLite for local dev initially)

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
```

`run-dev.sh` sources this file automatically before starting the backend. The admin user is only seeded when no `ConnectionStrings:Glovelly` value is configured and Glovelly is using the in-memory development database.

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
3. For local development, store the Google credentials with `dotnet user-secrets` from [backend/Glovelly.Api/Glovelly.Api.csproj](/Users/alexhowgego/dev/glovelly/backend/Glovelly.Api/Glovelly.Api.csproj:1):

```bash
cd backend/Glovelly.Api
dotnet user-secrets set "Authentication:Google:ClientId" "your-client-id"
dotnet user-secrets set "Authentication:Google:ClientSecret" "your-client-secret"
dotnet user-secrets set "ConnectionStrings:Glovelly" "your-postgresql-connection-string"
dotnet user-secrets set "Email:Resend:ApiKey" "your-resend-api-key"
```

4. As an alternative, you can provide the same values via environment variables:
   - `Authentication__Google__ClientId`
   - `Authentication__Google__ClientSecret`
   - `ConnectionStrings__Glovelly`
   - `Email__Resend__ApiKey`

The frontend signs users in through `/auth/login`, the backend completes the Google OpenID Connect flow, and the app stores the session in a secure cookie before allowing access to `/clients`, `/gigs`, `/invoices`, and `/invoice-lines`.

Press `Ctrl+C` to stop both services.

## Docker

The repository includes a single multi-stage [Dockerfile](/Users/alexhowgego/dev/glovelly/Dockerfile:1) that builds both services into one container image.

The container build:
- Builds the React frontend with Vite
- Publishes the ASP.NET Core backend
- Copies the frontend build output into the backend `wwwroot`
- Serves both the API and frontend from the same ASP.NET Core process

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

GitHub Actions runs the `Build and Push Container` workflow on pushes to `main`, on pull requests, and on manual dispatch.

The workflow:
- Builds the React frontend and ASP.NET Core backend into a single Docker image
- Pushes the image to Google Artifact Registry for main and internal pull request runs
- Tags images with `latest` on the default branch and with a commit SHA tag for each build
- Injects Google Secret Manager secrets into Cloud Run, including `Authentication__Google__ClientId`, `Authentication__Google__ClientSecret`, and `ConnectionStrings__Glovelly`
- Injects the Resend API key into Cloud Run as `Email__Resend__ApiKey`
- Deploys `main` to the `glovelly` Cloud Run service
- Deploys each internal pull request to the shared `glovelly-staging` Cloud Run service and comments the preview URL on the PR

The image is published to `europe-west1-docker.pkg.dev/glovelly-dev/glovelly/glovelly`.

You can view workflow runs from the [Actions tab](https://github.com/alexhowgego/glovelly/actions/workflows/main.yml).
