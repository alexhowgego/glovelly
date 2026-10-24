# Maps Static previews

Maps Static is an optional, separately configured provider for client/gig location images. It is not enabled by configuring Google Routes mileage estimates.

| Setting | Default | Purpose |
| --- | --- | --- |
| `Maps:GoogleStatic:Enabled` | `false` | Deployment gate for automatic eligible previews |
| `Maps:GoogleStatic:ApiKey` | empty | Required dedicated restricted Maps Static key |
| `Maps:GoogleStatic:SigningSecret` | empty | Required server-only URL-signing secret |

Environment variable names use double underscores, for example `Maps__GoogleStatic__Enabled`. Use .NET user-secrets locally and Secret Manager in Cloud Run. Never expose the URL-signing secret or commit credential values. The restricted API key and generated signature appear in direct Google image URLs by design; the signing secret does not. Avoid logging credential-bearing URLs or including them in screenshots. Missing/invalid signing configuration disables URL issuance; there is no unsigned fallback.

Staging and production read `MAPS_STATIC_ENABLED` (default false) and use these fixed Secret Manager bindings in each deployment project:

| Secret Manager secret (`latest`) | Cloud Run setting | Local user-secret key |
| --- | --- | --- |
| `glovelly-maps-static-api-key` | `Maps__GoogleStatic__ApiKey` | `Maps:GoogleStatic:ApiKey` |
| `glovelly-maps-static-url-signing-secret` | `Maps__GoogleStatic__SigningSecret` | `Maps:GoogleStatic:SigningSecret` |

Provision both secrets and runtime service-account access before deployment, even when imagery is disabled. Secret bindings are merged; removing a binding from the workflow does not necessarily retire an existing Cloud Run binding. Retire obsolete bindings explicitly.

Local credentials are stored under `backend/Glovelly.Api/Glovelly.Api.csproj` using .NET user-secrets, which the existing Development host loads automatically when `./run-dev.sh` launches the API. Keep credentials out of `.glovelly.dev.local`; no changes to that file or the startup script are necessary. Refresh credentials deliberately after rotation, not on every startup. For scripted refresh, capture Secret Manager responses in memory and pass a JSON object containing only the two configuration keys to `dotnet user-secrets set --project backend/Glovelly.Api/Glovelly.Api.csproj` via stdin; do not print values, use shell tracing, or pass secret values as command arguments.

Credential provisioning does not enable imagery. Authenticated record-scoped endpoints issue `private, no-store` JSON descriptors bound to the saved source and size. The browser assigns the signed URL directly to an image with `referrerPolicy="origin"`; no Glovelly image proxy, blob/data URL, outbound image client, or persistent signed-URL cache is used. Previews load automatically when the saved query and actual pane space/visibility are eligible. There is no approval state in client/gig write paths.

The map component measures its own reserved frame and requests matching dimensions, scaling both axes together to Google's 640px maximum. The `size` parameter permits only canonical `widthxheight` integers from 32 through 640 per axis, plus `small`/`medium` fallback presets. Initial measurement requests immediately; subsequent frame resizes settle for 300ms before requesting a new image. This does not delay/remount the shared overview or invoice thumbnails. Existing signing, source checks and issuance limits apply. The 12px rounded map frame must retain readable attribution; verify actual Google images on staging.

Each user can obtain at most ten descriptors per minute. This limits issuance, not actual provider calls: signatures do not expire, encrypt locations, or prevent replay of a copied URL. Use Google-side quotas for hard request caps, with budget alerts for monitoring. A 10-second client loading watchdog and image error handler remove failed imagery without retry loops. Direct image delivery cannot inspect Google's response warning headers or reliably identify provider-generated error images.

## Enablement checklist

- Resolve the pending provider privacy inquiry for the UK billing account and review automatic direct-delivery disclosures and applicable browser-storage/consent requirements before production.
- Enable Maps Static and billing separately; restrict the dedicated key to Maps Static API and the required website origins for staging/production. Authorize local origins separately only where intended. Origin-only referrers must match the configured restrictions; do not apply no-referrer to image requests.
- Verify exact signed URLs on staging, require signatures, and disable unsigned-request quota after signed requests work. Keep the signing secret server-side and do not reuse the Routes key.
- Configure Google-side quotas/budgets; application limits alone are not a billing cap.
- Publish Google Maps end-user terms/privacy disclosures explaining automatic location queries and direct browser network/request information, without anonymous or cookie-free claims.
- Verify disabled/missing-or-invalid-signing/provider-failure fallbacks with mocked descriptors/images in automated tests. A separate deliberate staging smoke check validates actual restrictions; never include live credentials in test fixtures or captures.
- Preserve full image attribution and prohibit durable/shared/offline map caches. Review changes to transient image handling against current terms.

Provider review is tracked in `openspec/changes/record-visual-previews/provider-review.md` while the change is active. Production enablement is a manual rollout gate, not a result inferred from successful tests.
