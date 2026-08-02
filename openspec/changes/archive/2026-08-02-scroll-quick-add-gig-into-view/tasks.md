## 1. Gig Navigation

- [x] 1.1 Add an opt-in Gig overview scroll request to the gig workspace and trigger it only for explicit quick-capture `Go to gig` navigation.
- [x] 1.2 Make the gig-list component scroll the selected Gig overview into view with the established smooth, start-aligned behavior, cleaning up pending scroll work.
- [x] 1.3 Wire both quick receipt and quick attachment `Go to gig` handlers to request the selected-row reveal without changing automatic draft-save behavior.

## 2. Verification

- [x] 2.1 Update the quick-attachment Playwright UAT to verify `Go to gig` directly reveals the target Gig overview rather than reopening the gig in the test.
- [x] 2.2 Add or extend browser coverage for quick-receipt `Go to gig` navigation and target overview visibility.
- [x] 2.3 Update quick receipt and attachment UAT documentation to describe the navigation visibility check and coverage.
- [x] 2.4 Run the relevant frontend checks and targeted UAT coverage.
