## 1. Workspace routing

- [x] 1.1 Add exact workspace-path resolution and canonical path mapping, including safe fallback for unknown SPA paths and coverage for malformed and trailing-slash paths.
- [x] 1.2 Initialize the active section from the incoming path and synchronize explicit navigation and cross-workspace actions with history replacement; handle Admin without a stale workspace URL.
- [x] 1.3 Preserve access-request review paths through authentication and review, then clear them on dismissal or navigation away without reopening a dismissed review.
- [ ] 1.4 Verify SPA fallback, Vite proxy, and service-worker handling of direct workspace document requests without changing existing API routing or authorization.

## 2. Authentication and navigation verification

- [x] 2.1 Add focused tests for workspace selection, URL replacement, cross-workspace navigation, Admin, fallback, and the access-request lifecycle.
- [x] 2.2 Test workspace sign-in return and existing safe-return restrictions, including external and scheme-relative redirect attempts; fix any safety gap without expanding allowed destinations.
- [x] 2.3 Add browser coverage for direct navigation and refresh for all three workspaces, workspace switching and history replacement, unknown paths, sign-in return, and admin/non-admin access-request review. (Added; Ubuntu CI execution remains outstanding.)

## 3. Guide and documentation

- [x] 3.1 Replace task-specific guide Menu links with the canonical Clients, Gigs, and Invoices entrypoints, including Clients for the first-invoice journey; retain appropriate general links.
- [x] 3.2 Update maintainer-facing public routing documentation and relevant UAT journeys with canonical entrypoints, authentication, and navigation expectations.
- [x] 3.3 Add or update appropriate guide/link checks so the intended handoffs are verified.

## 4. Validation

- [x] 4.1 Run relevant frontend tests, lint and build; run guide check and build, plus backend tests if backend behavior changes.
- [x] 4.2 Run browser UAT in Ubuntu CI rather than locally, and record results or any outstanding CI/deployment verification. (Not run locally; Ubuntu CI and deployed sign-in/review verification remain outstanding.)
- [ ] 4.3 Confirm Menu support is deployed before or with guide links and document rollback ordering.
