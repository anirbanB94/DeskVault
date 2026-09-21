# DeskVault

> A secure, offline-first document and enterprise knowledge management platform — powered by Local AI.

DeskVault is a Windows desktop application built with **.NET 10 and WinForms**, developed as an enterprise-oriented portfolio project around secure local document storage, processing, rendering, and search.

The core product principle is:

> **Your documents stay on your machine, under your control.**

DeskVault is being designed for environments where privacy, offline capability, controlled data processing, and secure document handling matter.

The product is being developed incrementally: the current implementation establishes the secure local document and knowledge foundation, while Local AI capabilities are introduced progressively on top of that foundation.

## Product Goal

DeskVault aims to evolve into a secure, offline-first knowledge platform that lets users discover, retrieve, organize, and eventually understand information from locally stored documents without requiring cloud document storage.

The long-term direction combines:

Secure local document storage
Structured document processing
Search and retrieval
Local embeddings and indexing
Retrieval-Augmented Generation (RAG)
Source-grounded Local AI assistance

---

## Current Status

**MVP 1 — Released**
**MVP 2 — In Development**

MVP 1 established the secure local document foundation, including:

* Document import and validation
* Duplicate detection using SHA-256 content hashing
* Encrypted document storage using AES-GCM
* Windows-protected encryption key management
* Encrypted `.dvault` document artifacts
* Persistent metadata using SQLite and Entity Framework Core
* Database schema evolution through EF Core migrations
* Encrypted SQLite database storage
* Application restart persistence
* Document listing, retrieval, opening, and removal
* In-app document workspace
* TXT, Markdown, and CSV rendering
* Document extraction, normalization, deterministic chunking, and persistence
* Reliable processing lifecycle with retry/reprocessing support
* Local keyword/full-text search across processed document chunks
* Search ranking, result boundaries, and continuation-based paging
* Automated tests across Application, Infrastructure, Integration, and UI projects
* Architecture Decision Records for significant architectural choices
* Centralized NuGet package version management
* Repository-level .NET SDK and build configuration

MVP 2 is extending these foundations toward richer workspace capabilities, stronger processing/search boundaries, and the architectural groundwork for future knowledge-assistance features.

The repository currently contains workspace domain/application and persistence infrastructure for **temporary and persistent workspaces**, including document membership, naming, activation, persistence, and recovery-related behavior. The broader workspace UI experience remains part of ongoing development.

---

## Implemented Document Capabilities

### Import and Storage

DeskVault validates imported files against the supported extension set before storing them locally.

The import pipeline provides:

```text
Source File
    │
    ▼
Validation
    │
    ├── File existence
    ├── Supported extension
    └── Non-empty content
    │
    ▼
Content Hashing
    │
    ▼
Duplicate Detection
    │
    ▼
Encryption
    │
    ▼
Encrypted .dvault Artifact
    +
    ▼
Persistent Metadata
```

The actual document content and document metadata are stored separately.

### Document Formats

DeskVault currently recognizes a broad set of document, office, image, source-code, data, and email extensions at the import boundary.

Current text-processing support includes:

* TXT
* Markdown
* CSV
* INI
* JSON
* XML
* YAML / YML
* Log files
* Common source-code formats such as C#, C++, C, Java, Python, JavaScript, TypeScript, CSS, SQL, and PowerShell

The current in-app rendering pipeline supports:

* TXT and text-based formats
* Markdown
* CSV

Text-based source and configuration formats use the common text-rendering path rather than requiring a separate renderer for every individual programming or configuration language.

Formats that do not currently have a dedicated in-app renderer can remain securely stored and may be opened externally through the workspace.

---

## Architecture

DeskVault uses a layered architecture with application-level vertical slices.

```text
┌──────────────────────────────────────────────────────────────┐
│                         DeskVault UI                         │
│                       WinForms + MVP                         │
│                                                              │
│  Forms / Views / Presenters / Rendering / Composition       │
└──────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────┐
│                        Application                           │
│                                                              │
│  Commands / Queries / Handlers / Interfaces / Services      │
│  Documents / Processing / Search / Workspaces               │
└──────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────┐
│                           Domain                             │
│                                                              │
│  Documents / Workspaces / Business Rules / State            │
└──────────────────────────────────────────────────────────────┘
                              ▲
                              │
┌──────────────────────────────────────────────────────────────┐
│                      Infrastructure                          │
│                                                              │
│  SQLite / EF Core / Persistence                              │
│  Encrypted File Storage                                      │
│  Database Encryption                                         │
│  Key Management                                               │
│  Document Readers / Storage Services                         │
└──────────────────────────────────────────────────────────────┘

                    ┌──────────────────┐
                    │ DeskVault.Shared │
                    │ Cross-cutting    │
                    │ resources        │
                    └──────────────────┘

                    ┌──────────────────┐
                    │   DeskVault.AI   │
                    │ Future AI        │
                    │ integration      │
                    └──────────────────┘
```

The dependency direction keeps business and application behavior separated from infrastructure implementation details.

The Application layer defines contracts such as repositories, storage services, document readers, processing services, and search boundaries. Infrastructure provides the concrete implementations.

The UI acts as the presentation/composition boundary and currently uses a presenter-oriented interaction model.

`DeskVault.Shared` is intentionally lightweight and contains genuinely cross-cutting resources rather than becoming a general-purpose service or domain dumping ground.

`DeskVault.AI` establishes a future AI boundary. The project currently provides architectural space for AI-related capabilities but does not represent a completed local AI implementation.

### Application Architecture

The Application layer is organized around vertical slices for major capabilities.

```text
Application
├── Documents
│   ├── Commands
│   │   ├── ImportDocument
│   │   ├── ProcessDocument
│   │   └── RemoveDocument
│   │
│   ├── Queries
│   │   ├── GetDocument
│   │   ├── ListDocuments
│   │   ├── OpenDocument
│   │   ├── SearchDocuments
│   │   └── ReconcileDocumentArtifacts
│   │
│   ├── Extraction
│   ├── Normalization
│   ├── Parsing
│   ├── Chunking
│   └── Processing
│
└── Workspaces
    ├── Commands
    └── Queries
```

This keeps application workflows close to the concepts and contracts that they operate on while allowing infrastructure and presentation concerns to remain outside the business workflow.

---

## Document Workspace

DeskVault provides an in-app document workspace that separates document-level interaction from the main document library.

The current document workspace provides:

* Document identity
* Document content
* Document information
* Workspace actions
* External opening for unsupported previews
* Document removal
* Workspace close behavior
* Presenter-driven interaction

The current document opening flow is:

```text
Main Document Library
        │
        │ Open
        ▼
OpenDocumentHandler
        │
        ▼
Document Workspace
        ├── Document Identity
        ├── Document Content
        ├── Document Information
        └── Workspace Actions
```

### Workspace Model

The domain now includes a workspace model supporting:

* Temporary workspaces
* Persistent named workspaces
* Workspace document membership
* Document ordering
* Last active document tracking
* Workspace activation
* Workspace creation
* Workspace opening
* Workspace renaming
* Workspace deletion
* Adding/removing documents
* Saving temporary workspaces as persistent workspaces

The current repository therefore contains the persistence and application foundations for richer multi-document workspace experiences, while the full user-facing workspace management experience continues to evolve during MVP 2.

---

## Document Rendering

Rendering is isolated behind a renderer abstraction and resolver.

```text
DocumentViewForm
      │
      ▼
IDocumentContentRendererResolver
      │
      ▼
IDocumentContentRenderer
      ├── Text
      ├── Markdown
      └── CSV
```

This keeps rendering concerns outside document lifecycle and processing workflows.

### Text Rendering

The text renderer is used for plain text and text-oriented formats, including source-code and configuration formats.

This provides a single presentation path for formats whose primary representation is textual content.

### Markdown Rendering

Markdown is parsed using **Markdig** and presented through **WebView2**.

Imported Markdown is treated as untrusted content. The current rendering policy disables or controls:

* Raw HTML
* JavaScript
* External resources
* External navigation

Renderer-specific security and presentation behavior remains inside the rendering boundary.

### CSV Rendering

CSV follows a parser-plus-renderer pipeline:

```text
CSV
 │
 ▼
CsvDocumentParser
 │
 ▼
Structured CsvDocument
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

This preserves CSV semantics before presentation and allows bounded preview behavior without forcing the UI to understand CSV parsing rules.

---

## Document Processing

Document processing is deliberately independent from UI rendering.

The current processing pipeline is:

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
Deterministic Chunking
      │
      ▼
Persisted Derived Representation
      │
      ▼
Search / Future Retrieval
```

Current processing capabilities include:

* Text extraction
* Markdown extraction
* CSV extraction
* INI extraction
* JSON extraction
* XML extraction
* YAML extraction
* Text-based source-code extraction
* Extraction failure boundaries
* Text normalization
* Deterministic chunking
* Persisted processing state
* Processing attempts
* Retry/reprocessing support
* Cancellation propagation
* Idempotent replacement of derived results
* Persisted document chunks
* Chunk identity and provenance

Processing does not depend on document rendering.

A document can therefore be processed for search or future AI workloads without first being rendered in the UI.

### Processing Lifecycle

Processing has its own lifecycle rather than being coupled directly to document status.

```text
Pending
   │
   ▼
Processing
   ├──────────► Completed
   │
   └──────────► Failed
                    │
                    │ Retry / Reprocess
                    ▼
                Processing
```

The implementation separates the current successful derived result from failed or incomplete processing attempts.

---

## Document Chunk Identity and Provenance

Document chunks have stable logical identity based on document identity and chunk order.

The current model supports:

* Stable chunk logical IDs
* Deterministic content hashing
* Chunk ordering
* Provenance-related data
* Backfilling of chunk identity for existing persisted data

This establishes a stable foundation for future indexing, retrieval, embeddings, and source-grounded AI responses.

---

## Search

DeskVault currently provides local keyword/full-text search across processed document chunks.

The search flow is:

```text
Search Query
     │
     ▼
Document Search Store
     │
     ▼
Search Results
     │
     ▼
Application Ranker
     │
     ▼
Paged Search Results
```

The current search boundary includes:

* Query validation
* File-type filtering
* Search result ranking
* Match counts
* Match context
* Continuation-based paging
* Incremental loading
* Cancellation-aware search operations
* Stable application-level search result models

The search layer operates on processed document data rather than scraping rendered UI content.

This maintains a clear separation between:

```text
Document Semantics
       │
       ├── Rendering
       ├── Search
       ├── Future Indexing
       └── Future AI / Retrieval
```

---

## Persistence and Storage

DeskVault separates document artifacts from application metadata.

The default local storage boundary is:

```text
%LOCALAPPDATA%\DeskVault
├── DeskVault.db
├── Documents
│   └── <document-id>.dvault
├── Security
│   └── <protected key material>
└── Logs
```

### SQLite Database

SQLite stores application metadata and derived processing information, including concepts such as:

* Document identity
* File name
* Display name
* SHA-256 hash
* Import timestamp
* Document status
* Encrypted artifact path
* Processing execution state
* Processing generation information
* Document chunks
* Workspace data
* Workspace memberships

Entity Framework Core provides the persistence infrastructure and migration-based schema evolution.

### Encrypted SQLite

The application includes encrypted SQLite database support using the SQLite encryption provider used by the infrastructure layer.

Database encryption keys are protected using Windows Data Protection with the current Windows user scope.

The database initialization pipeline also contains migration and recovery handling for transitioning existing plaintext SQLite databases into the encrypted database format.

---

## Security

Security is treated as an architectural concern rather than a feature added after the core implementation.

Current security-related capabilities include:

* AES-GCM encryption for document artifacts
* Encrypted SQLite database storage
* SHA-256 content hashing
* Windows-protected document encryption keys
* Windows-protected database encryption keys
* Local-only document storage
* Controlled Markdown rendering
* Explicit external opening for unsupported in-app previews
* Migration safeguards for database encryption
* Cryptographic key material zeroing after use where appropriate

DeskVault does not require cloud document storage for its current core functionality.

Security-related documentation and future hardening work are tracked separately in the repository.

See [`SECURITY.md`](SECURITY.md) for the project's security policy.

---

## Local AI Direction

DeskVault is designed to support future local AI-powered knowledge retrieval without moving the source documents into cloud storage.

The intended long-term architecture is:

```text
Local Documents
      │
      ▼
Encrypted Storage
      │
      ▼
Extraction
      │
      ▼
Normalization
      │
      ▼
Deterministic Chunking
      │
      ▼
Embeddings
      │
      ▼
Vector / Hybrid Retrieval
      │
      ▼
RAG
      │
      ▼
Local AI Model
      │
      ▼
Source-Grounded Answer
```

The repository contains a dedicated `DeskVault.AI` project and application configuration for an Ollama endpoint and model name.

However, the following are **not currently implemented as finished product capabilities**:

* Embedding generation
* Vector indexing
* Hybrid retrieval
* RAG orchestration
* Functional AI assistant interaction
* Source-grounded AI responses
* Background AI/document workers

Ollama is the planned local model runtime, with `phi4-mini` currently represented in development configuration.

---

## Design Principles

DeskVault is being developed around the following principles:

1. **Local-first** — documents remain under the user's local control.
2. **Security by design** — encryption, key management, and storage boundaries are architectural concerns.
3. **Separation of concerns** — Domain, Application, Infrastructure, and UI maintain distinct responsibilities.
4. **Abstraction over infrastructure** — application workflows depend on contracts rather than concrete persistence or storage technologies.
5. **Vertical slices** — application behavior is organized around complete capabilities rather than technical layers alone.
6. **Semantic preservation** — parsing and extraction establish document meaning; rendering presents it.
7. **Replaceable infrastructure** — persistence, storage, and platform-specific services remain replaceable behind abstractions.
8. **Incremental evolution** — the architecture is extended through working product increments rather than speculative implementation.
9. **Evidence over claims** — implemented functionality is distinguished clearly from roadmap direction.
10. **AI-ready foundations** — processing and search are designed so future retrieval and AI capabilities can consume structured application-level results rather than UI output.

---

## Technology Stack

| Area                   | Technology                                         |
| ---------------------- | -------------------------------------------------- |
| Runtime                | .NET 10                                            |
| SDK                    | .NET SDK 10.0.400                                  |
| UI                     | Windows Forms                                      |
| Language               | C#                                                 |
| Dependency Injection   | Microsoft.Extensions.DependencyInjection           |
| Configuration          | Microsoft.Extensions.Configuration                 |
| Logging                | Serilog                                            |
| Persistence            | SQLite                                             |
| ORM                    | Entity Framework Core 10                           |
| Database Encryption    | SQLite3MC                                          |
| Document Encryption    | AES-GCM                                            |
| Windows Key Protection | Windows Data Protection                            |
| Hashing                | SHA-256                                            |
| CSV Parsing            | CsvHelper                                          |
| YAML Parsing           | YamlDotNet                                         |
| INI Parsing            | ini-parser-netstandard                             |
| Markdown Parsing       | Markdig                                            |
| Markdown Presentation  | Microsoft WebView2                                 |
| Local AI Runtime       | Ollama — planned/under development                 |
| Testing                | xUnit, Moq, Microsoft.NET.Test.Sdk                 |
| Coverage               | coverlet.collector                                 |
| Architecture           | Layered architecture + application vertical slices |

---

## Project Structure

The repository is organized as follows:

```text
DeskVault/
│
├── src/
│   │
│   ├── DeskVault.UI/
│   │   ├── Forms/
│   │   ├── Hosting/
│   │   ├── Presenters/
│   │   ├── Rendering/
│   │   │   ├── CsvDocumentRendering/
│   │   │   ├── MarkdownDocumentRendering/
│   │   │   └── TextDocumentRendering/
│   │   ├── Resources/
│   │   ├── Services/
│   │   └── Views/
│   │
│   ├── DeskVault.Application/
│   │   ├── Configurations/
│   │   ├── Documents/
│   │   │   ├── Chunking/
│   │   │   ├── Commands/
│   │   │   ├── Extraction/
│   │   │   ├── Normalization/
│   │   │   ├── Parsing/
│   │   │   ├── Processing/
│   │   │   └── Queries/
│   │   ├── Interfaces/
│   │   └── Workspaces/
│   │       ├── Commands/
│   │       └── Queries/
│   │
│   ├── DeskVault.Domain/
│   │   ├── Documents/
│   │   └── Workspaces/
│   │
│   ├── DeskVault.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── Configurations/
│   │   │   ├── Context/
│   │   │   ├── Entities/
│   │   │   └── Migrations/
│   │   ├── Repositories/
│   │   └── Services/
│   │
│   ├── DeskVault.AI/
│   │   ├── Chats/
│   │   ├── Clients/
│   │   ├── Embeddings/
│   │   ├── Models/
│   │   ├── Prompts/
│   │   └── Services/
│   │
│   └── DeskVault.Shared/
│       └── Resources/
│
├── tests/
│   ├── DeskVault.Application.Tests/
│   ├── DeskVault.Infrastructure.Tests/
│   ├── DeskVault.Integration.Tests/
│   └── DeskVault.UI.Tests/
│
├── docs/
│   └── adr/
│
├── DeskVault.slnx
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── CONTRIBUTING.md
├── SECURITY.md
└── LICENSE
```

The tree above focuses on the architectural structure rather than listing every individual source file.

---

## Development

DeskVault targets **Windows** because the current application uses WinForms, WebView2, and Windows Data Protection.

The repository pins the .NET SDK through `global.json`.

### Restore

```bash
dotnet restore DeskVault.slnx
```

### Build

```bash
dotnet build DeskVault.slnx
```

### Test

```bash
dotnet test DeskVault.slnx
```

The test suite is divided by architectural area:

```text
Application Tests
        │
        ├── Application behavior
        │
        ▼
Infrastructure Tests
        │
        ├── Persistence / storage / security
        │
        ▼
Integration Tests
        │
        ├── Cross-layer behavior
        │
        ▼
UI Tests
```

The exact passing-test count should be taken from the current test run rather than maintained as a fixed README claim.

---

## Development Progression

DeskVault is being developed incrementally:

```text
Secure Foundation
       │
       ▼
Domain Model
       │
       ▼
Document Import
       │
       ▼
Encrypted Document Storage
       │
       ▼
Persistent Metadata
       │
       ▼
Document Retrieval / Removal
       │
       ▼
In-App Document Workspace
       │
       ▼
Document Rendering
   ├── Text
   ├── Markdown
   └── CSV
       │
       ▼
Document Processing
   ├── Extraction
   ├── Normalization
   ├── Chunking
   └── Persistence
       │
       ▼
Local Search
   ├── Ranking
   ├── Match Context
   └── Continuation Paging
       │
       ▼
Workspace Persistence Foundations
       │
       ▼
[Future]
Embeddings
       │
       ▼
Vector / Hybrid Retrieval
       │
       ▼
RAG
       │
       ▼
Local AI Knowledge Assistant
```

Major architectural changes are documented through ADRs rather than being left implicit in implementation details.

---

## Roadmap

The longer-term product direction includes:

* Richer workspace management
* Related-document workflows
* Persistent multi-document workspaces
* Workspace recovery and recent activity
* Additional document rendering where a dedicated presentation model is justified
* Expanded document-processing capabilities
* Embeddings
* Vector indexing
* Hybrid search
* Retrieval-Augmented Generation
* Local AI integration
* Source-grounded AI responses
* Background processing workers
* Durable retry scheduling
* Richer processing observability
* Additional security hardening and security-focused testing

Roadmap items are directional and do not imply that every item belongs to the next MVP or release.

---

## Architecture Decision Records

Significant architectural decisions are documented in [`docs/adr/`](docs/adr/).

Current ADRs:

* [ADR 0001 — Project Vision](docs/adr/0001-project-vision.md)
* [ADR 0002 — Vertical Slice Architecture](docs/adr/0002-vertical-slice-architecture.md)
* [ADR 0003 — Document Import Workflow](docs/adr/0003-document-import-workflow.md)
* [ADR 0004 — Document Encryption at Rest](docs/adr/0004-document-encryption-at-rest.md)
* [ADR 0005 — Document Metadata Persistence](docs/adr/0005-document-metadata-persistence.md)
* [ADR 0006 — In-App Document Workspace](docs/adr/0006-in-app-document-workspace.md)
* [ADR 0007 — Document Workspace UI and Interaction Model](docs/adr/0007-document-workspace-ui-and-interaction-model.md)
* [ADR 0008 — Document Semantic Preservation Through Rendering Pipeline](docs/adr/0008-document-semantic-preservation-through-rendering-pipeline.md)
* [ADR 0009 — Encrypted SQLite Database Provider](docs/adr/0009-encrypted-sqlite-database-provider.md)
* [ADR 0010 — Reliable Document Processing Lifecycle](docs/adr/0010-reliable-document-processing-lifecycle.md)
* [ADR 0011 — Stable Document Chunk Identity and Provenance](docs/adr/0011-stable-document-chunk-identity-and-provenance.md)
* [ADR 0012 — Search Result and Relevance Boundary](docs/adr/0012-search-result-and-relevance-boundary.md)

---

## Contributing

Development and contribution guidance is available in [`CONTRIBUTING.md`](CONTRIBUTING.md).

The project is developed incrementally, with architectural decisions, implementation boundaries, and product capabilities documented alongside the codebase.

---

## Security

For vulnerability reporting and security-related information, see [`SECURITY.md`](SECURITY.md).

---

## License

See [`LICENSE`](LICENSE) for license information.
