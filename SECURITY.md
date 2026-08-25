# Security Policy

## Supported Versions

Security fixes are currently considered for the latest version of DeskVault
on the `main` branch.

DeskVault is currently developed as a local-first Windows desktop application.
Support for older releases is not guaranteed during the MVP development phase.

## Reporting a Vulnerability

If you believe you have found a security vulnerability in DeskVault, please
do not open a public GitHub issue with the vulnerability details.

Please report the issue privately to the project maintainer. If a private
security reporting mechanism is configured for this repository, use that
mechanism.

Please include, where possible:

- a clear description of the vulnerability
- the affected component or file
- steps to reproduce the issue
- the potential security impact
- relevant logs, screenshots, or proof-of-concept material
- any suggested mitigation

Please do not include real documents, credentials, encryption keys, or other
sensitive personal information in a report.

## Security Scope

Security-sensitive areas of DeskVault include, but are not limited to:

- document encryption at rest
- encryption-key protection
- document storage and retrieval
- document import and processing
- Markdown rendering and content sanitization
- local SQLite persistence
- document removal and associated derived data

## Disclosure

Please allow the maintainer reasonable time to investigate and address a
reported vulnerability before publicly disclosing technical details.

Security fixes may be accompanied by updates to documentation, tests, or
Architecture Decision Records where appropriate.

## Out of Scope

The following are generally outside the scope of the MVP security policy:

- vulnerabilities in third-party dependencies that cannot be reproduced
  through DeskVault
- issues requiring physical access to an already unlocked user session
- denial-of-service conditions that do not materially affect the local
  application
- unsupported operating systems or configurations

Out-of-scope does not necessarily mean that a report will be ignored; each
report may be evaluated based on its actual impact.

## Security Philosophy

DeskVault follows a local-first security model. User documents are intended
to remain under local user control, with encryption at rest and explicit
boundaries between document storage, processing, rendering, and future
knowledge features.

Security decisions that materially affect the architecture should be
documented through the project's Architecture Decision Records (ADRs).
