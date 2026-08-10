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

## Architectural Boundaries

The responsibilities remain separated:

- Application orchestrates document import and does not perform encryption directly.
- Domain represents document metadata and business invariants.
- Infrastructure performs encryption, key management, and physical storage.
- UI displays import results and does not handle encryption keys or cryptographic operations.

The intended dependency flow is:

UI
 ↓
Application
 ↓
IStorageService
 ↓
Encrypted Storage
 ├── Key Protection
 └── File System

## Consequences

### Positive

- Documents are protected at rest.
- Unauthorized users cannot simply open files from the DeskVault storage directory.
- Cryptographic operations remain isolated from the Application and UI layers.
- The storage implementation remains replaceable.
- Authenticated encryption provides confidentiality and tamper detection.
- The architecture leaves room for future key rotation and stronger key-management strategies.

### Negative

- Storage and retrieval become more complex.
- Encryption and decryption introduce additional CPU and I/O overhead.
- Key management becomes a critical security responsibility.
- Existing plaintext files created by earlier development versions require migration or cleanup.
- Files can no longer be opened directly from the managed storage directory.

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

## Future Considerations

The following are intentionally deferred:

- Key rotation.
- Multiple encryption keys per document.
- Secure deletion.
- Backup and restore of encryption keys.
- Portable encrypted document export.
- Cross-device key synchronization.
- Enterprise key-management integration.