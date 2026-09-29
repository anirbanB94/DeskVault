# ADR-0003: Document Import Workflow

## Status

Accepted

## Context

DeskVault needs a reliable document import workflow that can accept supported local files, prevent duplicate documents, store imported files under application-managed storage, and create the corresponding Domain document.

The import workflow crosses multiple architectural boundaries:

- Application validation and orchestration
- Domain document creation and invariants
- Infrastructure hashing
- Infrastructure file storage
- Repository persistence

The workflow must remain independent of the UI so that the same use case can later be triggered by other application entry points such as background processing or an API.

## Current Processing Boundary

The document import workflow establishes the stored document and its
persistent document state. Import does not perform the subsequent document
knowledge-processing pipeline.

After a document has been successfully imported and persisted, subsequent
processing is a separate Application-layer workflow. That workflow may
read the stored document, extract its content, normalize it, chunk it, and
persist the resulting derived representation.

Conceptually:

```text
Document Import
    ↓
Stored Document
    ↓
Separate Document Processing Workflow
    ↓
Extract
    ↓
Normalize
    ↓
Chunk
    ↓
Persist Derived Result
```

The processing workflow remains separate from document import. Its semantic
processing and orchestration boundary are described by ADR-0008, while the
authoritative processing lifecycle, processing-state transitions,
cancellation and failure behavior, retry/idempotency requirements, and
processing-generation fencing are governed by ADR-0010. The persistence
responsibility for processing state and document-to-chunk data is defined by
ADR-0005.

This separation keeps document acquisition and storage independent from
document knowledge processing. The import use case remains responsible for
establishing the document; the processing workflow is responsible for
creating derived knowledge representations from that stored document.

## Decision

DeskVault will implement document import as an Application-layer command use case.

The `ImportDocumentHandler` will orchestrate the workflow through application-defined interfaces.

The workflow is:

```text
ImportDocumentCommand
        |
        v
Validate
        |
        +---- Invalid ----> Validation Result
        |
        v
Compute SHA-256
        |
        v
Check Duplicate
        |
        +---- Exists ----> Duplicate Result
        |
        v
Generate Document ID
        |
        v
Store Encrypted Content
        |
        v
Verify Source Hash
        |
        +---- Mismatch ----> Import Failure
        |
        v
Create Domain Document
        |
        v
Persist Document
        |
        +---- SHA-256 Conflict ----> Duplicate Result
        |
        v
Success Result
```

## Current Implementation

The document import workflow is implemented as a dedicated Application
vertical slice.

The current import boundary is:

```text
ImportDocumentCommand
        |
        v
Validate
        |
        v
Compute SHA-256
        |
        v
Check Duplicate
        |
        +---- Exists ----> Duplicate Result
        |
        v
Generate Document ID
        |
        v
Store Encrypted Content
        |
        v
Verify Source Hash
        |
        +---- Mismatch ----> Import Failure
        |
        v
Create Domain Document
        |
        v
Persist Document Metadata
        |
        +---- SHA-256 Conflict ----> Duplicate Result
        |
        v
Success Result
```

The import workflow is responsible for establishing the stored document and
its persistent document metadata.

Document knowledge processing is intentionally separate. After successful
import, a separate Application-layer processing workflow may read the
stored document, extract content, normalize it, chunk it, and persist the
derived processing result.

The current Infrastructure implementation stores document content as
encrypted `.dvault` files and persists document metadata through SQLite and
Entity Framework Core.

Import therefore establishes the source document and its persistent
lifecycle state, while processing establishes derived knowledge
representations.

## Import Reliability Invariants

The import workflow establishes additional correctness guarantees across the
hashing, encrypted storage, and persistence boundaries.

### Content Identity

The SHA-256 hash calculated during the initial import phase must correspond
to the exact source bytes consumed by encrypted storage.

The storage implementation therefore hashes the source bytes while those
same bytes are passed to the encryption pipeline. After encryption completes,
the calculated hash is compared with the hash produced during the initial
import phase.

If the hashes do not match, the import must not establish a successful
document representation. This detects source mutation between the independent
hashing and storage reads and prevents document metadata from silently
describing different content from the stored encrypted artifact.

The storage implementation also removes the partially created encrypted
artifact when this consistency check fails.

### Provisional Stored Artifact

Encrypted source storage and document metadata persistence are separate
operations and cannot be treated as one atomic transaction.

Once encrypted storage succeeds and returns the stored artifact path, that
artifact is provisional until document persistence completes successfully.

The import handler therefore tracks the stored artifact and performs
compensating cleanup whenever the import does not complete successfully after
storage, including:

- metadata persistence failure;
- cancellation after source storage;
- a duplicate conflict caused by losing the persistence race.

Cleanup uses a non-cancelled cleanup operation so that the cleanup attempt is
not itself abandoned because the import cancellation token was cancelled.

A cleanup failure is logged without converting the original unsuccessful
import into a successful result.

A successfully completed import retains its valid encrypted artifact.

### Duplicate-Race Compensation

The application-level duplicate check remains the initial fast path, but it
is not the authoritative concurrency guarantee.

When concurrent imports of the same content both pass that check, the
persistence-level unique SHA-256 constraint determines which document can be
committed. The import that loses that persistence race receives the existing
duplicate outcome and its own provisional artifact is cleaned up.

Cleanup is restricted to the artifact created by the unsuccessful import and
does not perform general artifact reconciliation or document-removal work.

### Boundary Preservation

These reliability guarantees extend the existing import lifecycle without
changing the Application/Domain/Infrastructure boundaries, the document
identity model, or the encrypted storage architecture.

Import compensation is limited to artifacts created by the unsuccessful
current import. Pre-existing document/artifact reconciliation and document
removal remain separate responsibilities.

## Result

The import and processing responsibilities remain explicitly separated:

```text
Document Import
    ↓
Validate
    ↓
Hash
    ↓
Application duplicate fast path
    ↓
Encrypt + store source content
    ↓
Verify hash-to-stored-content consistency
    ↓
Persist document metadata
    ↓
Stored + Persisted Document
    ↓
Separate Processing Workflow
    ↓
Extract → Normalize → Chunk → Persist Derived Result
```

The import workflow now guarantees that the persisted document hash
corresponds to the source bytes consumed by encrypted storage and that a
stored artifact remains provisional until document persistence succeeds.

Unsuccessful imports are compensated by cleaning up the artifact created by
that import, while successful artifacts are preserved.

Concurrent duplicate protection is enforced at the persistence boundary, with
duplicate-race cleanup remaining part of the unsuccessful-import compensation
path.

General artifact reconciliation and document removal remain outside the
import workflow.
