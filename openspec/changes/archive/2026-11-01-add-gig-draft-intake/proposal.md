## Why

Unified Add to Glovelly currently interprets non-URL sources through receipt analysis and treats URLs as resources. Booking emails and tour bibles need another possible interpretation: information about one or more new gigs. Issue #284 will deliver staged gig preparation, not live gig creation.

## What Changes

- Extend unified AI analysis to propose Receipt, Resource, or Gig(s), with visible confidence, evidence, and an authoritative Treat as override.
- Support gig extraction from pasted text/email, uploaded PDFs, and authenticated Google Docs content. Source format does not determine intent: a PDF can still be a receipt or resource.
- Prepare existing gig-import batches containing one or more editable drafts; use existing review, duplicate detection, and explicit commit safeguards.
- Preserve submitted sources through analysis, failures, and review, and retain durable batch provenance after temporary intake cleanup.
- Preserve receipt automatic-application preferences and explicit resource confirmation.
- Defer dedicated set-list routing. Set-list URLs remain resources; users go to the gig and invoke existing interpretation afterwards.

## Capabilities

### New Capabilities
- `gig-intake-drafts`: validated extraction, durable source-backed draft preparation, recovery, and handoff to existing gig-import review.

### Modified Capabilities
- `unified-intake`: multi-intent analysis, persistent override, Google Docs content access, and an outcome-specific draft-prepared state alongside existing attachment states.

## Impact

Backend intake models/services/endpoints, Google connection permissions and document reading, AI contracts/validation, import source storage, database migrations, frontend intake state/types/modal and import handoff, integration/unit tests, UAT and user/operator documentation.

No live gigs, clients, expenses, gig-resource relationships, or set lists are created by the new gig-draft route. Existing receipt behaviour is intentionally unchanged.

## Non-goals

- Dedicated set-list intent, interpretation, or chart matching inside intake.
- General URL scraping, Google Sheets gig extraction, Word/Excel uploads, or arbitrary document formats.
- Automatic live-gig creation, automatic attachment of source documents to eventual gigs, or replacement of import commit logic.
