## Context

Administrators provision an active user record and can send that user an invitation email. The email already links to `/auth/login`; after Google authenticates the recipient, Glovelly binds the verified Google email address to the active, pre-provisioned user record. The current generic CTA, "Open Glovelly", under-explains this recipient journey.

The flow deliberately separates email delivery from authorisation. The durable external identity is the Google subject, and the email link is not proof that a recipient is entitled to access the application.

## Goals / Non-Goals

**Goals:**

- Make the invitation's primary action unambiguously communicate acceptance and Google sign-in.
- Give recipients clear guidance about the email address that must be selected at Google.
- Preserve a usable plain-text email path and test the rendered email contract.
- Document the end-to-end enrolment journey for manual UAT.

**Non-Goals:**

- Create invitation tokens, a new acceptance endpoint, or a link that directly grants access.
- Change Google OIDC configuration, user provisioning, subject binding, roles, or account activation.
- Add approval actions for administrator access-request notifications; that work belongs to GitHub issue #248.

## Decisions

### Reword the existing login CTA rather than introduce an invitation token

The HTML button and plain-text fallback will retain the current login URL and describe the action as accepting the invitation and signing in. Google OIDC will continue to establish the recipient's identity and the existing verified-email match will continue to enrol only active, pre-provisioned users.

This minimises the change while resolving the ambiguity reported in #188. A signed invitation token was considered, but it would add expiry, forwarding, storage, and binding rules without removing the need for Google authentication.

### State the required Google account in both email formats

The email will name the provisioned email address as the Google account to use. This addresses the common account-selector ambiguity while leaving Google responsible for authentication and verified-email assertions.

### Treat email content as a tested contract

The invitation endpoint integration tests will assert the HTML CTA label and login destination as well as the corresponding plain-text guidance. The enrolment UAT will cover following the CTA and successfully signing in with the provisioned Google email.

## Risks / Trade-offs

- [Recipients interpret "Accept invitation" as immediate access] → Explain that the action continues to Google sign-in and retain the existing verified-email gate.
- [HTML email clients strip styles or links] → Retain a labelled plain-text URL containing the same login destination.
- [Request-derived public URL is incorrect behind a proxy] → Verify deployed forwarded scheme and host configuration while testing the email journey; do not introduce a separate URL-generation mechanism in this scope.
