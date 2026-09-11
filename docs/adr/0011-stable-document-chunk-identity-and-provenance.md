# ADR-0011: Stable Document Chunk Identity and Provenance

## Status

Accepted

## Context

DeskVault processes imported documents through a separate
Application-layer workflow:

```text
Stored Document
    ↓
Extract
    ↓
Normalize
    ↓
Chunk
    ↓
Persist Derived Result
```

The processing lifecycle establishes an authoritative processing
generation for each document. That generation protects processing state
and derived content publication from obsolete concurrent processing
attempts.

Persisted document chunks have a separate architectural responsibility.
They represent canonical processed knowledge that is consumed by the
current search capability and will provide the foundation for future
retrieval and source-grounded capabilities.

The current chunk contract primarily represents chunk order and text.
Persisted chunk identity was previously generated randomly during
persistence, so logically equivalent chunks could receive different
persistence identities across reprocessing.

Future retrieval and source-grounded capabilities require a canonical
chunk representation that can distinguish the logical identity of a
chunk, its source document, its content representation, its processing
generation, and source-location information when the existing pipeline
can provide it.

These concepts must remain separate. Processing generation must not
become the logical chunk identity, and chunk content identity must not
be conflated with logical chunk identity.

The contract must remain independent of lexical search implementations,
embeddings, vector storage, RAG, and AI-specific models.

## Decision

DeskVault will treat a document chunk as a canonical logical occurrence
within a document's processed chunk sequence.

A persisted document chunk has the following conceptual contract:

```text
DocumentChunk
├── Id
├── DocumentId
├── Order
├── Text
├── ContentHash
├── ProcessingGeneration
└── SourceLocation?
```

### Stable Logical Identity

`Id` represents the stable application-level logical identity of the
chunk occurrence.

The identity is independent of processing generation and content
representation hash.

The stability guarantee applies to equivalent canonical processed chunk
sequences under the same chunking contract. It does not attempt to
preserve identity across changed chunk boundaries.

The implementation uses a deterministic SHA-256-derived GUID.

The identity domain is the UTF-8 string:

``` text
DeskVault.DocumentChunk.v1
```

The identity input is the concatenation of:

1.  the UTF-8 bytes of the identity domain;
2.  the 16 bytes produced by `Guid.TryWriteBytes` for `DocumentId`;
3.  the 4-byte signed little-endian representation of `Order`.

The SHA-256 hash of that byte sequence is computed, and the first 16
bytes of the hash are used to construct the resulting `Guid`.

Conceptually:

``` text
LogicalId =
    Guid(
        SHA256(
            UTF8("DeskVault.DocumentChunk.v1")
            || DocumentId bytes
            || Int32LittleEndian(Order)
        )[0..16]
    )
```

The domain string and byte encoding form part of the versioned
application contract. Any future change to this construction must
preserve or explicitly revise the stability contract rather than
silently changing the algorithm.

The logical identity is not a content hash.

### Source Document Relationship

`DocumentId` identifies the source document from which the chunk is
derived.

Every persisted chunk must retain an unambiguous relationship to its
source document.

This relationship remains the authoritative path for tracing derived
knowledge back to the document.

### Deterministic Ordering

`Order` represents the deterministic position of the chunk within the
canonical processed document representation.

For the same canonical processed document content and chunking contract,
chunk ordering is deterministic.

Order is part of the canonical chunk occurrence semantics but must not
be treated as interchangeable with processing generation.

A changed chunk boundary is a changed logical occurrence. DeskVault does
not attempt fuzzy lineage tracking or automatic split/merge identity
continuity.

### Content Identity

`ContentHash` identifies the content representation of the chunk.

Content identity is intentionally separate from logical chunk identity.

`ContentHash` is the lowercase hexadecimal SHA-256 hash of the UTF-8
bytes of the canonical chunk text. It is therefore a 64-character
lowercase hexadecimal SHA-256 representation.

Therefore, if the same logical chunk is represented by different content
across processing generations:

```text
Logical identity: X
Generation: 7
ContentHash: A

Logical identity: X
Generation: 8
ContentHash: B
```

the logical chunk identity remains distinct from the changed content
representation.

The existing document-level file SHA-256 hash and its file-oriented
`IHashService` contract remain separate concerns and are not repurposed
as the chunk content identity mechanism.

### Processing Provenance

`ProcessingGeneration` records the processing generation whose attempt
produced the persisted chunk representation.

It is provenance, not logical identity.

If a newer processing attempt is cancelled and previously successful
derived content remains published, preserved chunks retain the
generation that actually produced them.

The processing-generation concurrency guarantees remain governed by
ADR-0010.

For legacy chunks backfilled during migration,
`ProcessingGeneration = 0` means that the historical producing
generation is unknown. New processing attempts use the authoritative
positive processing generation established by ADR-0010.

### Source Location

Source-location information may be persisted when the existing
extraction or normalization pipeline can provide it without introducing
format-specific semantic source-code analysis.

DeskVault will not require language-aware source analysis, AST
processing, compilation, or semantic code intelligence to establish this
contract.

Source location is therefore optional and pipeline-dependent.

## Architectural Boundaries

The document chunk contract is an Application-level semantic contract.

The processing workflow remains responsible for orchestration and for
providing document and processing context to the persistence boundary.

The chunking component remains responsible for producing deterministic
chunk order and canonical chunk text. It does not become responsible for
persistence, search, database concerns, or AI-specific metadata.

Infrastructure remains responsible for EF Core persistence entities,
mappings, database constraints and indexes, migrations, transactions,
and SQLite-specific implementation details.

EF Core and SQLite types must not leak into Domain or Application
contracts.

Search remains a consumer of the canonical persisted representation.
Search must be able to resolve a chunk to its source document through
`DocumentId` and must not become the owner of chunk identity semantics.

The contract does not introduce dependencies on embeddings, vector
databases, RAG, AI models, or a particular lexical search
implementation.

## Processing Lifecycle Relationship

ADR-0010 defines the authoritative processing-generation lifecycle.

This ADR does not replace or modify that lifecycle.

The responsibilities are intentionally separate:

```text
ADR-0010
Which processing attempt is authoritative?
        ↓
ProcessingGeneration

ADR-0011
What canonical chunk representation was produced?
        ↓
Chunk Id
DocumentId
Order
ContentHash
ProcessingGeneration
SourceLocation?
```

A processing generation may therefore appear as chunk provenance without
becoming part of the logical chunk identity.

The fact that generation is stored as chunk provenance does not change
the concurrency decision in ADR-0010.

## Persistence and Migration

The canonical chunk contract is represented in the existing
Infrastructure persistence boundary.

Schema evolution uses the existing EF Core migration mechanism.

The migration adds `ContentHash` and `ProcessingGeneration` to the
existing `DocumentChunks` table. The application-level logical identity
algorithm is implemented at the Application/persistence boundary rather
than embedded in the database migration.

Existing persisted documents and their processed content are preserved
through the transition.

Legacy chunk rows are backfilled after EF Core migrations complete. The
backfill:

-   reads existing chunks without tracking;
-   deterministically derives the logical `Id` from `DocumentId` and
    `Order`;
-   derives `ContentHash` from the existing chunk text;
-   assigns `ProcessingGeneration = 0` because the historical producing
    generation is not available;
-   replaces the legacy rows inside a database transaction so the
    transition is atomic;
-   is idempotent when the rows already conform to the contract.

The transition replaces legacy random chunk IDs because repository/code
inspection established that those IDs have no external consumers outside
the persistence boundary and migration tests. Existing document IDs,
chunk text, and chunk ordering are preserved.

The migration/backfill therefore establishes the new identity and
provenance contract without silently losing existing derived content.

## Alternatives Considered

### Content Hash as Logical Chunk Identity

Rejected.

Using chunk content as the logical identity would conflate logical
identity with representation identity. A content change would
necessarily create a new logical identity and would prevent the system
from distinguishing a stable logical chunk from a changed
representation.

Content identity is therefore represented separately by `ContentHash`.

### Document ID Plus Processing Generation as Chunk Identity

Rejected.

Processing generation identifies a processing attempt, not a logical
chunk. Including generation in chunk identity would cause every
successful reprocessing attempt to create a new logical chunk identity
even when the canonical chunk remains equivalent.

### Random Persistence GUID as the Application-Level Identity

Rejected.

A persistence-generated random identifier does not provide stable
identity across equivalent reprocessing and does not establish a
canonical application-level identity contract.

### Search-Specific Chunk Identity

Rejected.

Chunk identity is part of the canonical derived knowledge
representation, not a property owned by the current lexical search
implementation. Future retrieval mechanisms must be able to consume the
same identity without coupling the canonical model to a particular
search technology.

### AI-Specific Chunk Identity

Rejected.

The canonical chunk contract must exist before embeddings, vector
retrieval, RAG, or source-grounded AI are introduced. Making identity
AI-specific would prematurely couple MVP 2 foundations to MVP 3
implementation details.

## Consequences

### Positive

-   Logically equivalent processed chunks can retain stable
    application-level identity under the same chunking contract.
-   Changed content can be distinguished through separate content
    identity.
-   Changed chunk boundaries are explicitly treated as new logical
    occurrences rather than requiring fuzzy lineage.
-   Every chunk can be traced to its source document.
-   Persisted chunks can record the processing representation that
    produced them.
-   Current search can continue to resolve chunks to source documents.
-   Future retrieval and source-grounded capabilities have a canonical
    reference point.
-   The contract remains independent of search engines, embeddings,
    vectors, and AI implementations.
-   Existing Application/Infrastructure architectural boundaries remain
    intact.
-   Processing lifecycle and chunk representation concerns remain
    separately governed.
-   Legacy persisted chunks can be transitioned without losing their
    document, order, or text content.

### Negative

-   The persistence model becomes richer.
-   Existing persisted data requires schema evolution and transition
    handling.
-   Identity and content hashing introduce additional deterministic
    behavior that requires explicit tests.
-   Source-location support may vary by extraction format.
-   The exact stable identity construction is now part of the canonical
    contract and must not be changed silently.
-   Legacy rows retain `ProcessingGeneration = 0` because their
    historical producing generation cannot be reconstructed.

These trade-offs are acceptable because stable, traceable canonical
derived content is a prerequisite for reliable future retrieval and
source-grounded capabilities.

## Implementation Constraints

The implementation must:

-   preserve the Application/Infrastructure dependency boundary;
-   keep the chunker independent of persistence and database concerns;
-   establish deterministic logical chunk identity;
-   keep logical identity separate from content identity;
-   preserve document provenance;
-   preserve processing-generation provenance;
-   preserve deterministic ordering;
-   preserve existing transactional chunk replacement;
-   preserve cancellation and stale-attempt protections established by
    ADR-0010;
-   preserve existing search-to-document traceability;
-   provide a safe EF Core migration/transition for existing persisted
    data;
-   avoid sensitive document content or security material in logs;
-   remain independent of embeddings, vectors, RAG, and AI.

The deterministic identity contract specifically requires the
`DeskVault.DocumentChunk.v1` domain, `DocumentId` byte representation,
little-endian `Order` representation, SHA-256 hashing, and first-16-byte
GUID construction described above.

## Validation

The implementation was validated with:

-   focused Application identity tests: 7/7 passed;
-   focused Infrastructure persistence identity tests: 5/5 passed;
-   focused legacy migration integration test: 1/1 passed;
-   focused backfill tests: 5/5 passed;
-   full Infrastructure test suite: 125/125 passed;
-   full solution test suite: 503/503 passed;
-   solution build: succeeded;
-   `git diff --check`: clean.

The validation covers deterministic identity, separation of identity and
content hash, persistence of document/order/content/provenance, legacy
backfill, idempotence, cancellation behavior, migration compatibility,
search traceability, and regression behavior.

## Related Decisions and Work

-   ADR-0002 defines the vertical-slice architecture and the
    Domain/Application/Infrastructure boundaries.
-   ADR-0005 establishes SQLite with EF Core as the local persistence
    boundary for document metadata and derived processing results.
-   ADR-0009 establishes the encrypted SQLite provider and database
    key-management boundary.
-   ADR-0010 establishes authoritative processing generation and
    stale-result protection for the document-processing lifecycle.
-   Technical task #75 defines and implements the stable document chunk
    identity and provenance contract.
-   Parent PBI #21 expands DeskVault's supported text-oriented knowledge
    formats.

## Result

DeskVault maintains a canonical document chunk contract in which logical
chunk identity, source document identity, content identity, processing
provenance, deterministic ordering, and optional source location are
separate concepts.

The stable logical identity is deterministic from the document
identifier and chunk order under the versioned
`DeskVault.DocumentChunk.v1` construction, while content identity is
independently derived from canonical chunk text.

Legacy persisted chunks are safely transitioned to the contract with
historical provenance represented by generation `0`.

This establishes the stable derived-knowledge foundation required for
current local search and future retrieval and source-grounded
capabilities without introducing MVP 3-specific AI architecture into
MVP 2.
