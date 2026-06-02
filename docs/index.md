# Glovelly Handbook

The Glovelly Handbook is the repo-owned guide for running, testing, and evolving Glovelly.

Glovelly is a personal business platform for self-employed music work. The product covers authenticated access, clients, gigs, gig expenses and receipt attachments, invoice generation, invoice issue/reissue/delivery, seller profile setup, Google Drive invoice publishing, Google Calendar gig sync, email delivery, admin user management, MCP business data tools, and staged imported-gig review.

## Start Here

- [UAT and regression testing](uat/index.md): manual tester journeys for checking the product before release.
- [About Glovelly](about.md): what Glovelly is, why it exists, and the spirit of the project.
- [Domain notes](domain.md): durable business concepts and vocabulary.
- [Privacy policy](privacy.md): public privacy notice for Glovelly.
- [Application terms](terms.md): public terms of service for Glovelly.
- [Engineering](engineering/index.md): architecture, authentication, deployment, data, email, and ADR notes.
- [Testing notes](testing.md): automated and manual verification guidance for engineers.
- [Codebase map](agent-map.md): compact technical orientation for future agent work.
- [Conventions](conventions.md): repo conventions and maintenance notes.
- [Roadmap](roadmap.md): current direction and known follow-up areas.
- [MCP notes](mcp.md): MCP business data and staged gig import surface.

## How To Use This Handbook

Use these Markdown files as the canonical source of truth. The published handbook is generated from this tree with DocFX and GitHub Pages.

For release confidence, start with the UAT section. For code changes, use the testing and conventions pages alongside the source files they reference.

## Local Preview

Restore the repo-local .NET tools:

```bash
dotnet tool restore
```

Build and serve the handbook from the repo root:

```bash
dotnet tool run docfx docs/docfx.json --serve
```

The generated site is written to `docs/_site`, which is ignored by the DocFX build configuration.
