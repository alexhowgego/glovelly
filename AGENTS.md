# Glovelly Agent Guide

Compact, repo-specific context for future OpenCode sessions. Keep only facts an agent would otherwise likely miss.

## Shape

- Glovelly is a personal business platform for authenticated music-work admin: clients, gigs, expenses/receipts, invoices, seller profile, Google Drive/email delivery, admin users, and a small MCP surface.
- Backend: ASP.NET Core minimal API on .NET 10, EF Core, PostgreSQL when `ConnectionStrings:Glovelly` exists, EF in-memory otherwise. Shared package versions live in `Directory.Packages.props`; target framework/nullable/implicit usings live in `Directory.Build.props`.
- Frontend: React 19 + TypeScript + Vite in `frontend/glovelly-web`. `npm run build` is `tsc -b && vite build`.
- Deployment builds one Docker image: Vite `dist` is copied into ASP.NET Core `wwwroot`, then the API serves the SPA and API from one process. CI deploys same-repo PRs to shared Cloud Run staging and `main` to production.

## Commands

Run from repo root unless noted.

```bash
./run-dev.sh                                    # backend :5153 + Vite :5173; sources .glovelly.dev.local
dotnet test glovelly.sln -m:1                  # backend suite; keep -m:1 for shared in-memory/factory state
dotnet test glovelly.sln -m:1 --filter FullyQualifiedName~GigEndpointsTests
npm --prefix frontend/glovelly-web run lint
npm --prefix frontend/glovelly-web run build
./verify.sh                                    # dotnet test, frontend lint, frontend build
dotnet tool restore && dotnet tool run docfx docs/docfx.json
dotnet tool run docfx docs/docfx.json --serve  # local handbook
```

- Use `./verify.sh` before handing over broad changes. For backend-only changes, `dotnet test glovelly.sln -m:1` is the best first check.
- Frontend has lint/build checks only; no unit/e2e runner is configured.

## Backend Map

- `backend/Glovelly.Api/Program.cs` is the real entrypoint: startup settings, infrastructure/auth registration, DB init, HTTP pipeline, endpoint mapping, SPA fallback.
- `Configuration/StartupSettings.cs` chooses PostgreSQL vs in-memory by presence of `ConnectionStrings:Glovelly`; development seed data only runs for non-Postgres, non-testing startup.
- `Endpoints/CrudEndpoints.cs` maps protected `/clients`, `/gigs`, `/gig-imports`, `/invoices`, `/invoice-lines`, and `/seller-profile` groups. Auth/access/Google Drive/MCP/admin/expense statements are mapped separately in `Program.cs`.
- `Endpoints/EndpointSupport.cs` owns high-risk shared behavior: `WhereVisibleTo`, create/update stamping, gig validation, invoice status transition rules, filename/subject validation, and gig expense normalization.
- `Services/InvoiceWorkflowService.cs` and related invoice services own invoice creation, generated lines, PDFs, issue/reissue, delivery, and gig linkage. Do not duplicate that flow inside endpoints.
- `Services/GlovellyMcpQueryService.cs` performs user-scoped MCP EF projections; MCP tools should remain scoped by authenticated user visibility.

## Frontend Map

- `src/App.tsx` coordinates session, active section, initial data loads, modals, and cross-workspace actions. Avoid adding large workflow bodies there when a hook can own them.
- `src/hooks/` owns stateful workspace logic for clients, gigs, gig imports, invoices, admin, user settings, seller profile, and quick receipts.
- `src/components/` is presentational sections/modals. Preserve the current plain React/CSS approach; there is no component library.
- Use `buildApiUrl`, `fetchWithSession`, `parseProblemDetails`, and session-expiry helpers from `src/api.ts`; avoid raw `fetch` for authenticated API calls.
- Update `src/types.ts` whenever backend JSON shapes change.
- When adding a frontend-consumed API prefix, update both `frontend/glovelly-web/vite.config.ts` proxy and `frontend/glovelly-web/public/sw.js` API bypass list, or local Vite/service-worker responses can hide backend data.

## Tests And Fixtures

- Backend tests are integration-style xUnit tests in `backend/Glovelly.Api.Tests` using `WebApplicationFactory` and EF in-memory.
- `Infrastructure/GlovellyApiFactory.cs` resets DB data on each `CreateClient()`, replaces auth by default, injects fake email, fake mileage estimation, and in-memory attachment/blob storage.
- Use `GlovellyApiFactory.WithConfiguration(...)` for auth/development-style tests that need real auth wiring.
- Seed IDs live in `Infrastructure/TestData.cs`; default authenticated user claims live in `Infrastructure/TestAuthContext.cs`; email assertions should use `factory.Emails`.
- When changing a user journey or cross-workspace navigation, update the matching scenario under `docs/uat/`.

## Local And Secrets

- `.glovelly.dev.local` is git-ignored and sourced by `run-dev.sh`; do not read or edit it unless explicitly asked.
- Store local secrets such as Google OIDC, Resend, Routes API key, and PostgreSQL connection string with `dotnet user-secrets` under `backend/Glovelly.Api`, not in repo files.
- Local admin seeding requires `DevelopmentSeeding__AdminGoogleSubject` and only applies when using the in-memory development DB.

## High-Token Files

Search within these before opening large chunks:

- `frontend/glovelly-web/src/App.tsx`
- `backend/Glovelly.Api/Services/InvoiceWorkflowService.cs`
- `backend/Glovelly.Api/Endpoints/InvoiceEndpoints.cs`
- `backend/Glovelly.Api/Endpoints/GigCrudEndpoints.cs`
- `frontend/glovelly-web/src/hooks/useGigsWorkspace.ts`
- `frontend/glovelly-web/src/hooks/useInvoicesWorkspace.ts`

## Useful Searches

```bash
rg "Map.*Endpoints|MapCrudEndpoints" backend/Glovelly.Api/Endpoints backend/Glovelly.Api/Program.cs
rg "WhereVisibleTo|StampCreate|Validate|NormalizeGigExpenses" backend/Glovelly.Api
rg "fetchWithSession|parseProblemDetails|buildApiUrl" frontend/glovelly-web/src
rg "TestData\\.|TestAuthContext|factory\.Emails" backend/Glovelly.Api.Tests
```

## Avoid

- Do not bypass owner visibility checks for user-owned data; null creator IDs may be intentional for shared/dev seed visibility.
- Do not add package versions to individual `.csproj` files; use central package management in `Directory.Packages.props`.
- Do not refactor large coordination files just to tidy them while solving a narrow task.
- Do not create git commits, amend commits, push commits, or otherwise mutate git history unless the user explicitly asks for that git action.
