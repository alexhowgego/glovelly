# UAT And Regression Testing

[![Glovelly UAT](https://github.com/alexhowgego/glovelly/actions/workflows/uat.yml/badge.svg)](https://github.com/alexhowgego/glovelly/actions/workflows/uat.yml)

These pages capture high-value manual regression journeys for Glovelly. They are written for testers who need to use the product, not inspect the implementation.

The aim is not to test every field. The aim is to walk the product like a user would and catch cross-workflow breakage that automated backend tests or frontend build checks may miss.

## Before You Start

Ask the engineer or release owner which environment to test, then confirm you can sign in.

If you are running Glovelly locally, the usual startup command is:

```bash
./run-dev.sh
```

Use a fresh browser session if possible. If the environment already contains clients, gigs, invoices, or receipts, make a note of the records you plan to use before changing anything.

## Automation Status

Each top-level journey should show its coverage with a short status line. Use these labels consistently:

- **Automated UAT**: fully covered by the named Playwright UAT test.
- **Partially automated UAT**: the named Playwright UAT test covers the main path, but documented variants, manual judgement, or environment checks remain.
- **Backend automated; manual UAT**: server-side rules have automated backend/API coverage, but the browser journey still needs manual UAT.
- **Manual UAT**: no specific automated coverage for this journey yet.
- **Environment/manual UAT**: external services, OAuth, real delivery, or human judgement make the journey manual even where some backend rules are automated.

`Automated UAT` and `Partially automated UAT` mean browser-level Playwright coverage in `tests/Glovelly.Uat.Tests`, not just backend integration tests.

When adding or changing Playwright UAT coverage, update the matching UAT page in the same pull request so release reviewers can see what still needs human attention.

## Regression Pages

- [Pre-merge regression](pre-merge-regression.md): the main end-to-end checklist before shipping changes.
- [Invoices](invoices.md): invoice creation, regeneration, preview, status, and delivery journeys.
- [Expenses](expenses.md): receipts, quick receipts, reimbursement, and expense statement journeys.
- [Imported gigs](gig-imports.md): MCP-staged gig imports, review, autosave, accept/reject, notification dots, and commit.
- [Google Calendar](calendar.md): Calendar connection, eligible gig statuses, and asynchronous sync expectations.
- [Enrolment and access](enrolment.md): sign-in, seller profile, settings, and admin access checks.

> **Automation:** Partially automated UAT: `Glovelly.Uat.Tests.SmokeTests` covers public smoke endpoints and sign-in entry point visibility; journey-specific automation is noted on each page.

## Good Testing Notes

When a journey fails, record:

- The environment and browser used.
- The client, gig, invoice, or user record involved.
- The step where the actual result first differed from the expected result.
- Any visible error message.

Do not continue changing the same records after a serious failure unless the engineer or release owner asks you to. A clean reproduction is more useful than a heavily edited test record.
