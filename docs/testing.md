# Testing

This repo currently has backend integration tests and frontend lint/build checks.

## One-Command Verification

From the repo root:

```bash
./verify.sh
```

That script runs:

```bash
dotnet test glovelly.sln -m:1
npm --prefix frontend/glovelly-web run lint
npm --prefix frontend/glovelly-web run build
```

## Backend Tests

Backend tests live in `backend/Glovelly.Api.Tests`.

Important support files:

- `Infrastructure/GlovellyApiFactory.cs`: creates the test host, replaces auth, uses EF in-memory, injects fake email/storage, and resets seeded data for each client.
- `Infrastructure/TestAuthContext.cs`: default authenticated user identity and claim constants.
- `Infrastructure/TestData.cs`: seeded client, gig, invoice, and related IDs.
- `Infrastructure/FakeEmailSender.cs`: fake email sink for assertions.
- `Infrastructure/TestPolicyEvaluator.cs`: default test authorization bypass.

Run backend tests:

```bash
dotnet test glovelly.sln -m:1
```

Use `-m:1` because the test setup uses shared web application factory patterns and in-memory state; serial execution avoids noisy cross-test behavior.

## Frontend Checks

Frontend code lives in `frontend/glovelly-web`.

Run lint:

```bash
npm --prefix frontend/glovelly-web run lint
```

Run production build:

```bash
npm --prefix frontend/glovelly-web run build
```

There is no dedicated frontend unit/e2e test runner configured yet. For frontend changes, lint and build are the available automated checks.

## Manual UAT

Use [docs/uat/index.md](uat/index.md) for human regression journeys that cut across gigs, expenses, receipts, invoices, expense statements, delivery, seller profile, and admin workflows.

Start with [docs/uat/pre-merge-regression.md](uat/pre-merge-regression.md) for broad pre-merge checks. Use the focused invoice, expense, and enrolment/access pages when a change touches those areas. Keep the journeys scenario-based so they can later be automated as browser tests.

## Browser UAT

Staging seeds a test-only regression user, baseline client, and seller profile for browser automation. Playwright can authenticate by posting to `POST /test-auth/login` with `X-Glovelly-Uat-Secret`; the secret should come from `GLOVELLY_UAT_SECRET` in staging and GitHub Actions.

The core invoice browser regression creates a run-specific client using `GLOVELLY_UAT_INVOICE_RECIPIENT_EMAIL` and sends the invoice through the configured email provider. Configure that value as a controlled staging/GitHub environment variable so UAT delivery goes to an approved inbox rather than seeded or personal client data.

Tests should create run-specific records using a run ID such as `UAT-<timestamp>-<short-sha>` rather than mutating shared baseline data.

## What to Add When Changing Behavior

- New backend route: add endpoint tests for success, validation failure, authorization/session behavior when relevant, and user visibility boundaries.
- New invoice behavior: add tests around generated lines, status transitions, issue/reissue metadata, delivery side effects, and gig linkage.
- New email behavior: assert fake email count, recipient, subject, and meaningful body/attachment details.
- New Google Drive behavior: prefer interface-backed tests or endpoint tests with test doubles; avoid real network calls.
- New frontend API shape: update `src/types.ts`, affected hooks, and run lint/build.
