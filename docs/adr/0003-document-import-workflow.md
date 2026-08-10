# ADR-0003: Document Import Workflow

## Status

Accepted

## Context

DeskVault needs a reliable document import workflow that can accept supported local files, prevent duplicate documents, store imported files under application-managed storage, and create the corresponding Domain document.

The import workflow crosses multiple architectural boundaries:

- Application validation and orchestration
- Domain document creation and invariants
- Infrastructure hashing
- Infrastructure file storage
- Repository persistence

The workflow must remain independent of the UI so that the same use case can later be triggered by other application entry points such as background processing or an API.

## Decision

DeskVault will implement document import as an Application-layer command use case.

The `ImportDocumentHandler` will orchestrate the workflow through application-defined interfaces.

The workflow is:

```text
ImportDocumentCommand
        |
        v
Validate
        |
        +---- Invalid ----> Validation Result
        |
        v
Compute SHA-256
        |
        v
Check Duplicate
        |
        +---- Exists ----> Duplicate Result
        |
        v
Generate Document ID
        |
        v
Store File
        |
        v
Create Domain Document
        |
        v
Persist Document
        |
        v
Success Result