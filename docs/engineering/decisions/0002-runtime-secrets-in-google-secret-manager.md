# ADR 0002: Runtime Secrets In Google Secret Manager

Status: Accepted

Date: 2026-05-19

## Context

Glovelly deploys to Google Cloud Run from GitHub Actions. The app needs runtime secrets such as database connection strings, Google OAuth credentials, Resend API keys, email sender addresses, and MCP OAuth client credentials.

GitHub Actions also needs deployment-time configuration such as project, region, service, and Workload Identity Federation metadata.

These two categories should not be treated as the same ownership boundary.

## Decision

Store production runtime secrets in Google Secret Manager and bind them into Cloud Run configuration.

Use GitHub Environment variables/secrets for CI/CD deployment-time configuration and visibility. GitHub Actions should authenticate to Google Cloud through Workload Identity Federation rather than long-lived Google service account JSON keys.

Keep the separation clear:

- Runtime secrets belong with the runtime platform.
- CI/CD variables describe how GitHub deploys the runtime.

## Consequences

Secret values stay out of source control and do not need to be duplicated unnecessarily in GitHub.

Cloud Run deployments can reference Secret Manager secret versions and merge secret updates into service configuration.

The deployment pipeline depends on correctly configured GCP resources: Secret Manager secrets, Cloud Run service, Artifact Registry repository, Workload Identity Federation pool/provider, and appropriate service accounts.

Operational documentation should name required configuration keys without recording real secret values.

## Follow-Up

Future operations docs should cover secret rotation, environment inventory, backup/restore expectations, and incident/runbook procedures.

