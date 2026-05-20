# Engineering

This section records the engineering shape of Glovelly: how it is built, deployed, authenticated, configured, and extended.

The goal is durable context rather than exhaustive implementation notes. Keep these pages focused on intent, constraints, and consequences. Source files remain the authority for exact code behavior.

## Start Here

- [Architecture](architecture.md): system shape, boundaries, integrations, and security posture.
- [Authentication](authentication.md): Google OIDC, Glovelly user mapping, enrolment, and roles.
- [Deployment pipeline](deployment-pipeline.md): container build, GitHub Actions, Artifact Registry, Cloud Run, and runtime configuration.
- [Database](database.md): PostgreSQL, EF Core, data ownership, and migration posture.
- [Email](email.md): outbound email abstraction, Resend integration, and delivery assumptions.
- [Mileage and routes](mileage-routes.md): Google Routes mileage estimation and fallback behavior.
- [Architecture decisions](decisions/index.md): ADR index for decisions that should outlive individual pull requests.

## Principles

- Runtime architecture should stay simple, while internal code boundaries stay clean.
- Google authenticates identity; Glovelly authorises access.
- Business data should relate to internal Glovelly users/domain entities, not directly to raw provider claims.
- Runtime secrets belong in secure runtime configuration, not source control.
- External providers should sit behind application abstractions where the provider choice is not core domain logic.
