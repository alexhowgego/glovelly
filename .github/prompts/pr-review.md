# Pull Request Review

Review the current pull request.

## Goal

Perform a focused engineering review of the changed code. Your objective is to identify genuine issues that would improve the quality of the pull request before it is merged.

## Constraints

- Work within a budget of 16 tool calls.
- Reserve enough tool calls to publish the final review.
- Use `mcp_github_pull_request_read` exactly once to retrieve the pull request metadata and diff.
- Inspect only changed files and, where necessary to verify a suspected issue, the nearest referenced types, functions, configuration, or tests.
- Do not inspect unrelated parts of the repository simply to search for additional issues.
- Do not run shell commands.
- Do not scan the entire repository.
- Do not retry failed tool calls.
- Do not call `activate_skill`.
- Use only the provided mcp_github_* tools for GitHub operations; use native read-only file tools for repository context.

## What to report

Only report:

- demonstrable correctness bugs
- security vulnerabilities
- performance regressions
- significant maintainability problems introduced by this pull request

Do **not** report:

- style preferences
- naming suggestions
- formatting
- speculative concerns
- undocumented product assumptions
- duplicate manifestations of the same root cause

Only raise an issue when the changed code and available context establish a concrete failure mode.

## Severity

Classify findings as:

- **CRITICAL** – catastrophic security, data loss or system-wide failure
- **HIGH** – likely production failure or serious vulnerability
- **MEDIUM** – concrete defect with meaningful impact
- **LOW** – genuine maintainability concern (never style)

## Review quality

For every finding:

- explain the concrete failure mode
- explain why it matters
- keep the explanation concise
- attach inline comments only to changed diff lines
- avoid repeating the same issue

## Publishing

If findings exist:

1. Create a pending COMMENT review.
2. Add one inline comment per distinct finding.
3. Submit the pending review with a concise summary.

If no actionable issues are found:

Submit a COMMENT review stating:

> No actionable issues were found in the changed code.