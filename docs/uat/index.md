# UAT And Regression Testing

These pages capture high-value manual regression journeys for Glovelly. They are written for testers who need to use the product, not inspect the implementation.

The aim is not to test every field. The aim is to walk the product like a user would and catch cross-workflow breakage that automated backend tests or frontend build checks may miss.

## Before You Start

Ask the engineer or release owner which environment to test, then confirm you can sign in.

If you are running Glovelly locally, the usual startup command is:

```bash
./run-dev.sh
```

Use a fresh browser session if possible. If the environment already contains clients, gigs, invoices, or receipts, make a note of the records you plan to use before changing anything.

## Regression Pages

- [Pre-merge regression](pre-merge-regression.md): the main end-to-end checklist before shipping changes.
- [Invoices](invoices.md): invoice creation, regeneration, preview, status, and delivery journeys.
- [Expenses](expenses.md): receipts, quick receipts, reimbursement, and expense statement journeys.
- [Enrolment and access](enrolment.md): sign-in, seller profile, settings, and admin access checks.

## Good Testing Notes

When a journey fails, record:

- The environment and browser used.
- The client, gig, invoice, or user record involved.
- The step where the actual result first differed from the expected result.
- Any visible error message.

Do not continue changing the same records after a serious failure unless the engineer or release owner asks you to. A clean reproduction is more useful than a heavily edited test record.
