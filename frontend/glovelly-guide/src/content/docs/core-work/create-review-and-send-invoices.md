---
title: Create, review, and send invoices
description: Turn saved work into a checked invoice without sending it by accident.
---

## Create an invoice from a gig

<a class="menu-link" href="https://menu.glovelly.net/workspaces/invoices">Open Glovelly Menu to review an invoice</a>

1. Open a saved gig that has not already been invoiced.
2. Choose the invoice action.
3. Check the generated preview and download the PDF if needed.
4. Open the invoice to review its details and line items.

Glovelly links the gig to the invoice and creates lines from the fee, claimable expenses, and applicable mileage. You can also create one invoice from selected gigs for the same client, or generate a monthly invoice from a client.

The invoice pane shows dates, total, status, PDF readiness, full description, and issue/delivery information directly. Scroll the details region if a long description needs more room. Choose **Line items** to use the existing editor for invoice lines, adjustments, and description changes.

When the pane has enough room and the PDF is current, a first-page thumbnail fits beside the details, centred within the available space. Click it, or focus it and press Enter, to open the full PDF preview. Hover or focus shows page-one help without a permanent caption. The existing **Preview** action is still available if the thumbnail is hidden or cannot load.

The thumbnail reads the saved PDF; viewing an invoice does not generate, repair, issue or send it. If the PDF changes while the thumbnail loads, refresh or reselect the invoice. A concurrent update can briefly leave the supplementary image behind the latest document, so use the full preview to review before sending. Large or unusual PDFs may need the explicit **Preview** action instead.

While a replacement is being prepared, the preview stays in place with a blurred image and **Updating preview…** label. That image cannot be opened until the fresh version is ready. If replacement fails, the same space shows an explanation instead of an old document.

## Review before you send

Draft PDFs show a **DRAFT** watermark on every page, including downloaded copies. To get a clean PDF, change the invoice status to **Issued**, then preview or download the new copy. The invoice pane includes a reminder that issuing removes the watermark. Issuing does not send the invoice; sending is a separate step.

If an older draft says its PDF needs regenerating, choose **Try again** beside the PDF status. Viewing or previewing an invoice does not regenerate it automatically. If regeneration fails, use **Try again** when you are ready; this does not issue or send the invoice.

1. Open the invoice and choose **Send to client**.
2. Check the recipient, subject, message, and current PDF attachment.
3. Add an optional message if it helps.
4. Choose **Send invoice** only when everything looks right.

For a draft, choose **Issue and send** to generate the clean PDF before sending it. Issuance is required; Glovelly does not send watermarked drafts. You can optionally mark linked gigs completed in the same review—there is no extra confirmation. The attachment download in review is still the current draft until you issue.

If issuance fails, no email is attempted. If issuance succeeds but delivery fails, the invoice stays Issued; retry sending from the open review without issuing again. Issued, paid, and overdue invoices use the same email review without another issuance.

For Google Drive, the publication dialog offers **Issue and publish** for a draft or **Publish invoice** for an issued, paid, or overdue invoice. There is no separate browser confirmation. A Cancelled invoice cannot be sent or published: restore it to Draft and issue it first. Local draft preview and download remain available.

The review screen is deliberately the last step. Closing it does not send anything.

<figure class="product-shot">
  <img class="product-shot-light" src="/screenshots/invoice-email-review-light.png" alt="The Review and send invoice dialog showing the recipient, email preview, PDF attachment, and Send invoice button." />
  <img class="product-shot-dark" src="/screenshots/invoice-email-review-dark.png" alt="The Review and send invoice dialog showing the recipient, email preview, PDF attachment, and Send invoice button." />
  <figcaption>The confirmation dialog gives you one last check before sending the invoice.</figcaption>
</figure>

:::note
Set up your [seller profile](/set-up/seller-profile/) before sending invoices so the PDF includes your sender and payment details.
:::

<div class="help-panel"><p>Need help? <a href="mailto:support@glovelly.net">Contact help</a> or <a href="https://github.com/alexhowgego/glovelly/issues/new">report a problem</a>.</p></div>
