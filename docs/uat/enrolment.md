# Enrolment And Access UAT Journeys

## Purpose

Use these journeys when a change may affect sign-in, session handling, seller profile details, user defaults, or admin access management.

## Preconditions

- You know which user account to test with.
- For admin checks, the account has administrator access.
- For non-admin checks, use a normal active account.

## Sign-In And Session Smoke

> **Automation:** Partially automated UAT: `Glovelly.Uat.Tests.SmokeTests.SignInEntryPointIsVisible` covers the public sign-in entry point and authenticated UAT tests cover the test-auth session path; real Google sign-in, refresh, and sign-out remain manual.

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

> **Automation:** Backend automated; manual UAT: `Glovelly.Api.Tests.SellerProfileEndpointsTests` covers profile persistence and validation; PDF/default browser checks remain manual.

### Steps

1. Open seller profile.
2. Add or edit seller name, address, email, and payment details.
3. Save.
4. Generate or redraft an invoice.
5. Download the PDF.

### Expected Results

The invoice PDF reflects seller profile and payment details. Missing profile details produce helpful UI notices rather than broken invoices.

## User Settings

> **Automation:** Backend automated; manual UAT: `Glovelly.Api.Tests.AuthEndpointsTests.UpdateSettings_*` covers settings persistence and validation; browser default reuse remains manual.

### Steps

1. Open user settings.
2. Change default invoice or mileage settings.
3. Save.
4. Create a new client or gig that uses defaults.
5. Generate an invoice where those defaults should apply.

### Expected Results

Saved defaults are reused in later client, gig, or invoice workflows where expected. Existing records are not unexpectedly overwritten.

## Admin Access

> **Automation:** Backend automated; manual UAT: admin access APIs have backend coverage; browser role-management flow remains manual.

### Steps

1. Open Admin as an administrator.
2. Create a user record and leave `Email this user an invitation to sign in` checked.
3. Save.
4. Open the invitation as the recipient and confirm it identifies the Google email address to use and provides an `Accept invitation and sign in` button.
5. Follow the button and sign in with Google using that provisioned, verified email address.
6. Confirm Glovelly grants access and subsequent sign-ins continue to work.
7. Return to the administrator session and confirm the user list updates and the status confirms the invitation was sent.
8. Create another user record and clear `Email this user an invitation to sign in`.
9. Save.
10. Confirm the user list updates without an invitation-sent status.
11. Edit a user record.
12. Toggle active state or role.
13. Save.

### Expected Results

Admin changes persist and non-admin users cannot access admin workflows. New users can accept an email invitation only by signing in with the provisioned verified Google email, after which Glovelly enrols that Google identity. Admins can choose not to send the invitation when needed.

## Access-Request Approval

> **Automation:** Backend automated; manual UAT: `Glovelly.Api.Tests.AccessRequestAdminEndpointsTests` covers approval, expiry, decline idempotency, invitation delivery failure, and notification review links.

### Steps

1. Sign in to Google with an unauthorised account with a verified email address and request access.
2. Open the access-request notification received by an active administrator.
3. Follow `Review access request` and, if needed, complete Google sign-in as an administrator.
4. Confirm Glovelly opens the Access requests dialog with the requester selected and the requester email is displayed but cannot be edited.
5. Choose the requester role and active state, leave `Send invitation email` selected, and approve access.
6. Confirm the requester is removed from the pending list and the result confirms provisioning and invitation delivery.
7. Sign in as the requester with the same verified Google email and confirm normal first sign-in grants the provisioned role.
8. Open Access requests from the administrator profile menu, select another pending request, choose Decline, and cancel the confirmation.
9. Confirm the request remains pending, then decline it again and confirm the decline.
10. Confirm declined requests can no longer be approved and a standard user cannot see Access requests in the profile menu.

### Expected Results

Notification links only navigate to an authenticated administrator review; they never provision access by themselves. Approval provisions the stored request identity without retyping the email, preserves Google first-login binding, and optionally sends an invitation. Decline requires confirmation and makes no user changes. Pending request count and profile-menu indicator reflect outstanding review work.

## Inactive User Deletion

> **Automation:** Backend automated; manual UAT: `Glovelly.Api.Tests.AdminEndpointsTests.DeleteUser_WhenInactive_DeletesUser`, `DeleteUser_WhenActive_ReturnsValidationProblem`, and `DeleteUser_WhenCurrentUser_ReturnsValidationProblem`

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
