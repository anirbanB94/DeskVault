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

### Configuration composition and precedence

The UI composition root owns runtime configuration construction and establishes the application content root explicitly. Configuration must resolve the shipped defaults consistently, independent of the process's current working directory.

- `appsettings.json` is a required shipped defaults file. If it is missing, application initialization must fail rather than silently proceeding with an incomplete configuration.
- Preserve the default host configuration provider ordering. Shipped JSON settings provide defaults, while supported external sources, including environment variables, override those defaults according to provider precedence.
- Do not append a duplicate `appsettings.json` provider after the host's default configuration setup. A later JSON provider can override values supplied by external providers and defeat the intended override contract.
- Supported configuration sources must be deliberate and tested. DeskVault currently constructs the host with an empty argument collection (`Args = []`); command-line arguments are therefore not an application-supported override channel under this contract unless explicitly introduced and tested.
- Configuration providers are value sources, not inherently protected secret stores. Shipped configuration must not contain actual credentials. Before an in-scope capability introduces a genuine secret, its source, protection mechanism, access boundary, and lifecycle must be explicitly decided.
- Existing DPAPI-backed key protection and key provisioning/retrieval responsibilities remain governed by the established key-management design and #220. This decision does not introduce a general-purpose secret-management subsystem.

#### Automatic-lock policy configuration

The optional automatic-lock policy is supplied through the effective application configuration section `Security:AutomaticLock`.

- When the entire effective section is absent, automatic locking is disabled by default.
- When the section is present, it must supply a valid Boolean `Enabled` value. A present but empty, partial, or malformed section is invalid and must not silently disable automatic locking.
- When `Enabled` is `true`, `InactivityTimeoutMinutes` is required and must be an integer from 1 through 1440, inclusive.
- When `Enabled` is `false`, the timeout may be omitted. If it is supplied, it must still be a valid integer from 1 through 1440, inclusive.
- Validation uses the effective configuration after the supported configuration-provider precedence has been applied. Environment-variable overrides therefore participate in the same validation as shipped JSON values.
- The UI composition root creates one validated, immutable automatic-lock settings snapshot and registers it with the Application services. Runtime configuration reload does not mutate that snapshot; policy changes take effect after application restart.
- Invalid explicit automatic-lock configuration must fail host construction before startup authentication proceeds. Invalid configuration must never be interpreted as permission to run with a weaker or silently disabled policy.
- If host construction fails, `Program.Main` presents a fixed, actionable startup-configuration error and returns without resolving startup authentication, initializing the vault, or creating the main application interface. Exception details are not exposed in the user-facing message.

The Application layer consumes the validated settings abstraction. It does not read configuration files, environment variables, or UI configuration providers directly.

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

### Shared Generic Host lifecycle

The UI composition root owns one shared Generic Host for the lifetime of the DeskVault desktop process.

- The host is built once by the UI composition root and supplies the shared dependency-injection service provider.
- `Program.Main` owns the host lifetime from successful construction through application shutdown.
- The host is started before the startup authentication form is resolved and displayed. Hosted services therefore complete their startup initialization before startup authentication begins.
- The same host remains active through startup authentication, protected database initialization, and the WinForms message loop.
- Hosted services and UI components resolve the same singleton Application services from the shared host. They must not create independent Application security sessions, authentication services, or lifecycle coordinators.
- Host shutdown is required when startup authentication is cancelled, startup initialization fails, the WinForms message loop exits, or an unhandled application-startup failure occurs.
- Hosted services must be stopped before their host-owned dependencies are disposed. Host disposal is attempted even when shutdown reports a failure.
- The host lifetime is not owned by an individual Form or Presenter. Closing a transient form must not independently stop or dispose the shared application host.

The Application layer remains independent of the Generic Host and WinForms. Hosting and scheduling adapters belong to the UI composition boundary; Application services expose framework-neutral contracts and behavior.

New features should generally be implemented as complete vertical slices through the required layers before being exposed through the UI.

## Consequences

### Positive

- Related use-case code remains easy to discover.
- Features can be developed and tested independently.
- Application orchestration remains independent from infrastructure implementations.
- New infrastructure implementations can be introduced without changing use-case logic.
- The architecture scales naturally as new capabilities are added.
- The approach supports future UI, background processing, and API consumers without coupling business logic to presentation technology.
- Automatic-lock configuration is validated once at the composition boundary and supplied to Application services as an immutable policy snapshot.
- Provider precedence remains consistent across automatic-lock settings and the rest of the application configuration.
- A shared host provides one controlled lifetime for hosted services and application dependencies.
- Hosted services can be initialized before startup authentication without placing hosting dependencies in the Application layer.
- Host shutdown and disposal are coordinated independently of individual form lifetimes.

### Negative

- Some concepts may initially appear in multiple feature slices.
- Shared abstractions must be introduced carefully to avoid unnecessary coupling.
- Developers need to understand both vertical slicing and the underlying architectural boundaries.
- Additional discipline is required to prevent business logic from leaking into Infrastructure or UI.
- The UI composition root owns additional startup and shutdown responsibilities for the shared host.
- Invalid automatic-lock configuration prevents application startup until the configuration is corrected.
- Automatic-lock policy changes require an application restart rather than being applied dynamically.

These trade-offs are acceptable because the architecture must preserve clear responsibility boundaries while
providing deterministic configuration, hosted-service initialization, and application lifetime management.

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

The UI composition root uses `HostConfigurator` to construct the application host, load and validate effective configuration,
configure dependency injection, and register hosted services.

`Program.Main` retains ownership of the resulting host for the complete desktop application lifetime.
It starts the host before startup authentication, keeps it available during the WinForms message loop,
and stops and disposes it on all application exit paths.

Automatic-lock policy parsing and validation remain in the UI configuration boundary.
The validated settings are supplied to the framework-neutral Application automatic-lock service through dependency injection.

This separation keeps document acquisition, storage, and knowledge
processing as distinct use-case boundaries while preserving the overall
Domain/Application/Infrastructure/UI architecture.
