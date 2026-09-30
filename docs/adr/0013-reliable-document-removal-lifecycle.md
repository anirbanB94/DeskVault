# ADR-0013: Reliable Document Removal Lifecycle

## Status

Accepted

## Context

DeskVault stores a document across separate persistence boundaries.

Document metadata is persisted in SQLite.

The encrypted source artifact is persisted in the application-managed
filesystem.

Persistent workspace memberships are also persisted in SQLite.

A document removal therefore spans multiple independently executed
operations:

```text
Document
├── Encrypted artifact
├── Workspace memberships
└── Document metadata
```

These operations cannot be treated as one physical transaction because the
encrypted artifact is stored outside the SQLite database.

A failure after one operation has completed can therefore leave the remaining
lifecycle work incomplete.

Document artifact deletion is already safe to repeat because the storage
boundary resolves the canonical document-owned artifact from the document
identifier and treats an already-missing artifact as an idempotent cleanup
case.

Workspace membership cleanup is also repeatable because it removes only
memberships belonging to the supplied document identifier.

Document metadata deletion is similarly safe to repeat when the metadata row
has already been removed.

The remaining lifecycle problem is operation ordering.

If document metadata is deleted before workspace membership cleanup, a failure
during membership cleanup removes the document record that would otherwise
be used to identify and retry the remaining cleanup.

The removal lifecycle therefore requires an explicit ordering and convergence
rule.

## Decision

DeskVault will implement document removal as a retry-convergent sequence of
independent lifecycle operations.

The Application removal workflow will execute the operations in this order:

```text
Delete document-owned encrypted artifact
        ↓
Remove document from all workspace memberships
        ↓
Delete document metadata
```

Each operation must use the existing application-defined responsibility
boundary for that resource.

### Artifact Cleanup

The encrypted artifact is deleted through `IStorageService` using the
document identifier.

The Application layer does not construct or accept an arbitrary physical
artifact path for ordinary document removal.

Infrastructure remains responsible for resolving the canonical
document-owned artifact and performing physical filesystem cleanup.

An already-missing canonical artifact is treated as successful cleanup.

### Workspace Membership Cleanup

Workspace memberships are removed through
`IWorkspaceRepository`.

Only memberships whose `DocumentId` matches the requested document are
eligible for removal.

An already-clean membership set is treated as successful cleanup.

Workspace lifecycle and document lifecycle remain distinct concepts, but
removing a document requires its persistent memberships to be cleaned before
the document metadata is removed.

### Metadata Cleanup

Document metadata is removed through `IDocumentRepository`.

Metadata deletion is deliberately performed after artifact and workspace
membership cleanup.

This preserves the document record as the retry anchor when an earlier
lifecycle operation has failed.

An already-absent metadata record is treated as an idempotent completion by
the persistence implementation.

## Retry and Convergence Invariant

Document removal is considered retry-safe when repeating the removal command
after a partial failure converges on the same intended final state.

The intended final state is:

```text
Document metadata       absent
Workspace memberships   absent
Encrypted artifact      absent
```

A previously completed operation must not make a later retry unsafe.

The lifecycle therefore allows states such as:

```text
Artifact        absent
Memberships     absent
Metadata        present
```

or:

```text
Artifact        absent
Memberships     operation failed
Metadata        present
```

to be safely retried.

The Application workflow must not assume that an earlier operation is still
pending merely because the overall removal command did not complete.

## Partial Failure

A failure during artifact cleanup prevents the subsequent lifecycle steps
from being attempted because the document content remains unresolved.

A failure during workspace membership cleanup prevents metadata deletion so
the document remains available as the authoritative retry anchor.

A failure during metadata deletion leaves artifact and membership cleanup
completed while the metadata record remains available for a subsequent
attempt.

Repeated removal must therefore operate on the remaining persisted state
without recreating or selecting another document's artifact.

## Architectural Boundaries

The responsibilities remain separated:

```text
Application
    ↓
RemoveDocumentHandler
    ├── document removal orchestration
    ├── lifecycle ordering
    └── result/error semantics
         ↓
Application abstractions
    ├── IStorageService
    ├── IWorkspaceRepository
    └── IDocumentRepository
         ↓
Infrastructure
    ├── encrypted filesystem artifact
    ├── workspace persistence
    └── document metadata persistence
```

The Application layer does not perform direct filesystem or SQLite operations.

Infrastructure continues to own:

- canonical document-owned artifact resolution;
- encrypted artifact storage;
- SQLite and EF Core persistence;
- physical filesystem operations.

The Domain model remains independent of storage implementation details.

## Ownership and Security

Document removal must operate only on the artifact owned by the requested
document.

The canonical document-to-artifact identity remains derived from the
document identifier.

A persisted physical artifact reference is not accepted as an arbitrary
deletion target.

The existing encrypted-at-rest architecture and key-management boundary are
unchanged.

No new encryption behavior is introduced by this lifecycle decision.

## Reconciliation Boundary

Retryable removal and pre-existing inconsistency reconciliation are separate
concerns.

The removal lifecycle handles the known progression of a removal command and
makes that progression safe to repeat.

Artifact reconciliation remains responsible for detecting and classifying
pre-existing inconsistencies that may exist independently of an active
removal attempt.

Reconciliation must continue to preserve the document-owned artifact
boundary and must not silently recreate or delete content merely to make
metadata and filesystem state appear consistent.

## Alternatives Considered

### Delete metadata before workspace membership cleanup

Rejected.

If workspace cleanup fails after metadata deletion, the document record is
no longer available as the authoritative retry anchor.

### Delete workspace membership only after all other operations

Rejected.

The operation can fail after metadata deletion and leave persistent
membership state without an active document record capable of driving normal
retry behavior.

### Introduce a distributed transaction across SQLite and the filesystem

Rejected for the current architecture.

DeskVault intentionally keeps encrypted document content in the filesystem
and metadata in SQLite. Introducing a distributed transaction mechanism
would substantially increase infrastructure complexity and would not align
with the current local-first storage architecture.

### Introduce a new removal-state persistence model

Deferred.

A durable removal-state machine is not required for the current lifecycle
guarantee because the existing storage and persistence operations can be
ordered so that completed work remains safely repeatable.

A future implementation may introduce explicit removal-state persistence if
additional asynchronous or recoverable lifecycle stages are introduced.

## Consequences

### Positive

- Partial document removal can be retried safely.
- Artifact cleanup remains idempotent.
- Workspace cleanup remains idempotent.
- Metadata remains available when workspace cleanup fails.
- Successful removal reaches one predictable final state.
- Unrelated documents and artifacts remain outside the removal target.
- The existing storage, persistence, encryption, and ownership boundaries are
  preserved.
- Removal retryability does not require a cross-database/filesystem
  transaction.

### Negative

- The removal workflow must preserve a deliberate operation ordering.
- Failures can leave transient partial state until a retry succeeds.
- Integration tests must cover multiple lifecycle states.
- Future lifecycle stages may require revisiting the ordering invariant.

## Result

DeskVault document removal is a retry-convergent lifecycle across the
filesystem artifact, workspace memberships, and document metadata.

The authoritative sequence is:

```text
Document-owned artifact cleanup
        ↓
Workspace membership cleanup
        ↓
Document metadata cleanup
        ↓
Final removed state
```

The workflow preserves existing encryption, ownership, persistence, and
workspace boundaries while allowing repeated removal attempts to complete
remaining lifecycle work safely.
