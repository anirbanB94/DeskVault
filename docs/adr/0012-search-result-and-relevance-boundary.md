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

The Application retrieval boundary must also prevent consumers from
becoming coupled to the way search candidates are obtained, stored, or
paged. Consumers should request a bounded page of application-level
results and should not need to perform full-result ranking, storage-level
pagination, or retrieval-specific continuation handling themselves.

Keyword search also requires consistent matching semantics across
document metadata and persisted processed content. Processing
normalization establishes a canonical representation for searchable
content, but search input and metadata can have different Unicode
representations. Canonically equivalent Unicode text must not produce
different search outcomes merely because it uses composed or decomposed
characters.

Matching must also remain deterministic and culture-independent. The
same canonical query should be interpreted consistently across metadata
and eligible processed content without changing document identity,
canonical processed knowledge, deterministic result ordering, or
pagination semantics.

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

### Keyword Matching and Unicode Canonicalization

Keyword search must apply consistent matching semantics to document
metadata and eligible persisted processed content.

Search input is trimmed and normalized to Unicode Normalization Form C
(NFC) before matching. Candidate values used for matching are also
normalized to NFC without trimming their content.

Matching uses ordinal, case-insensitive comparison semantics. It must
not depend on the current culture or the user's locale.

Consequently:

- canonically equivalent composed and decomposed Unicode forms must
  match;
- case differences must be handled consistently for metadata and
  processed content;
- equivalent search inputs must produce the same canonical search
  criteria;
- ASCII case-insensitive matching must remain supported;
- literal query characters must not be reinterpreted as wildcard syntax;
- returned matching evidence must preserve its original display text.

Canonical equivalence is not accent folding. Search must not remove
diacritics or treat text that is not canonically equivalent as
equivalent merely because it looks similar.

This matching contract does not redefine the document processing
normalization pipeline. Processing remains responsible for producing
canonical processed knowledge according to its existing normalization
contract. Search canonicalizes candidate values for comparison and
normalizes query input without rewriting the persisted document or
chunk representation.

Metadata and processed-content matching must use the same canonical
normalization and ordinal, case-insensitive comparison semantics.
Provider-specific implementations may differ internally, but the
observable matching behavior must remain equivalent.

Unicode matching is part of the keyword-search behavior rather than a
new persistence model or a new retrieval technology. Implementations
must preserve existing document eligibility, file-type filtering,
matching evidence, ranking, and bounded retrieval behavior.

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

A change that modifies observable keyword-matching behavior must not
silently reuse continuation state created under an incompatible
matching contract. The Unicode-aware matching change advances the
ranking contract version to version 2. The continuation format version
remains independent of that ranking contract version.

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

### Application Retrieval Boundary

The Application exposes retrieval through a storage-independent
`ISearchDocumentsRetriever` contract.

Application consumers request a page through this boundary rather than
through `IDocumentSearchStore` or another concrete persistence/search
abstraction.

The retrieval contract is page-shaped:

```text
SearchDocumentsPage
├── Results
├── HasMore
└── Opaque Continuation
```

`SearchDocumentsPage` represents the consumer-facing result of one
retrieval operation. It exposes application-level `SearchDocumentsResult`
values and the opaque continuation state required to request the next
page.

The Application search handler is a thin façade over the retrieval
boundary. It delegates the query and cancellation token to
`ISearchDocumentsRetriever` and does not own ranking, full-result
materialization, `Skip`/`Take`, storage pagination, or continuation
encoding/decoding.

Retrieval implementation details remain behind the boundary. Consumers
must not need to understand whether matching evidence was obtained from
SQLite, another search implementation, an index, or another future
retrieval mechanism.

The retrieval boundary does not require a particular underlying
implementation strategy. It therefore permits retrieval-side filtering,
ordering, bounded page selection, lookahead, and other implementation
optimizations while keeping the Application-facing page contract stable.

The retrieval implementation must keep paginated result state bounded.
A request for page size `N` must not require materializing the complete
matching ranked result set in Application or retrieval memory merely to
produce that page. The retrieval implementation may inspect more
candidate data internally, but it must retain only the bounded state
needed to produce the requested page and a lookahead result sufficient to
determine `HasMore`.

Continuation-aware retrieval must apply the decoded deterministic
ranking position at the retrieval boundary so that candidates at or before
the continuation are excluded before the page is formed. The
Application retriever must therefore not reconstruct a complete ranked
collection and apply `Skip`/`Take` after retrieval.

The retrieval implementation must preserve the application-defined
relevance contract while performing bounded retrieval. Storage/provider
optimizations may determine how candidates are obtained or ordered, but
they must remain behaviorally equivalent to the Application-level ranking
contract and must not introduce storage-specific observable ranking
semantics.

Document-level evidence and `MatchCount` semantics remain part of the
Application result contract. Retrieval may process matching evidence
incrementally, but it must not load every matching chunk solely to build a
complete intermediate result collection when only one page is requested.

Filtering and eligibility semantics must be established before the page
boundary is applied, including canonicalized search-text matching,
file-type filtering, metadata matching, and the eligibility of currently
successful processed knowledge. The precise provider-side optimization of
those operations remains an implementation concern as long as observable
search behavior is preserved.

First-page, subsequent-page, empty-result, and final-page behavior must be
predictable under the same deterministic ordering and opaque continuation
contract.

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
ordering, result representation, filtering/eligibility behavior, Unicode
matching semantics, and continuation semantics remain behaviorally
compatible.

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

Search normalization must not change the canonical persisted chunk text
or its identity and content hash. Unicode-aware matching affects how
candidate text is compared, not how canonical processed knowledge is
produced or persisted.

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
        ↓
Canonicalize search input and candidate text for comparison
        ↓
Search Result Model
        ↓
Represent the matched document and supporting evidence
        ↓
Relevance
        ↓
Determine deterministic ordering through a replaceable
Application boundary
        ↓
Pagination / Continuation
        ↓
Represent the last consumed position in the deterministic
Application-level ordering
        ↓
Application Retrieval Boundary
        ↓
Expose a bounded page-shaped contract without storage-specific details
        ↓
Retrieval Implementation
        ↓
Obtain matching results and implement bounded retrieval mechanics while
preserving the Application contract
        ↓
UI
        ↓
Present results and initiate the appropriate existing interaction
```

The Application layer owns the semantic search-result, relevance,
pagination, and consumer-facing retrieval boundaries.

Infrastructure owns persistence and search-provider implementation
details.

The UI consumes application-level results and does not own search
semantics, relevance ordering, retrieval mechanics, or continuation
interpretation.

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

### Expose Storage-Specific Retrieval to Application Consumers

Rejected.

Requiring consumers to depend directly on `IDocumentSearchStore`,
provider-specific pagination, or concrete retrieval mechanics would make
the Application consumer surface dependent on the current persistence
implementation.

DeskVault therefore exposes retrieval through `ISearchDocumentsRetriever`
and `SearchDocumentsPage`, keeping storage and retrieval implementation
details behind the Application boundary.

### Make the Retrieval Boundary Require the Final Scalable Strategy

Rejected.

The architectural boundary and the scalable implementation are separate
concerns. The boundary should not prematurely couple consumers to one
provider or one retrieval optimization strategy.

The scalable implementation is therefore defined in terms of durable
behavioral invariants: bounded retained result state, retrieval-side page
selection and lookahead, continuation-aware retrieval, preservation of
the application-defined ranking contract, and preservation of observable
search filtering and eligibility semantics.

The concrete provider strategy may evolve as long as those invariants and
the Application-facing search contract remain compatible.

## Consequences

### Positive

- Search results have a clear document-oriented representation.
- Multiple matching pieces of processed content can contribute to one
  document result.
- Matching evidence can be retained without making chunks the primary
  result type.
- Search relevance remains separate from persistence implementation.
- Deterministic tie-breaking produces predictable keyword-search order.
- Canonically equivalent composed and decomposed Unicode queries produce
  consistent matching outcomes.
- Metadata and processed-content matching share the same canonical,
  ordinal, case-insensitive matching semantics.
- Unicode matching is deterministic and independent of the current
  culture.
- Returned evidence retains its original display text.
- Continuations remain opaque to consumers and independent of storage
  implementation.
- Changing page size does not invalidate an otherwise compatible
  continuation.
- Pagination can resume strictly after a deterministic ranked position.
- Incompatible pre-change continuations are rejected.
- Application contracts remain independent of SQLite and EF Core.
- Application consumers depend on a page-shaped retrieval boundary rather
  than storage-specific retrieval mechanics.
- Paginated retrieval retains bounded result state instead of materializing
  the complete matching ranked result set for every request.
- `HasMore` can be established through bounded lookahead rather than a
  complete candidate count.
- Continuation-aware retrieval avoids Application-level full-result
  `Skip`/`Take` pagination.
- Future retrieval implementations can optimize result acquisition,
  filtering, ordering, and page selection without changing the
  Application search contract or supported keyword ordering.
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
- The retrieval boundary introduces an additional Application abstraction
  that must be maintained as search evolves.
- Continuation encoding and validation become part of the Application
  search infrastructure.
- Ranking and continuation semantics must remain compatible when the
  retrieval implementation changes.
- Previously issued continuations from an incompatible ranking contract
  must be rejected, requiring affected searches to restart.
- Retrieval implementations must maintain bounded page/lookahead state
  while preserving deterministic ranking and evidence semantics.
- Unicode-aware matching adds canonicalization work to candidate
  comparison.

These trade-offs are acceptable because document-oriented results,
deterministic pagination, bounded retrieval, consistent Unicode matching,
and replaceable relevance/retrieval boundaries are required foundations
for richer local document discovery.

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
- advance the ranking contract version when matching semantics change
  incompatibly;
- keep page size out of continuation identity;
- reject malformed or request-incompatible continuations;
- keep EF Core and SQLite types out of Application contracts;
- expose retrieval through a storage-independent Application contract;
- expose page results, `HasMore`, and opaque continuation without
  storage-specific pagination details;
- keep storage and retrieval mechanics out of Application consumers;
- keep full-result ranking and pagination operations out of Application
  consumers;
- keep concrete retrieval implementations replaceable behind the
  retrieval boundary;
- keep paginated retained result state bounded to the requested page and
  necessary lookahead rather than materializing the complete matching
  ranked result set;
- apply continuation position at the retrieval boundary rather than by
  full-result Application paging;
- preserve the application-defined ranking contract when retrieval
  ordering is optimized;
- preserve document-level evidence and `MatchCount` semantics while
  processing matching evidence incrementally;
- trim and normalize search input to NFC before keyword matching;
- normalize candidate values to NFC for matching without trimming their
  stored or returned content;
- apply ordinal, case-insensitive matching consistently to metadata and
  eligible processed content;
- keep Unicode matching independent of the current culture;
- preserve canonical processed text, chunk identity, content hash,
  provenance, and processing generation;
- preserve existing ASCII keyword-search behavior;
- preserve literal matching for query characters rather than treating
  those characters as wildcard syntax;
- preserve original text in returned matching evidence;
- apply matching, file-type filtering, metadata matching, and processed-
  content eligibility semantics before the page boundary is applied;
- preserve first-page, subsequent-page, empty-result, and final-page
  behavior;
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
implementation work and are not prescribed by this ADR beyond the durable
architectural invariants stated above.

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

Keyword search trims and normalizes input to NFC and normalizes candidate
values to NFC for comparison. Metadata and eligible processed content use
the same ordinal, case-insensitive matching semantics, independent of
culture. Canonically equivalent composed and decomposed Unicode text
therefore matches consistently, without changing the canonical persisted
processed representation or returned evidence text.

Pagination uses an opaque continuation representing the last consumed
position in that deterministic ordering. The continuation is independent
of page size and storage implementation and is bound to the applicable
normalized search criteria and continuation/ranking contract version.
The Unicode-aware matching change advances the ranking contract version
to version 2 so continuations issued under the previous matching contract
are rejected.

Application consumers use a storage-independent retrieval boundary that
returns a page-shaped `SearchDocumentsPage` containing results, a
`HasMore` indicator, and an opaque continuation. The search handler acts
as a thin façade and does not expose storage-specific retrieval or
pagination mechanics to consumers.

Paginated retrieval is performed with bounded retained result state. The
retrieval implementation applies continuation-aware filtering and
produces the requested page plus bounded lookahead rather than
materializing the complete matching ranked result set. This preserves the
Application-defined relevance, evidence, filtering, eligibility, Unicode
matching, and continuation contracts while allowing provider-specific
retrieval optimization behind the boundary.

The decision establishes the architectural foundation for predictable
and scalable local document discovery while leaving retrieval
implementation details and future MVP 3 AI capabilities outside the
Application contract.
