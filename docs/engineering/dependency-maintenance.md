# Dependency Maintenance

## October 2026 Refresh — Issue #301

This is routine maintenance, not remediation of an established corruption incident. Direct NuGet versions remain centralised in `Directory.Packages.props`; each frontend retains its own manifest and npm lockfile. No runtime settings, database migrations, or product workflows are changed.

### Selected Updates

| Area | Selection and migration notes |
| --- | --- |
| Google Cloud Storage | `4.15.0` → `5.0.0`; server-side upload checksum validation is now the default. See below. |
| Google GenAI | `1.19.0` → `1.26.1`; includes unified exception hierarchy, longer SDK HTTP timeouts, and automatic-continuation fixes. Receipt extraction retains its application-owned timeout and cancellation/error handling. |
| Microsoft ASP.NET Core, EF Core and Hosting | `10.0.11` → `10.0.13`, aligned across the solution and the local EF migration tool. Docker uses SDK `10.0.402` and ASP.NET runtime `10.0.13`. Npgsql remains on the latest stable `10.0.3`, compatible with EF Core 10. |
| IdentityModel | `8.22.0` → `8.23.0`. |
| Resend | `0.11.0` → `0.24.0`; the breaking contact-properties dictionary change concerns an unused API. The application email abstraction is unchanged. |
| Swashbuckle | `10.2.3` → `10.3.0`; includes Minimal API JSON-option and schema-nullability fixes. |
| Backend tooling | Test SDK `18.11.0`, xUnit `4.0.2`, VS runner `4.0.1`, System.CommandLine `2.0.13`. Playwright and plist-cil are already current. |
| Authenticated frontend | React/React DOM `19.3.1`, matching React typings `19.3.0`, SignalR `10.0.11`, Vite `8.3.4`, React plugin `6.1.2`, Vitest `5.0.5`, and current compatible lint tooling. |
| Landing page and guide | Astro `7.4.0`, Starlight `0.42.6`, TypeScript `6.0.3`. |

Vitest 5 requires Node ≥22.12 and a compatible Vite release; CI/Docker already use Node 24. Its new default clears mock call history between tests, and asynchronous assertions must be awaited. Existing frontend tests pass without compatibility opt-outs. Astro 7.4 raises its Node 22 minimum to 22.22; Node 24 remains supported.

### Deliberately Retained Versions

- **TypeScript 7.0.2:** deferred because `typescript-eslint@8.71.2` requires TypeScript `<6.1.0`, and `@astrojs/check@0.9.10` declares only TypeScript 5/6 support. All three projects now use `~6.0.3`; do not force incompatible peers merely to obtain the latest major.
- **Node typings:** stay on `@types/node@24.19.2`, matching the Node 24 build toolchain rather than the latest Node 26 typings.
- **Guide PostCSS override:** retained. Starlight still pulls `postcss-nested@6.2.0`, whose upstream selector-parser range is `^6.1.1`; the existing override resolves `postcss-selector-parser@7.1.6`. Removing it would return to the old major.
- Other unchanged direct packages were already current stable versions at the time of the inventory. No substantial architecture migration is bundled here.

### Storage Integrity and Error Behavior

The old SDK was not unprotected: `4.15.0` defaulted to client-side CRC32C comparison after upload, with `DeleteAndThrow` attempting to remove a mismatched object. In `5.0.0`, the default is `RejectAndThrow`: the SDK incrementally hashes the source and sends `x-goog-hash: crc32c=…` on the final resumable-upload request. A mismatch is rejected by GCS with HTTP 400 before object creation. The error propagates as `GoogleApiException`, rather than the previous post-upload `UploadValidationException`.

`ThrowOnly` was removed; `DeleteAndThrow` is obsolete. Glovelly uses neither explicitly, so `GcsBlobStore` retains the SDK defaults. No custom checksum implementation or configuration switch is needed. The SDK rejects validation of a resumed source that starts beyond previously hashed bytes; Glovelly supplies complete streams and does not persist/resume SDK upload sessions.

The release's XML documentation on `UploadObjectOptions.UploadValidationMode` still describes the old default. The tagged implementation and upstream change, not that stale comment, establish the new behavior.

`GcsBlobStoreTests` exercise the real SDK with an isolated HTTP transport: seekable/non-seekable source checksum headers, an HTTP 400 rejection without a cleanup delete, and propagation through the attachment endpoint without attachment metadata or a workspace success event. `IntakeEndpointsTests` additionally verify that a destination-write failure retains the source, creates no expense/attachment, and can be retried successfully. These tests do not replace verification against a real bucket.

### Validation and Deployment Handoff

Run `./verify.sh`, plus `check` and `build` in both Astro projects. NuGet vulnerability inspection must include transitive packages; run npm audit in each frontend. Browser UAT and documentation capture remain Ubuntu CI-only.

Local validation completed on 18 October 2026:

- Solution restore/build, `./verify.sh`, and the focused GCS/intake regression tests passed.
- Clean `npm ci` installs, Astro checks/builds, and the DocFX handbook build passed.
- NuGet (including transitive dependencies) and all three npm audits reported no known vulnerabilities. The direct NuGet inventory reported no remaining stable updates.
- `dotnet tool restore` and EF tool `10.0.13` version verification passed.
- The web build retains a non-blocking large-chunk warning; the guide build reports missing optional `i18n`/`404` content warnings.
- The Docker daemon was unavailable locally, so the updated image must be built in CI. Browser UAT and live staging GCS verification have not been run locally.

**Pending after deployment:** perform the [staging GCS round trip](../uat/expenses.md#staging-gcs-storage-verification) and record the deployed revision, result, and byte/hash comparison in the PR. A local transport fake or the existing in-memory store cannot validate staging credentials, bucket permissions, or GCS service behavior. Do not mark issue #301's staging acceptance criterion complete before this evidence exists.

### Release References

- [Storage version history](https://docs.cloud.google.com/dotnet/docs/reference/Google.Cloud.Storage.V1/latest/history)
- [Storage 5.0.0 tagged default](https://github.com/googleapis/google-cloud-dotnet/blob/Google.Cloud.Storage.V1-5.0.0/apis/Google.Cloud.Storage.V1/Google.Cloud.Storage.V1/UploadObjectOptions.cs)
- [Storage checksum implementation and HTTP 400 behavior](https://github.com/googleapis/google-cloud-dotnet/pull/15769)
- [GenAI changelog](https://github.com/googleapis/dotnet-genai/blob/Google.GenAI-v1.26.1/CHANGELOG.md)
- [Resend releases](https://github.com/resend/resend-dotnet/releases)
- [ASP.NET Core 10.0.13](https://github.com/dotnet/aspnetcore/releases/tag/v10.0.13)
- [Swashbuckle 10.3.0](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/releases/tag/v10.3.0)
- [Vitest 5 migration](https://vitest.dev/guide/migration/)
- [Vite 8.3.4](https://github.com/vitejs/vite/releases/tag/v8.3.4)
- [Astro 7.4.0](https://github.com/withastro/astro/releases/tag/astro%407.4.0)
- [Starlight 0.42.6](https://github.com/withastro/starlight/releases/tag/%40astrojs%2Fstarlight%400.42.6)
