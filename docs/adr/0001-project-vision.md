# ADR-0001: Project Vision

## Status

Accepted

## Context

Modern AI applications frequently depend on cloud-hosted services, requiring internet connectivity and external data processing.

Many organizations operate in environments where documents cannot leave local infrastructure due to privacy, regulatory, or security requirements.

The project aims to demonstrate how a modern enterprise desktop application can integrate local AI while maintaining clean architecture, maintainability, and offline operation.

## Decision

DeskVault will be designed as an **offline-first enterprise knowledge platform**.

The application will:

- Store application data locally.
- Maintain a layered architecture following modern .NET engineering practices.
- Prioritize maintainability, extensibility, and security over rapid feature development.
- Provide a path for local AI inference using Ollama without making cloud AI a runtime dependency.
- Use lightweight language models suitable for enterprise laptops when local AI functionality is introduced.

Local AI is an architectural direction and future capability. It is **not considered implemented MVP 1 functionality**.

## Consequences

### Positive

- No dependency on cloud AI services for the intended local AI architecture.
- Suitable for privacy-sensitive environments.
- Strong architectural separation of concerns.
- Demonstrates enterprise software engineering practices.
- Provides a clear path toward local AI without coupling the current MVP to an AI runtime.

### Negative

- Local hardware limitations constrain model size.
- Additional engineering effort is required for local AI integration.
- Some cloud AI capabilities may not be available offline.
- Local AI functionality remains incomplete until the planned AI integration is implemented.

## Current MVP 1 Scope

MVP 1 establishes the secure, persistent, local document foundation and processing/search capabilities required for the platform.

Current MVP 1 functionality includes:

- Local document storage and metadata persistence.
- Encrypted document content at rest.
- In-app document workspace and supported document rendering.
- Document processing through extraction, normalization, and chunking.
- Persisted processing state and derived document chunks.
- Local keyword/full-text search across processed document chunks.

The following remain future capabilities:

- Local AI inference through Ollama.
- Embeddings and vector indexing.
- Retrieval-augmented generation (RAG).
- Source-grounded AI responses.
- Functional AI assistant interaction.

This distinction keeps the project vision intact while ensuring the ADR does not imply that future AI functionality is already implemented.
