# DeskVault Engineering Naming Conventions

## 1. Purpose

This document defines the naming conventions used across the DeskVault repository and development workflow.

The goal is to keep names consistent, predictable, and easy to understand across GitHub Issues, branches, commits, pull requests, code, and documentation.

These conventions apply unless a documented architectural or platform constraint requires an exception.

---

## 2. Product Backlog Item Naming

Product Backlog Items (PBIs) represent product-level capabilities, outcomes, or user value.

### Format

```text
<Verb> <object or capability>
```

### Rules

* Use an action-oriented verb.
* Describe the product-level **WHAT** or desired outcome.
* Do not describe implementation details or technical mechanisms.
* Keep the title concise.
* Use sentence case.
* Do not include issue numbers.
* Do not include sprint, priority, estimate, or status information.

### Examples

```text
Expand support for text-oriented knowledge formats
Evolve document workspace into multi-document workspaces
Evolve Search into richer document discovery
Encrypt Local Database at Rest
Establish document knowledge provenance and traceability
```

### Avoid

```text
Add SQLite tables for workspaces
Implement JSON parser
Create provenance service
Sprint 2 - Search improvements
#23 - Evolve Search
```

The first three describe implementation rather than product outcomes. The last two contain execution metadata that belongs elsewhere.

---

## 3. Technical Task Naming

Technical Tasks represent implementation work required to deliver a PBI or maintain the product.

### Format

```text
task(<area>): <technical action>
```

### Rules

* Describe the specific technical implementation or engineering action.
* Keep the title concise and actionable.
* Use sentence case for the description.
* Use a controlled area that identifies the primary technical domain.
* Do not restate the entire parent PBI when a more specific implementation action can be stated.
* Do not include sprint, priority, estimate, or status information.
* A Technical Task should normally be associated with a parent PBI when it exists to deliver that PBI.

### Controlled areas

Use one of the following areas where applicable:

```text
documents
workspace
search
persistence
security
processing
rendering
ui
testing
build
ci
documentation
database
github
repo
```

### Examples

```text
task(documents): add structured text extraction
task(processing): persist processing execution state
task(search): expose document-level result contract
task(database): configure encrypted SQLite provider
task(documentation): update naming conventions
```

### Avoid

```text
task(search): improve search
task(search): implement feature
task(documents): work on documents
task(search): Sprint 2 search work
```

The first three are too vague to communicate the implementation work. The last contains execution metadata that belongs in the GitHub Project.

### PBI and Technical Task distinction

The distinction is:

```text
PBI:
Evolve Search into richer document discovery

Technical Tasks:
task(search): add document discovery filters
task(search): implement document result aggregation
task(search): add search result ordering
```

The PBI describes the **product capability or outcome**.

The Technical Tasks describe the **technical work required to deliver it**.

---

## 4. Bug Naming

Bugs represent incorrect, broken, or unexpected existing behavior.

### Format

```text
fix(<area>): <problem>
```

### Rules

* Describe the behavior or problem being corrected.
* Keep the title concise.
* Use sentence case.
* Identify the affected technical area where practical.
* Do not describe only the proposed solution.

### Examples

```text
fix(documents): handle corrupt source artifacts
fix(search): preserve result ordering
fix(rendering): prevent invalid document layout
```

---

## 5. Spike Naming

Spikes are time-bounded investigations used to reduce technical or architectural uncertainty.

### Format

```text
spike(<area>): <question or investigation>
```

### Rules

* Describe the question being investigated.
* Identify the technical area.
* Avoid prematurely specifying the implementation.
* The outcome should reduce a known uncertainty or support a subsequent decision.

### Examples

```text
spike(database): evaluate encrypted SQLite providers
spike(search): investigate relevance strategies
spike(processing): evaluate cancellation behavior
```

---

## 6. Documentation Work Naming

Documentation-only work uses:

```text
docs(<area>): <action>
```

### Examples

```text
docs(architecture): document processing boundaries
docs(adr): clarify persistence decision
docs(documentation): update engineering conventions
```

When documentation work is tracked as a Technical Task under a PBI, use the Technical Task format:

```text
task(documentation): <technical action>
```

---

## 7. GitHub Issue Hierarchy

DeskVault uses the following work hierarchy:

```text
Epic
  └── Product Backlog Item (PBI)
        └── Technical Task / Bug / Spike
              └── Pull Request
```

PBIs represent product outcomes.

Technical Tasks, Bugs, and Spikes represent the implementation, correction, or investigation work required to achieve those outcomes.

GitHub sub-issues are used to represent the relationship between a PBI and its child implementation work.

---

## 8. Labels and Project Fields

Labels identify the broad issue type.

The primary issue-type labels are:

```text
feature
technical-task
bugs
spike
documentation
```

The meanings are:

| Label            | Meaning                    |
| ---------------- | -------------------------- |
| `feature`        | Product Backlog Item (PBI) |
| `technical-task` | Technical Task             |
| `bugs`           | Bug                        |
| `spike`          | Technical Spike            |
| `documentation`  | Documentation work         |

The `feature` label identifies an issue as a **PBI**.

Project fields are used for execution and planning metadata, including:

* Status
* Priority
* Estimate
* Sprint
* Other project-specific planning fields

Do not encode these values into issue titles or create labels for information already represented by Project fields.

---

## 9. Branch Naming

Branches use the following format:

```text
<type>/<issue-number>-<short-description>
```

Feature implementation branches use:

```text
feat/<issue-number>-<short-description>
```

A `feat/` branch is the standard implementation branch for feature and Technical Task work.

The branch normally corresponds to the GitHub issue being implemented, whether that issue is a PBI or a Technical Task.

### Examples

```text
feat/23-richer-document-discovery
feat/74-reliable-document-processing
feat/75-stable-chunk-identity
fix/81-corrupt-document-handling
spike/82-encrypted-database-provider
```

The branch prefix identifies the implementation workflow and does not need to match the GitHub issue label exactly.

---

## 10. Commit Naming

Commits follow Conventional Commits.

### Format

```text
<type>(<scope>): <description>
```

### Examples

```text
feat(search): add document discovery filters
feat(documents): add structured text extraction
fix(processing): preserve failed execution state
refactor(persistence): separate document and chunk storage
test(search): add relevance ordering coverage
docs(architecture): clarify processing boundaries
```

The commit should describe the change introduced by that commit, not necessarily repeat the full GitHub issue title.

---

## 11. Pull Request Naming

Pull request titles should follow Conventional Commit style where practical.

### Format

```text
<type>(<scope>): <description>
```

### Examples

```text
feat(search): evolve search into richer document discovery
feat(documents): expand text-oriented format support
fix(processing): handle cancelled document processing
docs(documentation): update engineering conventions
```

The PR description should reference the relevant GitHub issue and explain the implementation, validation, and any relevant architectural considerations.

---

## 12. Scope Naming

Scopes should use the same controlled vocabulary used by Technical Tasks where possible:

```text
documents
workspace
search
persistence
security
processing
rendering
ui
testing
build
ci
documentation
database
github
repo
```

Use the smallest meaningful scope.

Avoid introducing new scopes for individual classes, files, or implementation details unless there is a clear repository-wide need.

---

## 13. General Naming Rules

Across the repository:

* Prefer clear, descriptive names over abbreviations.
* Use established domain terminology consistently.
* Avoid names that describe temporary implementation details when a stable domain concept exists.
* Keep terminology consistent between code, issues, ADRs, and product documentation.
* Do not encode workflow metadata into product or technical work titles.
* Do not use sprint numbers, estimates, priorities, or statuses as part of work titles.
* Preserve established names when changing them would create unnecessary historical or technical churn.

---

## 14. Examples

| Work type      | Label            | Example                                                      |
| -------------- | ---------------- | ------------------------------------------------------------ |
| PBI            | `feature`        | `Expand support for text-oriented knowledge formats`         |
| PBI            | `feature`        | `Evolve document workspace into multi-document workspaces`   |
| Technical Task | `technical-task` | `task(documentation): update naming conventions`             |
| Technical Task | `technical-task` | `task(processing): persist processing execution state`       |
| Bug            | `bugs`           | `fix(search): preserve result ordering`                      |
| Spike          | `spike`          | `spike(database): evaluate encrypted SQLite providers`       |
| Documentation  | `documentation`  | `docs(architecture): clarify processing boundaries`          |
| Commit         | —                | `feat(search): add document discovery filters`               |
| Pull Request   | —                | `feat(search): evolve search into richer document discovery` |

This document is the authoritative reference for naming work items and development artifacts in the DeskVault repository.
