# DeskVault Estimation Conventions

## Purpose

This document defines how estimation is used across the DeskVault product backlog and technical work items.

The goal is to keep estimation consistent during backlog refinement, sprint planning, and implementation while distinguishing Product Backlog Item sizing from technical-task sizing.

This document defines the DeskVault project convention. It does not claim that Scrum requires this specific estimation model.

---

## Estimation Model

DeskVault uses relative estimation for Product Backlog Items and technical work items.

Story points represent relative size rather than:

- exact implementation hours
- calendar duration
- developer productivity
- a fixed conversion to time

Estimation considers factors such as:

- implementation complexity
- amount of work
- uncertainty
- technical risk
- integration effort
- testing effort
- architectural impact

---

## Estimation Scale

DeskVault uses the following relative scale:

```text
1 → 2 → 3 → 5 → 8 → 13
```

The scale is intentionally non-linear. Larger values indicate greater relative size and usually greater uncertainty or complexity.

Estimates should be compared against previously completed work where useful rather than interpreted as absolute units of time.

---

## Product Backlog Item Estimates

A Product Backlog Item (PBI) is estimated as a product-level unit of work.

The PBI estimate represents the relative size of the overall product capability described by that item, including the implementation, integration, testing, and other work required for the PBI to satisfy its acceptance criteria.

Example:

```text
#21 — Expand support for text-oriented knowledge formats
Estimate: 13
```

A PBI estimate should not be obtained by mechanically summing technical-task estimates.

Technical-task decomposition can inform the estimate, but the PBI remains the authoritative product-level sizing unit.

---

## Technical Task Estimates

Technical Tasks are implementation units that support a PBI or other engineering work.

Technical-task estimates are used to understand the relative size of individual implementation activities and to support decomposition and sprint planning.

A technical-task estimate does not change the estimate of its parent PBI.

Technical-task estimates should consider the work necessary to complete the task, including relevant implementation, testing, integration, and verification.

When a Technical Task is clearly understood, it may be estimated independently from the parent PBI.

---

## When to Estimate

Estimation should normally occur during backlog refinement once enough information is available to understand:

- the intended outcome
- acceptance criteria
- scope
- dependencies
- major technical considerations
- significant uncertainties

A PBI should not be treated as precisely estimated when important scope or technical uncertainty remains unresolved.

A spike may be used when investigation is required before reliable estimation is possible.

---

## Re-estimation

Estimates may be revised when new information materially changes the understanding of the work.

Examples include:

- significant scope changes
- newly discovered technical complexity
- previously unknown dependencies
- architectural discoveries
- substantial reduction or increase in uncertainty

Re-estimation should reflect the current understanding of the work rather than attempt to preserve a historical estimate.

Historical estimates should not be rewritten merely because actual implementation effort differed from the estimate.

---

## Estimation and Sprint Planning

Estimates support Sprint planning but do not determine Sprint duration or guarantee delivery within a specific period.

Sprint selection should consider the Sprint Goal, available capacity, dependencies, and the relative size of the work.

Story points must not be converted into a fixed number of hours or days for planning purposes.

---

## Estimation and Actual Effort

Actual implementation time may differ substantially from the relative estimate.

This does not automatically indicate that the estimate was incorrect.

Estimation is intended to communicate relative size and uncertainty for planning and comparison, not to measure individual performance or productivity.

Velocity or completed-point totals, when used, are historical planning observations and should not be treated as an individual performance metric or as a guaranteed delivery rate.

---

## Estimation Guidelines

During refinement:

1. Understand the PBI outcome and acceptance criteria.
2. Identify major implementation and integration considerations.
3. Identify significant uncertainty or risk.
4. Compare the work with previously completed items where useful.
5. Assign the relative size that best represents the current understanding.
6. Revisit the estimate when material new information emerges.

Avoid false precision. When two items are difficult to distinguish reliably, they may reasonably receive the same estimate.

---

## Estimation Principle

The DeskVault estimation convention can be summarized as:

> Story points communicate relative size and uncertainty; they are not units of time.

Estimates are planning inputs, not delivery commitments.
