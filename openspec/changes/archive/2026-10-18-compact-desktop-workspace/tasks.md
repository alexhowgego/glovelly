## 1. Compact shared chrome

- [x] 1.1 Refactor AppShell branding, section identification, navigation, capture, and account controls into a compact accessible header; remove repeated introductory copy and the large mascot introduction without changing sign-in/checking screens.
- [x] 1.2 Restyle existing summaries into a compact strip across all authenticated views, preserving contextual/fallback data, three contextual slots, actions, refresh behavior, and loading/empty/error states.
- [x] 1.3 Add or update frontend component tests for shared controls, contextual and fallback summaries, state rendering, and summary actions.

## 2. Desktop height and scroll ownership

- [x] 2.1 Add explicit shell/view containment for Gigs, Invoices, Clients, and admin Users; propagate shrinkable grid/flex sizing so compact chrome, workspace, and footer fit desktop target viewports.
- [x] 2.2 Bound record panes independently of detail selection, keeping search/filter/action controls outside the records scroll region and column headers visible; ensure records retain usable row heights.
- [x] 2.3 Give long details and open editors independent scrolling, keep closed editor slots height-neutral, and preserve accessible actions and focus outlines.
- [x] 2.4 Implement narrow/short viewport natural-flow fallbacks and preserve document-like settings scrolling, summary wrapping, and access at enlarged text/zoom without globally concealing overflow.
- [x] 2.5 Audit and adapt explicit navigation/scroll targeting for capture-to-gig, invoice-line links, and summary actions while preserving existing visibility and selection rules.

## 3. Regression coverage and documentation

- [x] 3.1 Extend browser UAT for all four workspaces at 1280×800 and 1440×900: assert bounded document geometry, internal list overflow, reachable final records, long details, and editor opening/closing.
- [x] 3.2 Retain and extend #249 empty-data and no-filter-match regressions, including reachable controls when detail selection is absent and recovery by clearing filters.
- [x] 3.3 Add responsive/accessibility checks for 390×844 and 1280×600 layouts, zoom or equivalent effective-viewport coverage, keyboard focus reveal, and naturally scrolling settings; verify menus, capture modals, and notification layering.
- [x] 3.4 Cover explicit gig navigation and summary action journeys in bounded and stacked layouts; update matching docs/uat journeys and pre-merge regression instructions.
- [x] 3.5 Review affected user-guide text and documentation-capture expectations; request real Ubuntu CI capture candidates and review paired light/dark assets before any screenshot replacement, without manufacturing local substitutes. **Resolved by user waiver at archive: CI capture generation and screenshot review were not performed; no screenshots were replaced.**

## 4. Validation

- [x] 4.1 Run frontend unit tests, lint, and production build; fix regressions without expanding the business-logic scope.
- [x] 4.2 Run ./verify.sh before handover of the broad frontend change and record any environmental blockers.
- [x] 4.3 Run browser UAT in Ubuntu CI only, review target viewport geometry and fallback behavior, and confirm no desktop document overflow at target sizes and no clipped controls in fallback layouts. **Resolved by user waiver at archive: browser execution and viewport validation were not performed.**

## 5. First-round review adjustments

- [x] 5.1 Remove the duplicate shell workspace title, summary action buttons, and their unused action/scroll-request wiring; retain invoice filter chips.
- [x] 5.2 Replace the Admin banner with directory-derived summary cards and explicit loading/error states, removing redundant banner markup, props, and styles.
- [x] 5.3 Update requirements, design, regression tests, and UAT documentation for informational summaries and Admin metrics.
- [x] 5.4 Re-run frontend tests, lint/build, full verification, and browser test compilation after the review adjustments; leave browser execution and screenshot review to Ubuntu CI.

## Validation notes

- Final visual refinements: rounded the 36px Gordon icon with a 12px radius and removed the authenticated outer frame's border/background/shadow/padding while preserving the page gutter and individual panels. Deferred header tips are tracked in https://github.com/alexhowgego/glovelly/issues/302; no tips are implemented here.

- Frontend after review adjustments: 50 tests passed; lint and production build passed (existing large-bundle advisory remains).
- Browser test project builds successfully; browser execution deliberately deferred to Ubuntu CI.
- Guide check passed. Guide text and capture expectations updated; real staging light/dark candidates were not generated or reviewed. The user explicitly waived the remaining capture/review checks when archiving.
- Initial ./verify.sh invocation exceeded its 120-second limit during backend tests. The longer-limit rerun passed; verification repeated after review adjustments also passed: 464 backend tests, 50 frontend tests, frontend lint, and production build.
- Tasks 3.5 and 4.3 were resolved by explicit user waiver when archiving, not by successful CI validation. Browser tests compile but were not run, and visual geometry remains unverified. No commits or pushes have been created on the user's behalf.
