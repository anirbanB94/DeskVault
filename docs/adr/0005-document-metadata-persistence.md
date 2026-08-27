# ADR-0005: Persist Document Metadata with SQLite

## Status

Accepted

## Context

DeskVault initially used an in-memory document repository to establish the document import vertical slice.

While this was sufficient for the initial MVP workflow, document metadata was lost whenever the application exited. DeskVault therefore requires persistent local metadata so imported documents remain available across application restarts.

The persistence solution should also preserve the existing architectural boundaries and provide a reasonable foundation for future document processing, search, indexing, and local AI capabilities.

## Decision

DeskVault will use **SQLite with Entity Framework Core** for persistent document metadata.

The SQLite database will be stored under the user's local application-data directory:

```text
%LOCALAPPDATA%\DeskVault\DeskVault.db
```

Document content will remain outside the database as encrypted `.dvault` files.

```text
%LOCALAPPDATA%\DeskVault\
├── DeskVault.db
├── Documents\
└── Security\
```

SQLite is responsible for document metadata, while the filesystem remains responsible for encrypted document content.

## Expanded Processing Persistence Boundary

The SQLite persistence boundary now extends beyond document metadata to
support the MVP 1 document-processing workflow.

Document metadata remains persisted through the existing document
repository boundary. In addition, document processing requires persistence
for:

- processing execution state
- processing attempt information required by the processing workflow
- the relationship between a document and its derived chunks
- persisted document chunks representing the current successful derived
  processing result

Conceptually:

```text
SQLite Persistence
├── Document metadata
├── Processing state
└── Document chunks
```

The relationship is:

```text
Document
   │
   ├── processing state
   │
   └── derived chunks
```

Processing execution state and derived chunks are persistence concerns
associated with document processing, but they remain separate from the
document's general lifecycle status and from presentation state.

The processing workflow is responsible for publishing a coherent derived
result. Derived chunks must be replaceable so that retries or repeated
processing do not accumulate duplicate content.

The exact processing orchestration and lifecycle rules are defined by
ADR-0008. This ADR establishes only the persistence responsibility and
SQLite boundary for those processing results.

The existing Application/Infrastructure separation remains unchanged:

```text
Application
    ↓
Application-defined persistence abstractions
    ↓
Infrastructure
    ↓
EF Core
    ↓
SQLite
```

EF Core and SQLite-specific types remain inside Infrastructure.

## Current Persistence Scope

The current MVP 1 persistence foundation therefore supports:

```text
Document
    ↓
Persistent metadata
    ↓
Processing state
    ↓
Derived document chunks
```

Encrypted source document content remains stored separately as encrypted
filesystem content and is not moved into SQLite.

This extension does not change the original decision to use SQLite with
Entity Framework Core for local persistence. It extends the persistence
model to support the document knowledge-processing pipeline established by
ADR-0008.

## Architectural Boundaries

The Application layer continues to depend on the repository abstraction:

```text
IDocumentRepository
```

Infrastructure provides the concrete implementation:

```text
Application
    ↓
IDocumentRepository
    ↓
SqliteDocumentRepository
    ↓
EF Core
    ↓
SQLite
```

EF Core and SQLite types remain inside Infrastructure and are not exposed through Domain or Application contracts.

## Persistence Model

The Domain `Document` is intentionally separate from the EF Core persistence entity.

```text
Domain
└── Document

Infrastructure
└── DocumentEntity
```

Infrastructure is responsible for mapping between the persistence and domain representations.

This prevents database-specific concerns from leaking into the Domain model.

## DbContext Lifetime

DeskVault is a WinForms desktop application and does not have a natural HTTP request scope.

Infrastructure therefore uses:

```text
IDbContextFactory<DeskVaultDbContext>
```

The repository creates a short-lived `DbContext` for each persistence operation and disposes it when the operation completes.

This keeps database context lifetime independent from the UI application's lifetime.

## Database Constraints

The document metadata table currently enforces:

* `Id` as the primary key
* required file name
* required display name
* required SHA-256 hash
* unique SHA-256 hash
* required import timestamp
* required document status
* required stored-file path
* an index on `ImportedAt`

The unique SHA-256 constraint provides a database-level safeguard against duplicate documents in addition to application-level duplicate detection.

## Domain Restoration

Creating a new document and restoring an existing persisted document are separate domain operations.

```text
Document.Create(...)
    ↓
Creates a new document

Document.Restore(...)
    ↓
Restores existing persisted state
```

Restoration preserves persisted values such as the original import timestamp and document status rather than applying new-document defaults.

## Database Initialization and Schema Evolution

DeskVault initializes its local SQLite database during application startup.

The database schema is managed through Entity Framework Core migrations.
Infrastructure owns the `DbContext`, migrations, and database initialization
responsibilities.

The application does not access SQLite or EF Core types directly.

The persistence boundary is:

```text
Application
    ↓
Application-defined persistence abstractions
    ↓
Infrastructure
    ↓
EF Core DbContext / Migrations
    ↓
SQLite
```

Using migrations provides explicit schema evolution as the document,
processing, and derived-content persistence model grows.

Database initialization remains separate from application use-case logic
and from the UI lifecycle.

## Current Implementation

The SQLite persistence decision is implemented in the current MVP 1
persistence foundation.

The current persistence model includes:

- document metadata
- processing execution state
- processing attempt information
- document-to-chunk relationships
- persisted document chunks representing the current successful derived result

Document metadata and processing-derived data are persisted through
Infrastructure-owned EF Core persistence while encrypted source document
content remains stored separately as `.dvault` files.

The current implementation uses EF Core migrations for schema evolution and
SQLite for local persistence.

The Application layer remains independent of EF Core and SQLite through
application-defined abstractions.

## Alternatives Considered

### In-memory repository

Rejected as the production persistence mechanism because data is lost when the application exits.

It may remain useful for tests or isolated development scenarios.

### Raw SQLite access

Rejected for the current implementation because DeskVault is expected to grow beyond a single metadata table.

EF Core provides a stronger foundation for future schema evolution, relationships, migrations, and query composition.

### Server database

Rejected for the MVP because DeskVault is intentionally local-first and offline-capable.

A future server-backed implementation could be introduced behind the existing Application abstractions if a deployment scenario requires it.

## Consequences

### Positive

* Document metadata survives application restarts.
* SQLite provides local persistence without requiring a database server.
* EF Core provides a path for future schema growth.
* Domain and Application remain database-agnostic.
* Encrypted document content remains separate from metadata.
* Database-level uniqueness reinforces duplicate detection.
* Persistence can evolve independently of the Domain model.

### Negative

* Infrastructure now has a database dependency.
* SQLite schema evolution requires migrations.
* A separate persistence entity must be maintained alongside the Domain entity.
* Database initialization adds startup work.

These trade-offs are acceptable for the current MVP.

## Result

DeskVault now has a persistent local document metadata layer while retaining encrypted filesystem storage for document content.

The resulting workflow is:

```text
Import
    ↓
Hash
    ↓
Duplicate detection
    ↓
Encrypt
    ↓
Store encrypted content
    ↓
Persist metadata
    ↓
Restart application
    ↓
Restore metadata
    ↓
Open and decrypt document
```

This establishes the persistence foundation for future document processing, search, embeddings, and local AI capabilities.
