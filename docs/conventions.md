# Conventions

These are the working conventions that save future agents from rediscovering local patterns.

## Backend

- Use minimal API route-group extension methods for new endpoint areas.
- Put new CRUD routes beside the relevant existing endpoint file.
- Put multi-step domain workflows in services rather than in `Program.cs` or `AppDbContext`.
- Keep EF relationship, index, precision, and delete behavior configuration in `AppDbContext`.
- Use `DateOnly` for business dates such as gig, invoice, and due dates; use `DateTimeOffset` for audit/issue/delivery timestamps.
- Keep money as `decimal`.
- Serialize enums as strings where existing code does this; frontend types usually expect string unions.
- Use `EndpointSupport.WhereVisibleTo(...)` or equivalent owner checks for user-scoped data.
- Use `EndpointSupport.StampCreate(...)` and `StampUpdate(...)` helpers where they exist.
- Use `EndpointSupport.ValidateGigRequest(...)`, invoice status transition helpers, filename/subject pattern validation, and gig expense normalization rather than duplicating behavior.
- Return validation errors through `Results.ValidationProblem(...)` with field names matching frontend form fields.
- Keep service interfaces small and place them near their implementations when that matches the existing pattern.

## Backend Testing

- Add or update xUnit tests in `backend/Glovelly.Api.Tests`.
- Prefer integration-style endpoint tests through `GlovellyApiFactory`.
- Use `TestData` constants for seeded IDs.
- Use `TestAuthContext` for seeded/authenticated user details.
- Use `factory.Emails` for email assertions.
- Add direct service tests when behavior is mostly pure or when endpoint setup would obscure the scenario.
- Run `dotnet test glovelly.sln -m:1` after backend changes.

## Frontend

- Keep workflow and data state in `src/hooks`.
- Keep section/modal rendering in `src/components`.
- Use `App.tsx` for top-level orchestration only: session, active section, app-wide loading/status, and cross-workspace coordination.
- Use `fetchWithSession` for authenticated API calls.
- Use `buildApiUrl` for endpoint URLs.
- Use `parseProblemDetails` for backend validation/problem responses.
- Treat `SessionExpiredError` and 401 handling consistently with existing hooks.
- Update `src/types.ts` whenever backend JSON shapes change.
- Keep form state string-based for numeric inputs where existing forms do so, then parse at submit boundaries.
- Reuse existing formatter helpers from `formatters.ts`.
- Preserve the current no-component-library setup unless the user explicitly asks for a UI framework.
- Run `npm --prefix frontend/glovelly-web run lint` and `npm --prefix frontend/glovelly-web run build` after frontend changes.

## Docs

- Keep `README.md` focused on setup, run, deploy, and broad project context.
- Keep `docs/domain.md` focused on durable domain concepts.
- Keep `docs/roadmap.md` current enough that it does not contradict implemented features.
- Keep agent docs concise. Their job is to point agents at the right source files, not to duplicate implementation details.

## Git and Local Files

- `.glovelly.dev.local` is local and git-ignored; do not edit it unless explicitly asked.
- Do not commit secrets, user-specific claims, API keys, or connection strings.
- Do not revert unrelated user changes.
- Avoid broad refactors when making a targeted fix.
