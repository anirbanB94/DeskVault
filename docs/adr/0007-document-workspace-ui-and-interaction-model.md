# ADR-0007: Define Document Workspace UI and Interaction Model

## Status

Accepted

## Context

ADR-0006 established the direction for an in-app document workspace and defined `DocumentViewForm` as the dedicated document-centric workspace.

The next implementation phase requires a concrete interaction model for that workspace.

DeskVault is intended to evolve from a document-management application into a secure, offline-first enterprise knowledge platform. The document workspace therefore needs to support temporary single-document viewing, persistent multi-document workspaces, multiple simultaneous workspace presentation contexts, document rendering extensions, and future local AI capabilities.

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
┌────────────────────────────────────────────────────────────────────┐
│ ← Documents                                                        │
│                                                                    │
│ report.pdf                                  [ AI ] [⋯] [Close]      │
│ Imported Aug 11 • PDF                                               │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│                         Document                                   │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

The exact visual styling may evolve during implementation, but the header will preserve the distinction between navigation, document identity, workspace actions, and AI access.

## MVP 1 Scope Boundary

The interaction model described by this ADR establishes both the current
workspace implementation and the longer-term interaction direction.

The current workspace boundary is:

```text
Document Library
      ↓
Workspace Presentation
      ├── Workspace / document identity
      ├── Document content
      ├── Document information
      └── Workspace/document actions
```

The current implementation supports:

- temporary single-document presentation when opening directly from Documents
- persistent named workspaces containing multiple documents
- Tabs as the default persistent-workspace navigation mode
- Sidebar as an alternative persistent-workspace navigation mode
- multiple simultaneous workspace presentation contexts
- activation/reuse of an existing presentation rather than duplication
- closing a document presentation without removing persistent membership
- reopening a closed workspace member
- explicit Save as Workspace conversion from a temporary session

Functional AI interaction, Recent activity implementation, workspace recovery,
PDF/DOCX in-app rendering, and future primary-document reassignment remain
future capabilities unless separately implemented.

This keeps the current workspace focused on document and workspace
interaction while preserving the architectural direction required for later
knowledge-work evolution.

## Current Implementation and Deferred Scope

The interaction model described by this ADR establishes both the current
workspace architecture and the intended direction for future workspace
capabilities.

Currently implemented:

- `DocumentViewForm` as the document presentation surface
- workspace/document identity
- temporary and persistent workspace presentation modes
- persistent multi-document workspace membership
- Tabs as the default navigation mode
- Sidebar as an alternative navigation mode
- multiple simultaneous workspace presentation contexts
- activation/reuse of existing workspace presentations
- document content area
- document information
- workspace/document actions
- document presentation close independent from persistent membership removal
- reopening workspace members after their presentation has been closed
- explicit Save as Workspace conversion
- loading/error/unsupported-format presentation boundaries
- renderer resolution through `IDocumentContentRendererResolver`
- TXT rendering
- Markdown rendering with the documented security policy
- CSV structured/grid rendering
- renderer-owned presentation resources and lifecycle
- separation between document lifecycle and workspace lifecycle

The following capabilities remain future or deferred and must not be
interpreted as implemented:

- functional AI assistant interaction
- workspace recovery
- Recent activity implementation
- primary-document reassignment in a future workspace model that introduces
  primary-document semantics
- PDF and DOCX in-app renderers
- future search, retrieval, and RAG behavior

The current implementation therefore establishes the document-centric
rendering boundary and the persistent multi-document workspace interaction
model without requiring the complete AI-assisted experience described as the
long-term direction.

## Current MVP 1 Workspace Boundary

For the current implementation, the workspace supports both temporary
single-document viewing and persistent multi-document workspace management:

```text
Document Library
      ↓
Temporary Workspace
      └── Document Presentation

or

Workspaces
      ↓
Persistent Workspace
      ├── Document Presentation
      ├── Document Presentation
      └── Document Presentation
```

The renderer boundary remains the extension point for additional document
formats, while document extraction and knowledge processing remain separate
from presentation.

The processing pipeline does not depend on the workspace UI:

```text
Document
   ↓
Extraction
   ↓
Normalization
   ↓
Chunking
   ↓
Persisted Derived Representation
   ↓
Search / Future AI
```

This distinction prevents future search or AI functionality from becoming
dependent on rendered controls such as `DataGridView` or WebView2.

## Workspace Header

The workspace header will provide:

- navigation appropriate to the current workspace type
- workspace or document identity appropriate to the current presentation
- document metadata appropriate for the current view
- AI assistant toggle
- contextual workspace/document menu
- workspace/document close action

For a temporary document presentation, the identity indicates the temporary
workspace context and the menu provides document-level actions.

For a persistent workspace, the identity shows the workspace name and
description and the menu provides workspace management actions.

The current temporary document menu is:

```text
[⋯]
├── Document Information
├── Save as Workspace
├── Remove Document
└── Close Document
```

The current persistent workspace menu is:

```text
[⋯]
├── Add Documents
├── Remove Documents from Workspace
├── Update Workspace Details
├── Delete Workspace
└── Close Workspace
```

Persistent workspaces also expose Tabs and Sidebar presentation choices, with
Tabs as the default.

The contextual menu remains extensible for future workspace capabilities.

## AI Assistant Interaction

The AI assistant will use an adaptive and collapsible side-panel model.

The header will provide an `[AI]` toggle.

When the assistant is collapsed, a persistent edge tab may remain available.

Conceptually:

```text
Large screen

┌──────────────────────────────────────────────────────────┬──────────────┐
│ Document                              [ AI ]              │ AI Assistant │
│                                                          │              │
└──────────────────────────────────────────────────────────┴──────────────┘
```

When collapsed:

```text
┌────────────────────────────────────────────────────────────────────────┐
│ Document                                              [ AI ]           │
│                                                   ┌────────┐           │
│                                                   │   AI   │           │
│                                                   └────────┘           │
└────────────────────────────────────────────────────────────────────────┘
```

The panel will adapt to available screen space.

On larger screens, the assistant may remain visible beside the document.

On smaller screens, the assistant may be collapsed by default or collapsed
when required to preserve document viewing space.

The initial implementation provides only the structural UI boundary where
required. Actual AI processing, retrieval, RAG, and local model interaction
remain future capabilities.

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

The initial implementation prioritizes simple formats that can be rendered directly within the application.

The current implementation includes:

```text
TXT → in-app text renderer
MD  → in-app Markdown renderer
CSV → in-app structured/grid renderer
```

PDF, DOCX, and other formats remain future renderer implementations.

### Renderer Extensibility

The renderer boundary is the architectural extension point for future document formats.

New formats must be introduced by adding a format-specific implementation of `IDocumentContentRenderer` and registering it through dependency injection. `DocumentViewForm`, `DocumentWorkspacePresenter`, and `DocumentContentRendererResolver` must not contain format-specific rendering logic.

The intended progression is:

```text
TXT       → current in-app renderer
Markdown  → current in-app renderer
CSV       → current in-app renderer

PDF       → future renderer
DOCX      → future renderer
XLSX      → future renderer
PPTX      → future renderer
```

PDF and Microsoft Office formats are intentionally deferred. The architecture must nevertheless allow them to be added without changing the workspace or renderer orchestration contract.

### CSV Rendering and Structured Representation

CSV rendering is implemented as a parser-plus-renderer pipeline rather than direct format handling inside the workspace.

The current flow is:

```text
CSV document
      ↓
CsvDocumentParser
      ↓
CsvDocument
├── Columns
├── Rows
├── Warnings
└── HasMoreRows
      ↓
CsvDocumentContentRenderer
      ↓
DataGridView
```

`CsvDocumentParser` is responsible for interpreting CSV structure and producing the structured `CsvDocument` representation.

The representation preserves:

- source column definitions
- row values
- structural warnings for uneven rows
- whether additional rows exist beyond a bounded preview

The renderer consumes this representation and is responsible for presentation.

The renderer must not become the source of truth for CSV semantics.

CSV preview materialization is bounded through `CsvParsingOptions`. The configured `MaxRows` value is supplied through the `CsvParsing` configuration section and bound through the .NET options infrastructure.

This allows large CSV documents to be previewed without requiring the UI to materialize the complete document.

### Markdown Rendering Strategy

Markdown will use `Markdig` as the parsing library and WebView2 as the initial rich presentation surface.

The implementation will begin directly in `MarkdownDocumentContentRenderer`:

```text
Markdown file
      ↓
MarkdownDocumentContentRenderer
      ↓
Markdig
      ↓
controlled HTML
      ↓
WebView2
      ↓
documentContentPanel
```

WebView2 is an implementation detail of the Markdown renderer. It is not exposed through the document workspace presenter, view contract, or renderer resolver.

The initial implementation deliberately follows a simple renderer-owned composition. If the Markdown presentation surface becomes sufficiently complex, the WebView2-hosting portion may later be extracted into a reusable `MarkdownViewerControl` without changing the `IDocumentContentRenderer` contract:

```text
Initial:

DocumentViewForm
      ↓
IDocumentContentRenderer
      ↓
MarkdownDocumentContentRenderer
      └── WebView2

Future extraction:

DocumentViewForm
      ↓
IDocumentContentRenderer
      ↓
MarkdownDocumentContentRenderer
      ↓
MarkdownViewerControl
      └── WebView2
```

Imported Markdown is treated as untrusted document content.

The Markdown renderer will use a controlled rendering policy. It will not
automatically load remote resources, and external navigation will be
handled through an explicit, controlled action rather than unrestricted
automatic navigation.

The renderer should support normal Markdown document features while
avoiding premature implementation of a general-purpose HTML/browser
framework.

### Markdown Rendering Security and Presentation Policy

Imported Markdown is treated as untrusted content. The Markdown renderer
will apply defense-in-depth controls at both the Markdown parsing and
presentation layers.

The initial policy is:

```text
Raw HTML
    ↓
Disabled by Markdig

JavaScript
    ↓
Disabled in WebView2

Remote HTTP/HTTPS resources
    ↓
Blocked by WebView2 resource interception

External HTTP/HTTPS navigation
    ↓
Cancelled unless the renderer policy explicitly permits it
```

The renderer exposes explicit policy options for future scalability:

```text
MarkdownRenderingOptions
├── AllowRawHtml
├── AllowExternalResources
└── AllowExternalNavigation
```

The initial application configuration will keep all three disabled.

`AllowRawHtml` controls whether the Markdig pipeline permits raw HTML.

`AllowExternalResources` controls whether remote HTTP/HTTPS resource requests may be loaded by the WebView2 presentation surface.

`AllowExternalNavigation` controls whether HTTP/HTTPS navigation is permitted by the renderer. Enabling this option must not be interpreted as turning the document viewer into an unrestricted browser. Any future external-navigation experience should remain an explicit, controlled user interaction.

The renderer will also provide a DeskVault-controlled HTML presentation shell rather than relying on browser-default document styling. The shell owns typography, spacing, code blocks, tables, links, and other Markdown presentation styles. Its CSS variables provide a future seam for DeskVault light, dark, system, and accessibility-oriented themes without changing the renderer contract.

### Markdown Renderer Lifecycle and Ownership

The Markdown renderer owns the WebView2 control it creates for document presentation, while the workspace remains responsible for the host control and overall workspace lifecycle.

When a renderer replaces existing content in the content host, existing child controls are explicitly disposed before the new renderer control is added.

The renderer also disposes a newly created WebView2 control if initialization or rendering fails after the control has been attached to the host.

Document streams remain owned by the caller. Renderers may read the supplied stream but must not close the underlying document stream as part of normal rendering.

This preserves a consistent ownership boundary across multiple renderer implementations.

### Renderer Registration and Composition

Format-specific renderers are registered through dependency injection.

The intended composition is:

```text
IDocumentContentRenderer
├── TextDocumentContentRenderer
├── MarkdownDocumentContentRenderer
└── CsvDocumentContentRenderer
```

The resolver discovers the registered implementations and selects the first renderer whose `CanRender` method accepts the document filename.

Renderer-specific dependencies remain inside the renderer boundary. The workspace presenter and view contracts must not acquire dependencies on Markdig, WebView2, CsvHelper, or other format-specific presentation technologies.

This allows future formats to be introduced without modifying the workspace orchestration:

```text
IDocumentContentRenderer
├── TextDocumentContentRenderer
├── MarkdownDocumentContentRenderer
├── CsvDocumentContentRenderer
├── PdfDocumentContentRenderer       ← future
├── DocxDocumentContentRenderer      ← future
└── ...
```

## Unsupported Formats

When no in-app renderer is available, DeskVault will not automatically
launch the operating system's associated application.

Instead, the workspace will display an in-app message explaining that the
format is not currently supported for preview.

The user will explicitly choose whether to open the document externally.

Conceptually:

```text
┌────────────────────────────────────────────────────────────────────┐
│                                                                    │
│  Preview unavailable for this file type.                           │
│                                                                    │
│  DeskVault does not currently support                              │
│  in-app preview for this format.                                   │
│                                                                    │
│                    [ Open Externally ]                             │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

The explicit external-open action is a user-controlled fallback and does
not change the workspace's responsibility for document lifecycle or
presentation.

## Loading State

Opening a document may involve document retrieval, decryption, renderer initialization, and future processing operations.

The workspace will therefore provide an explicit loading state.

The preferred initial behavior is:

```text
┌────────────────────────────────────────────────────────────────────┐
│ report.pdf                                      [ AI ] [⋯]          │
│ Loading document...                                                │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│                  Preparing document preview...                     │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
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

For MVP 1, the workspace is primarily a viewing experience and does not
introduce persistent editable workspace state. Therefore, close should not
require confirmation solely because a document is open.

The conditional confirmation rule is retained for future workspace state
such as related-document changes, AI notes, or other editable configuration.

## Document and Workspace Lifecycle

Document lifecycle, workspace membership lifecycle, and document presentation
lifecycle are separate concepts.

For a temporary document session:

```text
Documents
    ↓
Open Document
    ↓
Temporary Workspace Presentation
    ├── Close Document → presentation closes, document remains
    └── Remove Document → document removal pipeline runs and presentation closes
```

For a persistent workspace:

```text
Persistent Workspace
├── Document A
├── Document B
└── Document C

Close Document Presentation
        ↓
Presentation closes
        ↓
Workspace membership remains

Remove Document from Workspace
        ↓
Membership removed
        ↓
Underlying document remains in Documents
```

A closed workspace member can be reopened from the workspace without being
added again.

Removing a document from a persistent workspace is therefore distinct from
deleting the underlying document.

## Primary Document Removal

The current workspace implementation does not assign a special primary
document role to persistent workspace membership.

If a future multi-document workspace introduces primary-document semantics,
the existing architectural rule remains applicable: removing a primary
document must not silently change the workspace's meaning.

If related documents remain, a future implementation may ask the user to
choose a replacement primary document. If no related documents remain, the
workspace may close because it no longer has a document context.

This behavior is a future multi-document workspace rule and is not required
by the current implementation.

## Workspace Persistence

DeskVault distinguishes between a temporary workspace session and a
persistent workspace.

Opening a document directly creates a temporary workspace presentation.

The temporary workspace may remain temporary if the user is simply viewing
the document.

The user may explicitly choose `Save as Workspace` to convert the existing
temporary workspace into a persistent workspace:

```text
Open Document
      ↓
Temporary Workspace
      │
      ├── Read / inspect
      │       ↓
      │    Close
      │
      └── Save as Workspace
                  ↓
        Persistent Workspace
```

The conversion uses the existing workspace identity and keeps the current
presentation rather than creating a second workspace.

Persistent workspaces then support explicit document membership management.
Closing an individual document presentation does not automatically remove
that document from the workspace.

Persistent workspace changes use explicit user actions rather than silently
turning every temporary document session into a persistent workspace.

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

When a temporary workspace is saved as a persistent workspace, DeskVault
requires a workspace name and allows an optional description.

The save flow is explicitly presented as `Save as Workspace`.

The user controls the resulting workspace identity, which is subsequently
shown in the persistent workspace presentation.

For example:

```text
Save as Workspace

Name:
[ Project Alpha                 ]

Description:
[ Customer onboarding review   ]

[ Cancel ]          [ Save ]
```

The same workspace presentation remains open after a successful conversion.

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

The workspace architecture supports multiple simultaneous workspace
presentation contexts.

The current model is:

```text
MainForm
├── Workspace A presentation
├── Workspace B presentation
└── Workspace C presentation
```

Each workspace presentation maintains its own workspace identity and document
presentations.

If a workspace already has an active presentation, DeskVault activates the
existing presentation rather than creating a duplicate.

Direct document opening follows the same reuse principle for an already
active temporary presentation of that document.

The presentation mechanism remains independent of the underlying workspace
model and is not coupled to MDI.

## Scope of Initial Implementation

The current implementation phase establishes the concrete workspace
presentation and rendering boundaries.

Implemented capabilities include:

- temporary single-document workspace sessions
- persistent named workspaces
- multiple documents within persistent workspaces
- Tabs default and Sidebar alternative navigation
- multiple simultaneous workspace presentation contexts
- activation/reuse of existing presentations
- separation of document presentation from workspace membership
- explicit Save as Workspace conversion
- document information and removal behavior
- TXT, Markdown, and CSV rendering
- explicit unsupported-format handling and external opening

The following remain architectural targets or deferred capabilities:

- functional local AI processing
- retrieval-augmented generation
- Recent activity implementation
- automatic workspace recovery
- PDF renderer
- DOCX renderer
- primary-document semantics in a future model that requires them

The implementation establishes the correct boundaries without prematurely
implementing all future functionality.

## Alternatives Considered

### Use a legacy WinForms `WebBrowser` control for Markdown

Rejected.

Although it would provide a quick HTML presentation path, it would tie the modern Markdown workspace to a legacy browser technology. WebView2 provides a more appropriate current Windows presentation surface while remaining an implementation detail of the renderer.

### Make `DocumentViewForm` directly depend on WebView2

Rejected.

The workspace form must remain independent of the Markdown presentation technology. Direct WebView2 coupling would make the workspace harder to extend and would blur the renderer boundary.

### Create a reusable `MarkdownViewerControl` immediately

Deferred.

The first implementation will keep WebView2 inside `MarkdownDocumentContentRenderer`. A reusable viewer control can be extracted when the Markdown presentation surface develops enough behavior to justify the additional abstraction.

### Render Markdown entirely with native WinForms controls

Rejected for the initial Markdown implementation.

A native renderer would require DeskVault to implement and maintain substantial formatting and layout behavior for headings, lists, code blocks, tables, links, and related Markdown features. Using Markdig plus WebView2 keeps that UI infrastructure focused while preserving the renderer boundary.

### Trust imported Markdown as executable/active content

Rejected.

Imported documents are treated as untrusted content. Rendering must use a controlled policy and must not automatically load remote resources.

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
- Temporary document sessions remain focused on the opened document while persistent workspaces support multiple document members.
- The AI assistant has a defined adaptive interaction pattern.
- Document rendering is separated behind a resolver and renderer boundary.
- CSV is an implemented structured/grid renderer rather than a future renderer.
- CSV parsing preserves structural information before presentation.
- Bounded CSV previewing is represented explicitly through `HasMoreRows`.
- CSV parsing configuration is externally configurable through `CsvParsingOptions`.
- Unsupported formats have an explicit and user-controlled fallback.
- Loading states provide clear feedback during document preparation.
- Document lifecycle is separated from workspace lifecycle.
- Persistent workspace membership can survive closure of an individual document presentation.
- Workspace membership removal is distinct from deletion of the underlying document.
- Temporary document sessions do not automatically create persistent workspaces.
- Persistent workspace naming remains user-controlled.
- Recent activity has a clear future model without expanding the current implementation.
- Multiple simultaneous workspace presentation contexts are supported without coupling workspace identity to MDI.
- New document renderers can be added without changing workspace orchestration.
- Markdown rendering can evolve independently of the workspace contract.
- Markdig provides a mature Markdown parsing boundary while WebView2 provides a rich presentation surface.
- Markdown security/resource policy remains inside the renderer boundary.
- Rendering and future extraction/AI processing remain separate concerns.
- The current implementation remains intentionally scoped.

### Negative

- Additional workspace state and lifecycle concepts increase application complexity.
- Renderer resolution introduces additional Application/UI abstractions.
- Future workspace persistence and recovery will require additional storage design.
- Multi-document workspace management will require additional UI and domain modeling.
- Recent activity will require a separate persistence and retention implementation.
- AI functionality will require additional Application and AI-layer capabilities.
- Some document formats will initially lack in-app rendering.
- WebView2 adds a UI/runtime dependency for rich Markdown presentation.
- Markdown rendering requires an explicit security policy for HTML, links, and remote resources.
- Structured document representations add parsing and testing complexity before rendering.
- Bounded previews require explicit signaling so the UI does not mistake a partial representation for a complete document.

These trade-offs are acceptable because they establish a scalable foundation while keeping the first implementation focused.

## Result

DeskVault will implement an enterprise-oriented document workspace that
supports temporary single-document viewing and persistent multi-document
knowledge contexts.

The resulting direction is:

```text
Application Shell
    ↓
Document Library
    ↓
Temporary Document Workspace
    ├── Enterprise Header
    └── Document Renderer Boundary
             ├── Text
             ├── Markdown
             └── CSV
             ↓
        Save as Workspace
             ↓
      Persistent Workspace
        ├── Document
        ├── Document
        └── Document
             ↓
      Future AI Context
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

This separation allows DeskVault to evolve toward multi-document workspaces,
local AI, retrieval, RAG, and enterprise knowledge workflows without
requiring the document workspace implementation to contain those future
capabilities.
