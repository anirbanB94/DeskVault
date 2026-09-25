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

DeskVault will introduce a dedicated **in-app document workspace** represented by `DocumentViewForm`.

`MainForm` will remain responsible for the document library and document-management workflow.

Opening a document from `MainForm` will open the document inside DeskVault rather than launching the document through the operating system's default external application.

The intended UI structure is:

```text
LoginForm
    ↓
MainForm
    │
    ├── Document library
    ├── Import
    └── Document selection
            ↓
      DocumentViewForm
            ├── Document content
            ├── Document information
            └── AI assistant workspace
```

`MainForm` will serve as the primary application shell rather than being limited to the document list.

The application shell will use a left navigation rail for major application areas such as:

```text
Documents
Recent
Workspaces
Search
Settings
```

The document workspace presentation can represent either a temporary single-document session or a persistent workspace containing multiple documents.

A temporary document session is created when a document is opened directly from the document library. A persistent workspace can contain multiple document members and presents those documents through the same workspace presentation.

The document workspace will use a hybrid layout in which the document remains the primary content area and the AI assistant is presented as a collapsible side panel.

The AI assistant panel will adapt to the available screen space. On larger screens it may remain visible alongside the document, while on smaller screens it may collapse and be opened when required.

The document workspace will be designed so that the initial one-document model can scale to multiple related documents for future AI processing.

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

Related documents may be added ad hoc to the current workspace and, in a future implementation, the group may be saved as a named workspace or collection.

The document workspace itself will remain distinct from the AI context so that future retrieval and AI processing can operate over a group of related documents without changing the fundamental document model.

Each primary document may have its own application-managed `DocumentViewForm`.

If a user attempts to open a document that already has an active workspace, DeskVault should activate the existing workspace rather than create a duplicate workspace for the same document.

The application will not adopt MDI as a mandatory architectural requirement at this stage. The workspace model should remain independent of the eventual window-management strategy.

Document rendering will be designed around a scalable rendering boundary rather than placing format-specific rendering logic directly inside `DocumentViewForm`.

The initial implementation should prioritize simple in-app rendering for formats such as:

```text
TXT
MD
CSV
```

Additional renderers, such as PDF and DOCX rendering, can be introduced independently as the application evolves.

Formats that do not yet have an appropriate in-app renderer use the explicit user-controlled external-open action.

The AI assistant area will initially provide the structural foundation for future functionality. It will not imply that AI analysis, retrieval, or RAG functionality is already implemented.

Future AI capabilities may operate over the primary document and its related documents as a shared processing and retrieval context.

## MVP 1 Scope Boundary

The workspace architecture described by this ADR includes both the current
MVP implementation and the longer-term product direction.

For the current implementation, the workspace provides both temporary
single-document sessions and persistent multi-document workspace contexts.

The current workspace presentation provides:

```text
Workspace Presentation
├── Workspace / document identity
├── Document presentation
├── Multiple document presentation where applicable
├── Document information
├── Workspace/document actions
└── Adaptive AI presentation boundary
```

Current workspace interaction includes:

- temporary document presentation when opening directly from Documents
- persistent workspaces containing multiple documents
- Tabs as the default persistent-workspace navigation mode
- Sidebar as an alternative persistent-workspace navigation mode
- activation of existing workspace presentations instead of duplicate presentation
- closing a document presentation without removing workspace membership
- reopening a workspace member after its presentation has been closed
- explicit Save as Workspace conversion from a temporary session
- workspace-level add/remove/details/delete actions for persistent workspaces

Functional AI processing, Recent activity implementation, workspace recovery,
PDF/DOCX in-app rendering, and other future knowledge-work capabilities
remain outside the current implementation unless otherwise documented by
their own implementation decisions.

The workspace presentation is therefore intentionally functional for document
and workspace lifecycle while preserving the architectural boundaries required
for future AI-assisted and multi-document knowledge workflows.

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
- TXT, Markdown, and CSV in-app rendering
- an explicit user-controlled fallback for unsupported formats
- renderer lifecycle and resource ownership boundaries

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

`MainForm` remains responsible for the application shell and document-library workflow.

```text
MainForm
├── Application navigation
├── Document listing
├── Document selection
├── Import
├── Remove
└── Open
```

`DocumentViewForm` is responsible for the document-specific workspace.

```text
DocumentViewForm
├── Document content
├── Document information
└── Future AI workspace
```

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

The UI receives the document content through this existing Application boundary.

The UI must not access:

- SQLite directly
- `DocumentEntity`
- encrypted `.dvault` files directly
- encryption keys
- encryption services directly

This preserves the existing separation between UI, Application, Domain, and Infrastructure.

## Document Opening Workflow

The document opening workflow will become:

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
DocumentViewForm
    ↓
In-app document workspace
```

The external viewer remains available as an explicit user-controlled action when an in-app renderer is unavailable.

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
TXT → in-app text viewer
MD  → in-app text viewer
CSV → in-app structured/grid viewer
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

The document workspace will reserve a dedicated area for future local AI functionality.

The AI assistant will be adaptive and collapsible rather than permanently consuming a fixed portion of the workspace.

The intended future structure is:

```text
DocumentViewForm
├── Document Content
│
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

The UI must therefore provide the structural foundation without presenting future AI functionality as already implemented.

## Security Considerations

Documents remain encrypted at rest.

The document workspace receives document content through the existing Application workflow after the document has been retrieved and decrypted.

The UI must not introduce its own document decryption or key-management implementation.

The workspace should avoid unnecessarily persisting plaintext document content outside the controlled document-viewing workflow.

Future multi-document AI processing must also preserve the existing security boundaries around document storage and encryption.

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
- Document-specific functionality has a dedicated workspace.
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

### Negative

- In-app document viewing introduces additional UI complexity.
- Document rendering may require format-specific components and dependencies.
- Multiple document workspaces require window lifecycle and management decisions.
- Workspace grouping introduces additional concepts beyond individual documents.
- AI workspace functionality will require additional Application and AI-layer capabilities.
- Multi-document AI processing will require retrieval and context-management infrastructure.
- Some document formats may initially require fallback handling through external applications.

These trade-offs are acceptable because the decisions align with DeskVault's long-term product direction.

## Result

DeskVault will evolve from a document-management application with external
document opening into a document-centric knowledge workspace.

The resulting UI direction is:

```text
Login
    ↓
Application Shell
    ↓
Document Library
    ↓
Temporary Document Workspace
    └── Document Presentation
             ↓
        Save as Workspace
             ↓
      Persistent Workspace
        ├── Document
        ├── Document
        └── Document
             ↓
      Future Adaptive Local AI Assistant
```

The existing Application and Infrastructure boundaries remain responsible
for document retrieval, decryption, persistence, and storage.

The current workspace architecture establishes explicit separation between
workspace identity, workspace membership, document presentation, and future
AI context. It also supports multiple simultaneous workspace presentation
contexts without coupling the model to MDI or another specific windowing
strategy.

The new document workspace establishes the UI foundation for future document
processing, search, semantic retrieval, multi-document context, RAG, and
source-grounded local AI capabilities.

The architecture remains flexible enough to support future window-management
strategies, additional document renderers, workspace recovery, Recent
activity, and enterprise identity integration without requiring a
fundamental redesign of the document workspace.
