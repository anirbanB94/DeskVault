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
Protect key using platform key protection
        ↓
Retrieve key when configuring database access
        ↓
Supply key to encrypted SQLite connection
```

The encryption key itself must not be logged, exposed through exceptions,
or stored as plaintext application configuration.

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

## Consequences

The local SQLite database is protected at rest rather than being exposed
as an ordinary plaintext SQLite database.

Existing repository, processing, and search workflows can continue to
use the same Entity Framework Core and `IDbContextFactory` abstractions.

Database encryption is isolated inside Infrastructure, preserving the
existing Application and Domain boundaries.

Database key management becomes an explicit security responsibility
separate from the database provider itself.

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

The migration lifecycle introduces temporary plaintext recovery artifacts.
These artifacts are deliberately retained only until successful
encrypted database initialization and must not be treated as permanent
database copies.

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
