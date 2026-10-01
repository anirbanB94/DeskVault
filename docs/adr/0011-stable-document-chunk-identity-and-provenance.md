# ADR-0011: Stable Document Chunk Identity and Provenance

## Status

Accepted

## Context

DeskVault processes imported documents through a separate Application-layer
workflow:

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

```text
DeskVault.DocumentChunk.v1
```

The identity input is the concatenation of:

1. the UTF-8 bytes of the identity domain;
2. the 16 bytes produced by `Guid.TryWriteBytes` for `DocumentId`;
3. the 4-byte signed little-endian representation of `Order`.

The SHA-256 hash of that byte sequence is computed, and the first 16
bytes of the hash are used to construct the resulting `Guid`.

Conceptually:

```text
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

DeskVault represents a source location as an inclusive line range:

```text
DocumentSourceLocation
├── StartLine
└── EndLine
```

`StartLine` is the first source line associated with the resulting
content and must be greater than zero.

`EndLine` is the last source line associated with the resulting content
and must be greater than or equal to `StartLine`.

DeskVault also carries an explicit source-location mapping contract
through the processing pipeline:

```text
DocumentSourceLocationMappingKind
├── Unknown
└── DirectText
```

`DirectText` means that the extracted or normalized representation
remains directly mappable to the originating source text by source line.

`Unknown` means that a reliable source-location relationship is not
available or cannot be preserved through the applicable transformation.

The extraction stage establishes the mapping contract. The normalization
stage preserves that contract when the normalized representation remains
reliably mappable. The chunking stage derives a `DocumentSourceLocation`
for `DirectText` content and leaves `SourceLocation` unset when the
mapping contract is `Unknown`.

Normalization of line endings does not invalidate line-level provenance.
For example, CRLF-to-LF normalization preserves source line numbering.

Transforms that parse and render structured source content do not claim
`DirectText` unless the pipeline provides an explicit reliable mapping.
DeskVault must therefore leave source location unknown rather than infer
or fabricate a location.

Source location is optional and pipeline-dependent. The provenance
contract does not require language-aware source analysis, AST processing,
compilation, or semantic code intelligence.


### Persisted Source-Location Representation

The Infrastructure persistence model stores source location on the same
canonical `DocumentChunks` row as the chunk identity, document
relationship, content identity, and processing generation.

The persistence representation is:

```text
DocumentChunkEntity
├── Id
├── DocumentId
├── Order
├── Text
├── ContentHash
├── ProcessingGeneration
├── SourceLocationStartLine?
└── SourceLocationEndLine?
```

`SourceLocationStartLine` and `SourceLocationEndLine` are nullable
database columns. Unknown provenance is represented by `NULL/NULL`.
Known provenance is represented by a valid inclusive line range.

Infrastructure enforces that either both values are `NULL` or both are
present with `StartLine > 0` and `EndLine >= StartLine`.

When processed knowledge is replaced, the persisted source location is
taken from the current processing generation. If the current generation
does not provide reliable source location, both values are cleared to
`NULL` rather than retaining stale provenance from an earlier generation.

Historical chunks that predate source-location persistence are migrated
with the nullable columns left `NULL/NULL`; no historical source location
is reconstructed or fabricated.

## Architectural Boundaries

The document chunk contract is an Application-level semantic contract.

The processing workflow remains responsible for orchestration and for
providing document and processing context to the persistence boundary.

The chunking component remains responsible for producing deterministic
chunk order and canonical chunk text, and for carrying reliable
source-location provenance forward when the processing contract provides
a direct source mapping. It does not become responsible for persistence,
search, database concerns, or AI-specific metadata.

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

The `DocumentChunks` schema persists `ContentHash`, `ProcessingGeneration`,
and nullable source-location line values. The application-level logical
identity algorithm is implemented at the Application/persistence boundary
rather than embedded in the database migration.

Existing persisted documents and their processed content are preserved
through the transition.

Source-location provenance is persisted on the same canonical chunk row as
the chunk's `Id`, `DocumentId`, `Order`, `ContentHash`, and
`ProcessingGeneration`. Infrastructure stores the optional line range as
two nullable columns: `SourceLocationStartLine` and
`SourceLocationEndLine`.

The persisted source-location representation has two valid states:

```text
Unknown:
    SourceLocationStartLine = NULL
    SourceLocationEndLine   = NULL

Known:
    SourceLocationStartLine > 0
    SourceLocationEndLine >= SourceLocationStartLine
```

Infrastructure enforces this representation with a SQLite check
constraint. Partial ranges and invalid line ranges cannot be persisted.

When processed knowledge is replaced by a later processing generation,
the replacement persists the source location supplied by that generation.
When the replacement has unknown or unavailable source location, both
nullable line values are written as `NULL`. This prevents source-location
provenance from an earlier processing generation from being retained on a
later replacement.

Legacy chunk rows are backfilled after EF Core migrations complete. The
backfill:

- reads existing chunks without tracking;
- deterministically derives the logical `Id` from `DocumentId` and
  `Order`;
- derives `ContentHash` from the existing chunk text;
- assigns `ProcessingGeneration = 0` because the historical producing
  generation is not available;
- leaves `SourceLocationStartLine` and `SourceLocationEndLine` as
  `NULL` because historical source location cannot be reconstructed
  reliably;
- replaces the legacy rows inside a database transaction so the
  transition is atomic;
- is idempotent when the rows already conform to the contract.

The transition replaces legacy random chunk IDs because repository/code
inspection established that those IDs have no external consumers outside
the persistence boundary and migration tests. Existing document IDs,
chunk text, and chunk ordering are preserved.

The migration/backfill therefore establishes the new identity and
provenance contract without silently losing existing derived content or
inventing historical source provenance.

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

- Logically equivalent processed chunks can retain stable
  application-level identity under the same chunking contract.
- Changed content can be distinguished through separate content
  identity.
- Changed chunk boundaries are explicitly treated as new logical
  occurrences rather than requiring fuzzy lineage.
- Every chunk can be traced to its source document.
- Persisted chunks can record the processing representation that
  produced them.
- Current search can continue to resolve chunks to source documents.
- Future retrieval and source-grounded capabilities have a canonical
  reference point.
- The contract remains independent of search engines, embeddings,
  vectors, and AI implementations.
- Existing Application/Infrastructure architectural boundaries remain
  intact.
- Processing lifecycle and chunk representation concerns remain
  separately governed.
- Legacy persisted chunks can be transitioned without losing their
  document, order, or text content.
- Reliable source-location provenance can remain attached to the exact
  persisted knowledge unit and processing generation that produced it.
- Unknown source-location provenance can be represented explicitly as
  `NULL/NULL` without fabrication.
- Database constraints prevent invalid or partial persisted
  source-location ranges.
- Historical rows do not acquire fabricated source locations during
  migration.
- Reliable line-level source provenance can be carried through direct
  text processing without coupling the canonical chunk contract to a
  specific document format or AI implementation.
- Unmapped or transformed source content can explicitly remain unknown
  rather than introducing false provenance.

### Negative

- The persistence model becomes richer.
- Existing persisted data requires schema evolution and transition
  handling.
- Identity and content hashing introduce additional deterministic
  behavior.
- Source-location support may vary by extraction format.
- The exact stable identity construction is now part of the canonical
  contract and must not be changed silently.
- Source-location persistence requires an additional nullable database
  representation and validation constraint.
- Legacy rows retain `ProcessingGeneration = 0` because their
  historical producing generation cannot be reconstructed.
- Legacy rows retain unknown source location because historical
  source-location provenance cannot be reconstructed reliably.

These trade-offs are acceptable because stable, traceable canonical
derived content is a prerequisite for reliable future retrieval and
source-grounded capabilities.

## Implementation Constraints

The implementation must:

- preserve the Application/Infrastructure dependency boundary;
- keep the chunker independent of persistence and database concerns;
- establish deterministic logical chunk identity;
- keep logical identity separate from content identity;
- preserve document provenance;
- preserve processing-generation provenance;
- preserve deterministic ordering;
- preserve existing transactional chunk replacement;
- preserve cancellation and stale-attempt protections established by
  ADR-0010;
- preserve existing search-to-document traceability;
- persist source-location provenance on the same canonical chunk row as
  the knowledge unit it describes;
- represent unknown source location as `NULL/NULL`;
- reject invalid or partial persisted source-location ranges;
- ensure replacement of processed knowledge cannot retain stale
  source-location values from an earlier generation;
- provide a safe EF Core migration/transition for existing persisted
  data;
- avoid sensitive document content or security material in logs;
- remain independent of embeddings, vectors, RAG, and AI.

The deterministic identity contract specifically requires the
`DeskVault.DocumentChunk.v1` domain, `DocumentId` byte representation,
little-endian `Order` representation, SHA-256 hashing, and first-16-byte
GUID construction described above.

## Related Decisions and Work

- ADR-0002 defines the vertical-slice architecture and the
  Domain/Application/Infrastructure boundaries.
- ADR-0005 establishes SQLite with EF Core as the persistence boundary
  for document metadata and derived processing results.
- ADR-0009 establishes the encrypted SQLite provider and database
  key-management boundary.
- ADR-0010 establishes authoritative processing generation and
  stale-result protection for the document-processing lifecycle.

## Result

DeskVault maintains a canonical document chunk contract in which logical
chunk identity, source document identity, content identity, processing
provenance, deterministic ordering, and optional source location are
separate concepts.

The stable logical identity is deterministic from the document
identifier and chunk order under the versioned
`DeskVault.DocumentChunk.v1` construction, while content identity is
independently derived from canonical chunk text.

Persisted source location is represented on the canonical chunk row by
nullable start/end line values with database-level validation. Known
locations are stored as valid inclusive line ranges, while unknown
locations remain `NULL/NULL`.

Processed-content replacement writes provenance from the current
processing generation and clears source-location values when that
generation cannot provide reliable provenance, preventing stale
locations from earlier generations from surviving replacement.

Legacy persisted chunks are safely transitioned to the contract with
historical processing provenance represented by generation `0` and
historical source location represented as unknown.

This establishes the stable derived-knowledge foundation required for
current local search and future retrieval and source-grounded
capabilities without introducing MVP 3-specific AI architecture into
MVP 2.
