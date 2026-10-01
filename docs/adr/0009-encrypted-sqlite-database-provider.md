# ADR-0009: Encrypted SQLite Database Provider

## Status

Accepted

## Context

DeskVault stores document metadata, processing state, and derived document
chunks in a local SQLite database through Entity Framework Core.

Document content encryption is already established separately by
ADR-0004. The database therefore represents a distinct persistence
boundary that also requires protection at rest.

The database persistence architecture currently uses:

```text
Application
    ↓
Repository / Persistence Abstraction
    ↓
Entity Framework Core
    ↓
SQLite
    ↓
Local Database File
```

A standard plaintext SQLite database would expose persisted document
metadata and derived processing information directly to anyone who could
read the local database file.

Database encryption must therefore be introduced without changing the
existing Application-layer persistence abstractions or the
`IDbContextFactory<DeskVaultDbContext>` boundary.

Two native SQLite encryption approaches were investigated during the
database-encryption spike.

SQLCipher was investigated first but could not be loaded successfully in
the spike environment because of native library loading/runtime
integration problems.

SQLite3MC was subsequently evaluated through the SQLitePCLRaw provider
architecture. The required provider integration successfully supported
the EF Core SQLite persistence path and produced an encrypted database
whose file header was not exposed as a standard plaintext SQLite
database.

Database encryption also introduces a separate key-management concern.
The database encryption key must not be embedded in application
configuration, source code, or database connection strings as plaintext.
Key generation, protection, and retrieval therefore remain separate from
the database provider decision.

The database-encryption implementation must also preserve the existing
persistence lifecycle:

```text
Application Startup
    ↓
Resolve Database Encryption Key
    ↓
Configure Encrypted SQLite Provider
    ↓
Create DbContext
    ↓
Apply EF Core Migrations
    ↓
Application Persistence Operations
```

This ordering is important because database connectivity and migration
operations must occur against the encrypted database rather than first
creating or opening an unencrypted database.

Plaintext-to-encrypted migration introduces an additional lifecycle
requirement. Existing plaintext databases must not be rekeyed directly
in place because an interruption during the synchronous SQLite3MC
`sqlite3_rekey()` operation can leave the canonical database in an unsafe
state.

The migration therefore requires a staging and promotion strategy that
keeps the original plaintext database recoverable until the encrypted
database has been verified.

Existing-vault startup also performs schema evolution and any required
persistence backfills. These operations can modify the canonical database
or replace persisted rows and therefore form part of the same
initialization critical section as migration staging, promotion, and
recovery.

Without explicit coordination, two DeskVault application instances that
start against the same vault could concurrently inspect migration
artifacts, modify schema state, execute required backfills, or attempt
canonical-database replacement. These operations must therefore be
serialized per vault.

## Decision

DeskVault will use SQLite3MC as the SQLite database-encryption provider.

The provider will be integrated through the existing SQLitePCLRaw
architecture while retaining `Microsoft.EntityFrameworkCore.Sqlite.Core`
as the Entity Framework Core provider boundary.

The persistence architecture remains:

```text
Application
    ↓
Repository / Persistence Abstraction
    ↓
IDbContextFactory<DeskVaultDbContext>
    ↓
Entity Framework Core SQLite Provider
    ↓
SQLite3MC / SQLitePCLRaw
    ↓
Encrypted SQLite Database
```

Database encryption remains an Infrastructure concern. The Application and
Domain layers will not perform SQLite encryption operations directly and
will not depend on SQLite3MC-specific implementation details.

The database encryption key will be managed separately from the database
provider.

The Infrastructure layer will be responsible for:

```text
Generate database key
        ↓
Protect key using Windows DPAPI
        ↓
Store protected key in the local Security boundary
        ↓
Retrieve and unprotect key when configuring database access
        ↓
Supply key to encrypted SQLite connection
        ↓
Clear transient key material after use
```

The database encryption key is protected using Windows Data Protection
API (DPAPI) with `DataProtectionScope.CurrentUser`. The protected key is
stored as the local `Security\database.key` artifact rather than as
plaintext configuration.

The plaintext database encryption key is not intentionally persisted by
DeskVault. Where the implementation controls the transient key buffer,
the buffer is cleared after use.

The database encryption key must not be logged, exposed through
user-facing exceptions, or stored as plaintext application configuration.

Database initialization will configure the encrypted SQLite connection
before Entity Framework Core migrations are executed:

```text
Application Startup
        ↓
DatabaseInitializer
        ↓
Encrypted DbContext configuration
        ↓
EF Core MigrateAsync
        ↓
Application starts
```

The existing `IDbContextFactory<DeskVaultDbContext>` boundary will remain
intact so that repositories and processing services continue to operate
through the existing persistence abstractions.

Fresh databases must therefore be created encrypted from the beginning.

### Plaintext-to-Encrypted Migration

Existing plaintext SQLite databases are migrated by
`DatabaseInitializer` through a staged migration lifecycle.

The canonical plaintext database is never directly rekeyed. Instead:

```text
Canonical plaintext database
        ↓
Copy to .migration staging database
        ↓
SQLite3MC sqlite3_rekey()
        ↓
Verify staging database is encrypted
        ↓
File.Replace()
        ↓
Canonical encrypted database
        +
.migration-backup plaintext recovery copy
        ↓
EF Core initialization succeeds
        ↓
Remove .migration-backup
```

The `.migration` file is disposable staging state. It is removed when it
is stale or after successful promotion.

The `.migration-backup` file temporarily contains the previous canonical
plaintext database after successful promotion. It is retained until the
encrypted canonical database has successfully completed normal Entity
Framework Core initialization.

This ordering provides a recovery point if the process is interrupted
after promotion but before backup cleanup.

On a subsequent startup:

- If the canonical database is encrypted and a migration backup exists,
  the encrypted canonical database is treated as authoritative.
- Normal Entity Framework Core initialization is performed against the
  encrypted canonical database.
- The migration backup is removed only after successful initialization.
- A stale `.migration` staging file can be discarded when the canonical
  database is already encrypted.
- If the canonical database is plaintext while a migration backup exists,
  the backup must also be a plaintext SQLite database before it can be
  discarded and migration retried.
- If the canonical database is missing while migration artifacts exist,
  initialization fails rather than guessing which artifact is
  authoritative.
- If the canonical database and migration artifacts are all absent,
  normal first-run database initialization remains possible.

The migration process verifies that the staging database no longer has a
standard plaintext SQLite header before promotion and verifies the
canonical database again after promotion.

Database migration failures therefore leave the original canonical
plaintext database available for retry rather than silently replacing it
with an unverified staging result.

The synchronous SQLite3MC `sqlite3_rekey()` operation itself is not
directly cancellation-interruptible. The staging strategy isolates this
provider-level limitation from the canonical database.

The migration implementation does not change the document-file
encryption strategy.

### Cross-Process Initialization Coordination

Existing-vault and new-vault database initialization is coordinated per
vault by `VaultInitializationCoordinator`.

The coordinator derives a lock artifact from the canonical database path:

```text
DeskVault.db
    ↓
DeskVault.db.initialization.lock
```

The lock is acquired by opening the coordination file with exclusive file
sharing (`FileShare.None`). A held file handle therefore provides the
cross-process exclusion boundary.

The coordinator acquires the lock before any initialization work begins
and holds it for the complete initialization critical section:

```text
Acquire vault initialization lock
        ↓
Inspect migration/recovery artifacts
        ↓
Stage and rekey plaintext database when required
        ↓
Promote canonical database when required
        ↓
Create/open encrypted DbContext
        ↓
EF Core MigrateAsync
        ↓
Required existing-vault backfill
        ↓
Remove migration backup after successful initialization
        ↓
Release vault initialization lock
```

This critical section deliberately includes canonical-database
replacement, schema migration, and required backfill work. The individual
migration and backfill components therefore do not implement separate
coordination mechanisms for the same-vault initialization race.

Coordination is scoped to the canonical database path. Different vaults
therefore use different lock files and do not unnecessarily block one
another.

The existence of the `.initialization.lock` file is not interpreted as
proof that a process is still running. The authoritative coordination
state is the operating-system file handle and its exclusive sharing mode.
The lock file itself may remain after successful initialization.

If the initializing process terminates or otherwise abandons the critical
section, the operating system releases its file handle. A subsequent
initialization can then acquire the same lock and retry the existing
initialization/recovery workflow.

If the coordination file cannot be created or opened for reasons other
than the expected sharing violation, initialization fails rather than
proceeding without the required same-vault exclusion.

The coordinator ensures the vault root directory exists before creating
the coordination file so that first-run initialization remains valid even
when the vault has not yet created its application-data directories.

Cross-process coordination does not replace or weaken the existing
migration recovery protocol. It only serializes access to the lifecycle
that owns migration staging, promotion, schema evolution, backfill, and
cleanup.

### Startup Failure Handling

Database initialization is a security-sensitive startup boundary.
Failures are logged with the technical exception for diagnostics, but
raw exception details are not presented to the user.

When database initialization fails, DeskVault displays a fixed generic
message and exits without starting the main application window.

This prevents database-provider, migration, key-protection, or other
initialization exception details from being exposed through the
user-facing startup error path.

## Consequences

The local SQLite database is protected at rest rather than being exposed
as an ordinary plaintext SQLite database.

Existing repository, processing, and search workflows can continue to
use the same Entity Framework Core and `IDbContextFactory` abstractions.

Database encryption is isolated inside Infrastructure, preserving the
existing Application and Domain boundaries.

Database key management becomes an explicit security responsibility
separate from the database provider itself.

The database encryption key is protected by Windows DPAPI using the
current Windows user context. Consequently, access to the protected key
depends on continued access to the Windows DPAPI context under which the
key was protected. DeskVault does not provide an independent recovery
mechanism for a key that can no longer be unprotected by that Windows
user context.

Incorrect or unavailable database keys result in controlled database
initialization failure rather than silently falling back to an unencrypted
database.

Plaintext-to-encrypted migration now has an explicit recovery-safe
lifecycle based on staging, verification, promotion, and delayed cleanup.

The canonical database is protected from direct in-place rekey
interruption because SQLite3MC rekeying occurs against the staging copy.

The implementation introduces a native SQLite dependency that must be
packaged correctly for the supported Windows runtime architectures.

SQLite3MC also introduces an additional dependency and licensing
consideration that must be reviewed as part of production distribution.

The database encryption decision does not replace the document-content
encryption strategy defined by ADR-0004. Document content and database
metadata remain separate storage and encryption boundaries.

Same-vault initialization now has an explicit cross-process exclusion
boundary. Schema migration, required backfills, migration artifact
management, and canonical-database replacement cannot be performed by
multiple DeskVault initializers simultaneously for the same vault.

The coordination state does not depend on application-managed stale
timestamps or a separately persisted ownership record. Operating-system
handle release makes an interrupted or terminated initializer eligible
for retry without requiring manual lock cleanup.

The coordination file introduces a small persistent filesystem artifact
per vault. Its presence alone does not represent an active initialization
and it is not part of the database's authoritative data.

The initialization lock is scoped to the vault's canonical database path,
so unrelated vaults can initialize concurrently.

Plaintext-to-encrypted migration introduces temporary plaintext recovery
artifacts. These artifacts are deliberately retained only until
successful encrypted database initialization and must not be treated as
permanent database copies.

Database initialization failures are intentionally surfaced to users
through a generic startup error message rather than raw exception
details. Technical exception details remain available through application
logging for diagnostics, subject to the application's existing logging
configuration.

The selected provider can be reconsidered in the future if native
runtime support, licensing requirements, platform support, or security
requirements change.

## Verification

The provider-level migration spike established the following behavior:

- SQLite3MC `sqlite3_rekey()` successfully encrypts the database.
- The encrypted database no longer exposes the standard plaintext SQLite
  header.
- The correct encryption key can reopen the encrypted database.
- An incorrect key fails to open the encrypted database normally.
- A busy database returns a controlled SQLite busy result while leaving
  the source recoverable.
- Interrupted provider-level rekeying can affect the file being rekeyed,
  which is why production migration does not operate directly on the
  canonical database.
- Rekeying a staging copy preserves the original plaintext source.
- Interrupted staging rekey can be recovered without making the
  canonical database unavailable.
- Production integration tests verify migration, metadata preservation,
  document state, chunks, searchability, encrypted reopen, and
  post-promotion recovery cleanup.
- Database encryption key material is protected using Windows DPAPI with
  `DataProtectionScope.CurrentUser`.
- Database encryption key material is kept separate from the
  document-file encryption key.
- Database initialization establishes the encrypted database access path
  before EF Core migrations.
- SQLite runtime artifacts are checked to ensure persisted plaintext
  database content is not unintentionally exposed.
- The supported Windows `win-x64` Release publish was verified to include
  the SQLite3MC native runtime and provider dependencies.
- The production Infrastructure registration uses the encrypted
  SQLite3MC provider path rather than the previous plaintext SQLite
  runtime provider.
- Database initialization failures are logged without intentionally
  logging database key material and are presented to users through a
  fixed generic startup failure message.
- `VaultInitializationCoordinator` provides same-vault cross-process
  exclusion using an exclusive filesystem handle on the vault-scoped
  initialization lock.
- Coordinator regression tests verify same-vault serialization,
  release-after-failure, cancellation while waiting, unrelated-vault
  concurrency, and first-run root-directory creation.
- Production-path integration tests verify that concurrent
  initialization of an existing plaintext vault converges on a single
  encrypted canonical database with migration artifacts cleaned up and
  existing document data preserved.

## Related Decisions and Work

- ADR-0002 defines the vertical-slice architecture and the
  Domain/Application/Infrastructure boundaries.
- ADR-0004 establishes document-content encryption.
- ADR-0005 establishes SQLite with EF Core as the persistence boundary
  for document metadata and derived processing results.
- ADR-0010 establishes authoritative processing generation and
  stale-result protection for the document-processing lifecycle.
- ADR-0011 establishes stable document chunk identity and provenance,
  including legacy chunk transition semantics.
