# ADR 0005: Cloud Run Jobs For Background Workers

Status: Accepted

Date: 2026-05-31

## Context

Glovelly runs as a Cloud Run web service. The service hosts the ASP.NET Core API and bundled Vite frontend, and Cloud Run may scale the service down when there is no interactive traffic.

Google Calendar sync introduced durable background work: gig changes enqueue database-backed work items that need to be drained even when no user is actively using the app. An in-process hosted worker inside the web app is not a reliable production execution model for this because Cloud Run service instance lifetime and CPU availability are not owned by the application.

Future features such as automated notifications, reminders, retention tasks, or provider retries may need the same non-interactive execution pattern.

## Decision

Add a dedicated `Glovelly.Worker` project for non-interactive background commands.

Build one container image that contains both:

- the web app entrypoint, used by the Cloud Run service; and
- the worker binaries, used by Cloud Run Jobs.

Keep the default container entrypoint as the web app. Run worker commands through Cloud Run Job command overrides. The initial worker command drains the Calendar sync queue:

```bash
dotnet worker/Glovelly.Worker.dll calendar-sync drain
```

Trigger the Calendar sync job with Cloud Scheduler on a fixed cadence.

Use the same runtime configuration model as the web app: Secret Manager bindings, deployment environment variables, runtime service account, EF Core models, and application services.

## Consequences

Background execution is decoupled from the web request lifecycle and does not depend on a warm Cloud Run service instance.

The deployment pipeline must create and update a Cloud Run Job and a Cloud Scheduler trigger in addition to the existing Cloud Run service.

The Calendar sync queue must be safe for separate-process execution. Queue draining therefore needs durable status fields, processing ownership, stale-processing recovery, bounded retries, and useful diagnostics.

The same image remains the release unit, avoiding a second Artifact Registry image and reducing deployment drift. The worker can be split into a separate image later if operational needs justify that complexity.

Local development can run the worker directly with `dotnet run --project backend/Glovelly.Worker -- calendar-sync drain`.

## Alternatives Considered

### Drain On Gig Save Only

Draining synchronously or opportunistically when a user edits a gig gives a fast path but is not sufficient. Work would depend on interactive traffic and provider failures would be harder to retry reliably.

### ASP.NET Core Hosted Service In The Web App

A hosted service is simple locally but unreliable as the production execution model on Cloud Run services. The service can scale to zero, and instance lifecycle is controlled by Cloud Run rather than by Glovelly.

### Cloud Scheduler Calling An Internal HTTP Endpoint

This would wake the web service and may work as a stepping stone, but it keeps background execution coupled to HTTP request handling and the interactive service surface. A Cloud Run Job is a clearer non-interactive boundary.

### Pub/Sub, Cloud Tasks, Or A Job Framework

These may become useful if background work grows, but they are more machinery than the current Calendar sync queue needs. A database-backed queue plus scheduled Cloud Run Job is sufficient for the expected volume and operational maturity.

### Separate Worker Image

A separate image could reduce runtime contents and isolate deployments, but it adds build and release complexity. One image with distinct entrypoints is simpler at the current scale.

## Follow-Up

Revisit this decision when there are multiple independent background job types, near-real-time latency requirements, complex dead-letter handling, or enough worker-specific dependencies to justify a separate image or queue product.

Operational docs should continue to capture required IAM permissions for Cloud Scheduler to run the job and for the job runtime service account to access secrets, database, and provider APIs.

Related implementation issues: #164, #165, #166, #167, #168.
