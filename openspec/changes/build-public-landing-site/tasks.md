## 1. Establish The Landing-Site Project

- [x] 1.1 Create a standalone Astro project under `frontend/` with local development, lint/check, and production build scripts, plus a repo-level local launcher.
- [x] 1.2 Configure Astro for static output and add the landing site's own package, TypeScript, formatting, and asset configuration without importing the authenticated React application.
- [x] 1.3 Add documented public visual tokens for editorial and interface typography, colour, spacing, surfaces, focus states, and reduced-motion behaviour.

## 2. Build The Product Story

- [x] 2.1 Implement the semantic page structure and responsive header with a clear route to the user guide and a primary `Open Glovelly Menu` link to `https://menu.glovelly.net`.
- [x] 2.2 Implement the hero with the approved promise, working-musicians-and-small-creative-businesses audience statement, primary CTA, and an accessible art-directed composition.
- [x] 2.3 Implement the supporting "A gig is rarely just a gig" story using concrete, warm, lightly humorous copy.
- [x] 2.4 Implement the three outcome-led sections for planning paid work, recording business information, and understanding invoices, payments, and the tax year, each with a responsive art-directed interface composition.
- [x] 2.5 Implement the closing Menu invitation and footer links to the user guide, handbook, GitHub repository, privacy policy, and terms of service using their canonical public URLs.
- [x] 2.6 Review page copy and composition labels to avoid feature-catalogue language, advisory claims, and representations that imply compositions are literal current-product screenshots.

## 3. Meet Public-Site Quality Requirements

- [ ] 3.1 Verify semantic landmarks, heading hierarchy, keyboard navigation, visible focus states, meaningful alternative text, and decorative-art treatment for every composition.
- [ ] 3.2 Verify the layout, navigation, text, and compositions at narrow mobile, tablet, and desktop viewports without horizontal overflow or clipped controls.
- [x] 3.3 Implement and verify reduced-motion behaviour and confirm the site adds no authentication calls, analytics, tracking, embeds, forms, or non-essential browser storage.
- [x] 3.4 Run the landing-site production build and static checks, and record any manual accessibility/responsive verification in the appropriate project documentation or UAT material.

## 4. Deploy And Cut Over Safely

- [x] 4.1 Add a least-privilege GitHub Actions build, preview, and Firebase Hosting deployment path for the `landing` (`glovelly-landing`) target, reusing the established GCP identity model.
- [ ] 4.2 Deploy and verify the landing artifact at its Firebase Hosting platform or preview URL before attaching the production apex.
- [ ] 4.3 Confirm the handbook-domain migration is merged and that the final user-guide, handbook, legal, Menu, and GitHub destinations resolve correctly.
- [ ] 4.4 After the `reorganize-public-routing` Menu smoke checks and overlap window complete, attach `glovelly.net` to the verified Firebase landing target using its documented cutover and rollback procedure.
- [ ] 4.5 Run and record post-cutover smoke checks for the apex content, Menu CTA, guide, handbook, GitHub, privacy, and terms links, certificate state, responsive navigation, and no-tracking baseline.
