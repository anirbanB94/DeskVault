# DeskVault Engineering Naming Conventions

## Purpose

This document defines the naming conventions used across the DeskVault repository and GitHub development workflow.

The goal is to keep Product Backlog Items, technical tasks, branches, commits, pull requests, labels, and source-code identifiers consistent and easy to understand.

This document is the authoritative naming convention for the project.

---

## 1. Product Backlog Items

### Format

`<Verb> <object/capability>`

### Examples

- Expand support for text-oriented knowledge formats
- Evolve document workspace into multi-document workspaces
- Evolve Search into richer document discovery
- Encrypt Local Database at Rest

### Rules

- Do not use a prefix.
- Start with a strong action verb.
- Describe the product capability rather than the implementation.
- Do not mention classes, libraries, frameworks, or technical mechanisms.
- Use sentence case.
- Keep the title understandable to a non-developer.
- A Product Backlog Item describes the product-level WHAT and WHY.

---

## 2. Technical Tasks

### Format

`task(<area>): <technical action>`

### Examples

- `task(documents): define supported document formats`
- `task(documents): implement JSON and XML text extraction`
- `task(documents): implement JSON and XML document rendering`
- `task(documents): integrate and validate expanded document formats`

### Rules

- Always use the `task(<area>):` prefix.
- Use a stable technical area.
- Describe one concrete engineering outcome.
- Start the action with a verb.
- Keep the task implementation-focused.
- Technical tasks are children of a Product Backlog Item.
- Task titles must not repeat the full Product Backlog Item title.

---

## 3. Technical Areas

Use a controlled vocabulary rather than inventing new area names for individual issues.

Current areas:

- `documents`
- `workspace`
- `search`
- `persistence`
- `security`
- `processing`
- `rendering`
- `ui`
- `testing`
- `build`
- `ci`
- `documentation`
- `database`
- `github`
- `repo`

### Examples

- `task(documents): ...`
- `task(workspace): ...`
- `task(search): ...`
- `task(persistence): ...`
- `task(security): ...`
- `task(rendering): ...`
- `task(database): ...`

If a new technical area is genuinely required, establish it deliberately rather than creating an ad-hoc scope.

---

## 4. Bugs

### Format

`fix(<area>): <problem>`

### Examples

- `fix(documents): handle malformed document processing`
- `fix(search): prevent duplicate search results`

Bug titles describe the incorrect behavior being corrected.

---

## 5. Technical Spikes

### Format

`spike(<area>): <question or investigation>`

### Examples

- `spike(persistence): evaluate encrypted SQLite providers`
- `spike(search): evaluate full-text indexing options`

Spikes are used when investigation is required before the implementation approach can be determined.

---

## 6. Documentation Work

### Format

`docs(<area>): <action>`

### Examples

- `docs(product): update product development operating model`
- `docs(readme): update product goal and MVP details`
- `docs(issue-template): configure issue template chooser`
- `docs(repo): add engineering naming conventions`

---

## 7. GitHub Labels

Use labels to identify the type of work.

Current standard labels:

- `feature`
- `task`
- `bug`
- `spike`
- `documentation`

### Rules

Keep labels semantic and limited.

Do not create labels for implementation details or project fields.

Avoid labels such as:

- `json`
- `xml`
- `sqlite`
- `ef-core`
- `pdf`
- `renderer`
- `sprint1`
- `high`
- `medium`

Use issue content for technical details and GitHub Project fields for priority, estimate, status, and sprint information.

---

## 8. Branches

### Format

`<type>/<short-description>`

### Types

- `feature/`
- `task/`
- `fix/`
- `spike/`
- `docs/`

### Examples

- `feature/document-format-expansion`
- `task/json-xml-extraction`
- `task/json-xml-rendering`
- `task/document-format-integration`
- `task/database-key-protection`
- `fix/document-processing-failure`

### Rules

- Use lowercase.
- Use kebab-case.
- Keep the description concise.
- Do not include the GitHub issue number unless a repository workflow explicitly requires it.

---

## 9. Commits

DeskVault uses Conventional Commits.

### Format

`<type>(<scope>): <imperative description>`

### Standard commit types

- `feat`
- `fix`
- `test`
- `refactor`
- `perf`
- `build`
- `ci`
- `docs`
- `chore`

### Examples

- `feat(documents): add JSON and XML extraction`
- `feat(rendering): add JSON document renderer`
- `feat(rendering): add XML document renderer`
- `feat(database): encrypt local database`
- `feat(security): protect database encryption key`
- `test(documents): validate JSON and XML processing`
- `test(security): verify database key protection`
- `fix(documents): handle malformed JSON extraction`
- `docs(product): update MVP scope`

### Rules

- Use imperative wording.
- Use lowercase after the colon.
- Keep one logical change per commit.
- Use the scope to identify the technical area.
- Do not intentionally include GitHub issue numbers in project-authored commit subjects.
- Reference the GitHub issue in the commit body or footer when appropriate.

### Type vs Scope

Commit types describe the kind of change.

Commit scopes describe the area affected.

For example:

`feat(security): protect database encryption key`

Here:

- `feat` = a new capability/change
- `security` = the affected technical area

`security` is therefore a scope, not a commit type.

---

## 10. Pull Requests

### Format

`<type>(<area>): <short description>`

### Examples

- `feat(documents): add JSON and XML extraction`
- `feat(rendering): add JSON and XML rendering`
- `feat(database): encrypt local database`
- `feat(workspace): support multi-document workspaces`

A Pull Request should normally correspond to one coherent technical task.

### Issue relationship

Use the appropriate GitHub issue reference in the Pull Request body.

For example:

`Closes #26`

or, when automatic closure is not desired:

`Refs #26`

---

## 11. Naming Case Rules

### GitHub issue titles

Use sentence case.

Example:

`Expand support for text-oriented knowledge formats`

### Technical task titles

Use the `task(<area>):` convention with sentence-style wording after the colon.

Example:

`task(documents): implement JSON and XML text extraction`

### Commit messages

Use Conventional Commit syntax with lowercase wording after the prefix.

Example:

`feat(documents): add JSON extraction`

### Branch names

Use lowercase kebab-case.

Example:

`task/json-xml-extraction`

### Labels

Use lowercase.

Example:

`task`

### C# types and classes

Use PascalCase.

Example:

`DocumentTextExtractorResolver`

### C# methods and properties

Use PascalCase.

Examples:

`ExtractAsync`

`DatabasePath`

### Namespaces and folders

Follow the established C# and repository conventions.

---

## 12. Issue Numbers

Do not manually include GitHub issue numbers in issue titles.

Good:

`task(documents): implement JSON and XML text extraction`

Avoid:

`#26 task(documents): implement JSON and XML text extraction`

GitHub already owns the issue number.

Issue numbers may appear naturally in GitHub-generated dependency or PR metadata and do not require rewriting of historical commits.

---

## 13. Scope Rule

Use the smallest meaningful technical scope.

Good:

`task(documents): implement JSON and XML text extraction`

Avoid unnecessarily large scopes such as:

`task(application-documents-processing): implement JSON and XML text extraction pipeline`

The scope should normally correspond to a stable architectural or product area rather than a specific class.

---

## 14. Product-to-Code Naming Relationship

The development hierarchy is:

Product Backlog Item
    |
    +-- Technical Task
            |
            +-- Branch
                    |
                    +-- Commit(s)
                            |
                            +-- Pull Request

Each level has a different purpose.

### Product Backlog Item

Describes:

WHAT the product needs and WHY.

### Technical Task

Describes:

WHAT engineering work is required to deliver part of the Product Backlog Item.

### Branch

Identifies:

WHERE the implementation is being developed.

### Commit

Describes:

WHAT logical code change was made.

### Pull Request

Describes:

WHAT coherent change is being reviewed.

### Label

Describes:

WHAT KIND of work the issue represents.

### GitHub Project fields

Describe:

WHEN, PRIORITY, ESTIMATE, STATUS, and SPRINT information.

---

## 15. Example: Document Format Expansion

Product Backlog Item:

`#21 — Expand support for text-oriented knowledge formats`

Technical tasks:

- `#25 — task(documents): define supported document formats`
- `#26 — task(documents): implement JSON and XML text extraction`
- `#27 — task(documents): implement JSON and XML document rendering`
- `#28 — task(documents): integrate and validate expanded document formats`

Example branch:

`task/json-xml-extraction`

Example commits:

- `feat(documents): add JSON extraction`
- `feat(documents): add XML extraction`
- `test(documents): validate JSON and XML extraction`

Example Pull Request:

`feat(documents): add JSON and XML extraction`

---

## 16. Example: Database Encryption

Product Backlog Item:

`#24 — Encrypt Local Database at Rest`

Example technical task:

`task(security): protect database encryption key`

Example branch:

`task/database-key-protection`

Example commit:

`feat(security): protect database encryption key`

Example Pull Request:

`feat(security): protect database encryption key`

---

## 17. General Rules

- Product language and technical language must remain distinct.
- Prefer clear names over clever names.
- Avoid unnecessary abbreviations.
- Avoid implementation details in Product Backlog Item titles.
- Avoid product-level language in technical task titles.
- Avoid putting metadata into names when GitHub already provides a field for it.
- Keep terminology consistent across issues, documentation, branches, commits, and Pull Requests.
- When an existing convention is sufficient, do not introduce another naming pattern.
- New naming patterns should be introduced only when there is a clear project-level need.
- Existing historical commits do not need to be rewritten solely to conform to this document.

---

## 18. DeskVault Standard

From this point forward, DeskVault follows this naming hierarchy:

Product Backlog Item
    |
    +-- Technical Task(s)
            |
            +-- Branch
                    |
                    +-- Commit(s)
                            |
                            +-- Pull Request

The standard Product Backlog Item format is:

`<Verb> <object/capability>`

The standard Technical Task format is:

`task(<area>): <technical action>`

The standard Bug format is:

`fix(<area>): <problem>`

The standard Technical Spike format is:

`spike(<area>): <question or investigation>`

The standard Documentation format is:

`docs(<area>): <action>`

These conventions should be applied consistently to all new GitHub work and repository development activity.
