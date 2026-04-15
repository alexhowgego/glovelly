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
```

4. As an alternative, you can provide the same values via environment variables:
   - `Authentication__Google__ClientId`
   - `Authentication__Google__ClientSecret`

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
- Pushes the image to Google Artifact Registry on non-PR runs
- Tags images with `latest` on the default branch and with a commit SHA tag for each build

The image is published to `europe-west1-docker.pkg.dev/glovelly-dev/glovelly/glovelly`.

You can view workflow runs from the [Actions tab](https://github.com/alexhowgego/glovelly/actions/workflows/main.yml).
