# DeskVault

> Secure, Offline-First Enterprise Knowledge Platform powered by Local AI.

DeskVault is a modern desktop application built with .NET 10 and WinForms, designed to provide secure, offline-first document and knowledge management with locally hosted AI.

The application is designed around privacy, maintainability, extensibility, and clear architectural boundaries. Documents, application data, and AI processing are intended to remain on the local machine.

## Current Status

DeskVault is under active development.

The first complete vertical slice is the **Document Import** workflow.

Current capabilities include:

* Document import command workflow
* Supported file type validation
* SHA-256 content hashing
* Duplicate document detection
* Application-managed local file storage
* Domain document creation with enforced invariants
* Repository abstraction with an in-memory implementation
* Dependency injection across Application and Infrastructure
* Structured application results and recoverable storage error handling

## Architecture

DeskVault uses a vertical-slice approach for organizing application capabilities while maintaining clear architectural boundaries.

```text
DeskVault.UI
    │
    ├── Application
    │       │
    │       └── Domain
    │
    └── Infrastructure
            │
            └── Application abstractions
```

The main projects are:

| Project                    | Responsibility                                                   |
| -------------------------- | ---------------------------------------------------------------- |
| `DeskVault.Domain`         | Domain entities, state, and business invariants                  |
| `DeskVault.Application`    | Use cases, validation, orchestration, and application contracts  |
| `DeskVault.Infrastructure` | File storage, hashing, persistence, and external implementations |
| `DeskVault.UI`             | WinForms presentation and application composition                |
| `DeskVault.AI`             | Local AI integration and related capabilities                    |
| `DeskVault.Shared`         | Genuinely cross-cutting shared primitives                        |

Detailed architectural decisions are documented in [`docs/adr`](docs/adr).

## Document Import

The current import workflow is:

```text
Validate
   ↓
Compute SHA-256
   ↓
Check Duplicate
   ↓
Generate Document ID
   ↓
Store File
   ↓
Create Domain Document
   ↓
Persist Document
   ↓
Return Result
```

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

The initial AI integration is planned around **Phi-3 Mini** running locally through Ollama.

## Development Principles

DeskVault follows these principles:

* Offline-first by design
* Privacy and local data ownership
* Domain-driven business rules
* Dependency inversion
* Feature-oriented application organization
* Small, focused services
* Infrastructure behind application-defined abstractions
* Incremental development with a green build after each logical change
* Architectural decisions documented through ADRs

## Roadmap

Planned areas include:

* WinForms document import integration
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

```text
docs/
└── adr/
```

Current ADRs:

* `0001-project-vision.md`
* `0002-vertical-slice-architecture.md`
* `0003-document-import-workflow.md`

## License

See [`LICENSE`](LICENSE).
