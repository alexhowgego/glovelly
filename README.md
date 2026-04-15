# Glovelly

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

