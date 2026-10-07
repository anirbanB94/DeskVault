# ADR-0012: Search Result and Relevance Boundary

## Status

Accepted

## Context

DeskVault currently provides local search over persisted processed
document content. The existing search representation is oriented around
matching document chunks.

As richer document discovery is introduced, search needs to present the
document as the primary result rather than requiring consumers to
reconstruct a document from multiple matching chunks.

A document may match through its metadata, through one or more pieces of
processed content, or through multiple forms of matching evidence.
Search therefore requires an application-level result representation
that can describe the matched document and the evidence supporting the
match.

Search also requires relevance ordering. That ordering should not become
a responsibility of the persistence provider or the UI, and the
Application contract should not become tied to the current SQLite
implementation or to a future search technology.

As search result sets grow, pagination also requires a deterministic
ordering and continuation mechanism. A continuation must represent a
stable position in the Application-level ranked result sequence without
exposing storage-specific offsets or retrieval implementation details to
search consumers.

The architectural boundary must therefore allow the search
implementation and relevance strategy to evolve without replacing the
document-oriented result contract, while preserving deterministic
keyword-search ordering and continuation semantics.

## Decision

DeskVault will use a **document-oriented search result contract** at the
Application boundary.

A search operation may identify multiple matching pieces of evidence for
the same document, but the application-level result collection will
represent that document as a single search result.

The conceptual structure is:

```text
Search Result
├── Document identity
├── Document display information
├── Matching evidence
└── Relevance information
```

The exact application types and fields are implementation concerns of
the search-result work and must support the architectural responsibilities
defined by this ADR.

### Document-Oriented Results

The document is the primary unit of a search result.

The result must retain sufficient document identity and display
information for consumers to identify the matched document without
reconstructing it from individual chunk matches.

Underlying chunks or other persisted representations may provide
matching evidence, but they are not themselves the primary application
result.

Search result identity remains based on the existing document identity.
Search must not introduce a separate search-specific document identity.

### Matching Evidence

A document-level search result may contain representative evidence
showing why the document matched.

Matching evidence may originate from document metadata or processed
document content.

The result representation must allow consumers to distinguish relevant
matching evidence where that distinction is meaningful to the search
experience.

The evidence represents the match; it does not become a second
canonical representation of the document or its processed knowledge.

The search-result contract does not prescribe a particular snippet
algorithm, evidence limit, lexical matching taxonomy, or query syntax.

### Relevance Boundary

Relevance ordering is an Application-level search concern.

The ranking mechanism must operate on application-level search
information rather than on:

- EF Core query types;
- SQLite-specific objects;
- database-specific ranking expressions;
- UI presentation state; or
- search-provider-specific result types.

The ranking mechanism must be replaceable.

The deterministic keyword-search ordering contract is:

1. relevance score descending;
2. `DisplayName` using ordinal ascending comparison;
3. `DocumentId` ascending.

These ordering keys provide a stable and unambiguous Application-level
ordering for otherwise tied results.

This ADR establishes the deterministic ordering contract but does not
prescribe a particular ranking formula, weighting scheme, score
representation, or specific relevance signals.

### Deterministic Pagination and Continuation

Pagination is part of the Application-level search boundary.

A continuation represents the position of the **last consumed result** in
the deterministic ranked ordering.

The continuation is opaque to search consumers. Consumers must not need
to know whether the underlying implementation represents the position
through an offset, database key, provider-specific cursor, or another
retrieval mechanism.

The continuation must preserve the complete deterministic ordering
position, including:

- relevance score;
- `DisplayName`; and
- `DocumentId`.

The continuation must also be bound to the normalized search criteria
and the applicable continuation/ranking contract version.

A continuation created for materially different search criteria or an
incompatible contract must be rejected rather than interpreted as a
position in a different result sequence.

Page size is not part of continuation identity. A valid continuation
therefore remains meaningful when the requested page size changes.

Resuming from a continuation means returning only results that occur
strictly after the represented ranked position.

For an unchanged eligible result set, repeated execution of the same
search criteria must preserve relative result ordering and pagination
must not skip or duplicate results.

This decision does not provide snapshot semantics. Changes to the
eligible result set between requests may therefore affect later pages.

Malformed or incompatible continuation values must be rejected at the
Application boundary.

The encoding and decoding of continuation values is an Application
concern and must not expose storage-specific implementation details.

### Search and Persistence Boundary

Infrastructure remains responsible for obtaining matching persisted
representations through the appropriate persistence/search mechanism.

Infrastructure may use provider-specific query and indexing capabilities
to obtain matching data.

Those implementation details must be translated into the
Application-level search result contract rather than exposed through
that contract.

The search-result architecture therefore remains independent of the
current SQLite implementation and of any particular future search
technology.

A retrieval implementation may change how matching results are obtained
or efficiently bounded, provided that the Application-level search
ordering and continuation semantics remain behaviorally compatible.

### Relationship to Canonical Processed Knowledge

ADR-0011 establishes the canonical persisted document chunk
representation, including stable chunk identity and provenance.

Search consumes that canonical representation.

Search does not redefine:

- document chunk identity;
- chunk content identity;
- processing generation;
- source provenance; or
- deterministic chunk ordering.

A processed-content match may refer to the canonical document and its
underlying processed representation, while the application-level search
result remains document-oriented.

### Separation from UI and Workspace

Search identifies and describes matching documents.

UI presentation is responsible for deciding how those results are
displayed and interacted with.

Document opening remains governed by the existing document
responsibilities.

Workspace membership and workspace lifecycle remain governed by the
workspace architecture and are not introduced into the search-result
contract.

Search therefore does not become responsible for rendering,
document-opening orchestration, or workspace lifecycle.

## Architectural Boundaries

The responsibilities are separated as follows:

```text
Search / Discovery
        ↓
Identify matching documents and evidence

Search Result Model
        ↓
Represent the matched document and supporting evidence

Relevance
        ↓
Determine deterministic ordering through a replaceable
Application boundary

Pagination / Continuation
        ↓
Represent the last consumed position in the deterministic
Application-level ordering

Retrieval Implementation
        ↓
Obtain matching results while preserving the Application contract

UI
        ↓
Present results and initiate the appropriate existing interaction
```

The Application layer owns the semantic search-result, relevance, and
pagination boundaries.

Infrastructure owns persistence and search-provider implementation
details.

The UI consumes application-level results and does not own search
semantics, relevance ordering, or continuation interpretation.

The workspace architecture remains independent of the search-result
contract.

## Alternatives Considered

### Preserve One Result Per Matching Chunk

Rejected.

A chunk-level result makes consumers reconstruct the document-level
result, duplicates document information, and makes document-level
relevance representation more difficult to maintain consistently.

The search result should therefore represent the document as the
primary unit.

### Ranking Inside Persistence

Rejected.

Putting application relevance semantics directly into the persistence
provider would couple search behavior to the current storage and query
technology.

Provider-specific optimization may remain in Infrastructure, but the
application-level relevance boundary must remain replaceable.

### UI-Owned Ranking

Rejected.

Relevance is a search concern rather than a presentation concern.

UI-owned ranking would make consumers responsible for reproducing search
semantics and would prevent a consistent application-level ordering
model.

### Integer Offset Continuation

Rejected.

An integer offset represents a location in a materialized result set
rather than the deterministic ranked position of a specific consumed
result.

Offset-based continuation also makes the continuation dependent on
materialized result counts and page traversal rather than on the
Application-level ordering contract.

DeskVault therefore uses an opaque continuation representing the full
last-consumed ranking position instead.

## Consequences

### Positive

- Search results have a clear document-oriented representation.
- Multiple matching pieces of processed content can contribute to one
  document result.
- Matching evidence can be retained without making chunks the primary
  result type.
- Search relevance remains separate from persistence implementation.
- Deterministic tie-breaking produces predictable keyword-search order.
- Continuations remain opaque to consumers and independent of storage
  implementation.
- Changing page size does not invalidate an otherwise compatible
  continuation.
- Pagination can resume strictly after a deterministic ranked position.
- Application contracts remain independent of SQLite and EF Core.
- Future retrieval implementations can optimize result acquisition
  without changing the Application search contract or supported keyword
  ordering.
- Search remains separate from UI rendering and workspace lifecycle.
- The canonical processed representation established by ADR-0011 remains
  the source of processed-content search evidence.
- The architecture remains suitable for future retrieval evolution
  without introducing MVP 3 AI-specific implementation.

### Negative

- The existing chunk-oriented search result representation must evolve.
- Existing consumers of chunk-level results require migration.
- Search must aggregate matching evidence at the document level.
- The application introduces an explicit relevance boundary that must
  remain well-defined as ranking evolves.
- Continuation encoding and validation become part of the Application
  search infrastructure.
- Ranking and continuation semantics must remain compatible when the
  retrieval implementation changes.

These trade-offs are acceptable because document-oriented results,
deterministic pagination, and replaceable relevance/retrieval boundaries
are required foundations for richer local document discovery.

## Implementation Constraints

The implementation must:

- represent a matched document as the primary application-level result;
- preserve authoritative document identity;
- retain sufficient document display information for result consumers;
- support representative matching evidence;
- allow metadata and processed-content evidence to be distinguished where
  required;
- keep relevance ordering separate from UI presentation;
- keep relevance ordering separate from persistence implementation;
- preserve deterministic ranking order using relevance score descending,
  `DisplayName` ordinal ascending, and `DocumentId` ascending;
- represent continuation as opaque Application-level state rather than a
  storage-specific offset;
- encode enough ranking state to resume strictly after the last consumed
  result;
- bind continuation to normalized search criteria and the applicable
  continuation/ranking contract version;
- keep page size out of continuation identity;
- reject malformed or request-incompatible continuations;
- keep EF Core and SQLite types out of Application contracts;
- consume the canonical processed representation established by
  ADR-0011;
- preserve existing document traceability;
- preserve existing search cancellation and error-propagation behavior;
- remain independent of embeddings, vector retrieval, RAG, and AI;
- remain independent of workspace lifecycle and document-opening
  orchestration;
- allow retrieval implementation to evolve without changing supported
  keyword-search behavior or deterministic ordering unless explicitly
  refined.

The concrete search behavior, matching rules, ranking algorithm, retrieval
optimization strategy, and UI interaction are owned by their respective
implementation work and are not prescribed by this ADR.

## Related Decisions and Work

- ADR-0002 defines the vertical-slice architecture and the
  Domain/Application/Infrastructure boundaries.
- ADR-0005 establishes SQLite with EF Core as the local persistence
  boundary for document metadata and derived processing results.
- ADR-0006 establishes the in-app document workspace and its separation
  from other application concerns.
- ADR-0007 defines the workspace UI and interaction model.
- ADR-0008 establishes preservation of document semantics through
  rendering and keeps knowledge processing separate from UI rendering.
- ADR-0010 establishes the reliable document-processing lifecycle and
  authoritative processing-generation behavior.
- ADR-0011 establishes stable document chunk identity and provenance for
  canonical persisted derived knowledge.

## Result

DeskVault uses a document-oriented Application-level search-result
boundary.

A search result represents the matched document and can retain
representative matching evidence without making individual chunks the
primary result type.

Relevance ordering is separated from both persistence and UI through a
replaceable Application-level boundary.

Current keyword search uses deterministic ordering based on relevance
score descending, `DisplayName` ordinal ascending, and `DocumentId`
ascending.

Pagination uses an opaque continuation representing the last consumed
position in that deterministic ordering. The continuation is independent
of page size and storage implementation and is bound to the applicable
search criteria and continuation/ranking contract version.

The decision establishes the architectural foundation for predictable
and scalable local document discovery while leaving retrieval
implementation details and future MVP 3 AI capabilities outside the
Application contract.
