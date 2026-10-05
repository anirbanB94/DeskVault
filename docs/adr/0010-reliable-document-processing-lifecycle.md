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

Document processing lifecycle consistency is distinct from document-artifact
consistency. A persisted document record and its encrypted `.dvault` artifact
cross a database/filesystem persistence boundary. An interrupted import or
removal can therefore leave metadata and the corresponding encrypted
artifact temporarily inconsistent. That consistency problem must be handled
by document-artifact reconciliation and explicit recovery rather than by
changing the processing generation model.

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

Chunk replacement and successful-processing publication must form a single
atomic persistence boundary for an authoritative processing generation.

The successful publication operation must:

1. verify that the supplied processing generation is still authoritative;
2. replace the document's derived chunks with the candidate derived result;
3. mark the document `Available`;
4. set `LastSuccessfulProcessingGeneration` to the same processing generation;
5. commit all of those changes as one transaction.

The same successful publication must also persist
`LastSuccessfulProcessingRuleVersion` for the processing-rule version that
produced the candidate result.

Conceptually:

```text
Extract
    ↓
Normalize
    ↓
Chunk
    ↓
ONE TRANSACTION
    ├── Replace derived chunks
    ├── Status = Available
    ├── LastSuccessfulProcessingGeneration = current generation
    └── LastSuccessfulProcessingRuleVersion = current processing-rule version
    ↓
Commit
```

The candidate derived result remains non-authoritative until the transaction
commits successfully.

If processing fails, is cancelled, or the generation becomes stale before the
transaction commits, the transaction must not leave a partially replaced
derived result behind. The previous successful derived result and its
`LastSuccessfulProcessingGeneration` must remain intact.

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

### Processing Rule Version

`ProcessingGeneration` identifies a processing attempt and establishes
which attempt is authoritative.

`DocumentProcessingRuleVersion` identifies the processing behavior used
by that attempt, including the applicable extraction and normalization
rules.

These concepts are intentionally independent:

```text
ProcessingGeneration
    ↓
Which processing attempt?

DocumentProcessingRuleVersion
    ↓
Which processing rules produced the result?
```

Equivalent processing behavior must resolve to the same processing-rule
version, while behaviorally relevant rule changes must be capable of
producing a different version.

The processing-rule version does not replace, derive from, or participate
in authoritative generation fencing.

### Last Successful Processing Generation

The document persistence boundary also records the last processing generation
whose derived result was successfully published.

`ProcessingGeneration` identifies the current authoritative processing attempt.
`LastSuccessfulProcessingGeneration` identifies the most recent processing
attempt for which derived content and the corresponding successful document
state were published.

These values serve different purposes and must not be treated as interchangeable.

A successful processing attempt updates
`LastSuccessfulProcessingGeneration` in the same atomic transaction that
replaces its derived content and publishes `Available`.

The generation therefore becomes the last-successful generation only when the
corresponding derived representation and successful document state have been
committed together.

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

### Last Successful Processing Rule Version

The document persistence boundary also records the processing-rule version
associated with the last successfully published derived representation.

`DocumentProcessingRuleVersion` identifies the processing behavior used by
the processing attempt. `LastSuccessfulProcessingRuleVersion` identifies
the processing-rule version of the derived result represented by
`LastSuccessfulProcessingGeneration`.

These values are intentionally paired:

```text
LastSuccessfulProcessingGeneration
            +
LastSuccessfulProcessingRuleVersion
            ↓
The same successfully published derived representation
```

A successful processing attempt updates `LastSuccessfulProcessingRuleVersion`
in the same atomic transaction that replaces its derived chunks, publishes
`Available`, and updates `LastSuccessfulProcessingGeneration`.

The processing-rule version must never become current independently of the
successful derived representation. Starting a new processing attempt advances
`ProcessingGeneration` but does not change the last successful processing-rule
version.

When a processing attempt is cancelled, fails, or becomes stale, the prior
successful processing-rule version remains intact together with the prior
successful derived result. If no successful processing result exists, the
processing-rule version remains unknown (`NULL`).

The processing-rule version is lineage metadata, not a replacement for
processing generation and not part of authoritative generation fencing.

### Concurrency

For repeated processing of the same document, persistence behavior must be
deterministic.

If multiple processing attempts compete for the same document, only the
authoritative processing generation may publish the resulting state and
derived content.

The implementation must not rely on timing assumptions to determine which
attempt is authoritative.

### Processing Resource Boundary

Document processing must operate within an explicit maximum plaintext-size
boundary.

The configured boundary applies to plaintext produced while decrypting the
stored document for processing. The decryption boundary must evaluate the
cumulative plaintext size before allocating buffers for the next encrypted
chunk.

If the next decrypted chunk would cause the configured maximum to be
exceeded, processing must fail with the dedicated
`ResourceLimitExceededException`.

A resource-limit failure is a processing failure, not a successful completion
and not a cancellation. The existing processing lifecycle therefore records
the processing attempt as `Failed` through the normal generation-aware failure
publication path.

The resource boundary is a processing policy and does not change the
encrypted document format, encryption algorithm, or key-management boundary.

The reader contract keeps the resource limit optional so consumers outside
the processing workflow are not implicitly subject to the processing
boundary.

The boundary limits decrypted source materialization. It is not a
process-wide operating-system memory cap. Extractors, normalization, and
chunking may still materialize representations of content within the
supported processing boundary; those stages remain subject to the bounded
input established here.

Processing also enforces an explicit maximum processed-text size.

The configured processed-text boundary applies to text accumulated by
document extraction and normalization before chunking. Implementations must
check the cumulative processed-text size before accepting additional content.

If the configured maximum processed-text size would be exceeded, processing
must fail with the dedicated `ResourceLimitExceededException`.

The decrypted-source boundary and processed-text boundary are independent
processing-policy limits. The decrypted-source boundary protects source
materialization, while the processed-text boundary protects accumulation of
the derived textual representation.

Neither boundary is a process-wide operating-system memory cap. Parser-specific
preview limits remain separate from these processing resource policies.

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

### Document Artifact Consistency

Document processing lifecycle consistency and document-artifact consistency
are separate architectural concerns.

The document record in SQLite is the authoritative metadata representation,
while the corresponding encrypted `.dvault` artifact is the authoritative
stored-content representation. Their relationship crosses the database and
filesystem persistence boundary.

Each document therefore has one canonical managed artifact identity derived
from its document identifier:

```text
%LOCALAPPDATA%\DeskVault\Documents\{DocumentId}.dvault
```

Ownership is an exact identity rule, not a filename-only rule. A persisted
`StoredFilePath` is considered an owned artifact reference only when it
resolves to that document's canonical managed artifact. A path containing the
same `{DocumentId}.dvault` filename in another directory is not considered
owned by the document.

The canonical ownership rule is enforced through application-defined storage
abstractions. Ordinary document opening and lifecycle operations resolve the
managed artifact from `DocumentId` rather than trusting the persisted physical
path. Reconciliation may inspect `StoredFilePath` as evidence, but it must
validate the reference against the canonical ownership boundary before it is
classified as valid.

Artifact reconciliation is responsible for detecting and safely classifying
pre-existing inconsistencies such as:

- a persisted document record whose expected encrypted artifact is missing;
- a persisted document record whose stored artifact reference is outside its
  canonical document-owned boundary;
- an encrypted artifact with no corresponding persisted document record;
- an encrypted artifact that exists but cannot be opened or validated;
- an encrypted artifact that is readable but whose decrypted content does not
  match the persisted document identity;
- incomplete import or removal operations that leave metadata and artifacts
  inconsistent.

Readability alone is not sufficient to establish a valid relationship.
For a readable artifact associated with a persisted document, reconciliation
must compare the SHA-256 identity of the decrypted content with the
document's persisted `Sha256Hash`. A different content hash is an explicit
content-mismatch outcome and must not be treated as a match.

A persisted artifact-reference ownership mismatch must be surfaced as a path
mismatch and must not be treated as a valid readable artifact merely because
the file exists or the filename matches the document identifier.

Artifact reconciliation must remain detection-oriented. The reconciliation
query reports findings but does not perform destructive repair.

### Artifact Recovery

Recovery is a separate Application-layer capability that consumes the
reconciliation result and applies only explicitly safe recovery actions.

The safe automatic recovery rule for an orphaned artifact is:

```text
Artifact is orphaned
        ↓
Document identity is known
        ↓
No persisted document with that identity exists
        ↓
Artifact path is the canonical managed path for that identity
        ↓
Cleanup may proceed
```

If the artifact has no usable document identity, if a persisted document with
that identity still exists, or if canonical ownership validation fails, the
artifact is preserved for further recovery.

Missing expected artifacts are not silently recreated. Unreadable or invalid
artifacts are not silently replaced. Path mismatches are not adopted. Content
mismatches are not silently overwritten or repaired.

Recovery therefore prefers preservation when evidence is insufficient rather
than destructive normalization.

Recovery must be retry-safe. If cleanup is interrupted or a repeated
reconciliation finds the same orphan again, repeating the recovery operation
must converge without modifying a valid document/artifact relationship.

A successful orphan cleanup removes only the confirmed canonical artifact
owned by the absent document identity. It must not select another document's
artifact or use an arbitrary persisted physical path as the deletion target.

Recovery must not change the processing generation, processing state, or
derived chunks of any document. Processing-generation authority remains
governed exclusively by this ADR's processing lifecycle rules.

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

This separation also keeps artifact reconciliation and recovery independent
from chunk identity and provenance.

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
* Document-artifact consistency has a distinct reconciliation boundary.
* Reconciliation can distinguish ownership, readability, and content identity.
* Safe orphan cleanup can converge after interrupted import or removal
  lifecycles.
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
* Document-artifact reconciliation and recovery require additional detection,
  validation, and recovery-path tests.

These trade-offs are acceptable because stale-result protection is required
for a reliable document-processing lifecycle and artifact consistency must be
handled explicitly at the database/filesystem boundary.

## Implementation Constraints

The implementation must:

* establish processing generation atomically;
* prevent obsolete attempts from publishing document state;
* prevent obsolete attempts from publishing derived chunks;
* preserve transactional chunk replacement;
* make successful derived-content replacement, `Available` publication, and
  `LastSuccessfulProcessingGeneration` update one atomic commit boundary;
* preserve deterministic repeated processing;
* provide consistent cancellation behavior;
* provide consistent failure behavior;
* preserve existing extraction, normalization, chunking, and persistence
  boundaries;
* avoid logging sensitive document or security material;
* preserve the encrypted SQLite database boundary and existing database key
  management;
* remain independent of search, embeddings, vectors, and AI processing;
* keep document-artifact reconciliation separate from processing-generation
  authority;
* keep reconciliation detection-oriented and separate from destructive
  recovery;
* never silently recreate, overwrite, adopt, or activate document artifacts
  during reconciliation;
* keep destructive orphan cleanup behind an explicit safe recovery decision
  supported by sufficient evidence;
* only clean an orphan when its document identity is known, no persisted
  document with that identity exists, and canonical ownership validation
  succeeds;
* preserve missing, unreadable, invalid, path-mismatch, and content-mismatch
  findings when evidence is insufficient for safe cleanup;
* verify readable artifact content identity against the persisted document
  SHA-256 before classifying the relationship as matched;
* preserve valid document/artifact relationships during recovery;
* make recovery safe to repeat after partial execution or repeated
  reconciliation;
* never use an arbitrary persisted physical path as the ordinary document
  read or deletion target;
* enforce the configured maximum plaintext size before allocating the next
  decrypted chunk;
* distinguish resource-limit failures from cancellation and successful
  processing;
* preserve the existing encrypted document format and key-management
  boundary;
* keep processing resource policy separate from parser-specific preview
  limits;
* resolve ordinary document-owned artifact access from the document
  identifier;
* validate persisted artifact references against the canonical managed
  document-owned artifact before treating them as valid;
* reject or surface same-identity artifact paths outside the managed document
  storage boundary as ownership mismatches.

The exact Application method signatures, EF Core implementation details,
migration shape, and test structure are implementation concerns of the
reliable processing work and the separate document-artifact reconciliation
and recovery work.

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
* The document-artifact reconciliation work establishes a separate
  consistency boundary between persisted document metadata and encrypted
  document artifacts.
* The document-owned artifact boundary work establishes canonical
  `{DocumentId}.dvault` identity and managed-storage ownership enforcement.
* The artifact-reconciliation recovery work establishes safe, explicit orphan
  cleanup and preservation of ambiguous findings.
* The bounded document-processing resource policy is part of this lifecycle
  decision.
* Stable chunk identity and provenance are tracked separately.
* Document-artifact reconciliation and recovery are tracked separately from
  database migration recovery.

## Result

DeskVault will treat processing generation as the authoritative ordered
identity of a document-processing attempt.

Processing state and derived content may be published only while the
processing attempt remains authoritative.

Document-artifact consistency is handled separately through reconciliation
between persisted document metadata and encrypted `.dvault` artifacts.

The document-artifact consistency boundary uses canonical document-owned
artifact identity. Ordinary document access resolves the managed artifact from
the document identifier, while reconciliation treats persisted physical
artifact references as evidence that must satisfy the canonical ownership
rule before they are considered valid.

Reconciliation additionally verifies decrypted content identity against the
persisted document SHA-256 before classifying an artifact relationship as
matched.

Recovery remains separate from detection and may automatically clean only a
confirmed canonical orphan whose identity is known, whose document is no
longer persisted, and whose ownership validation succeeds. Missing,
unreadable, path-mismatch, content-mismatch, or otherwise ambiguous findings
are preserved for further recovery rather than normalized destructively.

Recovery does not alter processing-generation authority, processing state, or
derived chunks.

This separation prevents obsolete processing attempts from overwriting newer
document state or derived content while ensuring that database/filesystem
artifact inconsistencies are detected and handled without weakening encrypted
storage, key-management, ownership, or document-processing lifecycle
boundaries.

Successful processing-rule lineage remains bound to that same successful
derived result and processing generation rather than becoming independently
authoritative.
