# ADR-0002: Vertical Slice Architecture

## Status

Accepted

## Context

DeskVault is being designed as an offline-first enterprise knowledge platform with a strong emphasis on maintainability, extensibility, and security.

The application will contain multiple business capabilities such as document import, document management, AI processing, indexing, and search.

A traditional layer-first structure can cause related code for a single business capability to become distributed across large folders and projects. As the application grows, this can make features harder to understand, modify, and test.

DeskVault therefore needs an organizational approach that keeps business capabilities cohesive while preserving the dependency boundaries between Domain, Application, Infrastructure, and UI.

## Decision

DeskVault will use a vertical-slice approach for organizing application capabilities while maintaining clean architectural boundaries between projects.

Each business capability will be organized around its use case.

For example, document import is represented by an application slice containing:

- Command
- Handler
- Validator
- Result
- Result status
- Supporting contracts

The architectural responsibilities remain separated:

- **Domain** contains business entities, domain rules, and domain behavior.
- **Application** contains use cases, orchestration, application contracts, and interfaces required by those use cases.
- **Infrastructure** implements application interfaces for persistence, storage, hashing, AI, and other external concerns.
- **UI** handles presentation and composition of the application.
- **Shared** contains only genuinely cross-cutting primitives that do not belong to a specific business capability.

Dependency injection is configured at the composition root so that application code depends on abstractions rather than infrastructure implementations.

### MVP presentation and composition

The WinForms UI follows a Passive View MVP pattern.

- Forms implement View interfaces.
- Presenters depend on View interfaces rather than concrete Forms.
- Presenters receive their View at construction time and subscribe to View events.
- Forms must not receive concrete Application handlers merely to construct their Presenters.
- Application and infrastructure dependencies are supplied through the composition root.
- When a Presenter requires an already-created View instance, a Presenter factory may be used to compose the Presenter with that View while allowing its remaining dependencies to be supplied by dependency injection.
- The View is not registered as its own View interface implementation when doing so would create a circular dependency.
- Presenters must not use late View attachment as a substitute for a required View dependency.

New features should generally be implemented as complete vertical slices through the required layers before being exposed through the UI.

## Consequences

### Positive

- Related use-case code remains easy to discover.
- Features can be developed and tested independently.
- Application orchestration remains independent from infrastructure implementations.
- New infrastructure implementations can be introduced without changing use-case logic.
- The architecture scales naturally as new capabilities are added.
- The approach supports future UI, background processing, and API consumers without coupling business logic to presentation technology.

### Negative

- Some concepts may initially appear in multiple feature slices.
- Shared abstractions must be introduced carefully to avoid unnecessary coupling.
- Developers need to understand both vertical slicing and the underlying architectural boundaries.
- Additional discipline is required to prevent business logic from leaking into Infrastructure or UI.

## Implementation Notes

DeskVault's document import workflow is implemented as a vertical slice
through the Application and supporting architectural boundaries.

Its current flow is:

1. Validate the import command.
2. Compute the document SHA-256 hash.
3. Check for an existing document with the same hash.
4. Store the source content through the storage abstraction.
5. Create the Domain `Document` aggregate.
6. Persist document metadata through the repository abstraction.
7. Return an application result.

Infrastructure provides the concrete implementations for persistence,
encrypted document storage, hashing, and related platform concerns.

The current persistence implementation uses SQLite with Entity Framework
Core, while document content is stored separately as encrypted `.dvault`
files under application-managed local storage.

The Application layer remains independent of these infrastructure
implementations through application-defined abstractions.

The document processing workflow is a separate Application-layer vertical
slice. It reads the stored document, performs extraction, normalization,
and chunking, and persists the resulting processing state and derived
document chunks.

This separation keeps document acquisition, storage, and knowledge
processing as distinct use-case boundaries while preserving the overall
Domain/Application/Infrastructure/UI architecture.
