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

The processing lifecycle, orchestration boundary, processing state,
cancellation, retry/idempotency requirements, and document-to-chunk
persistence are governed by ADR-0008.

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
Store File
        |
        v
Create Domain Document
        |
        v
Persist Document
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
Create Domain Document
        |
        v
Persist Document Metadata
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

## Result

The import and processing responsibilities remain explicitly separated:

```text
Document Import
    ↓
Stored + Persisted Document
    ↓
Separate Processing Workflow
    ↓
Extract → Normalize → Chunk → Persist Derived Result
```

This separation allows document acquisition and storage to remain
independent from downstream document knowledge processing.
