# Glovelly Agent Guide

This file is the first stop for coding agents. Keep it compact and update it when repo shape or core workflows change.

## Purpose

Glovelly is a personal business platform for self-employed music work. The current product covers authenticated access, clients, gigs, gig expenses and receipt attachments, invoice generation, invoice issue/reissue/delivery, seller profile setup, Google Drive invoice publishing, email delivery, admin user management, and a small MCP read-only business data surface.

## Stack

- Backend: ASP.NET Core minimal API, .NET 10, Entity Framework Core, PostgreSQL in production, in-memory database for local development and tests when no connection string is configured.
- Frontend: React 19, TypeScript, Vite.
- Tests: xUnit integration-style API tests using `WebApplicationFactory` and EF in-memory.
- Deployment: one Docker image containing the Vite build copied into ASP.NET Core `wwwroot`, deployed by GitHub Actions to Cloud Run.

## Fast Commands

Run from the repo root unless noted.

```bash
dotnet test glovelly.sln -m:1
npm --prefix frontend/glovelly-web run lint
npm --prefix frontend/glovelly-web run build
./verify.sh
./run-dev.sh
```

Use `./verify.sh` before handing over broad code changes. For backend-only changes, `dotnet test glovelly.sln -m:1` is usually the best first check.

## Repo Map

- `backend/Glovelly.Api/Program.cs`: service setup, HTTP pipeline, endpoint registration.
- `backend/Glovelly.Api/Configuration/`: startup, auth, infrastructure, database initialization, Swagger/static file pipeline.
- `backend/Glovelly.Api/Auth/`: current user access, app policies, Google OIDC claim handling, development MCP auth handler.
- `backend/Glovelly.Api/Data/`: EF `AppDbContext` and development seeding.
- `backend/Glovelly.Api/Models/`: EF/domain models used directly by minimal APIs.
- `backend/Glovelly.Api/Endpoints/`: minimal API route groups and request validation helpers.
- `backend/Glovelly.Api/Services/`: workflow services, delivery channels, email, Google Drive, storage, MCP query service.
- `backend/Glovelly.Api.Tests/`: API tests. `Infrastructure/GlovellyApiFactory.cs` configures test auth, seeded data, fake email, and in-memory storage.
- `frontend/glovelly-web/src/App.tsx`: top-level app orchestration, session state, data loading, cross-workspace coordination.
- `frontend/glovelly-web/src/hooks/`: stateful workspace logic for clients, gigs, invoices, admin, user settings, seller profile, quick receipts.
- `frontend/glovelly-web/src/components/`: presentational sections and modals.
- `frontend/glovelly-web/src/api.ts`: fetch helpers, session expiry, problem details parsing.
- `frontend/glovelly-web/src/types.ts`: frontend API/domain types.
- `docs/`: handbook landing page, UAT journeys, domain, roadmap, and agent-oriented architecture/convention/testing notes.

## Backend Conventions

- Endpoints are grouped in extension methods. `Program.cs` maps auth/access/MCP/admin and CRUD groups.
- CRUD groups are registered via `MapCrudEndpoints`, then delegated to files such as `GigEndpoints.cs` and `InvoiceEndpoints.cs`.
- Use `EndpointSupport` for shared visibility filters, validation helpers, stamping helpers, invoice status transition rules, and gig expense normalization.
- Most user-owned data must be filtered with `WhereVisibleTo(userId)` or equivalent owner checks.
- Prefer workflow services for multi-step business behavior. Invoice creation, line generation, PDF generation, issue/reissue behavior, and delivery coordination live in `InvoiceWorkflowService` and related delivery services.
- Keep request validation close to endpoint behavior unless a reusable validation helper already exists in `EndpointSupport`.
- When adding endpoints, add focused tests in the matching `*EndpointsTests.cs` file or create a new test file for a new route group.

## Frontend Conventions

- Keep data/workflow state in hooks under `src/hooks`. Components should mostly render state and call hook-provided handlers.
- Use `fetchWithSession`, `buildApiUrl`, `parseProblemDetails`, and session-expiry helpers from `src/api.ts` for API calls.
- Update `src/types.ts` when backend response shapes change.
- `App.tsx` coordinates workspaces and cross-cutting state. Avoid adding large new workflow bodies there when a hook can own the behavior.
- Preserve the existing plain React/CSS style. There is no component library.

## Testing Notes

- Backend tests use fake authorization by default. `GlovellyApiFactory.WithConfiguration(...)` enables real auth/development-style configuration for auth-specific tests.
- Seeded test IDs live in `backend/Glovelly.Api.Tests/Infrastructure/TestData.cs`; seeded auth context lives in `TestAuthContext.cs`.
- Fake email assertions should use `GlovellyApiFactory.Emails`.
- Frontend has lint/build scripts but no dedicated frontend test runner yet.
- When a change affects a user journey or cross-workspace navigation, add or update the matching scenario under `docs/uat/`.

## High-Token Files

Read these selectively and search within them before opening large chunks:

- `frontend/glovelly-web/src/App.tsx`
- `backend/Glovelly.Api/Services/InvoiceWorkflowService.cs`
- `backend/Glovelly.Api/Endpoints/GigEndpoints.cs`
- `backend/Glovelly.Api/Endpoints/InvoiceEndpoints.cs`
- `frontend/glovelly-web/src/hooks/useGigsWorkspace.ts`
- `frontend/glovelly-web/src/hooks/useInvoicesWorkspace.ts`

## Useful Search Patterns

```bash
rg "Map.*Endpoints" backend/Glovelly.Api/Endpoints
rg "WhereVisibleTo|StampCreate|Validate" backend/Glovelly.Api/Endpoints
rg "fetchWithSession|parseProblemDetails" frontend/glovelly-web/src
rg "InvoiceWorkflowService|IInvoiceWorkflowService" backend/Glovelly.Api
rg "TestData\\.|TestAuthContext" backend/Glovelly.Api.Tests
```

## Avoid

- Do not commit secrets or edit `.glovelly.dev.local`.
- Do not bypass owner visibility checks for user data.
- Do not add frontend API calls using raw `fetch` unless there is a specific reason.
- Do not run destructive git commands unless the user explicitly asks.
- Do not refactor large files just to tidy them while solving a narrow task.
