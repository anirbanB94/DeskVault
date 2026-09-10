# ADR-0010: Reliable Document Processing Lifecycle

## Status

Accepted

## Context

DeskVault processes an imported document through a separate Application-layer
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

Document import and document processing are intentionally separate
responsibilities. The import workflow establishes the stored document and
its persistent metadata, while the processing workflow creates the derived
document representation.

The initial processing implementation supports extraction, normalization,
chunking, transactional chunk replacement, repeated processing, cancellation,
and failure handling. Investigation of the processing lifecycle identified a
concurrency and stale-result problem.

An older processing attempt can retain an older `Document` snapshot and later
publish a status update after a newer attempt has already changed the
document state.

Similarly, chunk replacement is transactional for an individual replacement
operation, but persisted chunks are not associated with a processing attempt.
An older processing result can therefore replace chunks produced by a newer
processing attempt.

Cancellation during extraction can also leave a document persisted in
`Processing` because cancellation may occur after the processing state
transition but before derived content is published.

Repeated sequential processing is deterministic and existing transactional
replacement prevents duplicate chunks from accumulating. These properties
must be preserved while adding protection against competing processing
attempts.

The processing lifecycle therefore requires an authoritative mechanism that
can identify which processing attempt is current and prevent obsolete
attempts
from publishing state or derived content.

## Decision

DeskVault will use a **monotonic processing generation** as the authoritative
processing-attempt identity for a document.

Each document will have a persisted processing generation.

The generation represents the ordering of processing attempts for that
document:

```text
Document
    |
    +-- ProcessingGeneration = 5
```

When a new processing attempt starts, the persistence boundary will
atomically establish the next generation:

```text
Current generation: 5
        ↓
Start processing
        ↓
Current generation: 6
        ↓
Processing attempt identity: 6
```

The processing attempt retains the generation it acquired.

All state and derived-content publication performed by that attempt must be
conditional on that generation still being authoritative.

If another processing attempt has already established generation `N + 1`,
attempt `N` is obsolete and must not overwrite the newer processing state or
derived content.

### Atomic Attempt Acquisition

Generation acquisition must occur atomically at the persistence boundary.

The implementation must not rely on a read-increment-write sequence in the
Application layer because two concurrent processing attempts could otherwise
observe the same previous generation.

The authoritative persistence operation must establish a unique, ordered
generation for each processing attempt.

### State Publication

Processing state transitions must be protected by the same processing
generation.

An obsolete processing attempt must not be able to overwrite a newer
processing state.

The lifecycle must preserve the intended processing states and their existing
meaning while ensuring that stale attempts cannot publish state after they
cease to be authoritative.

### Derived Content Publication

Persisted document chunks represent the current successful derived processing
result.

Chunk replacement remains transactional, but publication must also verify
that the processing generation is still authoritative.

An obsolete processing attempt must not replace chunks produced by a newer
authoritative attempt.

Existing deterministic replacement behavior must be preserved so that
successful repeated processing does not accumulate duplicate chunks.

### Cancellation and Failure

Cancellation remains cooperative and must continue to propagate through the
processing pipeline.

A cancelled processing attempt must not leave the document permanently in
`Processing` or publish misleading derived content as though that attempt
completed successfully.

When a processing attempt is cancelled:

- The cancelled processing generation remains obsolete and must not publish
  further state or derived content.
- If the document already has a previously successful derived result, the
  document returns to `Available` and the existing derived content remains
  intact.
- If the document has no previously successful derived result, the document
  returns to `Imported`.
- Cancellation must not remove or replace previously successful chunks.
- The cancellation recovery state transition must itself be conditional on
  the processing generation remaining authoritative, so an obsolete attempt
  cannot overwrite a newer attempt's state.

Processing failures remain distinct from cancellation. A non-cancellation
processing failure must publish `Failed` only when the failing processing
generation is still authoritative. Failures must not result in a document
being reported as successfully processed when the derived result is
incomplete or belongs to an obsolete attempt.

Cancellation and failure handling must respect the same authoritative
generation used for successful publication.

### Last Successful Processing Generation

The document persistence boundary also records the last processing generation
whose derived result was successfully published.

`ProcessingGeneration` identifies the current authoritative processing attempt.
`LastSuccessfulProcessingGeneration` identifies the most recent processing
attempt for which derived content and the corresponding successful document
state were published.

These values serve different purposes and must not be treated as interchangeable.

A successful processing attempt updates `LastSuccessfulProcessingGeneration`
only after its derived content has been durably replaced and its successful
processing state has been published.

Starting a new processing attempt advances `ProcessingGeneration` but does not
change `LastSuccessfulProcessingGeneration`.

When a processing attempt is cancelled:

- If `LastSuccessfulProcessingGeneration` identifies a previously successful
  result, cancellation recovery publishes `Available` and preserves that
  result.
- If no successful processing generation exists, cancellation recovery
  publishes `Imported`.
- Cancellation must not modify `LastSuccessfulProcessingGeneration`.

All updates to `LastSuccessfulProcessingGeneration` and cancellation recovery
remain conditional on the processing generation being authoritative.

### Concurrency

For repeated processing of the same document, persistence behavior must be
deterministic.

If multiple processing attempts compete for the same document, only the
authoritative processing generation may publish the resulting state and
derived content.

The implementation must not rely on timing assumptions to determine which
attempt is authoritative.

### Architectural Boundaries

The processing lifecycle remains an Application-layer use case.

The Application layer continues to depend on application-defined persistence
abstractions.

EF Core, SQLite, database transactions, and generation-persistence mechanics
remain Infrastructure concerns.

The design does not introduce a dependency on search, embeddings, vector
storage, or AI processing.

The existing encrypted SQLite provider and database key-management boundary
remain unchanged.

### Chunk Identity and Provenance

Processing generation provides the lifecycle identity required to prevent
obsolete processing attempts from publishing stale results.

It does not replace the separate requirement for stable chunk identity and
provenance.

Stable application-level chunk identity, document relationship, ordering,
content identity, and longer-term retrieval traceability are addressed
separately by the document chunk identity and provenance work.

The processing lifecycle must therefore expose sufficient generation
information for that work without coupling the lifecycle implementation to
search, embeddings, vectors, or AI-specific models.

## Alternatives Considered

### No processing attempt identity

Rejected.

Document ID alone cannot distinguish competing processing attempts. An older
attempt can therefore publish stale state or derived content after a newer
attempt has started.

### GUID-only processing attempt identity

Rejected as the authoritative ordering mechanism.

A GUID can uniquely identify an attempt but does not inherently provide an
ordering relationship between attempts. The lifecycle requires deterministic
ordering so that an older attempt can be recognized as obsolete after a newer
attempt is established.

### Generic repository-wide concurrency handling

Rejected for this lifecycle decision.

The stale-result problem is specific to the document-processing workflow.
Introducing processing-generation semantics into every generic document CRUD
operation would unnecessarily broaden the responsibility of the generic
document repository.

Processing-specific lifecycle behavior should remain behind
processing-oriented application abstractions.

### Generation stored independently from the document

Rejected.

The document is the natural persistence boundary for the authoritative
ordering of processing attempts. Storing the generation independently would
introduce another coordination point for determining which attempt is
current.

### Generation column on every document chunk

Not selected for the lifecycle implementation.

The processing generation protects chunk publication at the document
processing boundary. Adding the generation redundantly to every chunk would
increase the persistence model without being required for the lifecycle
guarantee.

Stable chunk identity and provenance are addressed separately.

## Consequences

### Positive

* Processing attempts have an authoritative ordered identity.
* Older processing attempts cannot overwrite newer processing state.
* Older processing results cannot replace newer authoritative derived content.
* Same-document concurrent processing has deterministic persistence semantics.
* Sequential repeated processing remains deterministic and idempotent.
* Existing transactional chunk replacement remains useful.
* Processing-specific lifecycle concerns remain separated from generic document
  CRUD.
* Application and Domain remain independent of EF Core and SQLite.
* The design remains compatible with the existing encrypted SQLite persistence
  boundary.

### Negative

* The document persistence model requires a new processing-generation field.
* Processing persistence operations become conditional on the authoritative
  generation.
* Additional lifecycle tests are required.
* Existing repository and processing-store contracts may require
  processing-specific lifecycle operations.
* Database schema evolution requires an EF Core migration.

These trade-offs are acceptable because stale-result protection is required
for a reliable document-processing lifecycle.

## Implementation Constraints

The implementation must:

* establish processing generation atomically;
* prevent obsolete attempts from publishing document state;
* prevent obsolete attempts from publishing derived chunks;
* preserve transactional chunk replacement;
* preserve deterministic repeated processing;
* provide consistent cancellation behavior;
* provide consistent failure behavior;
* preserve existing extraction, normalization, chunking, and persistence
  boundaries;
* avoid logging sensitive document or security material;
* preserve the encrypted SQLite database boundary and existing database key
  management;
* remain independent of search, embeddings, vectors, and AI processing.

The exact Application method signatures, EF Core implementation details,
migration shape, and test structure are implementation concerns of the
reliable processing work.

## Related Decisions and Work

* ADR-0003 defines document import as separate from subsequent document
  processing.
* ADR-0005 establishes SQLite / EF Core as the persistence boundary for
  document metadata, processing state, and derived chunks.
* ADR-0009 establishes the encrypted SQLite provider and database
  key-management boundary.
* The reliable document processing lifecycle investigation established the
  need for authoritative processing-attempt identity and stale-result
  protection.
* The implementation is tracked by the reliable document processing task.
* Stable chunk identity and provenance are tracked separately.

## Result

DeskVault will treat processing generation as the authoritative ordered
identity of a document-processing attempt.

Processing state and derived content may be published only while the
processing attempt remains authoritative.

This provides the lifecycle guarantee required to prevent obsolete processing
attempts from overwriting newer document state or derived content while
preserving the existing document-processing architecture and persistence
boundaries.
