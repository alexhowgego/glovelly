# Glovelly Handbook

The Glovelly Handbook is the repo-owned guide for running, testing, and evolving Glovelly.

Glovelly is a personal business platform for self-employed music work. The product covers authenticated access, clients, gigs, gig expenses and receipt attachments, invoice generation, invoice issue/reissue/delivery, seller profile setup, Google Drive invoice publishing, email delivery, admin user management, and a small read-only MCP business data surface.

## Start Here

- [UAT and regression testing](uat/index.md): manual tester journeys for checking the product before release.
- [Domain notes](domain.md): durable business concepts and vocabulary.
- [Engineering](engineering/index.md): architecture, authentication, deployment, data, email, and ADR notes.
- [Testing notes](testing.md): automated and manual verification guidance for engineers.
- [Codebase map](agent-map.md): compact technical orientation for future agent work.
- [Conventions](conventions.md): repo conventions and maintenance notes.
- [Roadmap](roadmap.md): current direction and known follow-up areas.
- [MCP notes](mcp.md): read-only MCP business data surface.

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
