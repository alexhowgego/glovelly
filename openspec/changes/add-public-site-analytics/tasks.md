## 1. Analytics Configuration

- [x] 1.1 Configure the GA4 property to retain only the agreed data period and disable Google Signals and advertising personalisation.
- [x] 1.2 Record the final consent-cookie lifetime and Google privacy-information link for the published policy.

## 2. Shared Consent And Analytics Loader

- [x] 2.1 Add the canonical framework-neutral public-site consent and GA4 loader with the public measurement ID and exact production-host allow-list.
- [x] 2.2 Implement accessible accept, reject, and Cookie settings controls with a secure `.glovelly.net` consent preference.
- [x] 2.3 Ensure rejection and withdrawal prevent GA loading and remove accessible GA cookies, while accepted consent loads only ordinary GA4 page views.
- [x] 2.4 Add automated coverage or source-level checks for consent gating, the production-host boundary, and the absence of direct duplicate GA bootstrap code in public shells.

## 3. Public-Site Integration

- [x] 3.1 Package and include the canonical loader in the Astro landing-site output, including its persistent Cookie settings control.
- [x] 3.2 Package and include the canonical loader in the Starlight user-guide output, including its persistent Cookie settings control.
- [x] 3.3 Package and include the canonical loader through the DocFX handbook template, including its persistent Cookie settings control.
- [x] 3.4 Confirm the Menu application has no analytics integration or public-site consent control.

## 4. Privacy And Verification

- [x] 4.1 Update the privacy policy to describe the public analytics purpose, Google Analytics provider information, consent and withdrawal controls, cookie details, and final retention period.
- [x] 4.2 Verify the landing and user-guide builds and the DocFX handbook build succeed with the shared asset packaging.
- [ ] 4.3 Manually verify undecided, accepted, rejected, and withdrawn consent on each production public hostname, including cross-subdomain preference behaviour, network requests, and cookie cleanup.
- [ ] 4.4 Verify analytics is absent from local, staging, preview, automated-test, and Menu hosts, then confirm GA4 receives only production public-site page views after release.
