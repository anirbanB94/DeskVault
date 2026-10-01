# ADR-0004: Document Encryption at Rest

## Status

Accepted

## Context

DeskVault is designed as an offline-first knowledge platform with privacy and local data ownership as core requirements.

The current document storage implementation copies imported documents into:

`%LOCALAPPDATA%\DeskVault\Documents`

The stored file currently retains its original contents and extension. The generated GUID filename provides storage-level indirection, but it does not protect the document contents.

SHA-256 hashing is used for document identity and duplicate detection. A hash is not encryption and does not provide confidentiality.

Documents stored on the local filesystem must therefore be protected against unauthorized access to the application's storage directory.

## Decision

DeskVault will encrypt document contents before they are persisted to application-managed storage.

Encryption will be implemented behind the existing `IStorageService` abstraction so that the Application layer remains independent of the encryption implementation.

The initial storage flow will become:

Source Document
    ↓
Compute SHA-256
    ↓
Create or obtain document encryption key
    ↓
Encrypt document contents
    ↓
Store encrypted content

Authenticated encryption will be used so that stored documents provide both confidentiality and tamper detection.

AES-256-GCM will be the initial authenticated encryption algorithm.

Encryption keys will not be stored directly beside the document files. A locally protected key-management mechanism will be introduced using Windows-protected storage for the initial desktop implementation.

The key-management implementation will remain behind an Infrastructure abstraction so that it can be replaced or extended in the future.

The stored file will continue to use an application-generated identifier rather than the original filename.

The original file extension may be retained as metadata where useful, but the stored content itself will be encrypted and must not be treated as an ordinary user-openable file.

### Document-Owned Artifact Identity

Each persisted document has a canonical managed encrypted artifact identified by its document identifier.

For a document with identifier `{DocumentId}`, the canonical managed artifact is:

```text
%LOCALAPPDATA%\DeskVault\Documents\{DocumentId}.dvault
```

This canonical path is resolved by Infrastructure from the document identifier and the application-managed documents directory. Document-owned lifecycle operations use the document identifier through the storage/reader abstractions rather than accepting an arbitrary physical artifact path.

A persisted physical `StoredFilePath` remains document metadata and may be used as reconciliation evidence, but it is not authoritative for ordinary document-owned artifact access. A persisted path is considered owned by the document only when it resolves to the canonical managed artifact for that same document identifier. A matching `{DocumentId}.dvault` filename in another directory is not a document-owned artifact.

### Artifact Reconciliation and Recovery

Document-artifact reconciliation is the consistency capability used to compare persisted document metadata with the encrypted filesystem artifacts that belong to those documents.

Reconciliation must distinguish artifact identity and ownership before recovery is considered.

A readable artifact is not automatically a valid document artifact. Readability establishes only that the encrypted artifact can be opened and decrypted successfully. Ownership remains an exact identity rule enforced against the canonical managed document artifact.

For a persisted document, reconciliation may classify the expected artifact as:

- matched when the canonical owned artifact is readable and its decrypted content has the expected SHA-256 identity;
- missing when the canonical expected artifact is absent;
- a path mismatch when a persisted physical reference does not resolve to the canonical managed artifact;
- unreadable when the artifact cannot be authenticated, decrypted, or otherwise validated;
- a content mismatch when the artifact is readable but its decrypted content does not match the document's persisted SHA-256 identity.

A content mismatch must not be treated as a valid document-artifact relationship merely because the artifact is readable.

For encrypted artifacts without a corresponding persisted document, reconciliation may identify an orphan artifact. Automatic orphan cleanup is permitted only when the artifact yields a document identifier, the identifier is not present in persisted document metadata, and the physical artifact path passes the canonical document-owned artifact boundary. An artifact that lacks sufficient identity or fails ownership validation is preserved for further recovery rather than deleted.

Recovery is an explicit Application-layer capability consuming reconciliation findings. Detection and recovery remain separate responsibilities: reconciliation determines what was observed, while recovery decides which findings are safe to act on.

Recovery must never silently recreate missing encrypted content, adopt an orphan artifact as a new document, overwrite another document's artifact, or delete an artifact when ownership or document identity is ambiguous.

Recovery operations must be safe to repeat. A successful orphan cleanup leaves no managed artifact for the absent document, and a repeated recovery attempt must converge without modifying valid document/artifact relationships.

### Architectural Boundaries

The responsibilities remain separated:

- Application orchestrates document import and reconciliation/recovery behavior and does not perform encryption directly.
- Domain represents document metadata and business invariants.
- Infrastructure performs encryption, key management, canonical artifact-path resolution, physical storage, filesystem inspection, and encrypted-artifact validation.
- UI displays document and recovery outcomes and does not handle encryption keys or cryptographic operations.

The intended dependency flow is:

UI
 ↓
Application
 ↓
IStorageService / IDocumentReader / reconciliation and recovery handlers
 ↓
Infrastructure
 ├── Canonical artifact-path resolution
 ├── Encrypted Storage
 ├── Key Protection
 ├── File System inspection
 └── Encrypted-artifact validation

The Application layer must not construct or accept physical managed artifact paths for normal document lifecycle operations. Infrastructure owns the canonical document-to-artifact path rule.

## Consequences

### Positive

- Documents are protected at rest.
- Unauthorized users cannot simply open files from the DeskVault storage directory.
- Cryptographic operations remain isolated from the Application and UI layers.
- The storage implementation remains replaceable.
- Authenticated encryption provides confidentiality and tamper detection.
- The architecture leaves room for future key rotation and stronger key-management strategies.
- Document-owned artifact operations have one canonical identity and cannot intentionally select another document's managed artifact by supplying an arbitrary path.
- Reconciliation can distinguish readable content from an actually owned and content-consistent artifact.
- Safe orphan cleanup can converge after interrupted lifecycle operations without weakening valid document/artifact relationships.

### Negative

- Storage and retrieval become more complex.
- Encryption and decryption introduce additional CPU and I/O overhead.
- Key management becomes a critical security responsibility.
- Existing plaintext files created by earlier development versions require migration or cleanup.
- Files can no longer be opened directly from the managed storage directory.
- Reconciliation and recovery require additional validation and lifecycle handling for inconsistent states.

## Security Considerations

Encryption keys must never be:

- Hard-coded in source code.
- Stored in application configuration files.
- Stored in plaintext beside encrypted documents.
- Logged.
- Included in document metadata.

Cryptographic operations must use cryptographically secure random values for nonces and keys.

The implementation must authenticate encrypted content before returning decrypted data.

Cryptographic failures must not expose sensitive document contents through error messages or logs.

Document-owned lifecycle operations must not accept caller-controlled artifact paths that can escape the application-managed `Documents` directory. Canonical document-to-artifact resolution is therefore performed by Infrastructure from the document identifier.

Reconciliation must not treat readability as proof of ownership. Content validation must compare decrypted artifact content with the persisted document identity before classifying an artifact as matched.

Recovery must not expose document contents or cryptographic material through logs. Recovery decisions must be based on document identity and canonical ownership evidence rather than arbitrary physical paths.

### Recovery Safety Invariants

The following invariants apply to document-artifact recovery:

- Missing, unreadable, invalid, path-mismatch, and content-mismatch findings are preserved for recovery unless a separate explicit and sufficiently evidenced recovery policy exists.
- An orphan artifact is eligible for automatic cleanup only when its document identity is known, no persisted document with that identity exists, and canonical ownership validation succeeds.
- An orphan artifact without a usable document identity is preserved.
- A path outside the canonical managed artifact boundary is not eligible for automatic cleanup.
- Valid matched document/artifact relationships are not modified by reconciliation or recovery.
- Repeating reconciliation or recovery must not make an already valid relationship invalid.

## Current Implementation

The encryption-at-rest decision is implemented in the current MVP 1
document-storage workflow.

The current storage boundary is:

```text
Source Document
    ↓
Compute SHA-256
    ↓
Store through IStorageService
    ↓
AES-GCM encrypted content
    ↓
Application-managed `.dvault` file
```

Document content is stored separately from document metadata.

The encrypted document artifacts are stored under:

```text
%LOCALAPPDATA%\DeskVault\Documents
```

Each document's managed artifact uses the canonical identity:

```text
%LOCALAPPDATA%\DeskVault\Documents\{DocumentId}.dvault
```

The encryption implementation remains inside Infrastructure. The
Application layer interacts with storage through its abstraction and does
not perform cryptographic operations directly.

Encryption keys are protected using the Windows-protected key-management
implementation rather than being stored beside encrypted document files.

Document retrieval follows the corresponding protected path:

```text
Application
    ↓
IDocumentReader / storage abstraction
    ↓
Infrastructure
    ↓
Canonical artifact resolution from DocumentId
    ↓
Protected key material
    ↓
AES-GCM authentication and decryption
    ↓
Readable document stream
```

Ordinary document opening and lifecycle operations do not trust the persisted
physical `StoredFilePath` as the authoritative artifact selector. Reconciliation
may inspect that persisted value as evidence, but ownership is validated
against the canonical document-owned path.

Artifact reconciliation additionally validates content identity for a readable
artifact by hashing the decrypted content and comparing that SHA-256 value
with the persisted document's `Sha256Hash`. A readable artifact whose content
hash differs is classified as a content mismatch and is preserved rather than
treated as a valid match.

Recovery is implemented as a separate Application command over reconciliation
findings. It may clean up a confirmed orphan only when the artifact identity is
known, no persisted document with that identity remains, and the artifact path
passes the canonical ownership check. Findings with insufficient identity,
ambiguous ownership, missing artifacts, unreadable artifacts, path mismatches,
or content mismatches remain available for further recovery.

Recovery is intentionally idempotent and does not recreate, adopt, overwrite,
or delete ambiguous content merely to force metadata and filesystem state to
appear consistent.

## Result

DeskVault now protects imported document content at rest while keeping
document metadata and encrypted document content in separate storage
boundaries.

The resulting MVP 1 model is:

```text
Document Metadata
    ↓
SQLite / EF Core

Document Content
    ↓
Canonical managed encrypted `.dvault` File
    ↓
Windows-protected Key Material
```

The canonical document-to-artifact identity is enforced by Infrastructure,
while Application interacts with the storage and reader abstractions by
document identity. Persisted physical artifact references remain metadata and
are not treated as authoritative for ordinary document lifecycle access.

Reconciliation verifies both ownership and decrypted content identity before
classifying a document/artifact relationship as valid.

Recovery remains a separate, explicit capability. It performs automatic
cleanup only for sufficiently evidenced canonical orphans and preserves
ambiguous or otherwise unsafe findings for further recovery.

The encryption boundary remains replaceable through Infrastructure
abstractions and does not couple cryptographic implementation details to the
Domain, Application, or UI layers.

## Future Considerations

The following are intentionally deferred:

- Key rotation.
- Multiple encryption keys per document.
- Secure deletion.
- Backup and restore of encryption keys.
- Portable encrypted document export.
- Cross-device key synchronization.
- Enterprise key-management integration.
- Automated repair of missing or content-mismatched document artifacts.
- Bulk recovery tooling and operator-directed recovery workflows.
