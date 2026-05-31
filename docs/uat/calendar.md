# Google Calendar

## Purpose

Use this checklist when changes touch gigs, user settings, Google OAuth, or Calendar sync behavior.

## Preconditions

- You can sign in to the environment being tested.
- Google Calendar OAuth is configured for the environment.
- The Calendar sync worker or scheduled job is running for deployed environments.

## Connect Calendar

### Steps

1. Open user settings.
2. Click `Connect Calendar` and complete the Google OAuth consent flow.
3. Return to user settings.

### Expected Results

The Google Calendar card shows as connected. Glovelly creates or reuses a dedicated `Glovelly Gigs` calendar. Calendar updates are asynchronous and may take a few minutes to appear.

## Gig Eligibility

### Steps

1. Create or identify one `Confirmed` gig, one `Completed` gig, one `Draft` gig, and one `Cancelled` gig.
2. Wait for the scheduled Calendar sync to run, or ask an engineer to run the Calendar sync worker for the environment.
3. Open the `Glovelly Gigs` calendar in Google Calendar.

### Expected Results

Confirmed and completed gigs appear as all-day events. Draft and cancelled gigs do not appear. If a previously synced gig is moved to cancelled, its Calendar event is removed during sync.

## Delayed Updates

### Steps

1. Edit the title, date, venue, or status of a synced gig.
2. Open user settings and review the Google Calendar card.
3. Wait for the scheduled Calendar sync to run.

### Expected Results

The user settings card may show pending Calendar work before the worker runs. The Calendar event updates after the scheduled sync completes; users are not expected to trigger the worker manually from the app.
