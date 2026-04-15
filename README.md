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
