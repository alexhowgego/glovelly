## Why

After saving a quick receipt or attachment, `Go to gig` selects and opens the matching gig but can leave its list row outside the viewport. This makes the navigation appear incomplete and obscures which gig received the new item.

## What Changes

- Scroll the selected Gig overview into view when `Go to gig` is chosen from a quick receipt.
- Scroll the selected Gig overview into view when `Go to gig` is chosen from a quick attachment.
- Preserve the existing selected-gig, filter-reveal, and gig-editor behavior.
- Cover the navigation result in the browser/UAT journey rather than reopening the gig in the test.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `gig-list-visibility`: Explicit quick-add navigation must reveal the selected gig row in the viewport.
- `uat-browser-automation`: Browser coverage must verify quick-add navigation opens the target gig without a follow-up manual selection.

## Impact

- Frontend gig workspace state and gig-list rendering.
- Quick receipt and quick attachment navigation callbacks.
- Playwright UAT coverage and quick-capture UAT documentation.
- No API, persistence, or dependency changes.
