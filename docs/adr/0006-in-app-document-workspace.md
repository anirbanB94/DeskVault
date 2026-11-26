# ADR-0006: Introduce an In-App Document Workspace

## Status

Accepted

## Context

DeskVault currently provides document import, persistent document metadata, encrypted document storage, document listing, document selection, document opening, and document removal.

The current document opening implementation decrypts the document into a temporary file and launches the Windows-associated external application.

While this is sufficient for the current MVP document workflow, it does not align with DeskVault's long-term product vision.

DeskVault is intended to evolve into a secure, offline-first enterprise knowledge platform with document processing, search, retrieval-augmented generation, and local AI capabilities.

The user interface therefore requires a dedicated document-centric workspace where users can view a document and, as the platform evolves, interact with AI capabilities in the context of that document.

The UI should also preserve clear responsibilities between the document library, document workspace, Application layer, and Infrastructure layer.

## Decision

DeskVault will implement an application-managed document workspace
presentation represented by `WorkspaceViewForm`.

`WorkspaceViewForm` owns the surrounding workspace presentation, including
workspace or temporary-document identity, workspace navigation, contextual
workspace/document actions, and the hosting arrangement for document
presentations.

`DocumentViewForm` remains the document-specific presentation surface. It
receives document content through the existing document presentation
boundary and is responsible for rendering that content, presenting
unsupported-format feedback, and exposing document-specific presentation
requests. It does not own the surrounding workspace identity, navigation,
workspace menu, or workspace lifecycle.

`MainForm` will remain responsible for the document library and
document-management workflow.

Document import-file selection is an application-shell interaction, not
authoritative view state. `IMainFormView` exposes the selection request as
the explicit `SelectFileToImport()` interaction. Because invoking this
interaction may display the native file-selection dialog, it is an action
rather than a property whose read appears to retrieve passive view state.
`MainFormPresenter` coordinates that interaction and the returned optional
file path with the existing import workflow. Reading a view-state property
must not initiate a file-selection dialog or another user interaction.
Import validation and document creation remain within their existing
Application workflow.

Opening a document from `MainForm` will open the document inside DeskVault
rather than launching the document through the operating system's default
external application.

The intended UI structure is:

```text
LoginForm
    ↓
MainForm
    ├── Document library
    ├── Import
    └── Document selection
            ↓
      Managed Workspace Presentation
            ↓
      WorkspaceViewForm
            ├── Workspace/document identity
            ├── Workspace navigation
            ├── Workspace/document action menu
            ├── Adaptive AI presentation boundary
            └── Active DocumentViewForm
                    ├── Document content
                    ├── Unsupported-format preview
                    └── Explicit external-open action
```

`MainForm` will serve as the primary application shell rather than being
limited to the document list.

The application shell will use a left navigation rail for major application
areas such as:

```text
Documents
Recent
Workspaces
Search
Settings
```

A workspace presentation can represent either a temporary single-document
session or a persistent workspace containing multiple documents.

A temporary document session is created when a document is opened directly
from the document library. A persistent workspace can contain multiple
document members and presents those documents through the same managed
workspace presentation boundary.

`WorkspaceViewForm` provides the surrounding workspace interaction model.
For a temporary presentation, it displays the current document identity and
exposes the applicable document-level actions. For a persistent workspace,
it displays workspace identity and exposes workspace-management actions and
document navigation.

The active `DocumentViewForm` provides the document content surface. The
workspace presentation and its presenter coordinate document selection,
document-specific requests, and workspace lifecycle without transferring
workspace ownership to the document view.

The document workspace will be designed so that the initial one-document
model can scale to multiple related documents for future AI processing.

A workspace may initially contain:

```text
Primary Document
```

and may later support:

```text
Primary Document
Related Document
Related Document
Related Document
```

Related documents may be added ad hoc to the current workspace and, in a
future implementation, the group may be saved as a named workspace or
collection.

The document workspace itself will remain distinct from the AI context so
that future retrieval and AI processing can operate over a group of related
documents without changing the fundamental document model.

Each primary document may have its own application-managed document
presentation represented by `DocumentViewForm`.

If a user attempts to open a document that already has an active workspace,
DeskVault should activate the existing workspace rather than create a
duplicate workspace for the same document.

The application will not adopt MDI as a mandatory architectural requirement
at this stage. The workspace model should remain independent of the eventual
window-management strategy.

Document rendering will be designed around a scalable rendering boundary
rather than placing format-specific rendering logic directly inside
`DocumentViewForm`.

The initial implementation should prioritize simple in-app rendering for
formats such as:

```text
TXT
MD
CSV
```

Additional renderers, such as PDF and DOCX rendering, can be introduced
independently as the application evolves.

Formats that do not yet have an appropriate in-app renderer use the
explicit user-controlled external-open action exposed by the document
presentation.

The AI assistant area will initially provide the structural foundation for
future functionality. It will not imply that AI analysis, retrieval, or RAG
functionality is already implemented.

Future AI capabilities may operate over the primary document and its
related documents as a shared processing and retrieval context. The
workspace-level AI presentation belongs to the workspace presentation
rather than to the document renderer or document-content view.

## MVP 1 Scope Boundary

The workspace architecture described by this ADR includes both the current
MVP implementation and the longer-term product direction.

For the current implementation, the workspace provides both temporary
single-document sessions and persistent multi-document workspace contexts.

The current workspace presentation provides:

```text
Workspace Presentation
├── WorkspacePresentation
├── WorkspaceViewForm
│   ├── Workspace / document identity
│   ├── Workspace navigation
│   ├── Workspace/document contextual actions
│   ├── Tabs or Sidebar hosting
│   └── Adaptive AI presentation boundary
└── Document Presentation
    ├── DocumentWorkspacePresenter
    └── DocumentViewForm
        ├── Document content
        ├── Unsupported-format preview
        └── Document-specific presentation requests
```

Current workspace interaction includes:

- temporary document presentation when opening directly from Documents
- persistent workspaces containing multiple documents
- Tabs as the default persistent-workspace navigation mode
- Sidebar as an alternative persistent-workspace navigation mode
- activation of existing workspace presentations instead of duplicate
  presentation
- closing a document presentation without removing workspace membership
- reopening a workspace member after its presentation has been closed
- explicit Save as Workspace conversion from a temporary session
- workspace-level add/remove/details/delete actions for persistent workspaces
- document-specific information and removal requests routed through the
  active document presentation

`WorkspaceViewForm` owns the workspace-level presentation surface.
`DocumentViewForm` remains responsible for the document-content presentation
surface and does not duplicate workspace chrome or workspace navigation.

Functional AI processing, Recent activity implementation, workspace
recovery, PDF/DOCX in-app rendering, and other future knowledge-work
capabilities remain outside the current implementation unless otherwise
documented by their own implementation decisions.

The workspace presentation is therefore intentionally functional for
document and workspace lifecycle while preserving the architectural
boundaries required for future AI-assisted and multi-document knowledge
workflows.

## Current Implementation and Deferred Scope

The architectural direction described by this ADR remains valid, and the
current implementation now includes the core multi-document workspace
presentation model described by the Issue #36 workspace vertical slice.

The current implementation includes:

- a dedicated application-managed workspace presentation
- temporary single-document workspace sessions for direct document opening
- persistent named workspaces
- multiple documents within a persistent workspace
- multiple simultaneous workspace presentation contexts
- activation/reuse of an existing workspace presentation rather than duplication
- Tabs as the default persistent workspace navigation mode
- Sidebar as an alternative persistent workspace navigation mode
- explicit separation between document presentation and workspace membership
- closing a document presentation without removing its workspace membership
- reopening a workspace member after its presentation has been closed
- workspace and document identity presentation
- explicit Save as Workspace conversion for temporary sessions
- workspace add/remove/details/delete actions for persistent workspaces
- document information and document removal behavior
- format-specific rendering through the renderer abstraction
- TXT, Markdown, CSV, and HTML/HTM in-app rendering through their
  format-specific renderers
- explicit user-controlled external opening independently of in-app preview
  availability, with unsupported formats using it as a fallback
- renderer lifecycle and resource ownership boundaries
- protected presentation invalidation when the application security session withdraws access
- session-revision checks that prevent stale workspace operations from publishing protected presentation state
- cleanup of retained document and workspace presentation resources when protected access is withdrawn

The following capabilities remain future or deferred scope:

- functional local AI processing
- retrieval, RAG, or AI interaction inside the workspace
- workspace recovery
- Recent activity implementation
- PDF and DOCX in-app rendering
- future primary-document semantics if a later workspace model introduces
  such a concept

These future capabilities remain architectural directions established by
this ADR. They must not be interpreted as implemented functionality.

The current workspace implementation therefore establishes both the
document-centric rendering boundary and the persistent multi-document
workspace/presentation lifecycle without coupling those concerns to future
AI or search functionality.

## Architectural Boundaries

`MainForm` remains responsible for the application shell and
document-library workflow.

```text
MainForm
├── Application navigation
├── Document listing
├── Document selection
├── Import
├── Remove
└── Open
```

`WorkspacePresentation` owns the in-memory state for a managed workspace
presentation. The state includes workspace identity and type, represented
workspace members, document presentations, navigation mode, and active
document identity.

Workspace identity remains authoritative at this workspace-presentation
boundary. It is not duplicated in an individual `DocumentWorkspacePresenter`
and is not propagated through `IWorkspaceDocumentPresentationFactory.Create`.
That factory creates a child document presentation from the document identity
and the owning security-session revision only.

`WorkspacePresentation` coordinates workspace-scoped presentation behavior,
including active-document selection, navigation, document-presentation
registration, and workspace membership reconciliation. `WorkspacePresentationManager`
maintains the registry of live workspace presentations and coordinates their
lookup, creation, reuse, activation, and closure. Workspace-close requests
remain at the workspace/application boundary; they are not operations of an
individual document presenter or document view.

`WorkspaceViewForm` implements the workspace presentation view contract and
remains a WinForms Passive View. It renders workspace or temporary-document
identity, workspace navigation, tabs/sidebar layout, workspace contextual
menus, and the control-hosting arrangement required to display the active
document presentation.

The document interaction and WinForms hosting surfaces are deliberately
separate. `WorkspaceDocumentPresentation` exposes the document-specific
interaction contract through its `IDocumentView` `View` property and the
generic WinForms hosting surface through its `Form`-typed `HostedForm`
property. Both properties refer to the same underlying view instance;
`HostedForm` does not create a second view or a separate document window.

`WorkspaceDocumentPresentationFactory` may construct the concrete
`DocumentViewForm`. It supplies that same instance to
`DocumentWorkspacePresenter` as the `IDocumentView` and to
`WorkspaceDocumentPresentation` as the document view. This keeps concrete
`DocumentViewForm` construction knowledge within the factory rather than
exposing that implementation dependency to workspace hosting.

`WorkspaceViewForm` uses the generic `HostedForm` surface when preparing,
adding, removing, activating, and moving a document presentation between
its tab and sidebar containers. The workspace view does not cast the
document interaction contract to `DocumentViewForm` or search its hosted
controls for that concrete type. Moving between tab and sidebar hosting
reparents the same Form instance.

`WorkspacePresentation` retains each `WorkspaceDocumentPresentation` in
its document-presentation collection and passes only the document
identifier, optional filename, and generic `Form` host to
`IWorkspacePresentationView.AddDocumentPresentation`. The workspace view
contract therefore receives the minimum inputs required to host the child;
it does not receive the child wrapper, document presenter, or `IDocumentView`.
`WorkspaceViewForm` attaches or detaches the supplied Form for presentation
layout and does not acquire ownership of or dispose the child presenter/view
pair. `WorkspaceDocumentPresentation` remains the owner of that pair's
lifecycle.

The generic Form hosting surface is a WinForms presentation boundary only.
It does not add Form, parent-control, or workspace-navigation members to
`IDocumentView`, move navigation ownership into the document view, or
change the authoritative workspace state held by `WorkspacePresentation`.

Exposing the host Form does not create a new disposal owner or redefine the
document and workspace presentation lifetime contracts. The hosting
boundary remains separate from disposal ownership.

`DocumentWorkspacePresenter` owns document-specific presentation workflows
and coordinates the document view with the existing Application operations,
renderer resolution, session-bound dialogs, and document resource lifecycle.
It is scoped to document behavior: it carries no `WorkspaceId`, accepts no
workspace identity during construction, and exposes no workspace activation
or closure operation. Its responsibilities include document acquisition
and opening, rendering requests, document information, global document
removal, external viewing, document-specific errors, cancellation,
session-revision checks, and active/pending document-stream ownership.

After successful global document removal, `DocumentWorkspacePresenter`
raises its `DocumentRemoved` event. `WorkspacePresentation` subscribes when
it registers the child presentation and uses that notification to locate
the corresponding document presentation, close and dispose that child
presentation, remove the document from the in-memory workspace membership,
and publish the workspace-level removal/update notifications. For a
temporary workspace, the workspace presentation also raises the existing
workspace-close request. This notification preserves the coordination
boundary: document removal execution remains document-specific, while
workspace membership and workspace closure remain workspace-level
responsibilities.

`WorkspaceDocumentPresentation` remains the wrapper for one document
presentation. It exposes the document presenter, the `IDocumentView`
interaction surface, and the same underlying WinForms view through its
generic hosting surface. It does not forward workspace activation to the
document presenter. Its existing disposal path continues to dispose the
presenter and its view; removing workspace identity from the child presenter
and factory does not change presentation ownership or disposal behavior.

`DocumentViewForm` implements `IDocumentView` and is responsible for the
document-specific presentation surface:

```text
DocumentViewForm
├── Document content host
├── Unsupported-format preview
├── Explicit external-open action
└── Document-specific information/removal request events
```

`DocumentViewForm` does not own workspace identity, workspace navigation,
workspace membership, workspace management actions, or the workspace
contextual menu. When hosted by `WorkspaceViewForm`, the outer workspace
view supplies the relevant workspace chrome and interaction surface.

The workspace view may expose document-specific actions for a temporary
document session. Those requests are routed through the workspace
presentation to the active document presentation and its document
presenter. The routing does not transfer document metadata processing,
document removal execution, external-viewing lifecycle, or rendering into
`WorkspaceViewForm`.

The Application layer remains responsible for retrieving the document.

The existing `OpenDocumentHandler` continues to:

```text
OpenDocumentHandler
    ↓
IDocumentRepository
    ↓
Document metadata
    ↓
IDocumentReader
    ↓
Decrypted document stream
```

The Application layer returns:

```text
OpenDocumentResult
├── Stream Content
└── string FileName
```

The UI receives the document content through this existing Application
boundary.

The UI must not access:

- SQLite directly
- `DocumentEntity`
- encrypted `.dvault` files directly
- encryption keys
- encryption services directly

This preserves the existing separation between UI, Application, Domain, and
Infrastructure.

## Document Opening Workflow

The document opening workflow uses the existing Application retrieval
boundary and separates workspace hosting from document rendering:

```text
User selects document
    ↓
MainForm
    ↓
MainFormPresenter
    ↓
OpenDocumentHandler
    ↓
IDocumentRepository
    ↓
IDocumentReader
    ↓
OpenDocumentResult
    ↓
Managed workspace/document presentation
    ├── WorkspacePresentation
    ├── WorkspaceViewForm
    └── DocumentWorkspacePresenter
            ↓
      DocumentViewForm (IDocumentView)
            ↓
      IDocumentContentRendererResolver
            ↓
      Format-specific document renderer
```

`WorkspaceViewForm` supplies the temporary or persistent workspace
interaction surface and hosts the active document view.

`DocumentViewForm` receives the document stream and filename through the
document presentation boundary. It renders document content through the
renderer resolver and exposes document-specific presentation behavior
without owning workspace navigation or workspace actions.

### Decrypted Source-Stream Ownership and Borrowed Operations

`DocumentWorkspacePresenter` is the authoritative in-memory owner of each
pending or current decrypted source stream received through the existing
Application opening boundary. The presenter owns a pending stream until it
is promoted or rejected. After promotion, the current stream remains
available for the active document presentation and its existing actions.
Receiving the stream does not transfer disposal ownership to
`DocumentViewForm`, a renderer, or `IDocumentViewer`.

`DocumentViewForm` and its concrete renderer borrow the source stream only
for the duration of the asynchronous presentation/rendering operation.
A renderer may read the supplied stream while `RenderAsync` is in progress,
but must neither dispose it nor retain the source-stream reference after
the operation completes. Renderer-owned controls and materialized
presentation data follow their own view lifecycle; they do not extend the
lifetime of the decrypted source stream.

The explicit `IDocumentViewer.OpenAsync` operation also borrows the
presenter-owned current stream. The presenter keeps that stream valid until
the returned task completes because the viewer may still be reading it
while materializing its separately owned protected temporary artifact.
Only one active external-open operation may borrow a given source stream
at a time. A duplicate request is rejected rather than allowing concurrent
operations to reset or consume the shared stream position.

If replacement, successful removal, session revocation, or presenter
disposal requires release of a current stream while an external-open
borrow is active, the presenter requests cancellation and marks the stream
for deferred release. It must not dispose the stream while the borrowed
operation may still be reading it. The operation completion/finally path
discharges the borrow and performs the deferred disposal exactly once.
A stale operation completion must not republish the released stream or
replace a newer presentation.

Completion of `IDocumentViewer.OpenAsync` ends only the source-stream
borrow. It does not establish external-application consumption completion
or change the separate temporary-artifact lifecycle.

The external viewer remains available through an explicit, user-controlled
action independently of in-app preview availability. It also provides the
fallback for formats without an appropriate in-app renderer.

### External Viewing Temporary Artifact Lifecycle

When external viewing requires temporary plaintext materialization, the
document-viewing lifecycle owns the temporary artifact. The artifact is not
owned by canonical document storage, workspace membership, the workspace
presenter, or the document view.

The production composition registers `IDocumentViewer` as an
application-host-owned singleton. Document and workspace presenters
delegate through this interface; closing an individual document or
workspace does not dispose the shared viewer or transfer temporary-artifact
ownership to those presentation objects.

`DocumentViewer` coordinates the lifecycle through
`TemporaryDocumentArtifactStore` and `TemporaryDocumentArtifact`.
The store owns namespace creation and protected file creation. Each
`TemporaryDocumentArtifact` represents one uniquely identified artifact and
owns its writable stream and cleanup operation. While the viewer is alive,
it retains the artifact registration until cleanup succeeds. A failed
cleanup does not relinquish that registration and remains eligible for a
later owner cleanup attempt.

Viewer disposal may attempt cleanup only for artifacts whose preparation
failed or whose trustworthy external-consumer lifecycle completed
successfully. An active or unobservable external lifecycle is not eligible
for disposal-triggered deletion. An unobservable artifact therefore remains
on disk after the application-host-owned viewer is disposed. The viewer's
in-memory registration is not itself durable. The protected ownership
journal described below is the durable recovery evidence and survives
viewer disposal and process termination.

Temporary viewing content is created in the dedicated per-user namespace:

```text
%LOCALAPPDATA%
    └── DeskVault
        └── TemporaryDocuments
            └── <artifact-guid-N><validated-extension>
```

The namespace is separate from the general operating-system temporary
directory and from canonical encrypted document storage.

The namespace directory is created with a protected Windows access-control
descriptor. Inherited access rules are disabled. The current Windows user
SID receives namespace traversal (`ReadAndExecute`), file-creation
(`CreateFiles`), permission-management (`ChangePermissions`), and
synchronization (`Synchronize`) rights. Each artifact file has a separate
protected access-control descriptor granting the current user `Read`,
`Write`, `Delete`, and `Synchronize` rights. The handle used to populate
an artifact requests only `Write` and `Synchronize`. The explicit
synchronization permission is included in the expected access masks and
verified as part of the protected ACL; it reflects the Windows access-rule
representation observed for this namespace and artifact policy. These rules
restrict access at the Windows user boundary; they do not isolate processes
that run under the same Windows identity or provide a security boundary
against an administrator.

The original document filename is not used as the physical artifact name.
The store derives only a validated extension and combines it with a new
GUID-based identity. The resulting filename follows the
`<artifact-guid-N><validated-extension>` convention. The generated
identity is the artifact owner key and provides a stable namespace and
naming convention for stale-artifact recovery. Each artifact is associated
with a protected ownership sidecar whose filename includes the complete
artifact filename:
`<artifact-guid-N><validated-extension>.owner.json`. Including the
validated extension in the sidecar identity prevents different artifact
filenames that share a GUID from sharing or overwriting one ownership
record. The sidecar is created exclusively before artifact creation; an
existing sidecar is a collision and is never replaced or adopted by a new
viewing operation. Its initial `CreationPending` record reserves the
identity but does not prove ownership of an artifact already at the
candidate path.

File creation uses exclusive create-new semantics and an explicit file
access-control descriptor. A collision or failed exclusive creation must
not overwrite, truncate, or delete a pre-existing artifact. The operation
that successfully creates the file is the owner of that artifact; a
generated identity or candidate path alone does not transfer ownership of
an existing file.

The materialization and viewing sequence is:

```text
Caller-owned decrypted document stream
        ↓
DocumentViewer
        ↓
TemporaryDocumentArtifactStore
        ↓
Create exclusive CreationPending ownership reservation
        ↓
Create artifact with FileMode.CreateNew
        ↓
Persist Preparing state after successful artifact creation
        ↓
Copy complete content and flush
        ↓
Close owned write stream
        ↓
External application launch
        ↓
External lifecycle observation
        ↓
Artifact cleanup by DocumentViewer or startup recovery
```

The source document stream remains caller-owned. The temporary artifact
owns only its own write stream and filesystem artifact. The artifact is
fully written, flushed, and its write handle is closed before the external
launcher receives its path.

The external viewing lifecycle is:

```text
External application launch
  |
  v
Trustworthy external-consumer completion task available?
  |-- Yes --> await completion
  |             |-- successful completion --> cleanup
  |             `-- faulted/cancelled --> unobservable; retain
  |
  `-- No --> unobservable; retain beyond viewer disposal
```

A successful external application launch does not by itself establish that
the application has finished consuming the temporary file. The artifact
must not be deleted merely because launch succeeded. A non-null
`IExternalDocumentLaunch.Completion` task is trustworthy only when it
represents the relevant lifecycle of the application actually consuming
the document. A null task means that consumer completion is unobservable.
A task that faults or is cancelled also fails to establish completion and
must be treated as unobservable.

The Windows `ExternalDocumentLauncher` uses `Process.Start` with
`UseShellExecute = true`. The returned `Process`, when one is returned,
may represent only a short-lived Windows Shell activation or handoff. It
may exit successfully while an already-running browser or other associated
application continues to open and read the temporary document. The current
Windows Shell adapter therefore reports no consumer-completion task.
Disposing the returned `Process` wrapper releases the launcher-owned
process handle; it does not terminate the external application or establish
that document consumption has finished.

The Shell handoff process must never be treated as the external consumer.
No fixed delay or arbitrary grace period is used as a substitute for a
trustworthy completion signal.

The external application owns its own windows and tabs. DeskVault does not
track, close, or terminate an external browser tab or process. The user
closes the external tab through that application. Tab closure is not
observed by `DocumentViewer` and does not supply a consumer-completion
signal.

When a trustworthy completion task completes successfully,
`DocumentViewer` marks the external lifecycle completed and attempts
artifact cleanup. If that task faults or is cancelled, the viewer marks
the lifecycle unobservable, records a bounded warning, and retains the
artifact without attempting completion-based cleanup. When no trustworthy
completion task is available, the artifact is likewise unobservable and
must remain retained beyond viewer disposal. Stale-artifact discovery and
reconciliation remain the responsibility of the separate recovery
capability.

Cancellation before external launch, source-copy failure, and launch failure
cause the owning viewer to attempt cleanup of the artifact. A trustworthy
external-consumer completion task permits cleanup only after successful
completion. An active, absent, faulted, or cancelled external lifecycle
does not permit disposal-triggered cleanup. Repeated disposal may retry
cleanup for an artifact whose preparation failed or whose external
consumer lifecycle completed successfully but whose previous deletion
attempt failed. It must not delete an unobservable artifact merely because
the viewer or application host is being disposed.

Cleanup is an explicit outcome rather than an assumed side effect.
`TemporaryDocumentArtifact.TryCleanup()` reports success or the exception
category associated with a failure. When deletion fails, `DocumentViewer`
records a bounded warning containing the generated artifact identity and
failure category; it must not disclose the physical path, original
filename, or document content. The artifact remains registered with its
owner when cleanup has not succeeded. A later owner cleanup attempt can
retry deletion, and ownership is removed only after successful cleanup.

Filesystem deletion is best-effort with respect to operating-system locks,
access failures, and process termination. Deleting a plaintext file is not
cryptographic zeroization and does not prove that external applications,
filesystem components, backups, or other processes retained no copies.
When external-consumer completion is unobservable, the temporary artifact
may remain after the external tab is closed and after the DeskVault
application host is shut down. If the process terminates before cleanup,
stale-artifact discovery and reconciliation remain the responsibility of
the separate recovery capability defined below. Recovery must not infer
safe deletion solely from external Shell-process exit, browser-process
state, artifact extension, or age. Active or ambiguous artifacts must be
preserved. This lifecycle does not scan or clean the general
operating-system temporary directory.

### Durable Temporary Artifact Ownership

Each temporary viewing artifact has a protected, durable ownership sidecar
in the same dedicated per-user namespace. The sidecar name is derived from
the complete artifact filename, not only its GUID. Its append-only journal
contains versioned ownership records identifying the artifact ID, exact
artifact filename, and lifecycle state. The sidecar uses the protected
current-user access-control policy established for the namespace and
artifact.

The ownership lifecycle uses four states:

- `CreationPending` — the store has created the durable ownership sidecar
  and appended its initial reservation record, but has not yet established
  ownership of an artifact file. The reservation records the intended
  artifact ID and exact filename; it is not proof that an existing object
  at that path was created by this operation.
- `Preparing` — `FileMode.CreateNew` has successfully returned the
  artifact's write handle and the store has persisted the corresponding
  ownership transition. The operation now has positive evidence that it
  created the artifact and is preparing/materializing it. No external
  consumer has been given the path.
- `ExternalConsumerMayNeedArtifact` — persisted before invoking the
  external launcher. The external application may have received the path
  and may still require the artifact.
- `CleanupEligible` — the owner has established that no usable external
  launch lifecycle was established, or that a trustworthy external-consumer
  lifecycle completed successfully, and cleanup may be attempted.

The allowed transitions are
`CreationPending → Preparing`,
`Preparing → ExternalConsumerMayNeedArtifact`,
`Preparing → CleanupEligible`, and
`ExternalConsumerMayNeedArtifact → CleanupEligible`. Transitions are
monotonic; a record cannot move backwards. Each committed transition is
appended to the journal and flushed before the owner proceeds across the
corresponding lifecycle boundary. `CreationPending → Preparing` is
committed only after exclusive artifact creation succeeds. A partial
trailing journal entry that was not terminated as a complete record is
not treated as a committed state. A malformed complete record or an
invalid transition makes the ownership evidence uncertain and the artifact
is preserved.

The artifact owner holds an exclusive process lease on its sidecar while
the owned operation is active. The lease prevents startup recovery from
mistaking an active in-process owner for an abandoned one. Releasing the
lease does not itself change the durable lifecycle state and is not
evidence that an external consumer has finished. In particular, disposing
the viewer or terminating DeskVault cannot convert
`ExternalConsumerMayNeedArtifact` into `CleanupEligible`.

The viewer records
`ExternalConsumerMayNeedArtifact` durably before invoking the external
launcher. A successful launch, a short-lived Shell activation-process exit,
a missing completion task, or a faulted/cancelled completion task does not
establish that the consuming application has finished. After a launch
handle has been returned, only a successful trustworthy completion
observation permits the transition to `CleanupEligible`. Under the
current launcher contract, a synchronous `Launch()` exception before a
launch handle is returned is a failed launch; the viewer transitions the
artifact to `CleanupEligible` and attempts owner cleanup. Once a handle has
been returned, a failure while reading its completion task or establishing
observation is an unobservable handoff: the viewer retains
`ExternalConsumerMayNeedArtifact` and does not infer that the artifact is
safe to delete. A launcher implementation that can hand off the path and
then fail before returning a handle must expose that ambiguous outcome
explicitly rather than report it as an ordinary pre-handle launch failure.

### Startup Recovery of Temporary Viewing Artifacts

Startup recovery is a separate capability from canonical encrypted
document-artifact reconciliation. The production host registers the
temporary-artifact store and recovery service as shared singletons and
registers the recovery hosted service before workstation-security
monitoring and automatic-lock hosted services. The host starts hosted
services before presenting startup authentication or allowing normal
document viewing. Recovery therefore runs before a new viewing operation
can create conflicting ownership state.

Recovery enumerates only top-level ownership sidecars in the exact
`%LOCALAPPDATA%\DeskVault\TemporaryDocuments` namespace. It does not
enumerate the general operating-system temporary directory, recurse into
other directories, or select files based on age, extension, or filename
fragment. Candidate artifact paths are derived from the validated ownership
record and must resolve to the exact artifact filename within the dedicated
namespace. A genuinely absent namespace produces no candidates. An
inaccessible namespace or a path that exists but is not a valid directory
is reported as a recovery failure rather than silently treated as an empty
namespace.

Before deleting anything, recovery validates the namespace and artifact
security boundary, rejects reparse-point candidates, verifies the sidecar
and artifact identity relationship, validates the complete ownership
journal, and acquires the exclusive ownership lease. A live owner lease
means the artifact is active and it is preserved. If the lease cannot be
acquired, ownership is unreadable, metadata is malformed, the artifact
identity/path is inconsistent, ACL or file-type checks fail, or any other
evidence is insufficient, recovery preserves the artifact and records an
observable finding rather than deleting speculatively.

The recovery eligibility policy is deliberately conservative:

- A valid `CreationPending` reservation is reconciled without assuming
  ownership of the candidate artifact path. If the owner lease is no
  longer held and the recorded artifact path is absent, recovery removes
  the empty reservation sidecar. If any filesystem object exists at that
  path, recovery preserves both the object and the reservation because
  `CreationPending` does not prove that the interrupted operation created
  it. This applies even when the object has the expected generated name.
- A valid `Preparing` artifact is eligible for cleanup only when its
  owner lease is no longer held and all ownership, path, file-type, and
  access-control checks succeed. The transition to `Preparing` proves
  exclusive artifact creation succeeded; by contract, the external
  launcher has not yet received this artifact.
- A valid `CleanupEligible` artifact is eligible for cleanup only when
  its owner lease is no longer held and the same validation checks succeed.
- An `ExternalConsumerMayNeedArtifact` artifact is preserved even when
  its owner lease has been released. The durable state records that an
  external consumer may still require it; process termination or lease
  release cannot prove otherwise.
- Ownership evidence with no valid complete record, a malformed complete
  record, an invalid state transition, ambiguous identity, or another
  unresolved validation failure is preserved for later review. A
  non-terminated partial trailing append is ignored; the latest valid
  complete record remains authoritative.

For an eligible `Preparing` or `CleanupEligible` candidate, recovery
removes the temporary artifact and then its ownership sidecar. An already
absent artifact is treated idempotently; the corresponding validated
sidecar can still be removed. An absent `CreationPending` artifact is a
reservation-only cleanup and has no artifact deletion step. If artifact or
sidecar deletion fails, the remaining ownership evidence is retained. If
one candidate fails, recovery records the per-artifact failure and
continues considering other independent candidates. Namespace-level
enumeration and validation failures are also observable. Diagnostics are
bounded and do not expose document content or unnecessary filesystem
details.

The ownership sidecar and artifact both use exclusive creation semantics.
A new viewing operation cannot overwrite an existing artifact or replace
its ownership sidecar. An existing retained artifact identity is never
adopted by a new operation; a collision is reported and the new operation
must use a distinct GUID-based identity.

This recovery capability does not reuse or broaden
`RecoverDocumentArtifacts` or `DocumentArtifactEnumerator`. Those
components remain scoped to canonical encrypted `.dvault` artifacts.
Temporary plaintext recovery operates only on the dedicated temporary
namespace and its ownership metadata. The canonical encrypted document
artifact and document metadata remain unchanged.

The canonical encrypted `.dvault` artifact is never replaced or modified by
the external-viewing lifecycle. Temporary viewing content is a disposable
presentation representation and is never an alternative authoritative
document store.

## Document Workspace Model

A workspace is a document context, while a document presentation is the UI
representation of an individual member of that context.

The current model supports:

```text
Temporary Workspace
└── Document

Persistent Workspace
├── Document
├── Document
└── Document
```

Workspace membership and document presentation are deliberately separate.
Closing a presented document removes its presentation from the workspace UI
but does not remove the document from persistent workspace membership.

A closed member can therefore be presented again without re-adding it to the
workspace.

The workspace itself and the AI context remain separate concepts:

```text
Documents
    ↓
Workspace Membership
    ↓
Document Presentations
    ↓
Future Retrieval / AI Context
```

This preserves a path toward shared processing and retrieval without making
rendered UI controls the source of truth for document membership or future AI
state.

## Related Documents and Workspaces

DeskVault now supports persistent grouping of related documents through
named workspaces.

A persistent workspace can add document members and remove document members
without deleting the underlying documents from the document library.

The current interaction is:

```text
Persistent Workspace
    ↓
Add Documents
    ↓
Workspace Membership
    ↓
Open / Present Documents
```

A document presentation can subsequently be closed while membership remains
intact. The member can be reopened from the workspace.

A temporary document session can also be explicitly converted into a
persistent workspace:

```text
Open Document
    ↓
Temporary Workspace
    ↓
Save as Workspace
    ↓
Persistent Workspace
```

The conversion retains the existing workspace identity and presentation
rather than creating a second workspace.

The workspace remains distinct from future AI context so that persistence
and presentation decisions do not determine future retrieval or AI
semantics.

## Multiple Document Workspaces

DeskVault uses an application-managed workspace model in which a persistent
workspace can present multiple documents.

Conceptually:

```text
MainForm
    │
    ├── Workspace A
    │     ├── Document A1
    │     └── Document A2
    │
    └── Workspace B
          ├── Document B1
          └── Document B2
```

Multiple workspace presentation contexts can exist simultaneously.

If a workspace is already represented by an active presentation, opening or
activating that workspace reuses the existing presentation rather than
creating a duplicate.

For direct document opening, an existing active temporary presentation for
the same document is activated rather than duplicated.

Document presentation identity and workspace identity are therefore
explicitly maintained independently of the eventual window-management
strategy.

## MDI Consideration

DeskVault will not adopt Multiple-Document Interface (MDI) as a mandatory architectural requirement at this stage.

The application-managed workspace model provides the required scalability without immediately coupling the application to a specific WinForms window-management pattern.

MDI may be evaluated later if it provides a clear usability benefit for managing multiple document workspaces.

The document workspace abstraction should remain independent of whether future workspaces are presented as:

- independent windows
- MDI child windows
- tabs
- another application-managed presentation

## Document Rendering Strategy

Document rendering will be designed around a scalable rendering boundary rather than embedding format-specific logic directly into `DocumentViewForm`.

Conceptually:

```text
DocumentViewForm
        ↓
Document Content Renderer
        ↓
┌────────────────┬────────────────┬────────────────┐
│ Text Renderer  │ PDF Renderer   │ DOCX Renderer  │
└────────────────┴────────────────┴────────────────┘
```

The initial implementation will prioritize formats that can be presented simply within the application.

Initial in-app support may include:

```text
TXT       → in-app text viewer
MD        → in-app text viewer
CSV       → in-app structured/grid viewer
HTML/HTM  → in-app static HTML preview
```

Additional renderers can be introduced independently as the application grows.

`DocumentViewForm` should not contain format-specific rendering logic.

Formats without an appropriate in-app renderer may temporarily use the existing external-view fallback.

## Current Implementation

The initial structured CSV rendering path has now been implemented.

CSV documents are parsed into a structured `CsvDocument` representation before being passed to the UI renderer.

The representation preserves:

- column definitions
- row values
- structural warnings
- preview-limit state

CSV rendering is implemented through `CsvDocumentContentRenderer` and resolved through the existing document-content-renderer boundary.

CSV parsing supports bounded preview materialization through `CsvParsingOptions`, with configuration supplied through the `CsvParsing` configuration section.

The renderer displays structural warnings without altering the underlying document representation.

## AI Workspace

The workspace presentation will reserve a dedicated area for future local
AI functionality.

The AI assistant will be adaptive and collapsible rather than permanently
consuming a fixed portion of the workspace. Its interaction and layout
belong to `WorkspaceViewForm`, which owns the surrounding workspace
presentation.

The intended future structure is:

```text
WorkspaceViewForm
├── Workspace identity and navigation
├── Document presentation host
│   └── DocumentViewForm
│       └── Document content
└── AI Assistant
    ├── Conversation
    ├── Prompt
    ├── Sources
    ├── Retrieved Context
    └── Model Information
```

Future AI capabilities may include:

- document summarization
- document question answering
- source-grounded responses
- document-specific retrieval
- multi-document retrieval
- semantic search
- RAG
- local model interaction

These capabilities are not part of the current implementation.

The workspace UI must therefore provide the structural foundation without
presenting future AI functionality as already implemented. The document
renderer and `DocumentViewForm` remain independent of the future AI
processing and presentation boundary.

## Security Considerations

Documents remain encrypted at rest.

The document workspace receives document content through the existing Application workflow after the document has been retrieved and decrypted.

The UI must not introduce its own document decryption or key-management implementation.

The workspace should avoid unnecessarily persisting plaintext document content outside the controlled document-viewing workflow.

Future multi-document AI processing must also preserve the existing security boundaries around document storage and encryption.

## Protected Presentation Lifecycle

The in-app workspace is a protected presentation boundary. Its authorization is governed by the authoritative `IApplicationSecuritySession` contract defined by ADR-0014; the workspace and its presenters must not establish an independent authoritative authentication state.

Before beginning a protected presentation operation, a consumer must observe the current immutable security-session snapshot and require protected access to be available. Asynchronous presentation operations must retain the snapshot revision under which they began and re-check the authoritative current snapshot before publishing results or attaching presentation resources. A delayed or out-of-order notification must not cause an older authorization observation to replace a newer authoritative snapshot.

When the session withdraws protected access, active in-app workspace and document presentations are invalidated. The application shell enters its locked presentation state, pending search and document-open work is invalidated or cancelled, retained workspace/document presentation state is cleared, and owned in-app presentation resources and document streams are disposed through their respective lifecycle owners. Cleanup must attempt all managed presentations and resources even when an individual disposal operation fails; a cleanup failure must not prevent the application from remaining in its locked state.

A completion from an operation started under an earlier session revision must not restore document content, workspace metadata, search results, selection state, or another protected presentation after revocation. Re-authentication permits a new authorized initialization using the current session snapshot; it does not restore a presentation that was invalidated by the earlier lock.

This presentation lifecycle does not revoke document or database encryption keys, change encrypted storage, or replace the external-viewing temporary plaintext artifact lifecycle described above. Those responsibilities remain governed by their existing Application, Infrastructure, and viewer boundaries.

## Alternatives Considered

### Continue using the external default application

Rejected as the long-term document-viewing architecture.

External applications prevent DeskVault from providing a unified document and knowledge workspace and make document-specific AI interaction difficult to integrate.

The existing implementation may remain useful temporarily as a fallback for formats that do not yet have an in-app renderer.

### Put document viewing directly inside MainForm

Rejected.

`MainForm` is intended to remain the application shell.

Embedding document rendering, document-specific functionality, and future AI interaction directly into `MainForm` would cause it to accumulate too many responsibilities.

### Create separate forms for every feature

Rejected.

Forms should represent meaningful user workflows rather than individual technical operations.

The document workspace is a meaningful user workflow. Separate forms for small actions such as import or remove are unnecessary.

### Use one reusable DocumentViewForm for every document

Rejected.

A document workspace should maintain document-specific context and should be independently manageable.

One workspace per primary document provides a better foundation for future document-specific AI conversations and retrieval contexts.

### Adopt MDI immediately

Deferred.

MDI may eventually provide useful multi-document behavior, but adopting it before the workspace requirements are fully understood would introduce unnecessary coupling to a specific WinForms presentation model.

### Build every document renderer immediately

Rejected.

Supporting every document format before validating the workspace architecture would increase complexity unnecessarily.

The rendering boundary allows document-format support to grow independently.

## Consequences

### Positive

- DeskVault gains a unified in-app document experience.
- `MainForm` remains focused on application-level navigation and document-library responsibilities.
- `WorkspaceViewForm` owns workspace identity, navigation, and contextual
  interaction, while `DocumentViewForm` owns document-content presentation.
- Document-specific presentation requests remain routed through the active
  document presentation and its presenter.
- The AI assistant has a natural location within the document context.
- The AI panel can adapt to available screen space.
- Application and Infrastructure boundaries remain intact.
- The UI does not need direct access to persistence or encryption infrastructure.
- One primary document per workspace keeps the initial implementation simple.
- Related documents can be added later without redesigning the workspace concept.
- Ad-hoc document groups can evolve into persistent named workspaces.
- Multiple document workspaces remain possible.
- Duplicate workspaces for the same document can be avoided.
- The application is not prematurely coupled to MDI.
- Document rendering can scale through independent renderer implementations.
- External document launching can be replaced incrementally.
- The architecture provides a foundation for future search, RAG, and local AI capabilities.
- Protected workspace content is invalidated when the authoritative application security session withdraws access.
- Session-revision fencing prevents stale asynchronous presentation work from publishing content after revocation.
- Workspace and document presentation cleanup is performed independently of workspace membership and persistent document data.
- External viewing uses a dedicated per-user temporary namespace with explicit Windows access-control rules rather than arbitrary files directly in the general operating-system temporary directory.
- Generated artifact identities and create-new semantics prevent a viewing operation from overwriting an existing file it does not own.
- Temporary artifact stream and file ownership remain within the viewer boundary, separate from caller-owned source streams and canonical document storage.
- Cleanup results and bounded diagnostics make deletion failures observable; unsuccessful cleanup does not silently discard the owner's artifact registration.
- The stable namespace, generated artifact identity, and protected
  ownership sidecar provide a narrow durable identification boundary for
  temporary viewing recovery.
- Versioned, append-only lifecycle records and an exclusive owner lease
  distinguish active ownership from recoverable abandoned work.
- Startup recovery runs before normal document viewing and selects only
  validated ownership sidecars in the dedicated temporary-document
  namespace.
- Recovery preserves live owners, possible external consumers, and
  uncertain ownership; it removes only validated `Preparing` or
  `CleanupEligible` artifacts with no live owner lease and removes an
  absent `CreationPending` reservation without touching an unconfirmed
  filesystem object.
- Per-artifact recovery failures remain observable and do not prevent
  independent safe candidates from being considered.
- Windows Shell activation-process exit is not treated as external-consumer
  completion; absent, faulted, or cancelled consumer observation leaves the
  artifact unobservable.
- Unobservable external-viewing artifacts remain on disk beyond viewer
  disposal and process termination for conservative recovery handling
  rather than being deleted speculatively.

### Negative

- In-app document viewing introduces additional UI complexity.
- Document rendering may require format-specific components and dependencies.
- Multiple document workspaces require window lifecycle and management decisions.
- Workspace grouping introduces additional concepts beyond individual documents.
- AI workspace functionality will require additional Application and AI-layer capabilities.
- Multi-document AI processing will require retrieval and context-management infrastructure.
- Some document formats may initially require fallback handling through external applications.
- Protected presentation consumers must observe session snapshots, compare revisions, invalidate stale operations, and coordinate resource cleanup when access is withdrawn.
- The protected per-user temporary namespace adds filesystem access-control and lifecycle management requirements.
- Windows Shell handoff may make external-consumer completion unobservable.
- A temporary artifact in
  `ExternalConsumerMayNeedArtifact` remains retained even after its owner
  lease is released and DeskVault terminates, because no reliable evidence
  establishes that the external consumer has finished. A filesystem object
  associated with `CreationPending` is also retained when ownership cannot
  be established. Either category may remain indefinitely; filesystem
  deletion is not cryptographic erasure.
- The Windows user ACL boundary does not isolate DeskVault from other processes running under the same user identity or from administrators.

These trade-offs are acceptable because the decisions align with DeskVault's long-term product direction.

## Result

DeskVault will implement a managed workspace presentation that supports
temporary single-document viewing and persistent multi-document knowledge
contexts.

The resulting UI direction is:

```text
Login
    ↓
Application Shell
    ↓
Document Library
    ↓
WorkspaceViewForm
    ├── Workspace/document identity and navigation
    ├── Workspace/document contextual actions
    ├── DocumentViewForm
    │   └── Document Renderer Boundary
    │       ├── Text
    │       ├── Markdown
    │       └── CSV
    └── Future Adaptive Local AI Assistant
             ↓
        Save as Workspace
             ↓
      Persistent Workspace
        ├── Document
        ├── Document
        └── Document
```

The architecture separates:

```text
Document lifecycle
        ≠
Workspace membership lifecycle
        ≠
Document presentation lifecycle
        ≠
Recent activity
        ≠
AI context
```

The renderer architecture additionally separates:

```text
Document parsing / extraction
        ≠
Document rendering
        ≠
AI / search processing
```

The existing Application and Infrastructure boundaries remain responsible
for document retrieval, decryption, persistence, and storage.

This separation allows DeskVault to evolve toward multi-document workspaces,
local AI, retrieval, RAG, and enterprise knowledge workflows without
requiring the document-content view to own workspace behavior or future
AI-processing capabilities.
