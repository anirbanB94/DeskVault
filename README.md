# DeskVault

> Secure, Offline-First Enterprise Knowledge Platform powered by Local AI.

DeskVault is a Windows desktop application built with **.NET 10 and WinForms**, designed as an enterprise-grade portfolio project for secure, local-first document and knowledge management.

The core product principle is simple:

> **Your documents stay on your machine, under your control.**

DeskVault is being designed for environments where privacy, offline capability, security, and controlled data processing matter.

## Current Status

DeskVault is currently in **MVP 1 development / release-hardening**.

MVP 1 focuses on establishing a secure, persistent, local document foundation with an in-app document workspace, document rendering, document processing, and local keyword/full-text search.

### Implemented in MVP 1

- .NET 10 / WinForms desktop application
- Layered separation across Domain, Application, Infrastructure, and UI
- Application-level vertical-slice architecture
- Document import workflow
- SHA-256 based duplicate detection
- Encrypted document storage using AES-GCM
- Windows-protected encryption key management
- Encrypted `.dvault` document artifacts
- Persistent document metadata using SQLite
- EF Core persistence infrastructure
- EF Core migrations and schema evolution
- Application restart persistence
- Document listing and selection
- Persistent document retrieval
- Document removal workflow
- Decryption and document opening
- Local application-data storage under `%LOCALAPPDATA%\DeskVault`
- In-app document workspace
- Dedicated `DocumentViewForm` workspace
- Workspace-oriented UI interaction model
- Presenter-driven document workspace integration
- In-app TXT document rendering
- In-app Markdown document rendering
- In-app CSV document rendering
- Structured CSV parsing with bounded preview support
- Extensible document renderer abstraction and resolver
- Secure Markdown rendering with controlled HTML, script, resource, and navigation policies
- Renderer-owned presentation resources and lifecycle
- Separation of document lifecycle and workspace lifecycle
- Document processing pipeline with extraction, normalization, chunking, and persisted processing lifecycle
- Processing failure handling and retry/reprocessing support
- Cancellation-aware processing contracts
- Idempotent replacement of derived processing results
- Persisted document chunks
- Local keyword/full-text search across processed document chunks
- Architecture Decision Records for significant architectural choices
- Centralized NuGet package version management
- Repository-level .NET SDK and build configuration
- Automated test coverage across Application, Infrastructure, Integration, and UI projects

### Future Roadmap

The following capabilities are intentionally **not MVP 1 functionality**:

- Additional document renderers such as PDF and Office formats
- Improved desktop UI/UX and visual polish
- Embeddings and vector indexing
- Hybrid search
- Retrieval-Augmented Generation (RAG)
- Local AI integration through Ollama
- Source-grounded AI responses
- Functional AI assistant interaction
- Background document-processing workers
- Durable retry scheduling and richer processing observability
- Persistent named workspaces
- Related-document workspace management
- Recent activity
- Multiple simultaneous workspaces
- Automatic workspace recovery
- Additional security hardening and security-focused enhancements

## Architecture

DeskVault follows a layered architecture designed to keep infrastructure concerns isolated from application and domain logic.

```text
┌──────────────────────────────────────────────────────────────┐
│                         DeskVault UI                         │
│                    WinForms + Presenter                      │
│                                                              │
│  MainForm                                                   │
│      │                                                       │
│      └── DocumentViewForm / Workspace                        │
└──────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────┐
│                        Application                           │
│              Commands / Queries / Interfaces                 │
│       Document Import / Processing / Search / Services       │
└──────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────┐
│                           Domain                             │
│              Documents / Business Rules / State              │
└──────────────────────────────────────────────────────────────┘
                              ▲
                              │
┌──────────────────────────────────────────────────────────────┐
│                      Infrastructure                          │
│                                                              │
│  SQLite / EF Core                                            │
│  Encrypted File Storage                                      │
│  Encryption Key Management                                   │
│  Document Readers                                            │
│  External Infrastructure Services                            │
└──────────────────────────────────────────────────────────────┘
```

The Application layer depends on abstractions such as `IDocumentRepository`. Infrastructure provides the concrete implementations.

The UI follows a presenter-oriented interaction model. The main document library remains responsible for document selection and document-level actions, while `DocumentViewForm` provides a dedicated document workspace boundary.

This keeps persistence, encryption, filesystem access, and other platform-specific concerns outside the Domain and Application layers.

## Document Storage Model

DeskVault separates **document content** from **document metadata**.

```text
%LOCALAPPDATA%\DeskVault
├── DeskVault.db
├── Documents
│   └── <document-id>.dvault
└── Security
    └── <protected security material>
```

### SQLite

SQLite stores document metadata and processing-related persistence such as:

- Document ID
- File name
- Display name
- SHA-256 hash
- Import timestamp
- Document status
- Encrypted storage path
- Processing execution state
- Processing attempt information
- Derived document chunks

EF Core provides the persistence implementation inside Infrastructure, with migrations used for schema evolution.

### Encrypted Files

The actual document content is stored separately as encrypted `.dvault` files.

The application decrypts the stored content only when the document needs to be opened or processed.

This separation allows the metadata store and document-content storage to evolve independently.

## Document Workspace

DeskVault provides an initial **in-app document workspace**.

The MVP 1 workspace is intentionally a single-document viewing and document-management experience.

The current workspace includes:

- Document identity
- Dedicated document workspace window
- Workspace header
- Document content area
- Document information
- Workspace/document actions
- Close workspace action
- Presenter-driven workspace opening

The AI assistant interaction model is part of the architectural direction, but functional AI processing and assistant interaction are future capabilities.

The current flow is:

```text
Main Document Library
        │
        │ Open Document
        ▼
OpenDocumentHandler
        │
        ▼
DocumentViewForm / Workspace
        ├── Document Identity
        ├── Document Content
        ├── Document Information
        └── Workspace Actions
```

The workspace architecture provides a path toward related documents, persistent workspaces, multiple workspace windows, additional document renderers, and local AI assistance without implementing those capabilities prematurely.

### Document Rendering

Document rendering is isolated behind `IDocumentContentRenderer` and `IDocumentContentRendererResolver`.

```text
DocumentViewForm
      │
      ▼
IDocumentContentRendererResolver
      │
      ▼
IDocumentContentRenderer
      ├── TXT
      ├── Markdown
      └── CSV
```

The renderer boundary allows additional formats to be introduced without changing workspace orchestration.

CSV uses a parser-plus-renderer pipeline:

```text
CSV
 │
 ▼
CsvDocumentParser
 │
 ▼
CsvDocument
 ├── Columns
 ├── Rows
 ├── Warnings
 └── HasMoreRows
 │
 ▼
CsvDocumentContentRenderer
 │
 ▼
DataGridView
```

This preserves CSV semantics before presentation and makes bounded preview state explicit.

Markdown uses Markdig for parsing and WebView2 as its rich presentation surface. Imported Markdown is treated as untrusted content. Raw HTML, JavaScript, remote resources, and external navigation are controlled by renderer policy and disabled by default.

Renderer-specific technologies and security policies remain inside the rendering boundary.

## Document Processing and Search

DeskVault includes a document knowledge-processing pipeline for supported document types.

The current processing flow is:

```text
Stored Document
      │
      ▼
Document Reader
      │
      ▼
Extraction
      │
      ▼
Normalization
      │
      ▼
Chunking
      │
      ▼
Persisted Derived Representation
      │
      ▼
Keyword / Full-Text Search
```

The MVP 1 processing pipeline provides:

- TXT extraction
- Markdown extraction
- CSV extraction
- Extraction failure-boundary handling
- Text normalization
- Deterministic chunking
- Persisted processing execution state
- Processing failure and retry/reprocessing support
- Cancellation propagation
- Idempotent derived-result replacement
- Persisted document chunks
- Local keyword/full-text search across processed chunks

Processing remains independent of document rendering.

A document does **not** need to be rendered in order to be processed for search or future AI use.

The processing lifecycle is separate from `DocumentStatus`:

```text
Pending
   │
   ▼
Processing
   ├──────────► Completed
   │
   └──────────► Failed
                    │
                    │ retry
                    ▼
                Processing
```

A successful derived result is published coherently. Failed or partial processing attempts must not masquerade as the current successful result.

### Semantic Preservation

DeskVault treats document parsing/extraction and document rendering as separate architectural responsibilities.

```text
Source Document
      │
      ▼
Parser / Extractor
      │
      ▼
Structured Document Representation
      ├── Rendering
      ├── Search
      ├── Indexing
      └── AI / Retrieval
```

The renderer is a consumer of document semantics rather than their source of truth.

This allows future search, indexing, and AI capabilities to consume application-level document-processing results rather than scraping UI controls, HTML, WebView2 output, or rendered text.

## Security Direction

Security is a core architectural concern rather than a later add-on.

The current implementation includes:

- AES-GCM document encryption
- SHA-256 content hashing
- Windows-protected encryption key management
- Local-only document storage
- Controlled Markdown rendering
- No requirement for cloud document storage
- Explicit user-controlled external opening for unsupported formats

Future security work will include additional hardening, validation, key-management improvements, and security-focused testing.

## Local AI Direction

DeskVault is designed to eventually provide local AI-powered knowledge retrieval.

The intended future pipeline is:

```text
Documents
    ↓
Encrypted Local Storage
    ↓
Text / Structured Extraction
    ↓
Normalization
    ↓
Chunking
    ↓
Embeddings
    ↓
Local Search / Retrieval
    ↓
RAG
    ↓
Local AI Model
    ↓
Source-Grounded Answer
```

Ollama is planned as the local model runtime.

AI functionality is **not implemented MVP 1 functionality**. Embeddings, vector indexing, RAG, and functional AI assistant interaction remain roadmap capabilities.

## Design Principles

DeskVault is being developed around the following principles:

1. **Local-first** — user documents remain on the local machine.
2. **Security by design** — encryption and key management are architectural concerns.
3. **Layered architecture** — Domain and Application remain independent of infrastructure technologies.
4. **Replaceable infrastructure** — repositories and infrastructure services are accessed through abstractions.
5. **Explicit boundaries** — UI, Application, Domain, and Infrastructure have distinct responsibilities.
6. **Semantic preservation** — parsing establishes document meaning; rendering presents it.
7. **Scalable foundations** — today's MVP should provide a reasonable path toward search, indexing, and AI workloads without premature complexity.
8. **Evidence over claims** — documentation should distinguish implemented functionality from planned capabilities.

## Technology Stack

| Area                  | Technology |
| --------------------- | ---------- |
| Runtime               | .NET 10 |
| UI                    | Windows Forms |
| Language               | C# |
| Persistence            | SQLite |
| ORM                    | Entity Framework Core |
| Document Encryption    | AES-GCM |
| Key Protection         | Windows Data Protection |
| Hashing                | SHA-256 |
| Markdown Parsing       | Markdig |
| Markdown Presentation  | WebView2 |
| Local AI Runtime       | Ollama (planned) |
| Planned Model          | Phi-4 Mini (planned) |
| Architecture           | Layered / Clean Architecture principles |

## Project Structure

```text
src/
├── DeskVault.UI/
│   ├── Assets/
│   ├── Controls/
│   ├── Forms/
│   ├── Hosting/
│   ├── Presenters/
│   ├── Rendering/
│   ├── Resources/
│   ├── Services/
│   ├── Themes/
│   └── Views/
│
├── DeskVault.Application/
│   ├── Behaviors/
│   ├── Configurations/
│   ├── Documents/
│   │   ├── Chunking/
│   │   ├── Commands/
│   │   ├── DTOs/
│   │   ├── Extraction/
│   │   ├── Mappings/
│   │   ├── Normalization/
│   │   ├── Parsing/
│   │   ├── Processing/
│   │   ├── Queries/
│   │   └── Validators/
│   ├── Interfaces/
│   ├── Resources/
│   └── Services/
│
├── DeskVault.Domain/
│   ├── Documents/
│   ├── Events/
│   ├── Exceptions/
│   ├── Interfaces/
│   └── ValueObjects/
│
├── DeskVault.Infrastructure/
│   ├── Configurations/
│   ├── Extensions/
│   ├── Logging/
│   ├── Persistence/
│   │   ├── Baseline/
│   │   ├── Configurations/
│   │   ├── Context/
│   │   ├── Entities/
│   │   └── Migrations/
│   ├── Repositories/
│   ├── Security/
│   └── Services/
│
├── DeskVault.AI/
│   ├── Chats/
│   ├── Clients/
│   ├── Embeddings/
│   ├── Models/
│   ├── Prompts/
│   └── Services/
│
└── DeskVault.Shared/
    ├── Constants/
    ├── Extensions/
    ├── Helpers/
    ├── Models/
    └── Resources/

tests/
├── DeskVault.Application.Tests/
├── DeskVault.Infrastructure.Tests/
├── DeskVault.Integration.Tests/
└── DeskVault.UI.Tests/

docs/
└── adr/
```

`DeskVault.AI` represents the planned AI boundary and is not evidence that local AI functionality is implemented in MVP 1.

## Development Approach

DeskVault is being developed incrementally as an enterprise-grade portfolio project.

Each development stage aims to produce a working vertical slice rather than isolated technical demonstrations.

The current progression is:

```text
Foundation
    ↓
Domain
    ↓
Document Import
    ↓
Encryption
    ↓
Document Queries
    ↓
Persistent Metadata
    ↓
Document Removal
    ↓
In-App Document Workspace
    ↓
Document Rendering
    ├── TXT
    ├── Markdown
    └── CSV
    ↓
Document Processing
    ├── Extraction
    ├── Normalization
    ├── Chunking
    └── Persistence
    ↓
Full-Text Search
    ↓
[Future]
Embeddings
    ↓
Vector / Hybrid Retrieval
    ↓
RAG
    ↓
Local AI Knowledge Assistant
```

Architectural decisions are documented through ADRs under `docs/adr/`.

## Project Status

DeskVault is an actively developed portfolio project.

The current MVP 1 focuses on establishing a **secure, persistent, local document foundation with an in-app document workspace, document rendering, processing, and search support**.

The current implementation provides TXT, Markdown, and CSV rendering; document extraction, normalization, deterministic chunking, persisted processing state, retry/reprocessing support, and local keyword/full-text search across processed document chunks.

Local AI, embeddings, vector indexing, hybrid search, RAG, additional document renderers, persistent workspaces, and other advanced capabilities remain on the roadmap.

Automated test coverage is established across the Application, Infrastructure, Integration, and UI test projects. The exact passing-test count should be verified from the current test run rather than treated as a permanent README claim.

## Architecture Decision Records

Significant architectural decisions are documented in `docs/adr/`.

Current ADRs include:

- `0001-project-vision.md`
- `0002-vertical-slice-architecture.md`
- `0003-document-import-workflow.md`
- `0004-document-encryption-at-rest.md`
- `0005-document-metadata-persistence.md`
- `0006-in-app-document-workspace.md`
- `0007-document-workspace-ui-and-interaction-model.md`
- `0008-document-semantic-preservation-through-rendering-pipeline.md`
