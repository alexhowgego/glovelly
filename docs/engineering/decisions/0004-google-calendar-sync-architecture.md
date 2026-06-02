# ADR 0004: Google Calendar Sync Architecture

Status: Accepted

Date: 2026-05-31

## Context

Glovelly manages gigs as business records. Users benefit from seeing those gigs in their day-to-day calendar, but Glovelly should remain the source of truth for gig status, dates, titles, venues, clients, and cancellation state.

The integration needs to fit the existing Google OAuth direction, avoid leaking business data through long-lived bearer URLs, and stay understandable for a small product without building a full cross-provider calendar reconciliation engine.

## Decision

Implement native Google Calendar sync as a one-way integration from Glovelly to Google Calendar.

Use a dedicated per-user Google Calendar named `Glovelly Gigs`. Glovelly creates and manages events only inside that calendar and stores provider IDs in local sync state.

Use the least-privilege Google Calendar scope that allows the app to manage calendars it created:

```text
https://www.googleapis.com/auth/calendar.app.created
```

Treat Glovelly as the source of truth. Google Calendar is a projection of eligible Glovelly gigs, not an editable upstream source.

Sync confirmed and completed gigs as all-day events. Do not sync draft gigs. Remove previously synced gigs when they become cancelled or are deleted in Glovelly.

Use deterministic Google event IDs based on the Glovelly gig ID so create retries and rebuilds are idempotent within the managed calendar.

Do not implement bidirectional sync, Google-side edit reconciliation, or arbitrary calendar discovery in this version.

## Consequences

The product explanation is simple: users connect Google Calendar, Glovelly creates a `Glovelly Gigs` calendar, and eligible gigs appear there after asynchronous sync.

User edits made directly in Google Calendar are not imported back into Glovelly. If a Glovelly gig changes later, Glovelly may overwrite the projected event.

Because the integration uses `calendar.app.created`, Glovelly intentionally avoids broad access to the user's calendars. Local sync state is therefore a best-effort cache of what Glovelly believes it created.

If the Google-side `Glovelly Gigs` calendar is deleted, Glovelly recreates it when the next sync detects the missing calendar, invalidates that user's Google Calendar sync state, queues a full rebuild, and retries the active event into the new calendar.

The sync worker must tolerate duplicate queue work, stale provider IDs, deleted provider calendars, deleted provider events, and retryable provider failures.

## Alternatives Considered

### Private iCalendar Feed

A private iCalendar feed would be simple and widely compatible, but the feed URL acts as a long-lived bearer credential. If leaked, it could expose client and gig information. That is not the preferred posture for a productised Glovelly integration.

### Writing To The User's Primary Calendar

Writing directly to the primary calendar would make ownership, cleanup, duplicate handling, and user explanation less clear. A dedicated calendar isolates Glovelly-managed events from personal calendar data.

### Bidirectional Sync

Bidirectional sync introduces conflict resolution, accidental deletes, partial provider edits, duplicate detection, webhook reliability, and unclear data ownership. That complexity is not justified for this version.

### Provider-Neutral Calendar Sync From Day One

A provider-neutral abstraction may be useful later, but Google Calendar is the concrete first integration. The current implementation should keep provider APIs behind services without pretending that Outlook/Graph semantics are already known.

### Broader Google Calendar Scopes

Broader scopes could allow discovery and reconciliation across calendars, but they increase consent surface and data access. The dedicated-calendar model works with `calendar.app.created`, so broader scopes are not needed for v1.

## Follow-Up

Future work may add user-facing sync health, clearer operational dashboards, additional providers, or explicit repair controls if the integration grows beyond one managed Google calendar per user.

Related implementation issues: #149, #150, #151, #165, #166, #167, #168.
