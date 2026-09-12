## 1. Establish Hosting and Deployment Ownership

- [x] 1.1 Record the current DNS records, Cloud Run mappings, GitHub Environment values, Google OAuth registrations, and GitHub Pages custom-domain state as the migration rollback baseline.
- [x] 1.2 Connect Firebase Hosting to the existing Google Cloud project and create independent landing-site and user-guide Hosting targets without adding Firebase application services.
- [x] 1.3 Add GitHub Actions workflows or workflow jobs that build, deploy, and preview each Firebase Hosting target with least-privilege GCP credentials.
- [x] 1.4 Document the owner, deployment path, DNS/certificate configuration, and rollback procedure for all four public hostnames.

## 2. Make the Application Origin Explicit

- [x] 2.1 Add validated `App:PublicBaseUrl` startup configuration, including explicit development behavior and failure for invalid deployed-environment values.
- [x] 2.2 Configure production and staging deployment environments with their canonical application public base URLs, and align MCP issuer/resource values with that setting.
- [x] 2.3 Update OIDC redirect creation and post-sign-in return validation to use the configured public application origin.
- [x] 2.4 Update Google Drive, Sheets, and Calendar OAuth callback construction to use the configured public application origin.
- [x] 2.5 Update invitation, access-request review, email application, and other generated application links to use the configured public application origin.
- [x] 2.6 Add focused backend tests for public-base-URL validation, sign-in returns, OAuth callback URLs, generated links, and MCP metadata.

## 3. Configure Menu and Provider Migration

- [x] 3.1 Create and verify the `menu.glovelly.net` Cloud Run domain mapping, DNS records, and TLS certificate before changing the production application origin.
- [x] 3.2 Register Menu's Google application origin, OIDC callback, and Drive, Sheets, and Calendar callbacks while retaining apex registrations for the overlap window.
- [x] 3.3 Deploy the application with Menu as the canonical production origin and verify sign-in, sign-out, invitation, access-request, integration, generated-link, and MCP flows on Menu.
- [x] 3.4 Implement and verify temporary legacy-apex application redirects that preserve paths and query strings without redirecting the future landing-page root.
- [x] 3.5 Define, observe, and complete the callback/session overlap window before reassigning the apex domain.

## 4. Move the Handbook and Update References

- [x] 4.1 Move the DocFX GitHub Pages custom domain, CNAME, sitemap base URL, and handbook deployment documentation to `handbook.glovelly.net`.
- [x] 4.2 Update application legal/help links, repository documentation, privacy/terms notices, email expectations, and UAT defaults to their new canonical public destinations.
- [x] 4.3 Add routing, migration, rollback, and post-deployment smoke-check guidance to the technical/operator handbook.
- [x] 4.4 Verify the handbook deployment at `handbook.glovelly.net` and confirm `docs.glovelly.net` is unblocked for the Starlight user-guide change.

## 5. Cut Over and Verify

- [x] 5.1 Deploy the landing-site Hosting target to `glovelly.net` only after Menu verification and the overlap window complete.
- [x] 5.2 Run and record production smoke checks for every public hostname, Menu authentication, generated links, all Google integration callbacks, MCP discovery metadata, and rollback readiness.
- [x] 5.3 Retire apex Google OAuth registrations only after the documented rollback window closes and record the final DNS, provider, and hosting ownership state.
- [x] 5.4 Run `dotnet test --solution glovelly.sln --max-parallel-test-modules 1`, `npm --prefix frontend/glovelly-web run lint`, and `npm --prefix frontend/glovelly-web run build` for application changes.
