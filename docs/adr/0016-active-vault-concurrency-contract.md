# ADR-0016: Active-Vault Concurrency Contract

## Status

Accepted

## Context

DeskVault is an offline-first desktop application, and more than one DeskVault
process may access the same local vault. A vault contains at least two
independently persisted resource classes:

- SQLite state, including document metadata and persistent workspace
  aggregates.
- Encrypted document artifacts stored in the vault filesystem.

SQLite transactions serialize individual database writes, but they do not
make a filesystem artifact operation atomic with a metadata operation. The
document import workflow stores an encrypted artifact before publishing its
metadata. Document removal deletes the artifact, removes workspace
memberships, and then deletes metadata. Artifact recovery scans for
inconsistencies and may delete artifacts that have no corresponding metadata.

The existing import compensation, document-removal convergence, and artifact
ownership rules protect individual failure cases. They do not, by themselves,
prevent a second process from acting on the same document while the first
process is between those steps.

Persistent workspaces have a different concurrency problem. The repository
restores a workspace aggregate from its persisted fields and memberships.
Updating that aggregate replaces its persisted memberships from the supplied
snapshot. Without a persisted revision check, a stale process can overwrite a
newer update or restore a membership removed by another process. A timestamp
such as `LastUpdated` is not an adequate concurrency token because it is
descriptive domain metadata, not an atomic compare-and-swap value.

The concurrency design must protect the resource actually being changed,
preserve independent work, and remain compatible with the existing offline
storage architecture. A single global vault lock, a distributed transaction
across SQLite and the filesystem, or a replacement persistence architecture
would introduce broader coordination than these use cases require.

## Decision

DeskVault will use two complementary and independent concurrency mechanisms:

1. A vault-scoped, document-scoped operating-system lease for operations that
   create, remove, or recover a canonical encrypted document artifact.
2. Optimistic concurrency revisions for persistent workspace aggregates.

These mechanisms protect different consistency boundaries. They must not be
collapsed into one global lock or used as substitutes for SQLite transactions.

The authoritative relationship is:

```text
Active Vault
├── Document artifact lifecycle
│   └── Cross-process lease keyed by canonical vault identity + document ID
└── Persistent workspace aggregates
    └── SQLite optimistic revision keyed by workspace ID
```

SQLite continues to provide transaction atomicity for operations contained
within the database. The document lease coordinates the filesystem and
metadata lifecycle across processes. The workspace revision prevents stale
aggregate snapshots from being accepted.

## Vault and Document Identity

### Canonical Vault Identity

Coordination is scoped to the physical vault root, not to a process, a window,
or an unnormalized path string.

The vault identity is derived from the canonical filesystem identity of
`DeskVaultDataPaths.RootDirectory`. Canonicalization must:

- produce an absolute path;
- normalize relative segments and trailing directory separators;
- resolve filesystem aliases such as directory reparse points so that
  different paths referring to the same vault root converge on one identity;
- use Windows path comparison semantics, including case-insensitive
  comparison; and
- fail closed when the canonical vault identity cannot be established.

On Windows, Infrastructure establishes this identity by opening the configured
root as a directory using `CreateFileW` with `FILE_FLAG_BACKUP_SEMANTICS`, then
retrieving its final normalized path using `GetFinalPathNameByHandleW` with
`FILE_NAME_NORMALIZED` and `VOLUME_NAME_DOS`. The directory handle follows
reparse points rather than opening the reparse point itself, so a configured
junction or symbolic-link alias resolves to the target directory identity.
The extended-length path prefix and trailing directory separator are
normalized before the path is used as the vault identity. Path identity
comparisons use `StringComparison.OrdinalIgnoreCase`.

Canonical resolution requires the vault root to exist and identify a
directory. It does not create the vault root. Failure to open the directory,
read its attributes, or obtain its final path fails closed. The directory
handle used to establish identity is disposed after resolution; it is not the
active document-artifact lease.

The canonical root identity is stable across DeskVault processes using the
same vault. A separately located vault has a different identity, even when
its database and document identifiers happen to match.

The canonical vault identity is an Infrastructure coordination concern. It
must not be persisted as a replacement for document identity or workspace
identity, and it must not expose user-specific filesystem paths through
ordinary diagnostics.

### Document Coordination Identity

A document coordination target is the tuple:

```text
(canonical vault identity, document ID)
```

A document ID is unique within its vault. The same document ID in two
different vaults therefore represents two independent coordination targets.

Different document IDs in the same vault must not share a document-artifact
lease. They may still contend briefly for SQLite's own transaction and file
access resources, but the coordination mechanism must not serialize their
entire application workflows.

The lease protects the canonical document-owned artifact and the
cross-resource lifecycle that publishes, removes, or recovers that artifact.
It is not a general document-processing lock, a database-wide lock, or a
workspace lock.

## Cross-Process Document-Artifact Coordination

### Lease Artifact and Ownership

Infrastructure will implement a disposable document-artifact lease using an
operating-system-held exclusive file handle.

The coordination artifact is stored under the dedicated vault coordination
directory, separate from the canonical `Documents` directory and from
enumerated `.dvault` artifacts. The implemented path is:

```text
<VaultRoot>\Security\Coordination\DocumentArtifacts\<document-id>.lock
```

The filename uses the document ID in the standard GUID `D` representation
followed by `.lock`. Its parent directory is resolved from the canonical vault
root. The following constraints are architectural requirements:

- the coordination artifact is outside the canonical document-artifact
  enumeration boundary;
- one vault/document tuple resolves to one coordination artifact;
- different document IDs resolve to independent artifacts;
- different vault roots resolve to independent artifacts; and
- the coordination file's existence is never treated as proof of ownership
  or proof that a process is still running.

Infrastructure creates the `Security`, `Coordination`, and
`DocumentArtifacts` directory levels sequentially. After creating each level,
it resolves that directory's physical path using a directory handle and checks
that the resolved path equals the expected normalized path and is a strict
descendant of the canonical vault root. A redirected coordination-directory
reparse point is rejected; Infrastructure does not continue through a child
directory whose physical location differs from its expected location. This
keeps the coordination namespace at the specified location inside the vault.

The lease is owned only while the process holds the exclusive operating
system file handle. The implementation opens the coordination file using
`FileMode.OpenOrCreate`, `FileAccess.ReadWrite`, and exclusive sharing
(`FileShare.None`) as the cross-process exclusion boundary. It does not write
document content, key material, or ownership metadata to the file. A persistent
empty lock file may remain after release; the handle, not the file's presence,
determines whether the lease is held. The lock file is not deleted during
normal lease release.

The lease is an explicit disposable lifetime. Acquisition is asynchronous
and cancellation-aware. The owning operation must release the handle on every
success, failure, and cancellation path. Disposal is safe and idempotent.
Abrupt process termination releases the operating-system handle, allowing a
subsequent process to acquire the lease without a stale-owner timeout or
manual lock-file deletion.

### Acquisition and Failure Semantics

A process that encounters the expected exclusive-sharing contention waits
for the current owner to release the lease. Waiting must observe the
operation's cancellation token and must not block the UI thread. The current
implementation retries the Windows `ERROR_SHARING_VIOLATION` (32) condition
using a 100-millisecond cancellation-aware asynchronous delay.

An expected sharing violation is contention, not permission to bypass
coordination. Cancellation while waiting ends the operation before it
performs a protected side effect. If cancellation is observed after the
exclusive handle is opened but before the lease is returned, Infrastructure
releases the handle and reports cancellation rather than granting ownership.

An I/O or access failure that is not the expected sharing violation is an
acquisition failure. The operation must fail closed and must not continue
without the lease. The implementation retries only the expected Windows
sharing-violation error; unexpected filesystem, path, and access failures
propagate as acquisition failures and are not treated as contention.

The operating-system handle is the only authoritative owner marker. The
implementation must not infer that a lock is abandoned from file age,
timestamps, process IDs stored in the file, or a missing heartbeat.

### Import Lease Lifetime

For an import, the document ID is generated after validation and the
application-level duplicate fast path. The lease must be acquired before
the canonical encrypted artifact is created.

The lease remains held across the complete provisional-artifact lifecycle:

```text
Generate document ID
        ↓
Acquire document-artifact lease
        ↓
Store encrypted artifact
        ↓
Verify source-content identity
        ↓
Persist document metadata
        ↓
Release lease after successful publication
```

If import does not complete after artifact storage, compensation for that
import's own artifact is attempted while the same lease is still held. The
lease is released only after the compensation attempt has completed. When
the import cancellation token has already been cancelled, the compensation
attempt must not be abandoned solely because of that cancellation.

Failure to acquire the lease prevents artifact creation and metadata
publication for that import attempt. The failure must not be represented as
successful import.

The document ID is generated per import attempt. Consequently, two imports
of identical content may hold different document-artifact leases and may
proceed through storage independently. The existing SQLite unique SHA-256
constraint remains the authoritative duplicate-content concurrency safeguard.
The lease does not replace duplicate detection, duplicate-race compensation,
or the persistence uniqueness constraint.

### Removal Lease Lifetime

Removal must acquire the document-artifact lease before reading and relying
on the document metadata snapshot.

After acquisition, the removal workflow re-reads current metadata and holds
the lease across the existing retry-convergent sequence:

```text
Acquire document-artifact lease
        ↓
Read current document metadata
        ↓
Delete canonical document-owned encrypted artifact
        ↓
Remove persistent workspace memberships
        ↓
Delete document metadata
        ↓
Release lease
```

The established order remains authoritative. The lease does not introduce a
distributed transaction between the filesystem and SQLite and does not
change the existing retry anchor or partial-failure semantics.

If removal fails or is cancelled after a lifecycle operation has completed,
the lease is released and the remaining persisted state stays eligible for
the existing retry-convergent removal workflow. An already-missing artifact
remains an idempotent cleanup case.

### Recovery Lease Lifetime and Revalidation

Artifact reconciliation remains a read-only discovery operation. It must not
hold a vault-wide or document-wide lease for the entire enumeration.

For each orphan candidate with a usable document ID, recovery acquires that
document's lease before making a destructive decision. After the lease is
acquired, recovery must revalidate the current state rather than relying on
the earlier scan:

1. Re-read current document metadata.
2. If metadata now exists, preserve the artifact.
3. Revalidate that the candidate path is still the canonical artifact path
   owned by the candidate document ID.
4. Revalidate that the canonical artifact still exists.
5. Delete only the canonical document-owned artifact while holding the
   lease.

If metadata exists after the wait, recovery preserves the artifact. This is
the expected outcome when an import completed while recovery was waiting.
If the artifact no longer exists, recovery performs no destructive action.
If ownership cannot be established, recovery preserves the artifact.
Unexpected lease-acquisition failures fail closed and must not be converted
into permission to delete.

When an import process terminates after creating a provisional artifact but
before metadata publication, the operating system releases its lease. A
later recovery operation may then acquire the lease, revalidate that metadata
is still absent, confirm canonical ownership and existence, and remove the
unpublished artifact.

Recovery and removal use the same coordination identity as import. A single
document therefore cannot be simultaneously in an import, removal, or
destructive recovery critical section across DeskVault processes.

## Persistent Workspace Optimistic Concurrency

### Persisted Revision

Each persistent workspace has a dedicated, monotonically increasing
64-bit integer revision (`long` in the Domain/Application contract and
SQLite `INTEGER` in persistence). The initial revision is `0`; existing
persistent workspaces receive revision `0` when the revision column is
introduced. Each accepted persisted aggregate update advances the revision
by exactly one.

The revision is distinct from `LastUpdated`:

- `LastUpdated` remains descriptive workspace/domain timestamp metadata.
- The revision is the authoritative optimistic concurrency token.
- A revision is carried with every persistent workspace aggregate restored
  from persistence.
- A newly created persistent workspace starts at revision `0`.
- Every successful persisted aggregate update advances the revision by
  exactly one.

Temporary, in-memory-only workspaces do not require a persisted revision
until they are converted into persistent workspaces.

### Consistent Aggregate Reads

Workspace aggregate reads must return a coherent point-in-time snapshot.
The workspace row (including its persisted revision) and all membership
rows used to restore that aggregate must be read from the same consistent
SQLite read transaction/snapshot. This applies to both `GetByIdAsync` and
`GetAllAsync`; separate autocommit queries must not be allowed to combine
workspace fields from one committed state with memberships from another.

This matters when a transaction changes workspace memberships,
`LastActiveDocumentId`, and the revision together. For example, a read must
not observe the pre-cleanup `LastActiveDocumentId` from the workspace row
and the post-cleanup membership collection after document removal commits.
Such a mixed result would not represent a valid persisted aggregate.

The returned revision must identify the exact workspace fields and
membership collection in the returned aggregate. A consistent snapshot may
become stale immediately after the read transaction completes; this is
expected. The subsequent conditional update or delete must reject the stale
revision rather than overwrite a newer committed state. This is a snapshot
consistency guarantee, not a promise that the value remains globally latest
after the read completes.

### Conditional Aggregate Update

A persistent workspace update must use the revision carried by the snapshot
from which the update was derived.

The repository must perform a conditional update equivalent to:

```sql
UPDATE Workspaces
SET
    ...aggregate fields...,
    Revision = Revision + 1
WHERE Id = @workspaceId
  AND Revision = @expectedRevision;
```

The revision comparison and advancement are atomic at the SQLite
persistence boundary.

The workspace row update and the corresponding membership replacement
must occur in one SQLite transaction. If any part fails or is cancelled
before commit, the transaction must not publish a partial aggregate update.

An update that affects no row must be classified by checking current
persistence state within the same transaction (or an equivalent serialized
persistence boundary):

- if the workspace no longer exists, report a missing-workspace outcome;
- if the workspace exists with a different revision, report a concurrency
  conflict.

The classification must not be based on a stale process-local snapshot. The
failed conditional update must not apply any workspace fields or membership
changes.

A stale update is not a successful update. It must not silently overwrite
the newer aggregate or be reported as an ordinary persistence success.

After a successful update, the repository contract must return or otherwise
make available the new persisted revision to the Application layer before
the updated aggregate is published to `ActiveWorkspaceRegistry`. The
registry must contain the post-update revision, not the pre-update snapshot.

### Deterministic Stale-Snapshot Interleavings

The optimistic revision contract must prevent the following interleavings:

**Two processes update the same workspace**

1. Processes A and B independently load the same persistent workspace at
   revision `n`, including its fields and memberships.
2. Process A applies its intended change using expected revision `n`. Its
   conditional row update advances the persisted revision to `n + 1`, and
   its workspace-field and membership changes commit in the same transaction.
3. Process B then submits its older snapshot using expected revision `n`.
   Its conditional update affects no row. Because the workspace still exists
   at revision `n + 1`, B receives a concurrency-conflict outcome.
4. B's stale fields and memberships are not written, and B must not publish
   its stale aggregate to `ActiveWorkspaceRegistry`. B may reload the current
   aggregate and let the user decide whether to retry the intended change.

**Document removal invalidates a previously loaded workspace**

1. Process B loads a workspace at revision `n` while it contains document D.
2. Document removal deletes D's membership and advances that workspace's
   revision to `n + 1` in the same SQLite transaction.
3. B submits its older aggregate using expected revision `n`. The conditional
   update is rejected as a conflict, so B cannot restore D's removed
   membership or overwrite newer workspace state.

**A stale delete follows a newer update**

1. Processes A and B independently load a workspace at revision `n`.
2. A successfully updates it, advancing the persisted revision to `n + 1`.
3. B attempts deletion using expected revision `n`. The conditional delete
   affects no row; because the workspace still exists at revision `n + 1`,
   B receives a concurrency-conflict outcome and the workspace remains intact.

### Conditional Workspace Delete

Deleting a persistent workspace must also use its expected revision.
Deletion is conditional on both workspace ID and expected revision.

A stale delete must be rejected as a concurrency conflict. A missing
workspace must remain distinguishable from a revision conflict. When a
conditional delete affects no row, the distinction must be made from current
authoritative persistence state within the same transaction (or an equivalent
serialized persistence boundary). The repository must not silently delete a
newer workspace based only on its ID.

### Document-Removal Membership Cleanup

The document-removal membership cleanup operation is also a persistent
workspace mutation.

When a document is removed from all persistent workspaces, the affected
workspace set is the union of:

- workspaces whose membership collection contains the removed document; and
- workspaces whose `LastActiveDocumentId` references the removed document.

Every affected workspace must have its revision advanced exactly once in the
same SQLite transaction as the membership deletion and/or active-document
reference cleanup. A workspace that satisfies both conditions still receives
one revision advancement, not two.

If the removed document was a workspace's `LastActiveDocumentId`, that
reference must be cleared in the same transaction so the persisted workspace
continues to satisfy its aggregate invariant. Its revision must advance even
when no membership row was deleted but the active-document reference changed.

The transaction must not expose membership deletion or active-document
reference cleanup without the corresponding revision advancement, or revision
advancement without the corresponding aggregate change. A workspace snapshot
loaded before this cleanup must therefore fail its next conditional update or
delete instead of restoring the removed membership or overwriting the newer
workspace state.

### Conflict Propagation and Refresh

A concurrency conflict is a normal, explicit unsuccessful outcome of an
optimistic update. It must be distinguishable from a missing workspace and
from an unexpected persistence failure.

The Application layer must not publish a stale aggregate to the active
workspace registry after a conflict or failed persistence operation.

A caller may refresh the workspace by loading the current persisted aggregate
and then decide whether to retry the user's intended change. The system must
not silently merge a stale snapshot with current state or automatically
replay a command against a newer aggregate without an explicit use-case
decision.

All persistent workspace aggregate mutations must respect the same revision
contract, including updates to name, description, last-active document,
membership collection, and deletion. Any repository operation that changes
persistent workspace aggregate state outside the normal aggregate update
path must advance the affected workspace revision atomically.

## Transaction and Resource Boundaries

The two concurrency mechanisms do not replace the existing SQLite
transaction boundary.

- Cross-resource document-artifact operations use a document lease because
  the encrypted artifact is outside SQLite.
- Workspace aggregate updates use conditional revision checks and SQLite
  transactions because workspace fields and memberships are inside SQLite.
- Existing document processing publication continues to use its
  processing-generation fencing and transaction rules.
- Existing vault initialization continues to use
  `VaultInitializationCoordinator` for migration, schema evolution,
  and required backfill work.

No application-wide lock is introduced. No vault-wide lifetime lock is
introduced for ordinary document operations. No blanket serialization is
introduced for all SQLite writes. Operations on unrelated document IDs and
separate vaults remain independent except for unavoidable short-lived
contention in the underlying filesystem or SQLite engine.

The active-document lease does not replace the SHA-256 uniqueness constraint,
processing-generation fencing, document-removal retry ordering, artifact
ownership validation, or vault initialization coordination.

## Cancellation, Failure, and Process Termination

Coordination acquisition is cancellation-aware. Cancellation before lease
acquisition completes must not begin the protected operation.

A successfully acquired document lease is released on every exit path,
including exceptions and cancellation. Process termination releases the
operating-system handle.

Import keeps the lease through its unsuccessful-import compensation attempt.
A failed compensation does not turn the import into success; it leaves the
existing recovery mechanism able to inspect the remaining artifact after the
lease is released.

Removal preserves its established retry-convergent partial states. A
failure after artifact deletion does not authorize metadata deletion before
workspace membership cleanup succeeds.

Recovery preserves candidates when current metadata, ownership, existence,
or coordination state cannot establish that deletion is safe.

Workspace transactions roll back on failure or cancellation before commit.
A failed conditional update/delete does not publish a new registry snapshot.

Unexpected coordination failures fail closed. They must not cause an
uncoordinated filesystem mutation or a stale workspace overwrite.

## Security and Diagnostics

Coordination files contain no document content, encryption keys, or other
secret material. Their names and directory placement must not add document
artifacts to the canonical document enumeration boundary.

Diagnostics must use the existing safe logging policy. They may identify a
stable operation category and failure classification but must not disclose
source paths, vault paths, encrypted content, key material, or raw exception
details through user-facing messages or ordinary logs.

The existing document-content encryption, database encryption, key
management, authenticated-session authorization, and access-control
boundaries are unchanged.

## Alternatives Considered

### One global lock for the active vault

Rejected.

A global lock would unnecessarily serialize unrelated document operations
and would make one long-running import, removal, or recovery operation block
all other documents in the vault. The actual cross-resource conflict is
document-scoped, so coordination is scoped to the canonical vault/document
identity.

### Reuse the initialization lock for all active operations

Rejected.

`VaultInitializationCoordinator` protects the critical section that performs
database creation, migration staging and promotion, schema evolution, and
required backfills. Active artifact lifecycles have a different resource
boundary and shorter, document-scoped lifetime. Extending the initialization
lock would conflate responsibilities and unnecessarily serialize normal
application work.

### Use only SQLite transactions

Rejected.

A SQLite transaction cannot atomically include encrypted filesystem
artifact creation or deletion. The document lease coordinates the
cross-resource lifecycle; SQLite transactions continue to protect
database-local state.

### Introduce a distributed transaction across SQLite and the filesystem

Rejected.

A distributed transaction would add substantial recovery and infrastructure
complexity to a local-first application. The existing ordered, retryable
lifecycle plus per-document coordination provides the required protection
without claiming atomicity across the two storage systems.

### Use `LastUpdated` as the workspace concurrency token

Rejected.

`LastUpdated` is a descriptive timestamp. It is not a dedicated monotonic
revision and does not provide the explicit expected-version contract
required for conditional updates and deletes.

### Automatically merge or replay stale workspace snapshots

Rejected.

A stale aggregate may contain user changes that conflict semantically with
newer membership or metadata changes. Automatic merging could reintroduce
removed members or silently discard intentional updates. The conflict must
be visible so the current persisted state can be refreshed before an explicit
retry decision.

### Use a persisted lease owner, heartbeat, or stale timestamp

Rejected.

Such a marker would require a second ownership protocol to distinguish
active from abandoned processes. The operating-system file handle already
provides automatic release on process termination and is the authoritative
lease state.

## Consequences

### Positive

- Import, removal, and destructive recovery cannot concurrently mutate the
  same document artifact across DeskVault processes.
- Recovery revalidates current state after waiting and cannot delete an
  artifact merely because an earlier scan classified it as orphaned.
- Different documents in one vault remain independently coordinated.
- Different vaults remain independently coordinated.
- Existing duplicate-import, compensation, processing-generation, removal,
  reconciliation, and initialization safeguards retain their own roles.
- Stale workspace aggregate updates and deletes are rejected explicitly.
- Document-removal membership cleanup invalidates stale workspace snapshots
  by advancing the affected workspace revisions atomically.
- Workspace timestamp semantics remain unchanged.
- No distributed transaction or global active-vault lock is required.

### Negative

- Infrastructure must maintain a stable canonical vault identity and
  cross-process lease-file lifecycle.
- Active document handlers must observe the lease lifetime and retain it
  through compensation or final cleanup.
- Workspace persistence contracts and callers must carry and propagate
  revision values.
- Workspace conflicts require a refresh and an explicit retry decision.
- Deterministic multi-instance tests are required to protect the
  interleavings and failure paths.

## Validation Scenarios

The implementation and end-to-end validation must demonstrate at least:

1. Two processes targeting the same canonical vault and document ID cannot
   hold the document-artifact lease simultaneously.
2. Different document IDs in the same vault can hold independent leases.
3. The same document ID in different vaults can be coordinated independently.
4. Equivalent canonical paths to the same vault resolve to the same lease.
5. Cancellation while waiting does not enter the protected operation.
6. Unexpected lease acquisition failures fail closed.
7. A process terminating while holding a lease releases it for a subsequent
   process.
8. Recovery waits for an in-progress import, then rechecks metadata and
   preserves the artifact when import succeeds.
9. If an import terminates before metadata publication, later recovery can
   acquire the lease and remove only the canonical orphan artifact.
10. Removal and recovery cannot destructively overlap for the same document.
11. Failed or cancelled removal leaves a state that can be retried safely.
12. Workspace reads return the workspace row, revision, and membership
    collection from one consistent SQLite read snapshot; a read interleaving
    with a committed membership/revision update returns a coherent before-or-after
    aggregate, never a mixed state.
13. A workspace update from a stale revision is rejected.
14. A workspace delete from a stale revision is rejected.
15. Document-removal membership cleanup advances each affected workspace
    revision exactly once in the same transaction as membership deletion
    and/or `LastActiveDocumentId` cleanup, including when only the active
    reference changes.
16. A deterministic two-process stale-update interleaving rejects the
    second update without changing fields or memberships or publishing stale
    state to the active registry.
17. A workspace snapshot loaded before document-removal membership cleanup
    cannot restore the removed membership.
18. A stale workspace delete after a newer update is rejected without
    deleting the newer workspace.
19. Workspace mutation conflicts do not publish stale state to the active
    registry and can be resolved by refresh before an explicit retry.
20. Existing duplicate-import, failed-import compensation,
    processing-generation, initialization, encryption, and safe-diagnostic
    regression coverage continues to pass.
21. Canonical vault resolution maps equivalent configured paths, including a
    directory-junction alias, to the same physical vault identity.
22. A coordination-directory reparse point that redirects a child directory
    away from its expected physical location is rejected before descendant
    coordination directories are created.
23. The persistent coordination file contains no application data, remains
    outside `Documents`, and does not become a canonical `.dvault` artifact.

## Related Decisions

- ADR-0003 defines the document import workflow and provisional-artifact
  compensation.
- ADR-0005 defines SQLite/EF Core persistence for document metadata and
  processing state.
- ADR-0006 defines the document workspace and persistent workspace model.
- ADR-0009 defines encrypted SQLite and the separate vault-initialization
  coordination boundary.
- ADR-0010 defines processing lifecycle and generation fencing.
- ADR-0013 defines retry-convergent document removal.
