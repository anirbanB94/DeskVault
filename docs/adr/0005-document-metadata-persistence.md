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
- the processing-rule version that produced the current successful derived
  document result
- the chunking-rule version that produced each persisted derived chunk
- optional source-location provenance associated with processed knowledge

Conceptually:

```text
SQLite Persistence
├── Document metadata
│   └── LastSuccessfulProcessingRuleVersion?
├── Processing state
└── Document chunks
    ├── stable chunk identity
    ├── document relationship
    ├── ordering
    ├── content identity
    ├── processing provenance
    ├── chunking-rule version
    └── optional source-location provenance
```

The relationship is:

```text
Document
   │
   ├── processing state
   │
   ├── last successful processing generation
   │
   ├── last successful processing-rule version
   │
   └── derived chunks
       ├── processing generation
       ├── chunking-rule version
       └── optional source-location provenance
```

Processing execution state and derived chunks are persistence concerns
associated with document processing, but they remain separate from the
document's general lifecycle status and from presentation state.

The processing workflow is responsible for publishing a coherent derived
result. Derived chunks must be replaceable so that retries or repeated
processing do not accumulate duplicate content.

Processing-rule version and chunking-rule version are descriptive lineage
metadata for the same successful derived representation. They must become
current only as part of the same successful publication that makes the
corresponding derived representation authoritative.

The processing orchestration and semantic-processing boundary are described
by ADR-0008. The authoritative processing lifecycle, processing-generation
fencing, cancellation and failure behavior, and retry/idempotency rules are
defined by ADR-0010. This ADR establishes only the persistence responsibility
and SQLite boundary for those processing results.

The source-location semantics and canonical chunk provenance contract are
defined by ADR-0011.

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
Successful processing lineage
    ├── processing generation
    └── processing-rule version?
    ↓
Derived document chunks
    ├── processing generation
    ├── chunking-rule version?
    └── optional source-location provenance
```

Encrypted source document content remains stored separately as encrypted
filesystem content and is not moved into SQLite.

Source-location information is persistence metadata attached to the
processed knowledge unit. It does not replace the encrypted source file
or introduce a separate provenance store.

### Version Lineage Persistence

The persistence model stores `LastSuccessfulProcessingRuleVersion` on the
document and `ChunkingRuleVersion` on each persisted document chunk.

These values describe the rules that produced the same successful derived
representation identified by `LastSuccessfulProcessingGeneration` and the
persisted chunk `ProcessingGeneration` values.

Successful publication therefore persists the following lineage together:

```text
ONE SUCCESSFUL PUBLICATION
    ├── derived chunks
    ├── chunk processing generation
    ├── chunking-rule version
    ├── Document.Status = Available
    ├── LastSuccessfulProcessingGeneration
    └── LastSuccessfulProcessingRuleVersion
```

The version fields do not become authoritative independently. A lower-level
chunk replacement operation must not publish or advance the document's
current processing-rule version by itself.

When a later successful processing attempt replaces the current derived
representation, the new processing-rule version and chunking-rule version
values are persisted with that same successful result. Failed, cancelled,
or stale processing attempts do not replace the previously successful
version lineage.

Historical persisted knowledge for which rule-version information is not
available is represented as unknown (`NULL`). Persistence must not fabricate
a historical processing-rule or chunking-rule version.

This extension does not change the original decision to use SQLite with
Entity Framework Core for local persistence. It extends the persistence
model to support the document knowledge-processing pipeline established by
ADR-0008 and the canonical chunk provenance contract established by
ADR-0011.

Version-lineage persistence further extends this model for the successful
derived document knowledge represented by the processing lifecycle in
ADR-0010.

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

Source-location semantics remain application-owned while Infrastructure
maps the optional source location to nullable persistence columns on the
`DocumentChunkEntity`.

Processing-rule version is supplied by the Application processing flow and
persisted by Infrastructure as document-level successful-result lineage.
Chunking-rule version is supplied with the derived chunk and persisted on
the same canonical `DocumentChunkEntity` row as its other chunk metadata.

## Persistence Model

The Domain `Document` is intentionally separate from the EF Core persistence entity.

```text
Domain
└── Document

Infrastructure
├── DocumentEntity
└── DocumentChunkEntity
```

Infrastructure is responsible for mapping between the persistence and domain representations.

This prevents database-specific concerns from leaking into the Domain model.

The persisted document chunk representation includes:

```text
DocumentChunkEntity
├── Id
├── DocumentId
├── Order
├── Text
├── ContentHash
├── ProcessingGeneration
├── ChunkingRuleVersion?
├── SourceLocationStartLine?
└── SourceLocationEndLine?
```

The persisted document metadata additionally includes:

```text
DocumentEntity
├── ProcessingGeneration
├── LastSuccessfulProcessingGeneration
└── LastSuccessfulProcessingRuleVersion?
```

Processing-rule and chunking-rule version fields are nullable because
existing persisted knowledge may predate version persistence and therefore
cannot have a reliable historical version assigned.

Source-location columns are nullable because not every extracted or
transformed representation has a reliable direct source mapping.

The valid persistence states are:

```text
Known source location
    SourceLocationStartLine != NULL
    SourceLocationEndLine   != NULL
    SourceLocationStartLine > 0
    SourceLocationEndLine >= SourceLocationStartLine

Unknown source location
    SourceLocationStartLine = NULL
    SourceLocationEndLine   = NULL
```

Infrastructure enforces this invariant at the SQLite boundary.

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

The unique SHA-256 constraint provides the persistence-level concurrency
safeguard for document deduplication in addition to the application-level
duplicate detection fast path.

The application-level duplicate check is intentionally retained as an early
duplicate-detection path, but it cannot by itself guarantee correctness when
multiple imports observe the same pre-existing state concurrently.

The SQLite unique SHA-256 constraint is therefore the authoritative persistence
boundary for concurrent duplicate imports.

## Duplicate Concurrency Boundary

Document import performs an application-level SHA-256 existence check before
creating and persisting a new document.

That check is a fast path, not the authoritative concurrency guarantee,
because two concurrent imports can both observe that the hash does not yet
exist before either import reaches persistence.

The authoritative guarantee is the unique SHA-256 constraint enforced by
SQLite on the `Documents.Sha256Hash` value.

The Infrastructure repository recognizes the specific SQLite uniqueness
conflict for the `Documents.Sha256Hash` constraint and translates it into the
application-defined `DocumentHashConflictException`.

The Application import handler translates that persistence conflict into the
existing `Duplicate` import result.

This creates the following responsibility boundary:

```text
Application
    |
    +-- ExistsByHashAsync() ----> Duplicate fast path
    |
    v
Infrastructure
    |
    +-- AddAsync()
            |
            v
        SQLite unique SHA-256 constraint
            |
            +-- success ----> document persisted
            |
            +-- SHA-256 conflict ----> DocumentHashConflictException
                                              |
                                              v
                                      Duplicate import result
```

Only the specific SHA-256 uniqueness conflict is translated this way.
Unrelated persistence conflicts continue to propagate normally.

This preserves the existing Application/Infrastructure separation while making
the SQLite persistence boundary authoritative for concurrent duplicate
protection.

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

The source-location persistence change uses the same EF Core migration
mechanism. It adds nullable source-location columns and a SQLite check
constraint without introducing a separate persistence subsystem.

Version-lineage persistence uses the same mechanism. The new document-level
processing-rule version and chunk-level chunking-rule version columns are
nullable so existing persisted data remains readable without a fabricated
historical version.

Existing persisted data remains usable after the migration. Historical
chunks receive `NULL/NULL` source-location values because their original
source locations cannot be reconstructed reliably.

Version-lineage fields remain `NULL` when the rules that produced historical
knowledge cannot be recovered reliably.

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
- stable document-chunk identity
- document-chunk content identity
- document-chunk processing-generation provenance
- document-level successful processing-rule version lineage
- chunk-level chunking-rule version lineage
- optional source-location provenance

Document metadata and processing-derived data are persisted through
Infrastructure-owned EF Core persistence while encrypted source document
content remains stored separately as `.dvault` files.

The current implementation uses EF Core migrations for schema evolution and
SQLite for local persistence.

Source-location provenance is stored on the same `DocumentChunks` row as
the chunk identity, document relationship, content identity, ordering,
and processing generation.

Chunking-rule version is stored on that same canonical `DocumentChunks` row.

The successful document-level processing-rule version is stored on the
`Documents` row together with the last successful processing generation.
The processing-rule version and chunking-rule versions are published only
through the successful-processing persistence boundary so they cannot become
current independently of the corresponding successful derived result.

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
* Persisted processed knowledge retains stable identity and processing provenance.
* Successful persisted knowledge also retains the processing-rule and chunking-rule versions that produced it.
* Version metadata remains bound to the same successful derived representation as the associated processing generation.
* Historical knowledge with unavailable rule-version information can remain explicitly unknown.
* Reliable source-location provenance can be stored alongside the exact
  processed knowledge unit that produced it.
* Unknown source-location provenance can remain explicitly unknown.
* SQLite prevents invalid or partial source-location state.

### Negative

* Infrastructure now has a database dependency.
* SQLite schema evolution requires migrations.
* A separate persistence entity must be maintained alongside the Domain entity.
* Database initialization adds startup work.
* Source-location persistence adds nullable schema fields and a database
  validation constraint.
* Historical source-location provenance cannot be reconstructed for
  legacy rows.
* Historical processing-rule and chunking-rule versions cannot be reconstructed
  when the rules that produced legacy knowledge are unavailable.

These trade-offs are acceptable for the current MVP.

## Result

DeskVault now has a persistent local document metadata and derived-knowledge
layer while retaining encrypted filesystem storage for document content.

The resulting workflow is:

```text
Import
    ↓
Hash
    ↓
Application duplicate detection
    ↓
Encrypt + store source content
    ↓
SQLite unique SHA-256 constraint
    ↓
Duplicate rejected at persistence boundary when required
    ↓
Persist metadata
    ↓
Process document
    ↓
Persist derived chunks
    ↓
Persist reliable source-location provenance when available
    ↓
Persist successful version lineage
    ↓
Restart application
    ↓
Restore metadata and derived state
    ↓
Open and decrypt document
```

The application-level duplicate check provides the normal fast path, while
the SQLite unique SHA-256 constraint provides the authoritative concurrency
guarantee when concurrent imports reach persistence simultaneously.

A SHA-256 uniqueness conflict is translated into the existing duplicate import
outcome. Other persistence failures are not converted into duplicate results.

Derived document chunks remain stored in SQLite as Infrastructure-owned
persistence entities, with source-location provenance stored alongside the
canonical chunk identity, document relationship, content identity,
ordering, and processing generation.

Chunking-rule version is stored on the same canonical `DocumentChunks` row.

The document record retains the processing-rule version corresponding to
its `LastSuccessfulProcessingGeneration`. These version values are advanced
only when the same transaction successfully publishes the corresponding
derived representation.

Historical persisted knowledge without recoverable rule-version information
retains `NULL` version values rather than receiving fabricated versions.

Encrypted document content remains stored separately as `.dvault` files, and
the Application layer remains independent of EF Core and SQLite-specific
implementation details.

This establishes the persistence foundation for future document processing,
search, embeddings, and local AI capabilities.
