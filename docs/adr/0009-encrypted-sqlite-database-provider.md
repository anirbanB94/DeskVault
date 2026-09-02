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
The implementation does not introduce an automatic plaintext-to-encrypted
database migration as part of this decision.

SQLCipher is not selected for the current implementation because the
spike demonstrated unresolved native loading/runtime integration
problems. SQLite3MC provided the required encrypted SQLite behavior
within the existing .NET and SQLitePCLRaw architecture.

Native SQLite runtime packaging is considered part of deployment
correctness. The required native SQLite3MC runtime assets must be
available for each supported application architecture.

SQLite3MC package licensing and distribution requirements must remain
part of the production release review.

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

The implementation introduces a native SQLite dependency that must be
packaged correctly for the supported Windows runtime architectures.

SQLite3MC also introduces an additional dependency and licensing
consideration that must be reviewed as part of production distribution.

The database encryption decision does not replace the document-content
encryption strategy defined by ADR-0004. Document content and database
metadata remain separate storage and encryption boundaries.

Plaintext-to-encrypted database migration is not addressed by this ADR
and requires a separate design if existing plaintext databases must be
supported in a future release.

The selected provider can be reconsidered in the future if native
runtime support, licensing requirements, platform support, or security
requirements change.
