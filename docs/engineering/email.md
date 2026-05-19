# Email

Glovelly sends outbound transactional email through an application email abstraction.

The current production provider is Resend.

## Boundary

Email sending is abstracted behind `IEmailSender`.

Domain workflows should ask the application to send a message; they should not depend directly on Resend APIs or provider-specific message shapes. Provider-specific behavior belongs in provider adapters and support services.

This keeps invoice delivery, access-request notifications, and future transactional email workflows flexible if the provider changes.

## Current Modes

Email mode is configured under `Email:Mode`:

- `Resend`: send through Resend.
- `Disabled`: use a null sender.
- any other value: use the logging sender.

The Resend adapter requires `Email:Resend:ApiKey`.

## Message Configuration

Sender addresses and display names are configuration, not domain constants. Invoice and access-request email settings can differ.

Production sender addresses and the Resend API key are secrets or runtime configuration and must not be committed to source control.

## Delivery Workflows

Invoice email delivery flows through invoice delivery services and the email delivery channel. Messages can include generated invoice PDFs as attachments.

Access-request/admin notification flows also use `IEmailSender` so local/testing modes can avoid real email delivery.

Tests should assert email behavior through fakes rather than depending on Resend.

## Future Operational Concerns

Likely follow-up areas:

- delivery status capture
- bounce handling
- Resend webhook processing
- retry policy and operational visibility
- support workflows for failed or rejected delivery

