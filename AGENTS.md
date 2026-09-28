# Glovelly Agent Guide

Compact, repo-specific context for future OpenCode sessions. Keep only facts an agent would otherwise likely miss.

## Shape

- Glovelly is a personal business platform for authenticated music-work admin: clients, gigs, expenses/receipts, invoices, seller profile, Google Drive/email delivery, admin users, and a small MCP surface.
- Backend: ASP.NET Core minimal API on .NET 10, EF Core, PostgreSQL when `ConnectionStrings:Glovelly` exists, EF in-memory otherwise. Shared package versions live in `Directory.Packages.props`; target framework/nullable/implicit usings live in `Directory.Build.props`.
- Frontend: React 19 + TypeScript + Vite in `frontend/glovelly-web`; independent Astro sites live in `frontend/glovelly-landing` and `frontend/glovelly-guide`. `npm run build` is `tsc -b && vite build` for the authenticated app.
- Deployment builds one Docker image: Vite `dist` is copied into ASP.NET Core `wwwroot`, then the API serves the SPA and API from one process. CI deploys same-repo PRs to shared Cloud Run staging and `main` to production.

## Commands

Run from repo root unless noted.

```bash
./run-dev.sh                                    # backend :5153 + Vite :5173; sources .glovelly.dev.local
dotnet test --solution glovelly.sln --max-parallel-test-modules 1  # backend suite; keep modules serial for shared in-memory/factory state
dotnet test --project backend/Glovelly.Api.Tests/Glovelly.Api.Tests.csproj -- --filter-class '*GigEndpointsTests'
npm --prefix frontend/glovelly-web run lint
npm --prefix frontend/glovelly-web run build
npm --prefix frontend/glovelly-guide run check
npm --prefix frontend/glovelly-guide run build
./verify.sh                                    # dotnet test, frontend lint, frontend build
dotnet tool restore && dotnet tool run docfx docs/docfx.json
dotnet tool run docfx docs/docfx.json --serve  # local handbook
```

- Use `./verify.sh` before handing over broad changes. For backend-only changes, `dotnet test --solution glovelly.sln --max-parallel-test-modules 1` is the best first check.
- Frontend has lint/build checks only; no unit/e2e runner is configured.

## Backend Map

- `backend/Glovelly.Api/Program.cs` is the real entrypoint: startup settings, infrastructure/auth registration, DB init, HTTP pipeline, endpoint mapping, SPA fallback.
- `Configuration/StartupSettings.cs` chooses PostgreSQL vs in-memory by presence of `ConnectionStrings:Glovelly`; development seed data only runs for non-Postgres, non-testing startup.
- `Endpoints/CrudEndpoints.cs` maps protected `/clients`, `/gigs`, `/gig-imports`, `/invoices`, `/invoice-lines`, and `/seller-profile` groups. Auth/access/Google Drive/MCP/admin/expense statements are mapped separately in `Program.cs`.
- `Endpoints/EndpointSupport.cs` owns high-risk shared behavior: `WhereVisibleTo`, create/update stamping, gig validation, invoice status transition rules, filename/subject validation, and gig expense normalization.
- Product wording may call `GigStatus.Confirmed` gigs "planned gigs"; code and persisted JSON use `Confirmed`.
- `Services/InvoiceWorkflowService.cs` and related invoice services own invoice creation, generated lines, PDFs, issue/reissue, delivery, and gig linkage. Do not duplicate that flow inside endpoints.
- `Services/GlovellyMcpQueryService.cs` performs user-scoped MCP EF projections; MCP tools should remain scoped by authenticated user visibility.

## Frontend Map

- `src/App.tsx` coordinates session, active section, initial data loads, modals, and cross-workspace actions. Avoid adding large workflow bodies there when a hook can own them.
- `src/hooks/` owns stateful workspace logic for clients, gigs, gig imports, invoices, admin, user settings, seller profile, and quick receipts.
- `src/components/` is presentational sections/modals. Preserve the current plain React/CSS approach; there is no component library.
- `frontend/glovelly-guide` is the public, task-led user guide. Write in a practical "do this next" voice for musicians and sole traders; keep technical/operator reference material in the DocFX handbook at `handbook.glovelly.net`.
- Terminal frontend feedback uses Sonner: mount `NotificationToaster` from `src/NotificationToaster.tsx` in `src/main.tsx` and call the Glovelly policy wrapper in `src/notifications.ts`, rather than importing Sonner in workspace code. Use notifications for completed actions and unexpected failures that close, navigate away from, or outlive their initiating UI; keep validation, progress, durable configuration/health warnings, and terminal feedback for still-open modals inline. The notification viewport must render below modal overlays so persistent notifications cannot block modal controls.
- Use `buildApiUrl`, `fetchWithSession`, `parseProblemDetails`, and session-expiry helpers from `src/api.ts`; avoid raw `fetch` for authenticated API calls.
- Update `src/types.ts` whenever backend JSON shapes change.
- When adding a frontend-consumed API prefix, update both `frontend/glovelly-web/vite.config.ts` proxy and `frontend/glovelly-web/public/sw.js` API bypass list, or local Vite/service-worker responses can hide backend data.

## Tests And Fixtures

- Backend tests are integration-style xUnit tests in `backend/Glovelly.Api.Tests` using `WebApplicationFactory` and EF in-memory.
- `Infrastructure/GlovellyApiFactory.cs` resets DB data on each `CreateClient()`, replaces auth by default, injects fake email, fake mileage estimation, and in-memory attachment/blob storage.
- Use `GlovellyApiFactory.WithConfiguration(...)` for auth/development-style tests that need real auth wiring.
- Seed IDs live in `Infrastructure/TestData.cs`; default authenticated user claims live in `Infrastructure/TestAuthContext.cs`; email assertions should use `factory.Emails`.
- When changing a user journey or cross-workspace navigation, update the matching scenario under `docs/uat/`.
- When changing terminal frontend feedback, update the relevant UAT journey and retain its notification, persistent-error, and mobile-placement coverage.
- Do not run Playwright UAT or documentation-capture tests locally. The pinned Chromium is unsupported on the local macOS runner, and documentation capture also requires staging-only credentials. Run them in the Ubuntu CI pipeline instead.
- Documentation captures use the isolated `Glovelly Docs` staging fixture. CI resets it before/after the suite, writes candidates to `TestResults/documentation-captures/`, compares them with `frontend/glovelly-guide/public/screenshots/`, and uploads a non-blocking review artifact/PR comment. Review candidates before adding or replacing checked-in PNGs; never create substitute screenshots manually.
- Guide screenshots are paired `*-light.png` and `*-dark.png` assets. Select pairs with the active Starlight `[data-theme]` attribute, not `<picture>` `prefers-color-scheme`, which does not follow the guide's theme toggle. The dark asset is Glovelly's actual `dark` product preference, not another product theme.

## Local And Secrets

- `.glovelly.dev.local` is git-ignored and sourced by `run-dev.sh`; do not read or edit it unless explicitly asked.
- Store local secrets such as Google OIDC, Resend, Routes API key, and PostgreSQL connection string with `dotnet user-secrets` under `backend/Glovelly.Api`, not in repo files.
- Local admin seeding requires `DevelopmentSeeding__AdminGoogleSubject` and only applies when using the in-memory development DB.
- When adding or changing any runtime setting/environment variable, update `backend/Glovelly.Api/appsettings.json`, both staging and production blocks in `.github/workflows/main.yml`, `README.md`, and the relevant `docs/engineering/` page in the same change. Update this guide when the setting changes agent-relevant operating constraints; never add secret values to repository files.

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
- Never create, amend, or push git commits on the user's behalf. The user manages git history themselves.
