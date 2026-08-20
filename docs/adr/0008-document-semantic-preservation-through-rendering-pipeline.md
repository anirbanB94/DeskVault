# ADR-0008: Preserve Document Semantics Through the Rendering Pipeline

## Status

Accepted

## Context

DeskVault is evolving from a document-management application into a document-centric knowledge workspace with future capabilities for search, retrieval, local AI, and retrieval-augmented generation.

The current document workspace introduces multiple responsibilities that must remain distinct:

```text
Source Document
      ↓
Parsing / Extraction
      ↓
Structured Document Representation
      ↓
Rendering
      ↓
User Interface
```

Future DeskVault capabilities will also consume document information for purposes other than visual presentation:

```text
Structured Document Representation
      ├── Rendering
      ├── Search / Indexing
      └── AI / Retrieval
```

Without an explicit architectural rule, format-specific renderers could become the place where document meaning is interpreted, normalized, truncated, or otherwise transformed for UI convenience.

That would create several problems.

First, the UI could become the accidental source of truth for document semantics.

Second, AI and search implementations could become coupled to presentation-specific controls such as `DataGridView`, WebView2, HTML, or other UI technologies.

Third, preview behavior could make a partial representation look like a complete document.

Fourth, structural anomalies in source documents could be silently discarded by rendering code.

The CSV implementation has already demonstrated why this distinction matters.

A CSV document may contain:

```text
Header: 4 columns
Row:    3 fields
```

or:

```text
Header: 3 columns
Row:    4 fields
```

The application needs to preserve those facts rather than silently reshaping the source into whatever the UI happens to display.

The CSV implementation therefore introduced a structured representation:

```text
CsvDocument
├── Columns
├── Rows
├── Warnings
└── HasMoreRows
```

The parser produces that representation and the renderer consumes it.

This establishes a general architectural pattern that should govern future document formats.

## Decision

DeskVault will preserve document semantics through a distinct parsing/extraction and rendering boundary.

The architectural rule is:

> **Parsing and extraction establish the structured meaning available to the application. Rendering consumes that representation for presentation and must not become the source of truth for document semantics.**

The intended pipeline is:

```text
Source Document
      ↓
Parser / Extractor
      ↓
Structured Document Representation
      ↓
┌───────────────┬────────────────┬─────────────────┐
│               │                │                 │
▼               ▼                ▼                 ▼
Rendering     Search          Indexing       AI / Retrieval
```

The structured representation may be format-specific when the source format has meaningful structure that cannot be safely reduced to generic text.

The representation must preserve information required by downstream consumers.

Rendering is therefore a consumer of document semantics, not the authority that defines them.

## Parsing / Extraction Boundary

Each supported document format will have a parsing or extraction boundary responsible for turning source content into an application-consumable representation.

Examples include:

```text
CSV
 ↓
CsvDocumentParser
 ↓
CsvDocument
```

Future examples may include:

```text
PDF
 ↓
PdfDocumentParser / Extractor
 ↓
PdfDocument
```

```text
DOCX
 ↓
DocxDocumentParser / Extractor
 ↓
DocxDocument
```

```text
XLSX
 ↓
XlsxDocumentParser
 ↓
Workbook / Worksheet representation
```

The exact representation will depend on the source format.

DeskVault will not force every document format into a single lowest-common-denominator model if doing so would destroy useful source semantics.

## Structured Representation

A structured document representation should preserve information that can materially affect interpretation.

Depending on the format, this may include:

- document metadata
- structural hierarchy
- sections
- pages
- tables
- rows
- columns
- cells
- links
- source positions
- warnings
- extraction limitations
- preview boundaries
- ordering
- relationships between structural elements

The representation should distinguish between:

```text
No content exists
```

and:

```text
Content exists but was not materialized
```

and:

```text
Content exists but could not be interpreted reliably
```

This distinction is especially important for large documents and imperfect source files.

## Preview and Bounded Materialization

DeskVault may intentionally materialize only part of a large document for interactive preview.

A bounded preview must never be treated as equivalent to the complete source document.

The representation should expose the fact that additional source content exists.

The current CSV implementation uses:

```text
MaxRows
    ↓
bounded row materialization
    ↓
HasMoreRows
```

This allows the UI to communicate that the displayed data is a preview rather than silently implying completeness.

The same principle applies to future formats.

For example:

```text
PDF
    ↓
first N pages extracted for preview
    ↓
HasMorePages / equivalent metadata
```

or:

```text
XLSX
    ↓
bounded worksheet preview
    ↓
additional-content indicator
```

The exact property names may vary by representation, but the semantic requirement remains the same.

## Structural Warnings

Parsers should preserve meaningful structural anomalies rather than silently normalizing them away.

The current CSV representation uses:

```text
CsvDocumentWarning
```

to preserve structural problems such as uneven rows.

The parser therefore performs:

```text
Source
  ↓
Interpret
  ↓
Detect structural anomaly
  ↓
Preserve warning
  ↓
Produce representation
```

The renderer may display those warnings to the user.

The renderer must not reinterpret or silently remove them merely to make the UI appear cleaner.

Future parsers should follow the same principle when source anomalies materially affect interpretation.

## Renderer Responsibility

A document renderer is responsible for presentation.

Its responsibilities include:

- choosing appropriate UI controls
- presenting document structure
- presenting warnings
- presenting preview boundaries
- formatting content for readability
- managing presentation-specific resources
- exposing user interactions appropriate to the viewing experience

A renderer is not responsible for redefining the document model.

For example, a CSV renderer may use:

```text
DataGridView
```

but `DataGridView` is a presentation mechanism.

The renderer must not make the application depend on `DataGridView` as the representation of CSV semantics.

The same principle applies to:

```text
WebView2
HTML
RichTextBox
PDF viewer controls
Office viewer controls
```

These are rendering technologies, not document semantic models.

## CSV as the Reference Implementation

The current CSV implementation is the reference example for this architectural decision.

Its flow is:

```text
CSV stream
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

The parser is responsible for:

- CSV field interpretation
- header handling
- column creation
- uneven-row detection
- warning creation
- bounded materialization
- cancellation
- `HasMoreRows` calculation

The renderer is responsible for:

- creating the grid
- creating columns for presentation
- populating displayed rows
- presenting structural warnings
- presenting empty-document state

This separation must be preserved as CSV functionality grows.

## Configuration Boundary

Parsing behavior that affects document semantics or resource usage must be configurable through parser-specific options rather than being hidden inside the renderer.

The current CSV configuration is:

```text
CsvParsing
└── MaxRows
```

which binds to:

```text
CsvParsingOptions.MaxRows
```

The flow is:

```text
appsettings.json
      ↓
CsvParsing configuration section
      ↓
IOptions<CsvParsingOptions>
      ↓
CsvDocumentParser
```

The renderer must not independently impose a different row limit.

This prevents contradictory limits such as:

```text
Parser: 10,000 rows
Renderer: 1,000 rows
```

where the UI could accidentally create a second semantic boundary.

## AI and Search Independence

Future AI and search functionality must consume document representations or dedicated application-level document-processing results rather than scraping the rendered UI.

The prohibited conceptual flow is:

```text
Document
   ↓
UI Renderer
   ↓
DataGridView / WebView2 / UI text
   ↓
AI
```

The preferred flow is:

```text
Document
   ↓
Parser / Extractor
   ↓
Structured Representation
   ├── Renderer
   ├── Search / Indexing
   └── AI / Retrieval
```

This keeps AI independent from presentation technology.

It also allows AI processing to operate when no interactive renderer exists.

For example, a future unsupported presentation format may still be extractable and searchable:

```text
Document
   ↓
Extractor
   ↓
Structured representation
   ↓
Search / AI

No renderer required for processing
```

## Rendering Does Not Define Completeness

The visible UI is not a reliable definition of document completeness.

A document may be:

- partially previewed
- structurally anomalous
- partially extracted
- subject to parser warnings
- represented with unsupported elements

The renderer should expose those limitations where appropriate.

The system must distinguish:

```text
Source completeness
```

from:

```text
Preview completeness
```

and:

```text
Extraction completeness
```

This is particularly important for enterprise workflows because users may make decisions based on document previews.

## Format-Specific Semantics

DeskVault will allow format-specific representations when the source format has meaningful semantics.

For example:

### CSV

```text
CsvDocument
├── Columns
├── Rows
├── Warnings
└── HasMoreRows
```

### XLSX

A future representation may preserve:

```text
Workbook
├── Worksheets
├── Tables
├── Cells
├── Formulas
└── Metadata
```

### PDF

A future representation may preserve:

```text
PdfDocument
├── Metadata
├── Pages
├── Text
├── Tables
├── Source positions
└── Extraction warnings
```

### DOCX

A future representation may preserve:

```text
DocxDocument
├── Paragraphs
├── Headings
├── Tables
├── Lists
├── Links
└── Metadata
```

The goal is not to make these representations identical.

The goal is to preserve enough source meaning that rendering, search, and AI can operate without depending on one another.

## Application Layer Boundary

The Application layer should expose document-processing capabilities through abstractions appropriate to the use case.

The UI must not access format-specific infrastructure directly.

The intended boundary remains:

```text
UI
 ↓
Application
 ↓
Document processing abstraction
 ↓
Infrastructure / format library
```

The exact placement of parsers may evolve as the architecture matures, but format-specific implementation details must not leak into presentation orchestration.

The UI renderer may depend on the structured representation it needs for presentation, but it should not own source parsing when parsing is a reusable application capability.

## Error Handling

Parsing and extraction failures must remain distinguishable from rendering failures.

Conceptually:

```text
Parse / Extract failure
        ↓
Document could not be interpreted

Render failure
        ↓
Document was interpreted but could not be presented
```

The application should preserve this distinction because future AI/search workflows may still be able to use partially extracted information even when a particular renderer cannot present it.

Warnings should be represented as data where practical.

Exceptions should be reserved for conditions that prevent the operation from completing according to its contract.

## Cancellation

Long-running parsing and extraction operations must support cancellation.

The current CSV parser checks the supplied `CancellationToken` during materialization.

Future parsers and extractors should follow the same principle.

Cancellation is particularly important for:

- large documents
- multi-page extraction
- workbook processing
- indexing
- future AI preparation

The renderer should propagate cancellation rather than silently converting cancellation into a generic rendering error.

## Resource Ownership

Document streams remain owned by the caller unless an explicit ownership transfer is documented.

Parsers may read from supplied streams.

Renderers may consume the parsed representation.

Format-specific resources such as WebView2 controls remain owned by the renderer that creates them, subject to the workspace lifecycle.

This prevents document-processing components from unexpectedly closing resources owned by higher-level workflows.



## Document Processing Lifecycle

The semantic-preservation boundary also governs the lifecycle of application-level document processing.

Document lifecycle and processing lifecycle are separate concepts.

The existing document lifecycle remains represented by `DocumentStatus`:

```text
Imported
Processing
Indexed
Available
Archived
Deleted
```

Processing execution state must not be inferred solely from the document lifecycle status.

A separate processing state is used to describe the state of a particular processing attempt:

```text
Pending
    ↓
Processing
    ├──────────────→ Completed
    │
    └──────────────→ Failed
                         │
                         │ retry
                         ▼
                     Processing
```

The intended meanings are:

- `Pending` — processing is scheduled or requested but has not successfully started.
- `Processing` — a processing attempt is currently executing.
- `Completed` — extraction, normalization, and chunking completed successfully for the current processing result.
- `Failed` — the processing attempt stopped before successful completion and retained failure information for diagnosis and retry.

This separation prevents the document's overall lifecycle from becoming overloaded with execution-specific concerns.

## Processing Orchestration Boundary

Document processing orchestration belongs in the Application layer.

The orchestrator is responsible for coordinating the processing stages in
their defined order:

```text
Read
  ↓
Extract
  ↓
Normalize
  ↓
Chunk
  ↓
Persist derived result
```

The orchestrator must coordinate these capabilities through application
contracts rather than depending directly on UI rendering components.

The processing workflow must therefore remain usable by:

```text
Interactive application flow
```

and, in the future:

```text
Background processing worker
```

without changing the semantic-processing boundary.

The orchestrator must not:

- parse source formats itself
- contain format-specific extraction logic
- render document content
- depend on UI controls
- treat rendered output as processing input
- silently replace processing failures with partial success

The existing asynchronous contracts are retained throughout the
processing pipeline. Cancellation must flow from the orchestration entry
point through reading, extraction, normalization, and chunking.

For MVP 1, processing may be invoked synchronously by the application
workflow even though the processing contracts are asynchronous. A
background-job framework is not required to establish this boundary.

## Processing State Ownership

Processing execution state belongs to the processing workflow, not to the
document renderer and not to the document's general lifecycle status.

The application-level processing boundary owns the transition:

```text
Pending
   ↓
Processing
   ├──────────────→ Completed
   │
   └──────────────→ Failed
```

Only the processing workflow should make these transitions.

A renderer may observe processing information for presentation, but it must
not change processing state as a side effect of rendering.

`DocumentStatus` remains responsible for the document's broader lifecycle.
Processing state remains responsible for the execution state of document
processing.

This separation allows later background execution, retries, and
observability without overloading the document lifecycle model.

## Processing Completion and Derived-Result Publication

A document must not become visibly `Completed` while its derived processing
result is known to be incomplete or invalid.

Conceptually:

```text
Processing
    ↓
Extract
    ↓
Normalize
    ↓
Chunk
    ↓
Persist derived result successfully
    ↓
Completed
```

If a processing attempt fails before the derived result is successfully
published, the attempt remains `Failed` and the application must not
present the partial attempt as the current successful processing result.

The exact transactional or replacement mechanism is a persistence
implementation concern. The observable application-level rule is:

> A successful processing result is published as a coherent derived result,
> and a failed attempt must not masquerade as a successful one.

This rule supports idempotent retries and prevents partially written chunks
from becoming authoritative search or AI input.

## Processing Pipeline

Document processing will follow the existing semantic-preservation boundary:

```text
Stored Document
      ↓
Document Reader
      ↓
Decrypted / Readable Stream
      ↓
Parser / Extractor
      ↓
Structured Document Representation
      ↓
Normalization
      ↓
Chunking
      ↓
Persisted Derived Representation
```

The processing pipeline must consume application-level document-processing results rather than rendered UI output.

The existing `IDocumentReader` boundary is responsible for obtaining a readable document stream. The caller owns the returned stream and is responsible for disposing it.

Extraction remains responsible for interpreting source content.

Normalization may produce a downstream representation suitable for consistent processing while preserving the semantic information required by downstream consumers.

Chunking produces derived units for future indexing, retrieval, and AI workflows.

Rendering remains an independent consumer and must not become a required stage of document processing.

## Failure and Retry Semantics

Processing failures must remain distinguishable from successful completion.

A failed processing record should preserve bounded diagnostic information sufficient to understand the failure and determine whether a retry is appropriate.

Processing attempts should retain, where applicable:

- processing state
- attempt count
- last attempted timestamp
- completion timestamp
- bounded failure information

Exceptions must not be used as the persisted processing model.

A retry transitions a failed processing record back into active processing:

```text
Failed
  ↓ retry
Processing
```

Retry behavior must be safe to repeat.

A failed attempt must not cause a later successful retry to accumulate duplicate derived content.

## Idempotent Derived Content

Derived document content is not an append-only record of processing attempts.

For a successfully processed document, the persisted derived representation represents the current successful processing result.

If processing is retried after failure, the implementation must prevent duplicate chunks or other derived records from being created.

Conceptually:

```text
Failed
  ↓
Retry
  ↓
Rebuild / replace derived result
  ↓
Completed
```

If a document is already `Completed`, repeating the same processing operation must not create duplicate derived content.

The exact transactional and replacement strategy is an implementation concern, but the observable semantic requirement is that retries are idempotent.

## Background Processing Readiness

The processing architecture must remain suitable for future background execution without requiring the document model or rendering layer to become coupled to a particular job framework.

Persisted `Pending`, `Processing`, `Completed`, and `Failed` states provide durable processing visibility and allow a future worker to acquire pending work.

The initial implementation does not require a background-job framework.

The application processing contract should therefore be usable by both:

```text
Interactive invocation
```

and:

```text
Future background worker
```

without changing the document semantic model.

Cancellation remains part of the processing contract and must be propagated through reading, extraction, normalization, and chunking.

A cancellation request must not be silently recorded as a generic processing failure unless the application explicitly defines cancellation as a failure outcome.

## Processing Persistence Boundary

Processing execution metadata and derived chunks are persistence concerns associated with document processing, but they must remain independent from presentation state.

The intended relationship is:

```text
Document
   │
   ├── 1 : 1 → Processing State
   │
   └── 1 : N → Document Chunks
```

The processing record owns execution-specific information such as state, attempts, timestamps, and bounded failure information.

Chunk records own derived content and ordering information required by downstream search and AI workflows.

The existing `DocumentStatus` remains the document lifecycle status and is not expanded to contain retry counters, exception details, or chunk-processing metadata.

The exact persistence schema may evolve as processing capabilities grow, provided these semantic boundaries remain intact.

## Security Considerations

The semantic-preservation boundary also provides a security boundary.

Imported document content must be treated as untrusted.

Parsing should not imply execution of document content.

Rendering should use controlled presentation policies appropriate to the format.

For Markdown, the current policy includes:

```text
Raw HTML
    ↓
disabled

JavaScript
    ↓
disabled

Remote resources
    ↓
blocked by default

External navigation
    ↓
explicitly controlled
```

The structured representation should contain document information rather than executable document behavior.

Future AI processing must also preserve the distinction between document content and instructions embedded inside that content.

A document containing text such as:

```text
Ignore previous instructions...
```

must remain document data.

It must not become an application instruction merely because an AI subsystem consumes the representation.

## Alternatives Considered

### Let each renderer parse its own source document

Rejected.

This would couple parsing and presentation, encourage duplicated parsing logic, and make search/AI processing dependent on renderers.

### Convert every format immediately into plain text

Rejected.

Plain text is useful for some downstream operations but can destroy important semantics such as tables, columns, pages, headings, formulas, links, and structural relationships.

Format-specific structured representations provide a better foundation.

### Use the rendered UI as the canonical document model

Rejected.

UI controls are presentation technologies and are not appropriate application-level semantic representations.

This would tightly couple AI and search to UI implementation details.

### Make every document format share one identical representation

Rejected.

A lowest-common-denominator model would risk losing meaningful format-specific information.

A shared higher-level abstraction may be introduced later where it provides real value, but it must not require destructive normalization.

### Materialize complete documents before rendering

Rejected as a universal requirement.

Large enterprise documents may be too expensive to fully materialize for interactive preview.

Bounded previewing with explicit completeness information is preferred.

### Hide structural warnings to provide a cleaner UI

Rejected.

Warnings that materially affect interpretation must remain available to the user and future processing components.

Presentation may summarize them, but must not silently discard them.

### Let AI read the rendered UI

Rejected.

AI should consume structured document-processing results or application-level retrieval results rather than screen controls, HTML, or rendered text.

## Consequences

### Positive

- Document semantics have a clear architectural owner.
- Rendering remains a presentation concern.
- Search and AI can reuse document-processing results.
- Format-specific information can be preserved.
- Large-document previewing can remain bounded without falsely implying completeness.
- Structural anomalies can be preserved and surfaced.
- Future document formats can introduce their own representations.
- AI does not become coupled to WinForms controls or WebView2.
- Renderer implementations remain replaceable.
- The architecture supports both visual and non-visual document processing.
- Security policy remains separated from document meaning.
- Cancellation and resource ownership have clear boundaries.
- CSV provides a concrete reference implementation for the pattern.

### Negative

- Structured representations require additional types and maintenance.
- Each document format may require format-specific parsing/extraction logic.
- Downstream consumers must understand the representations they consume.
- Preview completeness requires additional metadata and UI handling.
- More explicit boundaries increase implementation complexity compared with direct rendering.
- Future shared document abstractions may require careful design to avoid either duplication or semantic loss.

These trade-offs are accepted because DeskVault's long-term value depends on using documents for more than visual display.

## Result

DeskVault will treat document parsing/extraction and document rendering as separate architectural responsibilities.

The canonical direction is:

```text
Source Document
      ↓
Parser / Extractor
      ↓
Structured Document Representation
      ├── Rendering
      ├── Search
      ├── Indexing
      └── AI / Retrieval
```

The rendering layer is therefore a consumer of document semantics rather than the source of truth.

The current CSV implementation establishes the first concrete example of this pattern:

```text
CSV
 ↓
CsvDocumentParser
 ↓
CsvDocument
 ↓
CsvDocumentContentRenderer
 ↓
DataGridView
```

Future PDF, DOCX, XLSX, PPTX, and other document-processing implementations should follow the same architectural principle while preserving the unique semantics of their source formats.

This decision provides DeskVault with a stable foundation for document preview, search, indexing, local AI, retrieval, and RAG without coupling those capabilities to the presentation layer.
