# DeskVault

> Secure, Offline-First Enterprise Knowledge Platform powered by Local AI.

DeskVault is a Windows desktop application built with **.NET 10 and WinForms**, designed as an enterprise-grade portfolio project for secure, local-first document and knowledge management.

The core product principle is simple:

> **Your documents stay on your machine, under your control.**

DeskVault is being designed for environments where privacy, offline capability, security, and controlled data processing matter.

## Current Status

DeskVault is currently in **MVP development**.

### Implemented

* .NET 10 / WinForms desktop application
* Clean separation across Domain, Application, Infrastructure, and UI
* Document import workflow
* SHA-256 based duplicate detection
* Encrypted document storage using AES-GCM
* Windows-protected encryption key management
* Encrypted `.dvault` document artifacts
* Persistent document metadata using SQLite
* EF Core persistence infrastructure
* Application restart persistence
* Document listing and selection
* Persistent document retrieval
* Decryption and document opening
* Local application-data storage under `%LOCALAPPDATA%\DeskVault`
* Architecture Decision Records for significant architectural choices

### In Development

* Improved desktop UI/UX
* Document processing pipeline
* Text extraction
* Full-text and semantic search
* Local embeddings
* Retrieval-Augmented Generation (RAG)
* Local AI integration through Ollama
* Source-grounded knowledge retrieval
* Additional security hardening
* Automated test coverage
* Database migrations and schema evolution

## Architecture

DeskVault follows a layered architecture designed to keep infrastructure concerns isolated from the application and domain layers.

```text
┌──────────────────────────────────────────────┐
│                  DeskVault UI                │
│              WinForms + Presenter            │
└──────────────────────┬───────────────────────┘
                       │
                       ▼
┌──────────────────────────────────────────────┐
│                Application                   │
│       Commands / Queries / Interfaces        │
└──────────────────────┬───────────────────────┘
                       │
                       ▼
┌──────────────────────────────────────────────┐
│                   Domain                     │
│       Documents / Business Rules / State     │
└──────────────────────────────────────────────┘
                       ▲
                       │
┌──────────────────────┴───────────────────────┐
│                Infrastructure                │
│                                              │
│  SQLite / EF Core                            │
│  Encrypted File Storage                      │
│  Encryption Key Management                   │
│  Document Readers                            │
│  External Infrastructure Services            │
└──────────────────────────────────────────────┘
```

The Application layer depends on abstractions such as `IDocumentRepository`. Infrastructure provides the concrete implementations.

This keeps persistence, encryption, filesystem access, and other platform-specific concerns outside the Domain and Application layers.

## Document Storage Model

DeskVault separates **document content** from **document metadata**.

```text
%LOCALAPPDATA%\DeskVault\
│
├── DeskVault.db
│
├── Documents\
│   └── <document-id>.dvault
│
└── Security\
    └── <protected security material>
```

### SQLite

SQLite stores document metadata such as:

* Document ID
* File name
* Display name
* SHA-256 hash
* Import timestamp
* Document status
* Encrypted storage path

EF Core provides the persistence abstraction inside Infrastructure.

### Encrypted Files

The actual document content is stored separately as encrypted `.dvault` files.

The application decrypts the stored content only when the document needs to be opened or processed.

This separation allows the metadata store and document-content storage to evolve independently.

## Security Direction

Security is a core architectural concern rather than a later add-on.

The current implementation includes:

* AES-GCM document encryption
* SHA-256 content hashing
* Windows-protected encryption key management
* Local-only document storage
* No requirement for cloud document storage

Future security work will include additional hardening, validation, key-management improvements, and security-focused testing.

## Local AI Direction

DeskVault is designed to eventually provide local AI-powered knowledge retrieval.

The intended pipeline is:

```text
Documents
    ↓
Encrypted Local Storage
    ↓
Text Extraction
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

AI functionality is **not yet considered complete MVP functionality**; it is part of the platform roadmap.

## Design Principles

DeskVault is being developed around the following principles:

1. **Local-first** — user documents remain on the local machine.
2. **Security by design** — encryption and key management are architectural concerns.
3. **Layered architecture** — Domain and Application remain independent of infrastructure technologies.
4. **Replaceable infrastructure** — repositories and infrastructure services are accessed through abstractions.
5. **Explicit boundaries** — UI, Application, Domain, and Infrastructure have distinct responsibilities.
6. **Scalable foundations** — today's MVP should provide a reasonable path toward search, indexing, and AI workloads without premature complexity.
7. **Evidence over claims** — documentation should reflect implemented functionality separately from planned capabilities.

## Technology Stack

| Area                | Technology                              |
| ------------------- | --------------------------------------- |
| Runtime             | .NET 10                                 |
| UI                  | Windows Forms                           |
| Language            | C#                                      |
| Persistence         | SQLite                                  |
| ORM                 | Entity Framework Core                   |
| Document Encryption | AES-GCM                                 |
| Key Protection      | Windows Data Protection                 |
| Hashing             | SHA-256                                 |
| Local AI Runtime    | Ollama                                  |
| Planned Model       | Phi-4 Mini                              |
| Architecture        | Layered / Clean Architecture principles |

## Project Structure

```text
src/
├── DeskVault.UI/
│   ├── Forms/
│   ├── Hosting/
│   ├── Presenters/
│   ├── Services/
│   └── Views/
│
├── DeskVault.Application/
│   ├── Configurations/
│   ├── Documents/
│   │   ├── Commands/
│   │   └── Queries/
│   └── Interfaces/
│
├── DeskVault.Domain/
│   └── Documents/
│
├── DeskVault.Infrastructure/
│   ├── Persistence/
│   │   ├── Configurations/
│   │   ├── Context/
│   │   └── Entities/
│   ├── Repositories/
│   └── Services/
│
├── DeskVault.AI/
└── DeskVault.Shared/

docs/
└── adr/
```

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
UI Refinement
    ↓
Document Processing
    ↓
Search
    ↓
Embeddings
    ↓
RAG
    ↓
Local AI Knowledge Assistant
```

Architectural decisions are documented through ADRs under `docs/adr/`.

## Project Status

DeskVault is an actively developed portfolio project.

The current MVP focuses on establishing a **secure, persistent, local document foundation**. AI-powered knowledge retrieval will be built on top of that foundation in subsequent development stages.
