# Glovelly UAT Tests

This project contains Playwright-based UAT and regression smoke tests. It is intentionally kept outside `glovelly.sln` so `dotnet test glovelly.sln` remains a fast backend test command.

## Install Browsers

Build the project, then install Chromium for the Playwright version restored by NuGet:

```bash
dotnet build tests/Glovelly.Uat.Tests/Glovelly.Uat.Tests.csproj
pwsh tests/Glovelly.Uat.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
```

CI runs the Release build and installs Linux browser dependencies too:

```bash
dotnet build tests/Glovelly.Uat.Tests/Glovelly.Uat.Tests.csproj --configuration Release
pwsh tests/Glovelly.Uat.Tests/bin/Release/net10.0/playwright.ps1 install --with-deps chromium
```

If PowerShell is not installed, install it first or run the generated `playwright.ps1` script from an environment that has `pwsh` available.

## Run

`GLOVELLY_UAT_BASE_URL` is required and should point at the deployment under test. `GLOVELLY_UAT_SECRET` is required for tests that use staging-only test authentication. `GLOVELLY_UAT_INVOICE_RECIPIENT_EMAIL` is required for invoice delivery tests and should point at a controlled inbox because the core invoice regression sends through the configured email provider.

```bash
GLOVELLY_UAT_BASE_URL=https://staging.glovelly.net dotnet test tests/Glovelly.Uat.Tests/Glovelly.Uat.Tests.csproj
```

To mirror CI diagnostics locally:

```bash
GLOVELLY_UAT_BASE_URL=https://staging.glovelly.net \
GLOVELLY_UAT_SECRET=<secret> \
GLOVELLY_UAT_INVOICE_RECIPIENT_EMAIL=uat-invoices@example.com \
GLOVELLY_UAT_ARTIFACT_DIR=TestResults/uat \
dotnet test tests/Glovelly.Uat.Tests/Glovelly.Uat.Tests.csproj \
  --logger "trx;LogFileName=uat-test-results.trx" \
  --logger "html;LogFileName=uat-test-report.html" \
  --results-directory TestResults/uat/test-results
```

On failure, tests write Playwright trace zips to `TestResults/uat/playwright-traces` and screenshots to `TestResults/uat/screenshots`.

Tests run headless by default. To see the browser:

```bash
GLOVELLY_UAT_BASE_URL=https://staging.glovelly.net GLOVELLY_UAT_HEADLESS=false dotnet test tests/Glovelly.Uat.Tests/Glovelly.Uat.Tests.csproj
```
