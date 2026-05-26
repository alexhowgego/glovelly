# Glovelly UAT Tests

This project contains Playwright-based UAT and regression smoke tests. It is intentionally kept outside `glovelly.sln` so `dotnet test glovelly.sln` remains a fast backend test command.

## Install Browsers

Build the project, then install Chromium for the Playwright version restored by NuGet:

```bash
dotnet build tests/Glovelly.Uat.Tests/Glovelly.Uat.Tests.csproj
pwsh tests/Glovelly.Uat.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
```

If PowerShell is not installed, install it first or run the generated `playwright.ps1` script from an environment that has `pwsh` available.

## Run

`GLOVELLY_UAT_BASE_URL` is required and should point at the deployment under test.

```bash
GLOVELLY_UAT_BASE_URL=https://staging.glovelly.net dotnet test tests/Glovelly.Uat.Tests/Glovelly.Uat.Tests.csproj
```

Tests run headless by default. To see the browser:

```bash
GLOVELLY_UAT_BASE_URL=https://staging.glovelly.net GLOVELLY_UAT_HEADLESS=false dotnet test tests/Glovelly.Uat.Tests/Glovelly.Uat.Tests.csproj
```
