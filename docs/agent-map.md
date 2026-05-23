# Agent Map

This document is a compact orientation map for future agent work. It intentionally summarizes the codebase instead of replacing source-level inspection.

## Runtime Shape

Glovelly runs as one ASP.NET Core process in deployed form. The Vite frontend is built first, copied into the backend `wwwroot`, and served by the API application. In local development, `./run-dev.sh` starts the backend on `http://localhost:5153` and Vite on `http://localhost:5173`.

`Program.cs` is the backend entry point:

1. Build `StartupSettings`.
2. Register infrastructure and authentication.
3. Initialize the database.
4. Configure the HTTP pipeline.
5. Map metadata, auth, access, Google Drive, MCP, CRUD, and admin endpoints.
6. Fall back to `index.html` for the SPA.

## Backend Areas

- Configuration: startup settings, auth registration, database provider selection, CORS/Swagger/static file pipeline, and dev seeding.
- Auth: Google OIDC, local/development helpers, current user extraction, policy names, and MCP development auth.
- Data: `AppDbContext` owns EF mappings, indexes, precision, delete behavior, data protection keys, and model relationships.
- Models: mutable EF/domain entities are serialized directly by many endpoints, with navigation properties mostly hidden by `JsonIgnore`.
- Endpoints: minimal API route groups. Each file maps routes for one area and typically handles validation, authorization context, database calls, and simple response shaping.
- Services: cross-cutting or multi-step behavior. Use these for workflows that touch email, Google Drive, invoice PDFs, delivery, token protection, storage, or MCP query projection.

## Main Backend Route Groups

- `/auth`: login/logout/session/debug auth flows.
- `/access`: access request workflow and retention.
- `/admin`: admin user management.
- `/clients`: client CRUD plus client-level defaults/settings.
- `/gigs`: gig CRUD, expense handling, receipt attachment upload/download, invoice draft generation from gigs.
- `/gig-imports`: user review and commit workflow for staged gig imports.
- `/invoices`: invoice CRUD, status transitions, PDF download, issue/reissue, email delivery, Google Drive delivery, adjustments.
- `/invoice-lines`: invoice line CRUD.
- `/seller-profile`: seller profile used for invoice readiness and PDF details.
- `/google-drive`: Google Drive OAuth/connect/disconnect/configuration flow.
- `/mcp`: JSON-RPC style MCP endpoint exposing read-only business tools.
- `/app/metadata`: app/deployment metadata consumed by the frontend.

## Frontend Areas

- `main.tsx`: app bootstrapping and metadata loading.
- `App.tsx`: session state, active section, top-level data loading, modal wiring, and cross-workspace coordination.
- `AppSections.tsx`: barrel-style re-export layer for section/modal components and shared app section types.
- `api.ts`: API URL construction, authenticated fetch helper, session expiration helper, and problem details parsing.
- `types.ts`: frontend types mirroring backend responses.
- `forms.ts`: shared form conversion/empty-value helpers.
- `invoicePreview.ts`: invoice filename/email subject preview helpers and available tokens.
- `theme.ts`, `useThemePreference.ts`: theme preference state.

Workspace hooks:

- `useClientsWorkspace`: clients list, client CRUD, search, client invoice/mileage settings.
- `useGigsWorkspace`: gigs list, gig CRUD, expense editing, receipt attachments, multi-select, invoice draft preparation.
- `useGigImportsWorkspace`: staged gig import loading, autosave edits, accept/reject decisions, notification counts, and commit.
- `useInvoicesWorkspace`: invoices list, invoice status, adjustments, PDF download, email delivery, Google Drive publish.
- `useQuickReceipt`: quick receipt upload and candidate matching.
- `useSellerProfile`: seller profile loading/editing.
- `useUserSettings`: authenticated user defaults and invoice settings.
- `useAdminWorkspace`: admin user list/create/edit behavior.
- `useProfileMenu`: profile menu open/close state.

## Data Ownership

User-owned entities generally carry `CreatedByUserId` and `UpdatedByUserId`. Queries for client, gig, invoice, invoice line, and seller profile data should apply existing visibility helpers or equivalent user filtering. Some seed data can have null ownership for development/shared scenarios, so existing `WhereVisibleTo` helpers allow records with null creator IDs.

## Invoice Flow

The central invoice behavior lives in `InvoiceWorkflowService` and `InvoiceEndpoints`.

Common flow:

1. User creates or selects gigs.
2. Backend generates an invoice draft and system-generated invoice lines from gig fees, mileage, passenger mileage, and expenses.
3. Invoice can be edited with adjustment lines.
4. Invoice can be issued or reissued, which updates issue metadata and PDF content.
5. Invoice can be downloaded, emailed, or uploaded to Google Drive.
6. Gig records are linked back to the invoice and carry derived `isInvoiced` behavior via `InvoiceId`.

## Receipt Flow

Receipt attachments are modeled under gig expenses. Storage is abstracted through `IExpenseAttachmentStore`; tests use `InMemoryExpenseAttachmentStore`, and production can use Google Cloud Storage. Quick receipt behavior can infer nearby gig candidates, create a draft expense, and later move/update that draft.

## Imported Gig Flow

MCP gig import tools stage AI-extracted candidate gigs into `GigImportBatch` and `GigImportDraft` records. The frontend treats imported gigs as a secondary profile-menu workflow rather than a primary workspace.

Common flow:

1. MCP creates a gig import batch and draft rows for the signed-in user.
2. The profile avatar/menu show a notification dot when pending or accepted rows exist.
3. The user opens `Imported gigs`, edits rows inline, and accepts or rejects each draft.
4. Draft edits autosave. Rejected rows stay in place until decisions are committed.
5. `Commit decisions` creates real gigs from accepted rows, deletes rejected draft rows, and leaves pending rows for later review.
6. Created gigs keep source import batch/draft IDs for auditability and duplicate protection.

## MCP Surface

The MCP endpoint is user-scoped. Most tools are read-only. Gig import tools are write-only staging tools and do not create real gigs directly. It exposes:

- contact search
- invoice listing
- invoice details
- receipt/expense listing
- business summary totals
- gig import batch/draft staging and import batch lookup

Implementation is split between `McpEndpoints.cs` for protocol/tool handling and `GlovellyMcpQueryService.cs` for EF query projection.

## CI and Deploy

GitHub Actions restores .NET dependencies, runs backend tests, builds the Docker image, and deploys internal builds to Cloud Run. Pull requests from the same repository can deploy to the shared staging service and receive a preview URL comment.
