# Enrolment And Access UAT Journeys

## Purpose

Use these journeys when a change may affect sign-in, session handling, seller profile details, user defaults, or admin access management.

## Preconditions

- You know which user account to test with.
- For admin checks, the account has administrator access.
- For non-admin checks, use a normal active account.

## Sign-In And Session Smoke

### Steps

1. Open Glovelly.
2. Sign in with the test account.
3. Open Clients, Gigs, and Invoices.
4. Refresh the page.
5. Confirm the same workspace data returns without a new sign-in prompt.
6. Sign out.

### Expected Results

The user can sign in, navigate through the core workspaces, refresh without losing the session, and sign out cleanly.

## Seller Profile And Defaults

### Steps

1. Open seller profile.
2. Add or edit seller name, address, email, and payment details.
3. Save.
4. Generate or redraft an invoice.
5. Download the PDF.

### Expected Results

The invoice PDF reflects seller profile and payment details. Missing profile details produce helpful UI notices rather than broken invoices.

## User Settings

### Steps

1. Open user settings.
2. Change default invoice or mileage settings.
3. Save.
4. Create a new client or gig that uses defaults.
5. Generate an invoice where those defaults should apply.

### Expected Results

Saved defaults are reused in later client, gig, or invoice workflows where expected. Existing records are not unexpectedly overwritten.

## Admin Access

### Steps

1. Open Admin as an administrator.
2. Create or edit a user record.
3. Toggle active state or role.
4. Save.
5. Confirm the user list updates.

### Expected Results

Admin changes persist and non-admin users cannot access admin workflows.

## Inactive User Deletion

### Steps

1. Select an active user.
2. Confirm the `Delete user` button is red and disabled.
3. Edit that user, mark the account inactive, and save.
4. Confirm `Delete user` is enabled.
5. Click `Delete user` and decline the confirmation prompt.
6. Confirm the user remains in the list.
7. Click `Delete user` again and accept the confirmation prompt.

### Expected Results

Only inactive users can be deleted. Active users, including the current administrator account, cannot be deleted.

## Notes

Access changes can lock testers out of an environment. Confirm the target user before changing active state or administrator role.
