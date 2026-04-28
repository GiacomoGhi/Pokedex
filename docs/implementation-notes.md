# Implementation Notes

This project was implemented with AI assistance (Claude Code). The detailed step-by-step
implementation plan is in [claude-plan.md](claude-plan.md).

## My approach

I provided the project scaffolding and requirements upfront, then guided Claude through a
TDD-driven implementation, reviewing each step before proceeding to the next.

## Initial constraints I set

- **TDD** — failing tests written before production code, one commit per logical step.
- **Result pattern** — all service methods return `Result<T>` rather than throwing on expected failures.
- **Architecture conventions** — endpoint registration, DI extension, and coding style modelled under my explicit guidance on an existing codebase I own.
- **Caching** — in-memory, entry-count bounded (`Size = 1` per entry), config-driven TTLs.
- **Rate limiting** — per-IP fixed-window via ASP.NET Core's built-in middleware.

## Key decisions I made during implementation

- **Integration tests** — After the service unit tests were written I explicitly requested integration tests against the live PokeAPI and FunTranslations mirror, organized under `IntegrationTests/`.
- **Shared test fixture** — I proposed consolidating the repeated per-test HTTP client setup into a single `IntegrationTestFixture : IAsyncDisposable` shared via a base class, eliminating the boilerplate duplication Claude had initially written.
- **IntegrationTestBase** — I created this base class myself and asked Claude to validate the approach and fix a typo / namespace issue.
