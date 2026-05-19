# ADR 0003: Email Provider Abstraction

Status: Accepted

Date: 2026-05-19

## Context

Glovelly sends transactional email for invoice delivery, access/admin workflows, and likely future business notifications.

Resend is the current production provider, but provider choice is infrastructure. It should not become core domain logic.

Local development and tests also need modes that avoid sending real email.

## Decision

Send email through the `IEmailSender` abstraction.

Keep provider-specific behavior isolated in provider adapters such as the Resend sender and supporting email mapping code. Domain workflows should compose the intended message and call application services rather than depending directly on Resend APIs.

Support multiple operational modes: Resend for real delivery, disabled/null sending where delivery should be suppressed, and logging for development-style visibility.

## Consequences

Invoice delivery and access-request workflows can be tested with fakes and do not need provider credentials.

Provider-specific message formatting, attachments, API keys, and error behavior are contained near the email infrastructure.

Changing provider later should require replacing/adapting infrastructure code rather than rewriting domain workflows.

The app still needs operational handling for real delivery concerns such as bounces, webhook processing, retries, and support visibility.

## Follow-Up

Future work may add delivery status capture, Resend webhook handling, retry policy, and runbook guidance for failed or rejected email delivery.

