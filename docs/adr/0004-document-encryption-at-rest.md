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

Encryption key management is part of this security boundary. The master encryption key must remain protected at rest, and its initial creation must not permit concurrent application
operations to establish competing authoritative keys. If different callers use different keys while writing or reading encrypted content, the persisted artifact may become unreadable.

## Decision

DeskVault will encrypt document contents before they are persisted to application-managed storage.

Encryption will be implemented behind the existing `IStorageService` abstraction so that the Application layer remains independent of the encryption implementation.

The initial storage flow will become:

Source Document
    ↓
Compute SHA-256
    ↓
Resolve the initialized document-encryption key (initial provisioning belongs to authorized vault initialization)
    ↓
Encrypt document contents
    ↓
Store encrypted content

Authenticated encryption will be used so that stored documents provide both confidentiality and tamper detection.

AES-256-GCM will be the initial authenticated encryption algorithm.

Encryption keys will not be stored directly beside the document files. A locally protected key-management mechanism will be introduced using Windows-protected storage for the initial desktop implementation.

The key-management implementation will remain behind an Infrastructure abstraction so that it can be replaced or extended in the future.

### Atomic Master-Key Provisioning

Initial master-key creation must be atomic with respect to publication of the canonical key file.

The master key is stored in the application-managed security directory as `master.key` and is protected using Windows DPAPI with `DataProtectionScope.CurrentUser`.

When the canonical key file already exists, the service reads and unprotects that persisted key rather than generating and writing a replacement.

When the canonical key file does not exist, the service generates a candidate key and protects it before persistence. The protected candidate is written to a uniquely named temporary file in the same security directory.
The temporary file is flushed before publication.

Publication uses a same-directory file move that does not overwrite an existing destination. If another initializer publishes `master.key` first, the losing initializer discards its candidate and reads the authoritative persisted key.

The canonical `master.key` file is the source of truth after publication. A temporary candidate is never an alternative authoritative key. Concurrent initialization attempts must therefore converge on the same persisted key,
or fail without silently replacing an existing canonical key.

The implementation must preserve existing-key retrieval and must not log key material. Temporary protected data and unpublished in-memory key candidates are cleared when practical.

Candidate ownership begins only after exclusive create-new file creation succeeds; generating or assigning a candidate path alone does not establish ownership. If exclusive creation fails, the operation must not delete or modify a pre-existing file at that path.
The owning operation attempts best-effort cleanup only for its own candidate while ownership remains established. Once the non-overwriting move publishes the candidate as `master.key`, temporary-candidate ownership is relinquished and no subsequent cleanup may act
on the canonical key path. A losing initializer retrieves the canonical key persisted by the winner and cleans up only its own candidate.

A candidate left by abrupt process termination remains non-authoritative. Subsequent initialization does not promote or reuse it, and does not automatically delete it based only on its age, filename, the absence of `master.key`, or presumed inactivity.
Since the runtime cannot establish that an abandoned candidate is unowned by a live contender, such candidates are preserved. Cleanup failures use the existing generic, non-secret diagnostic and must not disclose key material, replace a primary provisioning exception or cancellation,
or turn successful retrieval of an authoritative key into a provisioning failure.

This section addresses atomic initial master-key provisioning. Strict existing-key retrieval and the provisioning authorization boundary are defined in the following section. The shared key identity/version contract is defined in the following subsection. Database-provider behavior is described by ADR-0009,
while encrypted-record format and compatibility requirements are defined in the Encrypted Document Record Format and Integrity section below. Intentional key replacement and rotation are unsupported by the current runtime. Missing or unusable key material is never evidence of authorization to start a new key lineage.

The stored file will continue to use an application-generated identifier rather than the original filename.

The original file extension may be retained as metadata where useful, but the stored content itself will be encrypted and must not be treated as an ordinary user-openable file.

### Strict Existing-Key Retrieval for Document Operations

Ordinary document encryption and normal document decryption use `IEncryptionKeyService.GetKeyAsync()` to retrieve the existing master key.

Strict retrieval must read the canonical `master.key`, unprotect it under the current Windows security context, and validate the retrieved 32-byte key before using it for document encryption or decryption. It must not provision a replacement when the expected key is missing, invalid, unavailable, or cannot be unprotected.

A failed retrieval must not create the security directory as a side effect of retrieval, generate a candidate key, create a replacement `master.key`, overwrite protected key material, or delete or otherwise modify stored key state. File-access failures remain failures and must not be converted into permission to provision another key.

`IEncryptionKeyProvisioningService.GetOrCreateKeyAsync()` is a provisioning-capable operation, not a replacement, rotation, repair, or recovery API. Callers may invoke it only from a lifecycle context that has explicitly established authorization for initial provisioning. The retrieval-only `IEncryptionKeyService` exposes only `GetKeyAsync()`
and does not inherit or expose provisioning capability. `DatabaseInitializer` owns the supported provisioning contexts: initial vault setup before document data exists, and compatibility initialization of an existing empty database when there is no document metadata and no managed document artifact. Ordinary document encryption and normal document
decryption must use `IEncryptionKeyService.GetKeyAsync()` to retrieve the established key. If the expected key is missing or unusable, a document write or read fails; ordinary document operations must not provision a new key.

Key-related failure diagnostics must not disclose plaintext key bytes, protected key blobs, decrypted document content, or credentials. Failures are surfaced through the existing Infrastructure exception and logging boundary without silent key recovery.

### Key Identity and Version Semantics

The document and database key services use a shared key-metadata payload for newly provisioned keys. The complete payload is protected as one unit with Windows DPAPI under `DataProtectionScope.CurrentUser`; plaintext payload bytes are not persisted as a key artifact.

The current plaintext payload layout (after DPAPI unprotection) is 58 bytes:

| Offset | Length | Meaning |
| --- | ---: | --- |
| 0 | 4 bytes | ASCII marker `DVKP` |
| 4 | 1 byte | Payload format version; currently `1` |
| 5 | 1 byte | Key purpose: `1` for document encryption, `2` for database encryption |
| 6 | 16 bytes | Non-empty key-lineage `Guid`, encoded by .NET `Guid` byte APIs |
| 22 | 4 bytes | Positive, little-endian signed 32-bit key lifecycle version |
| 26 | 32 bytes | AES-256 key bytes |

The payload format version, key lifecycle version, and encrypted-document record version are separate concepts. New `.dvault` writes use record format version `3`. The reader continues to support format version `2` under the explicit legacy compatibility contract below.
Neither record format version is the key lifecycle version, and changing the document-record framing does not change the DPAPI key metadata payload.

`KeyId` identifies a logical key lineage, while `KeyVersion` identifies a concrete key within that lineage. Newly provisioned keys receive a new non-empty `KeyId` and start at key version `1`. Normal retrieval preserves the persisted identity and version; it does not generate or rewrite lifecycle metadata.
`GetKeyAsync()` is strictly non-provisioning and fails when the expected key is missing, unreadable, invalid, unprotectable, or has a purpose marker that does not match the retrieving service. `IEncryptionKeyProvisioningService.GetOrCreateKeyAsync()` provisions only when the expected canonical key artifact is absent;
it does not silently replace an unusable existing artifact. The caller must still establish that initial provisioning is authorized by the owning lifecycle context.

The lifecycle distinction for future operations is:

- An authorized rotation within the same lineage retains `KeyId` and increases `KeyVersion` for the new concrete key.
- An intentional replacement that starts a new lineage uses a new `KeyId` and starts that lineage at version `1`.
- Missing, unreadable, invalid, or unprotectable key material is not evidence that replacement or rotation was intended. Normal retrieval fails without creating or changing the canonical key artifact.

Intentional key replacement and rotation are unsupported by the current runtime. Replacement must not be inferred from a missing, unreadable, invalid, or unprotectable key. A future replacement would require a separately authorized workflow with an explicit data-transition decision; it must not silently make existing ciphertext inaccessible. A future rotation within an established lineage must retain `KeyId`, increase `KeyVersion`,
transition and verify all dependent encrypted data before the new key becomes the sole canonical key, and provide a recoverable failure path. Since the current runtime has no multi-key hierarchy or rotation workflow, it must not leave a mixed state that requires implicit fallback to an earlier key. Operational recovery remains separate and must restore the established key identity/version rather than inventing a new lineage.

#### Legacy Pre-Versioned Key Artifacts

Legacy `master.key` and `database.key` artifacts contain only the raw 32-byte key within their DPAPI-protected payload and have no embedded identity, version, or purpose marker. The decoder represents these artifacts using key version `0` and derives a deterministic non-secret `KeyId` from a domain-separated SHA-256 digest using `DeskVault.LegacyProtectedKeyIdentity.v1`, the expected purpose, and the exact persisted protected artifact.
This preserves repeatable identity for repeated reads without persisting plaintext metadata, rewriting the artifact, replacing key bytes, or requiring existing encrypted data to be re-encrypted.

Because a legacy payload has no embedded purpose marker, its purpose is inferred from the canonical key service/path that reads it. The decoder therefore cannot prove from a legacy artifact alone that a blob copied between the document and database key paths was originally created for the receiving purpose. New metadata-bearing payloads include an explicit purpose marker and mismatched purpose is rejected.

A legacy `KeyId` is derived from the exact protected artifact. Re-protecting the same raw key into a different DPAPI blob can therefore produce a different derived identifier. The current compatibility behavior preserves the original artifact rather than re-protecting it. Any future transition from legacy metadata must explicitly preserve or migrate the established lineage; normal retrieval must not silently perform that transition.

#### Binding Document Ciphertext to Key Lineage

For version-2 records using versioned key material (key version greater than `0`), `DocumentEncryptionService` supplies AES-GCM associated data consisting of the `DeskVault.DocumentEncryption.KeyLineage.v1` domain separator, `KeyId`, and the little-endian key version. This authenticates the document ciphertext against the key identity/version returned by the key service without placing key bytes or lifecycle metadata in ordinary logs
or plaintext configuration.

For version-2 records using key version `0`, associated data remains empty so existing encrypted documents created before key identity/version support remain decryptable. Consequently, the legacy version-2 document format does not authenticate a derived legacy identity/version against ciphertext.

Format-3 records use the distinct `DeskVault.DocumentEncryption.Record.v3` associated-data domain. Their authenticated associated data binds the key identity/version, document identifier, record type, chunk sequence position, and declared data-chunk length. The authenticated final record binds the complete chunk count and total plaintext length. This extends the key-lineage guarantee without changing the key payload format or introducing another encryption hierarchy.

#### Encrypted Document Record Format and Integrity

The encrypted-document record format version identifies the on-disk framing and integrity contract. It is separate from the key lifecycle version and the key payload format version.

New writes use format version `3`. Readers retain support for legacy version-2 artifacts.

##### Record Header

Every supported `.dvault` record begins with an 8-byte header:

| Field | Length | Meaning |
| --- | ---: | --- |
| File magic | 4 bytes | Fixed DeskVault encrypted-document marker |
| Record format version | 4 bytes | Little-endian unsigned version value |

The parser must validate the complete header, the expected magic, and a supported version before processing any record body. A partial header, invalid magic, or unsupported version must fail explicitly; unsupported versions must not be treated as normal encrypted data.

The format version is a record-framing value. It must never be interpreted as `KeyVersion`.

##### Version-3 Data Chunks

A format-3 record contains zero or more authenticated data chunks followed by one mandatory authenticated final record.

Each data chunk contains:

| Field | Length | Meaning |
| --- | ---: | --- |
| Ciphertext length | 4 bytes | Positive little-endian length |
| Nonce | 12 bytes | AES-GCM nonce |
| Authentication tag | 16 bytes | AES-GCM authentication tag |
| Ciphertext | Declared length | Encrypted plaintext bytes |

A data chunk must have a length greater than zero and no greater than 1 MiB. The declared length must be structurally available and must agree with the ciphertext bytes available in the record.

A zero length is reserved for the version-3 final record and is not a valid data chunk. Missing length bytes, incomplete nonce, incomplete authentication tag, and incomplete ciphertext must all be rejected.

Every data chunk is authenticated independently using AES-GCM. Its associated data binds the chunk to:

- The version-3 record domain.
- The key identity and lifecycle version supplied by the key service.
- The document identifier.
- The data-chunk record type.
- The zero-based chunk sequence position.
- The declared plaintext/ciphertext length.

The same key and associated-data rules must be used for encryption and decryption. Chunks must not be accepted when moved to another position or substituted into a different document's artifact. Structural validation alone does not establish ciphertext authenticity; AES-GCM authentication remains authoritative.

No chunk's plaintext bytes may be written to the returned stream or made available to a caller unless authentication for that chunk succeeds.

##### Version-3 Authenticated Final Record

After all data chunks, a format-3 record must contain one final record. Its serialized structure is:

| Field | Length | Meaning |
| --- | ---: | --- |
| Length marker | 4 bytes | Little-endian zero, reserved for the final record |
| Nonce | 12 bytes | AES-GCM nonce |
| Authentication tag | 16 bytes | Authenticated final-record tag |
| Ciphertext | None | The final record contains no data ciphertext |

The final record authenticates an empty plaintext under AES-GCM. Its associated data binds the record to the final-record type, the key identity/version, the document identifier, the total number of data chunks, and the total plaintext length.

The final record is mandatory, including for an empty document. A version-3 empty document is represented by its header followed immediately by the authenticated final record; a zero-length data chunk is never emitted.

The reader must validate the final-record authentication tag and then require exact end-of-file. EOF before the final record, a partial final record, an invalid final tag, or trailing bytes after the final record must be rejected.

The final record prevents EOF at an otherwise complete data-chunk boundary from being sufficient evidence that a version-3 document is complete. It authenticates the declared end of the document against the accumulated chunk count and plaintext length.

##### Legacy Version-2 Compatibility

The version-2 record consists of the 8-byte header followed by zero or more chunks using the legacy structure: a 4-byte little-endian length, a 12-byte nonce, a 16-byte authentication tag, and ciphertext of the declared length.

Version-2 compatibility includes:

- Valid single- and multi-chunk documents.
- Header-only representation of an empty document.
- Existing key-lineage associated-data behavior for versioned keys.
- Empty associated data for legacy version-zero keys.
- Rejection of invalid magic, unsupported versions, invalid or oversized chunk lengths, incomplete fields, truncated ciphertext, and AES-GCM authentication failures.

Version 2 has no mandatory authenticated final record. Its ciphertext associated data does not bind a chunk to a document identifier or sequence position. The framing alone therefore cannot distinguish a complete document from a record truncated between otherwise complete chunks, nor can it independently establish document identity binding or chunk ordering.

Compatibility must not silently claim the stronger guarantees of version 3. The persisted document SHA-256 identity is the required whole-content integrity check for both format versions.

##### Persisted Content-Identity Verification

`Document.Sha256Hash` is the persisted identity of the complete original document content. A document must not be reported as successfully opened or as a matched artifact until the complete encrypted record has been processed and the SHA-256 hash of the complete plaintext has been compared with the expected persisted hash.

The `IDocumentReader` contract requires callers to supply the persisted expected SHA-256 value. Ordinary document opening uses `OpenReadAsync` with a finite, positive plaintext-size limit. It decrypts into a private, operation-owned staging stream and returns that stream only after record authentication, record-completeness checks, and the complete plaintext hash comparison succeed.
The configured limit bounds the number of plaintext bytes staged; the stream's allocated capacity can be higher because of buffer growth. If the limit is exceeded, decryption or authentication fails, cancellation occurs, or the content identity does not match, the read must fail rather than return partial or unverified plaintext.

Artifact reconciliation uses the separate `ValidateContentHashAsync` contract. Infrastructure decrypts the complete artifact into an operation-owned `HashingWriteStream`, which incrementally computes SHA-256 without retaining the complete plaintext. The validation call uses an explicit finite plaintext-size limit, compares the computed digest with the persisted expected hash,
and completes only after all required encrypted content has been processed. The hash-only validation path must not allocate a full-plaintext staging buffer or return plaintext to the caller.

The hash comparison must be case-insensitive for its hexadecimal representation. The implementation must not expose the calculated hash, expected hash, plaintext, key material, or authentication data through exception messages or diagnostics. If ordinary opening fails after staging allocation, the reader must explicitly overwrite the available backing array of its operation-owned
plaintext staging `MemoryStream` with `CryptographicOperations.ZeroMemory` before disposing it. `ZeroingMemoryStream` must also clear superseded staging backing arrays when stream growth replaces them. These guarantees apply to staging arrays owned by the stream and do not guarantee erasure of copies maintained by the runtime or cryptographic provider.

A format-2 document truncated at a complete chunk boundary must not be accepted when the resulting plaintext does not match its persisted whole-document hash. The same persisted-content verification applies to format-3 records, in addition to format 3's authenticated final record.

Reconciliation must classify a persisted-content identity mismatch as a content mismatch, not as a valid matched artifact. A resource-limit, cryptographic, format, or I/O failure that prevents complete validation remains an unreadable-artifact condition. An incomplete or cancelled validation must never be treated as a successful match; cancellation must remain cancellation.
An integrity failure must not trigger silent replacement, adoption, or deletion of content, and findings for unrelated artifacts should continue where safe to do so.

The SHA-256 check is a consistency check against persisted metadata, not a replacement for AES-GCM authenticity. It does not make legacy format-2 ciphertext independently bound to the document identity or restore its missing authenticated terminator.

##### Distinction from Persisted Document Chunks

Encrypted-record data chunks and final records belong to the Infrastructure encryption format. They are transport/framing units for protected file bytes.

They are separate from persisted application-level `DocumentChunks`, whose logical identity, content hash, processing provenance, ordering, and optional source-location semantics are governed by ADR-0011. Changing encrypted-record chunk boundaries must not change the canonical persisted knowledge-chunk contract.

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

A readable artifact is not automatically a valid document artifact. Successful decryption and authentication do not alone establish that the plaintext matches the persisted content identity. Reconciliation validates the complete plaintext against the persisted SHA-256 value through `IDocumentReader.ValidateContentHashAsync`, using incremental hashing without retaining a full plaintext buffer or returning a plaintext stream. Ordinary opening remains a separate bounded `OpenReadAsync` operation.

For a persisted document, reconciliation may classify the expected artifact as:

- matched when the canonical owned artifact is readable and its complete decrypted content has the expected SHA-256 identity;
- missing when the canonical expected artifact is absent;
- a path mismatch when a persisted physical reference does not resolve to the canonical managed artifact;
- unreadable when the artifact cannot be authenticated, decrypted, or otherwise structurally validated;
- a content mismatch when complete decrypted content does not match the document's persisted SHA-256 identity.

A content mismatch must not be treated as a valid document-artifact relationship merely because the artifact is readable or its individual encrypted chunks authenticate successfully.

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
- The architecture leaves room for a future explicitly authorized key-rotation workflow, while the current runtime does not support rotation or intentional key replacement.
- Concurrent first-time master-key initialization converges on one authoritative persisted key rather than allowing a losing initializer to overwrite the canonical key.
- Existing-key retrieval continues to use the protected persisted key.
- Ordinary document encryption and normal document decryption retrieve the established master key rather than silently provisioning a replacement when the expected key is unavailable.
- Document-owned artifact operations have one canonical identity and cannot intentionally select another document's managed artifact by supplying an arbitrary path.
- Reconciliation can distinguish readable content from an actually owned and content-consistent artifact.
- Format-3 records authenticate document identity, chunk sequence, declared data-chunk lengths, and whole-document completion.
- A required authenticated final record prevents a format-3 record from being accepted as complete solely because EOF occurs between valid chunks.
- Whole-document SHA-256 verification prevents the reader from returning plaintext that differs from the persisted document content identity, including for compatible legacy format-2 artifacts.
- Legacy version-2 compatibility is explicit about integrity properties that its record structure cannot independently guarantee.
- Safe orphan cleanup can converge after interrupted lifecycle operations without weakening valid document/artifact relationships.

### Negative

- Storage and retrieval become more complex.
- Encryption and decryption introduce additional CPU and I/O overhead.
- Key management becomes a critical security responsibility.
- Atomic provisioning adds temporary-file creation, flushing, publication, and cleanup handling.
- An abrupt process or system termination can leave an unpublished temporary key file. Such a file is not authoritative and must not be mistaken for `master.key`.
- Existing plaintext files created by earlier development versions require migration or cleanup.
- Files can no longer be opened directly from the managed storage directory.
- Reconciliation and recovery require additional validation and lifecycle handling for inconsistent states.
- Format 3 introduces a mandatory authenticated final record and stricter framing requirements that legacy artifacts do not contain.
- Legacy format-2 records cannot independently authenticate document identity, chunk ordering, or whole-document completion using their record structure alone. Their compatibility depends on validating the complete decrypted plaintext against the persisted document SHA-256 identity.
- Ordinary document opening must hold staged plaintext while verifying the whole-document hash before returning a stream; the configured finite limit caps the plaintext length accepted for opening, but not the exact peak memory allocation; stream-capacity growth and transient decryption buffers add overhead. Reconciliation avoids that full-plaintext buffer by hashing decrypted bytes incrementally,
  although its CPU and I/O work remains proportional to the content processed. Bounded metadata paging and incremental artifact enumeration reduce inventory working-set pressure but do not establish a global time, total-byte, or finding-count budget; reconciliation findings are still accumulated for the caller.

## Security Considerations

Encryption keys must never be:

- Hard-coded in source code.
- Stored in application configuration files.
- Stored in plaintext beside encrypted documents.
- Logged.
- Included in document metadata.

Cryptographic operations must use cryptographically secure random values for nonces and keys.

The implementation must authenticate encrypted content before returning decrypted data. Version-3 records must authenticate every data chunk and the mandatory final record, and the reader must require exact end-of-file after the final record. The reader must also verify the complete plaintext against the expected persisted document SHA-256 identity before returning a stream.

Version-2 compatibility does not by itself authenticate document identity, chunk sequence, or a whole-document terminator. The persisted-content hash gate is therefore mandatory for legacy reads as well; it must not be represented as a new cryptographic property of the legacy ciphertext.

Cryptographic and content-integrity failures must not expose sensitive document contents, plaintext keys, protected key blobs, authentication material, or credentials through exceptions or logs.

Document-owned lifecycle operations must not accept caller-controlled artifact paths that can escape the application-managed `Documents` directory. Canonical document-to-artifact resolution is therefore performed by Infrastructure from the document identifier.

Reconciliation must not treat readability as proof of ownership. Content validation must compare decrypted artifact content with the persisted document identity before classifying an artifact as matched.

Recovery must not expose document contents or cryptographic material through logs. Recovery decisions must be based on document identity and canonical ownership evidence rather than arbitrary physical paths.

### Master-Key Provisioning Invariants

- The canonical `master.key` file must not be silently replaced by a competing initializer.
- Initial document-key provisioning is owned by `DatabaseInitializer` and is permitted for a new vault before document data exists, or for an existing empty database when both document metadata and managed document artifacts are absent.
- `IEncryptionKeyProvisioningService.GetOrCreateKeyAsync()` must not be used as an automatic fallback for normal document writes or for an established vault with missing expected key material.
- A candidate key must be protected before it is written to a temporary file.
- Publication of a complete candidate must not overwrite an existing canonical key.
- A losing initializer must use the authoritative persisted key, not return its own unpublished candidate.
- If the authoritative key cannot be retrieved or unprotected, initialization must fail rather than silently replace it.
- Temporary candidate files are not authoritative key files.
- Key material must not be written to logs.
- In-memory candidate key material and protected temporary buffers must be cleared when practical.
- Cleanup failures must not expose key material or sensitive file contents through logs.

### Strict Retrieval Invariants

- Ordinary document encryption and normal document decryption must retrieve the established master key through `IEncryptionKeyService.GetKeyAsync()`.
- Strict retrieval must not create a replacement `master.key` or modify existing protected key material.
- A missing, invalid, unreadable, or unavailable expected key must result in explicit failure rather than fallback to provisioning.
- Explicit initial provisioning is owned by `DatabaseInitializer` and is limited to authorized first-use/empty-vault contexts. `IEncryptionKeyProvisioningService.GetOrCreateKeyAsync()` is not a fallback for ordinary document writes or for an established vault with missing key material.
- The retrieval-only `IEncryptionKeyService` does not expose or inherit provisioning capability.
- Key-related logs must not contain plaintext key bytes, protected key blobs, decrypted document content, or credentials.
- Cancellation remains cancellation and must not be converted into a cryptographic key-failure result.

### Consumer-Owned Document-Key Buffer Cleanup Invariants

- `EncryptionKeyMaterial.KeyBytes` is owned by the caller, which must clear the returned buffer after use.
- `DocumentEncryptionService` must clear the exact key buffer acquired for each encryption or decryption operation using `CryptographicOperations.ZeroMemory`, including successful completion and exception or cancellation exits after acquisition.
- The `AesGcm` instance must be disposed before its operation's key buffer is cleared.
- Cleanup must target only the buffer owned by the current operation. It must not clear key-service backing state, another operation's buffer, or a buffer that remains owned elsewhere.
- Clearing the operation-owned buffer does not guarantee erasure of copies held internally by `AesGcm`, the cryptographic provider, or the runtime.
- Key-buffer cleanup must not change strict key retrieval, persisted key state, key identity/version metadata, encrypted-record framing, or legacy compatibility.
- Cleanup must not expose key bytes through logs and must preserve the original operation's exception and cancellation behavior.

### Operation-Owned Transient Plaintext Buffer Cleanup Invariants

- `DocumentEncryptionService.EncryptCoreAsync` must clear its reusable operation-owned plaintext input buffer after each chunk has been consumed and when the operation exits through an exception or cancellation after allocation.
- `DocumentEncryptionService.DecryptCoreAsync` must clear each operation-owned plaintext chunk buffer after consumption, including when ciphertext reads, authenticated decryption, destination writes, record validation, or cancellation fail after allocation.
- `EncryptedDocumentReader.OpenReadAsync` must enforce the caller-supplied finite plaintext-size limit and must not return its private staging stream until all record authentication and completeness checks and the complete plaintext SHA-256 comparison succeed. When decryption, authentication, hashing, cancellation, a size-limit failure, or content-identity verification fails after staging allocation, it must use `CryptographicOperations.ZeroMemory` to overwrite the available backing array of its operation-owned staging `MemoryStream` before disposing it.
- `EncryptedDocumentReader.ValidateContentHashAsync` must enforce its explicit finite plaintext-size limit and decrypt into an operation-owned `HashingWriteStream` that computes SHA-256 incrementally without retaining the complete plaintext. It must dispose its input and hash resources on success, failure, and cancellation; it must not report successful validation until complete content and the expected persisted hash have both been verified. Hash/stream cleanup must not convert cancellation into a content mismatch or an incomplete validation into a matched result.
- `ZeroingMemoryStream` must clear each operation-owned staging backing array when stream growth replaces it, and clear its current backing array on disposal. The failed reader path explicitly clears the available current backing array before disposal. These guarantees do not extend to copies maintained independently by the runtime or cryptographic provider.
- Cleanup targets only buffers owned by the operation. It must not clear caller-owned streams or buffers, shared key-service state, or plaintext storage before it has been successfully returned to a caller.
- Explicit clearing reduces avoidable plaintext retention but does not guarantee erasure of copies maintained by the runtime or cryptographic provider.
- Cleanup preserves encrypted-record framing, authentication and completeness checks, persisted whole-document SHA-256 verification, document identity binding, resource limits, stream ownership, and exception/cancellation semantics.

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

Master-key provisioning uses a unique temporary file in the security directory
and a non-overwriting publication operation to establish `master.key`.
If concurrent initializers contend to publish a key, the initializer that does
not publish the canonical file discards its own candidate and retrieves the persisted key. The canonical file remains the authority for subsequent key retrieval.

This atomic publication rule covers initial master-key provisioning only.
It does not introduce a replacement-key hierarchy or define key rotation policy.

Vault initialization provisions the document key through `IEncryptionKeyProvisioningService`, which is injected into the authorized `DatabaseInitializer` lifecycle boundary. Ordinary document encryption and document decryption use `IEncryptionKeyService.GetKeyAsync()` to retrieve the established key. The retrieval-only `IEncryptionKeyService` exposes only `GetKeyAsync()` and does not inherit provisioning capability.
Missing or invalid master-key state fails explicitly without provisioning a replacement or modifying the stored key state. The initializer permits document-key provisioning for a new vault before existing document data is present, and for an existing empty database only when document metadata and managed document artifacts are both absent. An established vault with document metadata or stored artifacts cannot be treated as an initial-provisioning context.

### Operation-Owned Document-Key Buffer Cleanup

`EncryptionKeyMaterial.KeyBytes` is caller-owned. `DocumentEncryptionService` must clear the exact key buffer returned by `IEncryptionKeyService.GetKeyAsync()` after each encryption and decryption operation, including success, exceptions, stream failures, authentication failures, and cancellation after acquisition.

The cleanup must run after the operation's `AesGcm` instance has been disposed. It applies only to the key buffer owned by that operation and must not clear shared key-service backing state, another operation's buffer, or a buffer that remains owned by another component. It does not guarantee erasure of copies that may exist internally in `AesGcm`, a cryptographic provider, or the runtime.

This cleanup responsibility does not change strict key retrieval, persisted key state, key identity/version metadata, encrypted-record framing, or version-2 compatibility. It must preserve the original exception and cancellation behavior and must not disclose key material through diagnostics.

New master-key artifacts store `KeyId`, key version, and the document-purpose marker within the DPAPI-protected `DVKP` payload described above. New key artifacts start at version `1`. Existing raw-key artifacts are read as legacy version `0` with a deterministic identity derived from the exact protected artifact and are not rewritten during retrieval.

Newly stored document artifacts use encrypted-record format version `3`. The reader also supports version-2 records for compatibility. The encrypted-record format version remains distinct from key lifecycle version. Format 3 authenticates key identity/version, document identity, chunk order, declared data-chunk lengths, and whole-document completion. Version-2 records retain their existing key-lineage associated-data behavior and do not gain those record-level guarantees retroactively.

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
Strict retrieval of existing protected key
    ↓
AES-GCM authentication and decryption
    ↓
Complete plaintext SHA-256 verification against persisted Document.Sha256Hash
    ↓
Readable document stream
```

Ordinary document opening calls `IDocumentReader.OpenReadAsync` with a finite,
positive plaintext-size limit supplied by the application configuration.
The reader decrypts into a private, operation-owned staging stream and verifies the
complete plaintext against the caller-supplied persisted document hash before returning
a readable stream. A malformed record, authentication failure, unsupported format,
invalid final record, truncated content that fails the persisted hash check, an exceeded
size limit, or a hash mismatch must prevent a successful read. On failure after staging
allocation, the reader clears the available backing array of its staging stream with
`CryptographicOperations.ZeroMemory` before disposal. `ZeroingMemoryStream` also clears
superseded backing arrays when stream growth replaces them. Successful returns retain the
staged plaintext for the caller. These guarantees cover the stream-owned arrays, not copies
maintained independently by the runtime or cryptographic provider.

Artifact reconciliation does not use `OpenReadAsync` to create or retain a complete plaintext staging buffer.
It calls `IDocumentReader.ValidateContentHashAsync` with the persisted expected hash and finite maximum plaintext size.
Infrastructure decrypts the artifact into `HashingWriteStream`, which hashes bytes incrementally and does not retain the plaintext,
then compares the resulting SHA-256 value with `Document.Sha256Hash`. A hash mismatch becomes `DocumentContentIntegrityException`
and a content-mismatch finding. A limit, format, authentication, or I/O failure that prevents complete validation becomes an
unreadable-artifact finding. An artifact is never `Matched` unless complete validation and the expected-hash comparison succeed.
Per-artifact validation failures do not suppress unrelated findings where safe continuation is possible; cancellation remains
cancellation and stops the cancelled operation.

Ordinary document opening and lifecycle operations do not trust the persisted physical `StoredFilePath` as the authoritative artifact selector. Reconciliation may inspect that persisted value as evidence, but ownership is validated against the canonical document-owned path.

Reconciliation reads persisted document metadata through deterministic keyset-paged repository queries, with a maximum page size of 256 documents,
and processes the artifact inventory incrementally. The page cursor orders by `ImportedAt` with `DocumentId` as a deterministic tie-breaker.
The cursor avoids offset-based paging but does not establish a transactionally consistent snapshot across concurrent repository changes.
Filesystem discovery uses incremental enumeration rather than materializing all artifact paths into an array, and canonical artifact existence is checked per document.
This bounds intermediate inventory materialization; it does not cap total traversal time, cumulative bytes read, or the number of findings retained in the response.

Recovery re-reads the current document metadata for each orphan immediately before canonical ownership validation and deletion rather than relying on a stale full-repository snapshot.
This reduces stale-snapshot cleanup risk but does not turn reconciliation into an atomic snapshot of the filesystem and repository.

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

DeskVault protects imported document content at rest while keeping
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

Master-key initialization publishes one canonical protected key without allowing
a concurrent losing initializer to overwrite it or return a competing key as
authoritative. Subsequent retrieval uses the persisted protected key.

Ordinary document encryption and normal document decryption retrieve the established master key without provisioning a replacement. Missing or invalid key material results in explicit failure, and a failed operation does not modify the stored key state.

New document writes use format 3. Its authenticated per-chunk metadata binds protected bytes to the expected document, key identity/version, and chunk position, and its mandatory authenticated final record establishes
the expected whole-record completion boundary. Exact end-of-file is required after that final record.

Legacy version-2 documents remain readable under their documented compatibility limits. Because their record framing does not provide an authenticated document identity, chunk sequence, or final marker, compatibility
reads depend on verifying the complete plaintext against the persisted `Sha256Hash` before returning a stream. This consistency check does not retroactively make the legacy ciphertext provide format-3 guarantees.

Reconciliation verifies canonical ownership and the complete persisted content identity before classifying a document/artifact relationship as valid. Ordinary reads are bounded by a finite plaintext-size limit; reconciliation uses incremental hash-only
validation and paged/incremental inventory traversal rather than retaining a full plaintext or full artifact-path snapshot.

Recovery remains a separate, explicit capability. It performs automatic
cleanup only for sufficiently evidenced canonical orphans and preserves
ambiguous or otherwise unsafe findings for further recovery.

The encryption boundary remains replaceable through Infrastructure
abstractions and does not couple cryptographic implementation details to the
Domain, Application, or UI layers.

## Future Considerations

The following are intentionally deferred:

- Key rotation implementation and the end-to-end data-transition workflow. Runtime rotation remains unsupported until a complete, verifiable, and recoverable transition is implemented.
- Multiple encryption keys per document.
- Secure deletion.
- Backup and restore of encryption keys.
- Portable encrypted document export.
- Cross-device key synchronization.
- Enterprise key-management integration.
- Automated repair of missing or content-mismatched document artifacts.
- Bulk recovery tooling and operator-directed recovery workflows.
