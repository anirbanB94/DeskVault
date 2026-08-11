# ADR-0007: Define Document Workspace UI and Interaction Model

## Status

Accepted

## Context

ADR-0006 established the direction for an in-app document workspace and defined `DocumentViewForm` as the dedicated document-centric workspace.

The next implementation phase requires a concrete interaction model for that workspace.

DeskVault is intended to evolve from a document-management application into a secure, offline-first enterprise knowledge platform. The document workspace therefore needs to support the current single-document workflow while remaining scalable to related documents, persistent workspaces, multiple workspace windows, document rendering extensions, and future local AI capabilities.

The design should avoid prematurely implementing future functionality while ensuring that the first implementation does not create architectural constraints that would prevent those capabilities later.

## Decision

DeskVault will implement `DocumentViewForm` as an enterprise-oriented document workspace with the following structure:

```text
DocumentViewForm
├── Enterprise workspace header
├── Document content area
└── Adaptive AI assistant
```

The workspace header will provide document identity and high-level actions.

The intended header structure is:

```text
┌─────────────────────────────────────────────────────────┐
│ ← Documents                                             │
│                                                         │
│ report.pdf                         [ AI ] [⋯] [Close]   │
│ Imported Aug 11 • PDF                                   │
├─────────────────────────────────────────────────────────┤
│                                                         │
│                  Document                               │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

The exact visual styling may evolve during implementation, but the header will preserve the distinction between navigation, document identity, workspace actions, and AI access.

## Workspace Header

The workspace header will provide:

- navigation back toward the document library
- primary document name
- document metadata appropriate for the current view
- AI assistant toggle
- contextual workspace/document menu
- workspace close action

The contextual menu will support both document-level and workspace-level actions.

The intended menu is:

```text
[⋯]
├── Add Related Documents
├── Document Information
├── Save as Workspace
├── Remove Document
└── Close Workspace
```

Actions that are not yet implemented may remain future capabilities and should not be presented as completed functionality.

## AI Assistant Interaction

The AI assistant will use an adaptive and collapsible side-panel model.

The header will provide an `[AI]` toggle.

When the assistant is collapsed, a persistent edge tab will remain available.

Conceptually:

```text
Large screen

┌──────────────────────────────────────────────┬──────────────┐
│ Document                         [ AI ]      │ AI Assistant │
│                                              │              │
└──────────────────────────────────────────────┴──────────────┘
```

When collapsed:

```text
┌─────────────────────────────────────────────────────────────┐
│ Document                                      [ AI ]        │
│                                             ┌────┐          │
│                                             │ AI │          │
│                                             └────┘          │
└─────────────────────────────────────────────────────────────┘
```

The panel will adapt to available screen space.

On larger screens, the assistant may remain visible beside the document.

On smaller screens, the assistant may be collapsed by default or collapsed when required to preserve document viewing space.

The initial implementation will provide the structural UI boundary for the assistant. Actual AI processing, retrieval, RAG, and local model interaction remain future capabilities.

## Document Rendering Architecture

The document content area will not contain format-specific rendering logic.

The workspace will depend on a renderer resolution boundary:

```text
DocumentViewForm
        ↓
IDocumentContentRendererResolver
        ↓
IDocumentContentRenderer
        ↓
Format-specific renderer
```

The renderer resolver will determine the appropriate renderer for the document format.

Conceptually:

```text
.pdf → PDF renderer
.txt → Text renderer
.md  → Markdown renderer
.csv → CSV renderer
```

The architecture must allow new renderers to be added without changing `DocumentViewForm`.

The initial implementation will prioritize simple formats that can be rendered directly within the application.

The intended initial direction is:

```text
TXT → in-app text renderer
MD  → in-app text/Markdown renderer
CSV → in-app structured/grid renderer
```

PDF, DOCX, and other formats may initially use an explicit external-view fallback until an appropriate in-app renderer is introduced.

## Unsupported Formats

When no in-app renderer is available, DeskVault will not automatically launch the operating system's associated application.

Instead, the workspace will display an in-app message explaining that the format is not currently supported for preview.

The user will explicitly choose whether to open the document externally.

Conceptually:

```text
┌──────────────────────────────────────────────┐
│                                              │
│  Preview unavailable for this file type.     │
│                                              │
│  DeskVault does not currently support        │
│  in-app preview for this format.             │
│                                              │
│              [ Open Externally ]             │
│                                              │
└──────────────────────────────────────────────┘
```

This preserves user control and keeps the workspace active.

## Loading State

Opening a document may involve document retrieval, decryption, renderer initialization, and future processing operations.

The workspace will therefore provide an explicit loading state.

The preferred initial behavior is:

```text
┌──────────────────────────────────────────┐
│ report.pdf                    [ AI ] [⋯] │
│ Loading document...                      │
├──────────────────────────────────────────┤
│                                          │
│       Preparing document preview...      │
│                                          │
└──────────────────────────────────────────┘
```

The state model may later support more specific status messages such as:

```text
Loading document...
Preparing preview...
Loading pages...
Indexing...
Ready
```

The initial implementation should keep these states simple while preserving a path for more detailed processing states later.

## Workspace Lifecycle

Closing a workspace will use conditional confirmation.

If the workspace has no unsaved state, it should close without unnecessary confirmation.

If meaningful unsaved workspace state exists, DeskVault will ask the user to confirm before closing.

Conceptually:

```text
Close workspace
      ↓
Has unsaved state?
   ┌──┴──┐
  No     Yes
  ↓       ↓
Close   Confirm
```

This allows the current document-viewing workflow to remain frictionless while protecting future workspace configuration, related-document changes, AI notes, or other editable state.

## Document and Workspace Lifecycle

Document lifecycle and workspace lifecycle are separate concepts.

For the current single-document implementation:

```text
Workspace
└── Primary Document

Remove Primary Document
        ↓
Document removed
        ↓
Workspace closes
```

The future multi-document model will behave differently.

If one related document is removed while other documents remain:

```text
Workspace
├── Primary Document
├── Related Document
└── Related Document

Remove Related Document
        ↓
Workspace remains
```

The workspace is therefore not destroyed merely because one document is removed.

## Primary Document Removal

If the primary document is removed from a future multi-document workspace while related documents remain, DeskVault will ask the user to select a new primary document.

Conceptually:

```text
Project Alpha Workspace
├── Report.pdf        ← Primary
├── Contract.pdf
└── Notes.md

Remove Report.pdf
        ↓
┌──────────────────────────────────────┐
│ Primary document is being removed.   │
│                                      │
│ Choose a new primary document:       │
│                                      │
│ ○ Contract.pdf                       │
│ ○ Notes.md                           │
│                                      │
│ [ Cancel ]        [ Remove ]         │
└──────────────────────────────────────┘
```

DeskVault will not silently promote another document without user confirmation.

If no related documents remain, the current workspace closes because it no longer has a document context.

## Workspace Persistence

DeskVault will distinguish between a temporary workspace session and a persistent workspace.

Opening a document will initially create a temporary workspace.

The temporary workspace may remain temporary if the user is simply viewing the document.

If the user adds related documents, changes workspace configuration, or explicitly chooses to save the workspace, the workspace can become persistent.

Conceptually:

```text
Open Document
      ↓
Temporary Workspace
      │
      ├── Read / inspect
      │       ↓
      │    Close
      │
      └── Add context / Save
                  ↓
        Persistent Workspace
```

Persistent workspace state will use explicit saving rather than automatically converting every temporary session into a persistent workspace.

## Workspace Recovery

Persistent workspace state and active session state will remain conceptually separate.

Users explicitly save persistent workspace changes.

DeskVault may maintain recovery information for an active workspace session so that unexpected application shutdown does not unnecessarily destroy working context.

The intended model is:

```text
Persistent Workspace
        ↑
    Explicit Save
        ↑
Active Workspace Session
        ↓
Unexpected shutdown
        ↓
Auto-recovery
```

Recovery information should not be treated as authoritative workspace data.

## Workspace Naming

When a temporary workspace is saved as a persistent workspace, DeskVault will suggest a workspace name based on the primary document.

The user may edit the suggested name before saving.

For example:

```text
Save Workspace

Name:
[ Report.pdf                    ]

[ Cancel ]          [ Save ]
```

The user may change the name to a meaningful workspace name such as:

```text
Project Alpha
Q3 Financial Review
Customer Onboarding
```

This provides a low-friction default while supporting meaningful enterprise organization.

## Workspace Discovery

The application shell will provide both `Recent` and `Workspaces` navigation areas.

The intended navigation is:

```text
Left Navigation
├── Documents
├── Recent
├── Workspaces
├── Search
└── Settings
```

`Workspaces` represents intentionally saved and organized knowledge contexts.

`Recent` represents recently interacted-with application objects.

The distinction is:

```text
Documents
→ What exists

Recent
→ What I recently interacted with

Workspaces
→ What I intentionally organized and saved
```

## Recent Activity

Recent activity will be activity-based rather than limited to document opening.

Recent items may represent:

```text
Recent
├── Workspace: Project Alpha
├── Document: report.pdf
├── Workspace: Contract Review
├── Document: notes.md
└── ...
```

The ordering will be based on the most recent meaningful interaction.

Meaningful interactions may include opening, activating, adding related documents, saving, and future workspace or AI activity.

Recent retention will use a configurable or policy-based model rather than hard-coding an architectural limit such as ten or fifty items.

The initial implementation may use a sensible default policy.

## Recent Persistence and Ownership

Recent activity will persist locally across application restarts, but it will be treated as a cache rather than authoritative application data.

Conceptually:

```text
Documents / Workspaces
        ↓
Authoritative application data

Recent Activity
        ↓
Persisted local cache
        ↓
Retention / cleanup policy
```

Recent activity will be associated with the authenticated user and stored locally on the machine.

Conceptually:

```text
Authenticated User
        ↓
User-specific Recent Activity
        ↓
Local persistent cache
        ↓
Retention Policy
```

If the Recent cache is cleared, rebuilt, or becomes unavailable, the underlying documents and workspaces remain unaffected.

The detailed Recent implementation is deferred to a later navigation/activity phase.

## Workspace Concurrency

The workspace architecture will support multiple simultaneous document workspaces, but the initial implementation will manage one active workspace at a time.

The architecture must therefore preserve document/workspace identity so that multiple workspaces can be introduced without redesigning the workspace concept.

The future model is:

```text
MainForm
 ├── Workspace A
 ├── Workspace B
 └── Workspace C
```

When multiple workspaces are enabled, opening a document that already has an active workspace will activate the existing workspace rather than create a duplicate.

The exact presentation mechanism may remain independent of the underlying workspace model.

## Scope of Initial Implementation

The current implementation phase will focus on the `DocumentViewForm` and its supporting boundaries.

The following are architectural targets but are not required to be fully implemented in the first workspace vertical slice:

- persistent named workspaces
- multi-document workspace groups
- Recent activity
- local AI processing
- retrieval-augmented generation
- PDF renderer
- DOCX renderer
- multiple simultaneous workspace windows
- automatic workspace recovery

The first implementation should establish the correct boundaries and interaction model without prematurely implementing all future functionality.

## Alternatives Considered

### Automatically persist every opened document as a workspace

Rejected.

Most document openings are expected to be simple viewing sessions. Automatically persisting every document would create unnecessary workspace state.

A temporary workspace that becomes persistent when the user intentionally saves or enriches it provides a cleaner user experience.

### Automatically promote another document when the primary document is removed

Rejected.

Silently changing the primary document could change the meaning of the user's workspace.

The user should explicitly select the new primary document.

### Always confirm workspace closure

Rejected.

Confirmation for every workspace close would create unnecessary friction for simple document viewing.

Conditional confirmation provides protection only when meaningful unsaved state exists.

### Automatically save every workspace change

Rejected.

Automatic persistence would blur the distinction between temporary session state and intentional persistent workspace configuration.

Explicit save provides clearer user control.

### Automatically open unsupported formats externally

Rejected.

Silent external application launches reduce user control and interrupt the unified workspace experience.

An explicit `Open Externally` action provides a safer and clearer fallback.

### Implement Recent immediately

Deferred.

Recent activity has been designed so that its future behavior is clear, but implementation is deferred to avoid expanding the current document workspace vertical slice unnecessarily.

### Implement multiple workspace windows immediately

Deferred.

The architecture will support multiple workspaces, but the first implementation will manage one active workspace to reduce initial UI and lifecycle complexity.

## Consequences

### Positive

- `DocumentViewForm` has a clear enterprise-oriented interaction model.
- The workspace remains focused on the primary document.
- The AI assistant has a defined adaptive interaction pattern.
- Document rendering is separated behind a resolver and renderer boundary.
- Unsupported formats have an explicit and user-controlled fallback.
- Loading states provide clear feedback during document preparation.
- Document lifecycle is separated from workspace lifecycle.
- Future multi-document workspaces can survive the removal of individual related files.
- Primary-document removal does not silently change workspace semantics.
- Temporary document sessions do not automatically create persistent workspaces.
- Persistent workspace naming remains user-controlled.
- Recent activity has a clear future model without expanding the current implementation.
- Multiple workspace support can be introduced later without redesigning workspace identity.
- The current implementation remains intentionally scoped.

### Negative

- Additional workspace state and lifecycle concepts increase application complexity.
- Renderer resolution introduces additional Application/UI abstractions.
- Future workspace persistence and recovery will require additional storage design.
- Multi-document workspace management will require additional UI and domain modeling.
- Recent activity will require a separate persistence and retention implementation.
- AI functionality will require additional Application and AI-layer capabilities.
- Some document formats will initially lack in-app rendering.

These trade-offs are acceptable because they establish a scalable foundation while keeping the first implementation focused.

## Result

DeskVault will implement an enterprise-oriented document workspace that supports the current single-document workflow while establishing scalable boundaries for future multi-document knowledge work.

The resulting direction is:

```text
Application Shell
    ↓
Document Library
    ↓
Temporary Document Workspace
    ├── Enterprise Header
    ├── Document Renderer Boundary
    └── Adaptive AI Assistant
            ↓
      Save / Add Context
            ↓
     Persistent Workspace
            ↓
     Related Documents
            ↓
      Future AI Context
```

The architecture separates:

```text
Document lifecycle
        ≠
Workspace lifecycle
        ≠
Recent activity
        ≠
AI context
```

This separation allows DeskVault to evolve toward multi-document workspaces, local AI, retrieval, RAG, and enterprise knowledge workflows without requiring the initial document workspace implementation to contain those future capabilities.
