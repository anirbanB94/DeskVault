# DeskVault

> Secure, Offline-First Enterprise Knowledge Platform powered by Local AI.

DeskVault is a modern desktop application built with .NET 10 and WinForms, designed to provide secure, offline-first document and knowledge management with locally hosted AI.

The application is designed around privacy, maintainability, extensibility, and clear architectural boundaries. Documents, application data, and AI processing are intended to remain on the local machine.

## Current Status

DeskVault is under active development.

The first complete vertical slice is the Document Import workflow.

Current capabilities include:

* Document import command workflow
* Supported file type validation
* SHA-256 content hashing
* Duplicate document detection
* Application-managed local file storage
* AES-GCM encryption at rest
* Windows-protected encryption key management
* Encrypted document reading
* Domain document creation with enforced invariants
* Repository abstraction with an in-memory implementation
* List and open document application queries
* WinForms document list and open workflow
* MVP-based presentation flow
* Dependency injection across Application, Infrastructure, and UI
* Structured application results and recoverable storage error handling

## Architecture

DeskVault uses a vertical-slice approach for organizing application capabilities while maintaining clear architectural boundaries.

```
`DeskVault.UI
    │
    ├── Application
    │       │
    │       └── Domain
    │
    └── Infrastructure
            │
            └── Application abstractions
`
```

The main projects are:

| Project                    | Responsibility                                                               |
| -------------------------- | ---------------------------------------------------------------------------- |
| `DeskVault.Domain`         | Domain entities, state, and business invariants                              |
| `DeskVault.Application`    | Use cases, validation, orchestration, and application contracts              |
| `DeskVault.Infrastructure` | File storage, hashing, encryption, persistence, and external implementations |
| `DeskVault.UI`             | WinForms presentation, MVP views/presenters, and application composition     |
| `DeskVault.AI`             | Local AI integration and related capabilities                                |
| `DeskVault.Shared`         | Genuinely cross-cutting shared primitives                                    |

Detailed architectural decisions are documented in `docs/adr`.

## Document Import

The current import workflow is:

```
`Validate
   ↓
Compute SHA-256
   ↓
Check Duplicate
   ↓
Generate Document ID
   ↓
Encrypt and Store File
   ↓
Create Domain Document
   ↓
Persist Document
   ↓
Return Result
`
```

Imported files are encrypted before being stored on disk. The encryption key is protected using Windows DPAPI, while document reading transparently decrypts the stored content for the UI.

The workflow is implemented independently of the UI so that it can later be reused by other application entry points such as background processing or APIs.

## Technology

* .NET 10
* C#
* WinForms
* Microsoft.Extensions.Hosting
* Microsoft.Extensions.DependencyInjection
* Microsoft.Extensions.Configuration
* Serilog
* Ollama for local AI
* Local file-system storage
* AES-GCM encryption
* Windows DPAPI for encryption key protection

The initial AI integration is planned around Phi-3 Mini running locally through Ollama.

## Development Principles

DeskVault follows these principles:

* Offline-first by design
* Privacy and local data ownership
* Domain-driven business rules
* Dependency inversion
* Feature-oriented application organization
* Small, focused services
* Infrastructure behind application-defined abstractions
* MVP for WinForms presentation concerns
* Incremental development with a green build after each logical change
* Architectural decisions documented through ADRs

## Roadmap

Planned areas include:

* Persistent SQLite storage
* Document text extraction
* Document indexing
* Local AI processing
* Embeddings
* Semantic and hybrid search
* RAG-based knowledge retrieval
* Background document processing
* Document lifecycle management

The roadmap will evolve as architectural decisions are made and implemented.

## Documentation

Architectural decisions are maintained under:

```
`docs/
└── adr/
`
```

Current ADRs:

* `0001-project-vision.md`
* `0002-vertical-slice-architecture.md`
* `0003-document-import-workflow.md`
* `0004-document-encryption-at-rest.md`

## License

See `LICENSE`.
