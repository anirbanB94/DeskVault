# Contributing to DeskVault

## Engineering Principles

- Build for maintainability over speed.
- Keep the application offline-first.
- Follow Clean Architecture boundaries.
- Prefer readability over cleverness.
- Every architectural decision should have a clear rationale.

## Coding Standards

- Constructor Injection only.
- No business logic inside the UI project.
- One public class per file.
- Use `var` only when the type is obvious.
- Enable nullable reference types.
- No static service locators.
- Avoid utility/helper classes with mixed responsibilities.

## Git Commit Convention

Use Conventional Commits.

Examples:

- feat:
- fix:
- refactor:
- docs:
- test:
- chore:

Each commit should represent one logical change.

## Pull Request Checklist

Before merging:

- Solution builds successfully.
- No compiler warnings.
- Logging verified (where applicable).
- Documentation updated if architecture changed.
- ADR added for significant architectural decisions.
