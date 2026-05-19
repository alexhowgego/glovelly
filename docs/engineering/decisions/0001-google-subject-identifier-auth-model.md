# ADR 0001: Google Subject Identifier Auth Model

Status: Accepted

Date: 2026-05-19

## Context

Glovelly uses Google OpenID Connect for sign-in, but Glovelly owns application access and permissions.

Email addresses are useful for administration, display, contact, and bootstrap enrolment. They are not the most durable external identity key because they can change, be reassigned, or differ in meaning across identity-provider and application workflows.

Google OIDC provides a subject identifier (`sub`) that is stable for the user within the Google client/application context.

## Decision

Use the Google subject identifier as the durable external login binding once it is known.

Keep internal Glovelly users as first-class records. Domain data, permissions, and audit metadata should relate to internal Glovelly user IDs rather than raw Google claims or email addresses.

Allow an admin-friendly bootstrap flow where a user can initially be provisioned by email with no Google subject ID. On first successful Google login, if the email is verified and matches an active provisioned Glovelly user with no subject ID, bind the Google subject ID to that user. Future logins use the subject ID as the canonical mapping.

## Consequences

Google authenticates identity; Glovelly authorises access.

A valid Google login is not, by itself, sufficient to access Glovelly. Users must map to active internal Glovelly user records.

Email remains important administrative/contact data, and Glovelly may update the stored email after login, but email is not the durable login identity once the Google subject is known.

The data model can evolve toward richer account/tenant structures without making provider claims the owner of domain records.

## Follow-Up

Future work may include richer admin enrolment UX, more explicit audit trails, and a more formal role/permission model.

