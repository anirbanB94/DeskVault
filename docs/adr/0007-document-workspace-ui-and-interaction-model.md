# ADR-0007: Define Document Workspace UI and Interaction Model

## Status

Accepted

## Context

ADR-0006 established the direction for an in-app document workspace and defined `DocumentViewForm` as the dedicated document-centric workspace.

The next implementation phase requires a concrete interaction model for that workspace.

DeskVault is intended to evolve from a document-management application into a secure, offline-first enterprise knowledge platform. The document workspace therefore needs to support temporary single-document viewing, persistent multi-document workspaces, multiple simultaneous workspace presentation contexts, document rendering extensions, and future local AI capabilities.

The design should avoid prematurely implementing future functionality while ensuring that the first implementation does not create architectural constraints that would prevent those capabilities later.

## Decision

DeskVault will implement the surrounding workspace interaction surface in
`WorkspaceViewForm` and the document-content presentation surface in
`DocumentViewForm`.

The intended composition is:

```text
WorkspaceViewForm
├── Workspace/document identity
├── Workspace navigation
├── Workspace/document contextual menu
├── Adaptive AI presentation boundary
└── Active DocumentViewForm
    ├── Document content
    ├── Unsupported-format preview
    └── Explicit external-open action
```

`WorkspaceViewForm` owns the header and surrounding workspace chrome. It
presents the appropriate navigation and contextual actions for temporary
and persistent workspace modes.

`DocumentViewForm` implements `IDocumentView` and presents document content
through the renderer boundary. It retains document-specific presentation
behavior and request events but does not own workspace identity, workspace
navigation, workspace management actions, or the workspace contextual menu.

The workspace presentation and its presenter coordinate document selection
and route document-specific requests to the active document presentation.
The document view does not become an independent owner of workspace state.

The exact visual styling may evolve during implementation, but the
responsibility distinction between workspace presentation and document
content presentation remains explicit.

## MVP 1 Scope Boundary

The interaction model described by this ADR establishes both the current
workspace implementation and the longer-term interaction direction.

The current workspace boundary is:

```text
Document Library
      ↓
Workspace Presentation
      ├── Workspace / document identity
      ├── Document content
      ├── Document information
      └── Workspace/document actions
```

The current implementation supports:

- temporary single-document presentation when opening directly from Documents
- persistent named workspaces containing multiple documents
- Tabs as the default persistent-workspace navigation mode
- Sidebar as an alternative persistent-workspace navigation mode
- multiple simultaneous workspace presentation contexts
- activation/reuse of an existing presentation rather than duplication
- closing a document presentation without removing persistent membership
- reopening a closed workspace member
- explicit Save as Workspace conversion from a temporary session

Functional AI interaction, Recent activity implementation, workspace recovery,
PDF/DOCX in-app rendering, and future primary-document reassignment remain
future capabilities unless separately implemented.

This keeps the current workspace focused on document and workspace
interaction while preserving the architectural direction required for later
knowledge-work evolution.

## Current Implementation and Deferred Scope

The interaction model described by this ADR establishes both the current
workspace architecture and the intended direction for future workspace
capabilities.

Currently implemented:

- `DocumentViewForm` as the document presentation surface
- workspace/document identity
- temporary and persistent workspace presentation modes
- persistent multi-document workspace membership
- Tabs as the default navigation mode
- Sidebar as an alternative navigation mode
- multiple simultaneous workspace presentation contexts
- activation/reuse of existing workspace presentations
- document content area
- document information
- workspace/document actions
- mode-dependent child document-header visibility for temporary and
  persistent presentations, including restoration during Save as Workspace
  conversion
- document presentation close independent from persistent membership removal
- reopening workspace members after their presentation has been closed
- explicit Save as Workspace conversion
- loading/error/unsupported-format presentation boundaries
- renderer resolution through `IDocumentContentRendererResolver`
- TXT rendering
- Markdown rendering with the documented security policy
- CSV structured/grid rendering
- dedicated imported-HTML WebView2 security policy, embedded-resource
  validation and isolated profile-lifecycle infrastructure; this does not
  establish the complete HTML renderer
- renderer-owned presentation resources and lifecycle
- separation between document lifecycle and workspace lifecycle

The following capabilities remain future or deferred and must not be
interpreted as implemented:

- functional AI assistant interaction
- workspace recovery
- Recent activity implementation
- primary-document reassignment in a future workspace model that introduces
  primary-document semantics
- PDF and DOCX in-app renderers
- complete imported HTML/HTM renderer integration
- future search, retrieval, and RAG behavior

The current implementation therefore establishes the document-centric
rendering boundary and the persistent multi-document workspace interaction
model without requiring the complete AI-assisted experience described as the
long-term direction.

## Current MVP 1 Workspace Boundary

For the current implementation, the workspace supports both temporary
single-document viewing and persistent multi-document workspace management:

```text
Document Library
      ↓
Temporary Workspace
      └── Document Presentation

or

Workspaces
      ↓
Persistent Workspace
      ├── Document Presentation
      ├── Document Presentation
      └── Document Presentation
```

The renderer boundary remains the extension point for additional document
formats, while document extraction and knowledge processing remain separate
from presentation.

The processing pipeline does not depend on the workspace UI:

```text
Document
   ↓
Extraction
   ↓
Normalization
   ↓
Chunking
   ↓
Persisted Derived Representation
   ↓
Search / Future AI
```

This distinction prevents future search or AI functionality from becoming
dependent on rendered controls such as `DataGridView` or WebView2.

## Workspace Header

The workspace header belongs to `WorkspaceViewForm`. It provides navigation
appropriate to the current workspace type, workspace or temporary-document
identity, the contextual workspace/document menu, and workspace-level
presentation actions.

The document view does not provide a second workspace header. It retains a
document-specific header for the active document identity and document-
specific presentation actions. This child header is distinct from the
workspace header and does not own workspace identity, navigation, or
workspace actions.

The child document header follows the authoritative workspace mode.
`WorkspacePresentation` applies its visibility through
`IDocumentView.SetDocumentChromeVisible(bool)` before the child view is
hosted. For a temporary presentation, it hides the child header because
`WorkspaceViewForm` already identifies the opened document and exposes the
document-level requests and temporary-session actions. For a persistent
workspace, it keeps the child header visible because `WorkspaceViewForm`
identifies the workspace while the child identifies the active document
and exposes its document-specific presentation actions.

For a temporary document presentation, the `WorkspaceViewForm` identity
indicates the opened document and the contextual menu provides document-level
requests and temporary-session actions. The current temporary document menu
is:

```text
[⋯]
├── Document Information
├── Save as Workspace
├── Remove Document
└── Close Document
```

For a persistent workspace, the identity shows the workspace name and
description and the menu provides workspace management actions. The current
persistent workspace menu is:

```text
[⋯]
├── Add Documents
├── Remove Documents from Workspace
├── Update Workspace Details
├── Delete Workspace
└── Close Workspace
```

Persistent workspaces also expose Tabs and Sidebar presentation choices,
with Tabs as the default.

Document-specific requests raised by the workspace menu are routed through
the workspace presentation to the active document presentation and its
document presenter. `WorkspaceViewForm` does not perform document metadata
processing or global document removal itself.

## AI Assistant Interaction

The AI assistant will use an adaptive and collapsible side-panel model owned
by the surrounding workspace presentation.

The `WorkspaceViewForm` header will provide an `[AI]` toggle. The document
content view remains focused on document rendering and does not own the
workspace-level AI control or side-panel layout.

When the assistant is collapsed, a persistent edge tab may remain
available.

Conceptually:

```text
Large screen

┌──────────────────────────────────────────────────────────┬──────────────┐
│ Workspace / document presentation                         │ AI Assistant │
│                                                          │              │
│   DocumentViewForm content                               │              │
└──────────────────────────────────────────────────────────┴──────────────┘
```

When collapsed:

```text
┌────────────────────────────────────────────────────────────────────────┐
│ Workspace / document presentation                                      │
│                                                   ┌────────┐           │
│                                                   │   AI   │           │
│                                                   └────────┘           │
└────────────────────────────────────────────────────────────────────────┘
```

The panel will adapt to available screen space.

On larger screens, the assistant may remain visible beside the document.

On smaller screens, the assistant may be collapsed by default or collapsed
when required to preserve document viewing space.

The initial implementation provides only the structural UI boundary where
required. Actual AI processing, retrieval, RAG, and local model interaction
remain future capabilities.

## Document Rendering Architecture

The document content area will not contain format-specific rendering logic.

The workspace will depend on a renderer resolution boundary:

```text
DocumentViewForm
        ↓
IDocumentContentRendererResolver
        ↓
IDocumentContentRenderer
        ↓
Format-specific renderer
```

The renderer resolver will determine the appropriate renderer for the document format.

Conceptually:

```text
.pdf → PDF renderer
.txt → Text renderer
.md  → Markdown renderer
.csv → CSV renderer
```

The architecture must allow new renderers to be added without changing `DocumentViewForm`.

The initial implementation prioritizes simple formats that can be rendered directly within the application.

The current implementation includes:

```text
TXT → in-app text renderer
MD  → in-app Markdown renderer
CSV → in-app structured/grid renderer
```

PDF, DOCX, and other formats remain future renderer implementations.

### Renderer Extensibility

The renderer boundary is the architectural extension point for future document formats.

New formats must be introduced by adding a format-specific implementation of `IDocumentContentRenderer` and registering it through dependency injection. `DocumentViewForm`, `DocumentWorkspacePresenter`, and `DocumentContentRendererResolver` must not contain format-specific rendering logic.

The intended progression is:

```text
TXT       → current in-app renderer
Markdown  → current in-app renderer
CSV       → current in-app renderer

PDF       → future renderer
DOCX      → future renderer
XLSX      → future renderer
PPTX      → future renderer
```

PDF and Microsoft Office formats are intentionally deferred. The architecture must nevertheless allow them to be added without changing the workspace or renderer orchestration contract.

### CSV Rendering and Structured Representation

CSV rendering is implemented as a parser-plus-renderer pipeline rather than direct format handling inside the workspace.

The current flow is:

```text
CSV document
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

`CsvDocumentParser` is responsible for interpreting CSV structure and producing the structured `CsvDocument` representation.

The representation preserves:

- source column definitions
- row values
- structural warnings for uneven rows
- whether additional rows exist beyond a bounded preview

The renderer consumes this representation and is responsible for presentation.

The renderer must not become the source of truth for CSV semantics.

CSV preview materialization is bounded through `CsvParsingOptions`. The configured `MaxRows` value is supplied through the `CsvParsing` configuration section and bound through the .NET options infrastructure.

This allows large CSV documents to be previewed without requiring the UI to materialize the complete document.

### Markdown Rendering Strategy

Markdown will use `Markdig` as the parsing library and WebView2 as the initial rich presentation surface.

The implementation will begin directly in `MarkdownDocumentContentRenderer`:

```text
Markdown file
      ↓
MarkdownDocumentContentRenderer
      ↓
Markdig
      ↓
controlled HTML
      ↓
WebView2
      ↓
documentContentPanel
```

WebView2 is an implementation detail of the Markdown renderer. It is not exposed through the document workspace presenter, view contract, or renderer resolver.

The initial implementation deliberately follows a simple renderer-owned composition. If the Markdown presentation surface becomes sufficiently complex, the WebView2-hosting portion may later be extracted into a reusable `MarkdownViewerControl` without changing the `IDocumentContentRenderer` contract:

```text
Initial:

DocumentViewForm
      ↓
IDocumentContentRenderer
      ↓
MarkdownDocumentContentRenderer
      └── WebView2

Future extraction:

DocumentViewForm
      ↓
IDocumentContentRenderer
      ↓
MarkdownDocumentContentRenderer
      ↓
MarkdownViewerControl
      └── WebView2
```

Imported Markdown is treated as untrusted document content.

The Markdown renderer will use a controlled rendering policy. It will not
automatically load remote resources, and external navigation will be
handled through an explicit, controlled action rather than unrestricted
automatic navigation.

The renderer should support normal Markdown document features while
avoiding premature implementation of a general-purpose HTML/browser
framework.

The renderer will also provide a DeskVault-controlled HTML presentation shell rather than relying on browser-default document styling. The shell owns typography, spacing, code blocks, tables, links, and other Markdown presentation styles. Its CSS variables provide a future seam for DeskVault light, dark, system, and accessibility-oriented themes without changing the renderer contract.

### Markdown Rendering Security and Presentation Policy

Imported Markdown is treated as untrusted content. The Markdown renderer
applies defense-in-depth restrictions at the parsing, generated-HTML and
WebView2 presentation boundaries. Rendering must not turn imported document
content into unrestricted browser activity.

The renderer exposes explicit policy options:

```text
MarkdownRenderingOptions
├── AllowRawHtml
├── AllowExternalResources
└── AllowExternalNavigation
```

The shipped application configuration keeps all three options disabled.
The options control separate capabilities and must not be treated as
interchangeable security switches.

`AllowRawHtml` controls whether the Markdig pipeline permits raw HTML.
When false, the pipeline uses `DisableHtml()` and treats raw HTML as text.
When true, Markdig preserves configured raw HTML; this is not an HTML
sanitization guarantee and must not be interpreted as permission to execute
scripts or other active content.

JavaScript remains disabled in WebView2 independently of the raw-HTML option.

#### Navigation URI Policy

The renderer permits its internal `about:blank` document navigation target,
including its fragment form. This internal exception is navigation-only;
it does not make `about:blank` an approved resource URI.

`NavigateToString` is the renderer-owned initial HTML document-load operation.
WebView2 reports this initial navigation as a `data:text/html` URI. The
renderer permits exactly one non-user-initiated, non-redirected
`data:text/html` navigation while a host-side initial-load token is pending.
The token is consumed by the first top-level navigation event, including a
rejected event. This exception applies only to the renderer's initial HTML
presentation. It does not permit Markdown-originated `data:` navigation or
`data:` resource loading. All subsequent navigation continues through the
explicit URI policy.

External navigation is restricted to absolute, well-formed HTTP/HTTPS URIs.
The URI must have a non-empty host and must not contain embedded user
information. Relative references, local-file and UNC paths, malformed
references, unsupported schemes and custom URI schemes are rejected.

External navigation requires both:

- `AllowExternalNavigation` to be enabled; and
- the initial navigation request to be user-initiated.

A redirect is permitted only when it belongs to a previously authorized
external navigation identified by the same WebView2 navigation ID. A
redirect must not independently acquire authorization merely because the
external-navigation option is enabled.

Enabling `AllowExternalNavigation` therefore does not turn the Markdown
viewer into an unrestricted browser or permit automatic navigation initiated
by untrusted document content.

#### Resource URI Policy

Resource loading is independently controlled by `AllowExternalResources`.

Only absolute, well-formed HTTP/HTTPS resource URIs are permitted when that
option is enabled. The resource URI must have a non-empty host and must not
contain embedded user information.

The renderer rejects local-file and UNC paths, relative references,
`about:blank`, data/blob/FTP/mail/custom schemes, malformed URI values and
other unsupported resource forms.

The navigation and resource switches remain independent. Permitting an
external navigation must not permit external resources, and permitting
external resources must not authorize navigation.

#### WebView2 Enforcement

The renderer enforces the policy at the WebView2 boundary:

- JavaScript remains disabled.
- Document-source resource requests are intercepted through the registered
  `WebResourceRequested` filter. A request rejected by the resource URI
  policy receives an HTTP 403 response.
- `NavigationStarting` applies the explicit URI policy, the user-initiated
  requirement and same-navigation-ID redirect authorization.
- Navigation events for created frames are cancelled.
- New-window requests are handled without opening a new window.
- External URI-scheme launching is cancelled.

Resource-request interception is a defense-in-depth mechanism, not an
assumption that every URI scheme or browser activity must raise the same
resource-request event. The explicit URI policy and generated HTML Content
Security Policy provide independent restrictions.

#### Generated HTML Content Security Policy

The renderer supplies a DeskVault-controlled HTML presentation shell. Its
head contains a `Content-Security-Policy` meta element before the
Markdown-derived content is inserted.

The generated policy establishes the following restrictions:

```text
default-src 'none'
script-src 'none'
object-src 'none'
frame-src 'none'
child-src 'none'
worker-src 'none'
connect-src 'none'
base-uri 'none'
form-action 'none'
```

Image, font and media sources are restricted to HTTP/HTTPS only when
`AllowExternalResources` is enabled. Otherwise, those source directives
use `'none'`.

The presentation shell uses inline styles. Its `style-src` directive
therefore permits `'unsafe-inline'`; HTTP/HTTPS style sources are added
only when external resources are enabled.

Enabling external resources does not enable scripts, objects, frames,
child documents, workers, connections, base-URI changes or form submission.
CSP remains an independent restriction even when raw HTML is explicitly
permitted by configuration.

The CSP and WebView2 event controls must remain aligned with the URI policy.
Future changes to supported schemes, allowed resource types or the default
policy require corresponding security-policy and regression-test updates.

#### Policy Verification and Runtime Boundary

The policy contract requires regression coverage for:

- raw HTML disabled and explicitly enabled behavior;
- JavaScript and active-content restrictions;
- legitimate HTTP/HTTPS navigation and resource cases;
- local-file, UNC, relative, malformed, encoded and custom-scheme references;
- independent navigation and resource options;
- user-initiated external navigation and authorized redirects;
- frame navigation, new-window requests and external URI-scheme attempts;
- generated CSP directives and their placement before untrusted content.

Policy-method tests and generated HTML assertions establish the policy
construction and integration contract; they do not, by themselves, prove
that every browser-runtime behavior is enforced. Actual WebView2 runtime
behavior, including CSP enforcement, resource requests, navigation,
redirects, frames, new windows and external URI schemes, must be verified
in the integrated security review.

The Markdown renderer remains an implementation detail behind
`IDocumentContentRenderer`. These security controls do not move WebView2
into the workspace presenter or view contract and do not change the
renderer-owned resource lifecycle.

### Markdown Renderer Lifecycle and Ownership

The Markdown renderer owns the WebView2 control it creates for document presentation, while the workspace remains responsible for the host control and overall workspace lifecycle.

When a renderer replaces existing content in the content host, existing child controls are explicitly disposed before the new renderer control is added.

The renderer also disposes a newly created WebView2 control if initialization or rendering fails after the control has been attached to the host.

Document streams remain owned by the caller. A concrete renderer borrows
the supplied source stream only for the lifetime of its `RenderAsync`
operation. It may read the stream while that task is in progress, but it
must neither dispose the caller-owned stream nor retain the source-stream
reference after the task completes. Renderer-owned controls and derived
presentation data follow their own view lifecycle and do not extend the
source-stream borrow.

This preserves a consistent ownership boundary across multiple renderer implementations.

### Imported HTML Preview Security and Profile Lifecycle

Imported `.html` and `.htm` documents are untrusted source documents. They
are not generated Markdown output and must not inherit Markdown renderer
options, URI rules, or generated-content assumptions implicitly.

The HTML preview has a dedicated security boundary, independent of
`MarkdownRenderingSecurityPolicy`. The implementation establishes the
following policy and lifecycle components:

- `HtmlDocumentRenderingSecurityPolicy` defines the initial-document
  navigation rule, embedded-image data-URI rule, resource authorization
  rule, and restrictive HTML Content Security Policy.
- `HtmlDocumentEmbeddedResourcePolicy` validates imported markup and CSS.
  It rejects prohibited constructs rather than attempting to rewrite
  untrusted content into a permitted document.
- `HtmlDocumentWebView2SecurityPolicy` configures the initialized WebView2
  instance, registers the resource-request filter, applies runtime
  restrictions, and owns its event subscriptions.
- `HtmlDocumentWebView2ProfileSession` owns the WebView2 control, its
  dedicated WebView2 environment, and its unique user-data folder.

These components establish the policy, validation and profile-lifecycle
primitives. The complete renderer integration that invokes the markup
validator, applies the generated Content Security Policy and coordinates
profile initialization with the initial document navigation is a separate
renderer concern. These components alone do not implement that renderer.

This is a format-specific presentation security boundary. It does not
change the document-format capability declaration or establish HTML
extraction or processing semantics.

#### Imported HTML and Embedded-Resource Validation

Imported HTML is treated as untrusted markup and must be validated before
it is passed to WebView2.

AngleSharp parses the markup with scripting disabled and without a
configured resource loader, so validation does not fetch external
content. The validator rejects documents containing prohibited constructs
rather than silently sanitizing or rewriting them.

The validator does not navigate to or display documents. The completed
renderer must invoke it before passing source markup to WebView2.

The preprocessing boundary rejects active or embedded document features,
including:

- script elements and inline event-handler attributes;
- frame, iframe and frameset elements;
- object, embed and applet elements;
- SVG and MathML content;
- canvas, portal and unsupported media elements;
- form elements and form controls;
- base elements and meta refresh navigation;
- resource-bearing attributes that are not explicitly supported;
- resource references through external links, local paths, UNC paths,
  `srcset`, and other unsupported element attributes.

Only the following embedded-resource form is permitted:

- an `img` element with a `src` attribute;
- a Base64 data URI using the exact `image/png` or `image/jpeg` MIME type;
- a syntactically valid Base64 payload;
- a decoded payload whose binary signature matches the declared PNG or
  JPEG format.

Other data MIME types, malformed Base64 payloads, SVG, GIF and other
animated image formats, and unsupported image encodings are rejected.
Declaring an allowed MIME type is not sufficient without a matching
binary signature.

The validator permits only fragment-form `href` values on supported
`a` and `area` elements; these do not identify an external resource.
Any resulting navigation remains subject to the WebView2 runtime
navigation policy. Other resource-bearing attributes and arbitrary
navigation references are rejected.

Inline style attributes and inline style elements are parsed and validated
without loading resources. CSS `url(...)` functions and imported stylesheets
are rejected. Unsupported or unknown CSS at-rules fail closed rather than
being silently discarded by parsing. Inline styles are presentation data;
they do not authorize resource loading or active browser capabilities.

This preprocessing boundary is authoritative for the source markup.
WebView2 runtime interception and Content Security Policy provide
additional independent enforcement and must not be treated as substitutes
for HTML and CSS validation.

#### HTML Preview Content Security Policy

The HTML preview security policy defines a restrictive Content Security
Policy with default-deny behavior. The completed renderer must apply the
generated policy to the document before its initial navigation.

The policy includes:

```text
default-src 'none'
script-src 'none'
object-src 'none'
frame-src 'none'
child-src 'none'
worker-src 'none'
connect-src 'none'
base-uri 'none'
form-action 'none'
img-src data:
font-src 'none'
media-src 'none'
manifest-src 'none'
style-src 'unsafe-inline'
```

The `img-src data:` directive is required for the explicitly approved
embedded PNG and JPEG images. It does not independently authorize every
data URI or every way of embedding an image. The HTML preprocessing
boundary and WebView2 resource policy continue to enforce the MIME,
Base64-payload, image-context and signature restrictions.

Inline styles are permitted for static presentation. CSS resource functions,
imported stylesheets and unsupported CSS rules remain prohibited by the
preprocessing boundary.

The Content Security Policy blocks the resource and active-content
categories listed above. Top-level navigation is enforced separately
by the WebView2 navigation policy; CSP is not relied on to provide
that control.

#### WebView2 Runtime Enforcement

The HTML preview uses a dedicated WebView2 configuration. It disables
capabilities that are not required for displaying static imported HTML,
including:

- JavaScript execution;
- WebView2 web messaging and native host-object access;
- default script dialogs;
- default context menus and DevTools;
- browser accelerator keys and zoom controls;
- the browser status bar and built-in error page;
- general autofill and password autosave.

The security policy registers a `WebResourceRequested` filter for all
resource contexts and request source kinds before the initial document is
loaded. A resource request is permitted only when it is an image request
using an approved embedded PNG or JPEG data URI. Other resource requests
receive an explicit HTTP 403 response.

The resource-request filter is defense in depth. It must not be interpreted
as a guarantee that every prohibited URI scheme, local-file attempt, or
browser activity will necessarily raise the same resource-request event.
The HTML preprocessing boundary, top-level navigation policy, Content
Security Policy and WebView2 event handlers remain separate controls.

The completed renderer must perform exactly one host-authorized initial
`NavigateToString` operation. The host issues a one-time authorization
immediately before the initial load. `NavigationStarting` consumes that
authorization on the first top-level navigation event, whether the event
is accepted or rejected. The navigation must represent the expected
`data:text/html` document and must not be a redirect. Subsequent top-level
navigation requests, including local-file navigation and unsupported
schemes, are rejected.

The runtime policy also:

- cancels navigation initiated within created frames;
- handles new-window requests without opening a new browser window;
- cancels and handles downloads;
- denies browser permission requests without saving the denial in the
  WebView2 profile;
- cancels external URI-scheme launch requests;
- cancels browser basic-authentication prompts;
- cancels client-certificate selection requests.

External opening remains an explicit document-level user action. It must
not result from automatic WebView2 navigation or from a URI scheme
embedded in untrusted HTML.

The `LaunchingExternalUriScheme` handler remains a defense-in-depth
control. The top-level `NavigationStarting` policy rejects subsequent
navigation before an external protocol handoff can be authorized. A
runtime test that verifies cancellation at `NavigationStarting` does not
by itself demonstrate that the later external-scheme event was raised.

#### Isolated Off-the-Record Profile

Imported HTML must not use the persistent/default WebView2 profile or the
profile used by the Markdown renderer or general application browsing.

Every HTML preview receives:

- a dedicated WebView2 environment;
- a unique user-data folder for that preview session;
- a dedicated HTML preview profile name;
- an InPrivate WebView2 profile.

The user-data folder is created under the DeskVault-specific HTML preview
profile directory beneath the current user's temporary directory. A
unique generated identifier distinguishes each preview folder. The
environment's actual user-data folder and the runtime's
`IsInPrivateModeEnabled` state are checked after initialization rather
than assuming that requested controller options were applied.

InPrivate mode is not, by itself, a no-retention guarantee. WebView2 still
uses a user-data folder, and browser-managed data may be written there
during the session. The unique folder and its explicit cleanup lifecycle
are therefore part of the security boundary.

The dedicated profile must remain isolated from the default WebView2
environment and the Markdown renderer's browsing profile. It must not be
reused by another preview session.

#### Profile Resource Ownership and Cleanup

`HtmlDocumentWebView2ProfileSession` owns the supplied WebView2 control,
the dedicated environment and the unique user-data folder. The HTML
renderer coordinates the lifetime of the security-policy object and the
profile session. It must apply the HTML-specific WebView2 security policy
after the profile session has initialized and before the initial
`NavigateToString` navigation.

The security-policy event subscriptions and resource filter must be
released before the profile session disposes its WebView2 control. The
renderer must therefore dispose the HTML WebView2 security policy before
disposing the profile session.

Profile cleanup is an ordered resource-lifecycle operation:

1. Stop remaining WebView2 navigation and resource activity.
2. Clear browsing data associated with the dedicated preview profile.
3. Dispose the owned WebView2 control.
4. Wait for WebView2 to signal that its browser-process collection has
   exited and released the resources associated with the profile.
5. Delete the unique user-data folder and verify that the folder is
   physically absent.

An empty process-information snapshot alone is not sufficient evidence
that WebView2 has released the profile's resources after initialization.
The environment's browser-process-exited lifecycle notification is the
authoritative process-exit signal. When initialization did not complete
and no browser process was started, cleanup may establish that no
browser-process collection exists to await.

A preview profile is not considered fully cleaned up merely because
`Dispose` was requested on the WebView2 control or because browsing-data
clearing was attempted. The control must be disposed, browser-process
termination must be established, and the unique user-data folder must
be absent.

Cleanup failures are observable through
`HtmlDocumentWebView2ProfileCleanupException`, which identifies the
cleanup stage that failed. Cleanup attempts the remaining safe teardown
operations so that one failure does not silently prevent all subsequent
cleanup work. An incomplete control, browser-process or directory-cleanup
operation remains eligible for a later disposal attempt. A failure must
not be represented as successful cleanup.

A cleanup exception remains observable even when later teardown work
succeeds in removing the user-data folder. Physical deletion is not
cryptographic zeroization and does not revoke content already copied
outside the profile by another process.

Creation and disposal of the profile session are bound to the owning
WinForms/WebView2 UI thread. The workspace remains responsible for its
host control and overall workspace lifecycle; it does not become the
owner of the browser profile, renderer-specific policy, or profile
cleanup operation.

#### Content-Retention and Diagnostics Boundary

The completed HTML renderer must pass document content through its
presentation input boundary and load it into the in-memory WebView2
document. It must not create a separate plaintext source-document file.

The isolated user-data folder may contain browser-managed data during the
preview session. After the preview session ends, cleanup must clear the
profile's browsing data, terminate the browser-process collection, and
remove the unique folder. The resulting no-retention verification is
based on the physical profile cleanup and inspection of disk-backed
preview data, not on an assumption that InPrivate mode alone prevents
writes.

Runtime retention tests use unique synthetic document-content and
embedded-image payload markers. They verify that the preview content is
rendered during the session, then inspect disk-backed preview data after
session cleanup to ensure those markers are not retained. The unique
profile folder must also be absent after successful cleanup.

Rejected navigation and resource requests must not disclose document
content, embedded payloads, original filenames, or physical filesystem
paths through user-facing diagnostics or logs. Cleanup failures are
surfaced through a typed exception that identifies the cleanup stage.
Callers must keep user-facing diagnostics and logs to the bounded stage
and failure category; they must not serialize raw underlying exception
details when those details could disclose document content or the
user-data folder path.

These guarantees apply to the resources and files owned by the HTML preview
profile lifecycle. They do not claim cryptographic erasure of arbitrary
memory copies, operating-system memory, or content that was independently
copied outside the profile.

#### HTML Policy Verification and Runtime Boundary

The HTML-specific policy requires automated unit and runtime coverage for:

- the one-time initial `data:text/html` navigation authorization;
- rejection of subsequent, redirected and unsupported top-level navigation;
- default-deny Content Security Policy directives;
- disabled JavaScript, web messaging and native host-object access;
- allowed PNG/JPEG Base64 data URIs with matching signatures;
- rejected unsupported MIME types, SVG, GIF and other animated formats;
- rejected malformed Base64 payloads and MIME/signature mismatches;
- rejected remote and local resource references in markup;
- rejected CSS `url(...)`, `@import`, and unsupported CSS rules;
- prohibited active elements, event handlers, frames, forms and media;
- WebView2 resource-request denial;
- frame navigation, popup and download restrictions;
- form-submission navigation rejection;
- local-file top-level navigation rejection;
- external URI-scheme navigation rejection;
- unique InPrivate profile configuration and separation from the
  default WebView2 user-data folder;
- document and embedded-image marker retention after profile cleanup;
- profile cleanup failure observability, retry and physical folder removal.

Policy-method tests and generated CSP assertions establish the
preprocessing and policy contracts. Actual WebView2 runtime tests verify
representative browser behavior and resource lifecycle outcomes.

The `NavigateToString` document has an `about:blank` location. A local
resource reference embedded in that document does not necessarily reach
`WebResourceRequested`; runtime resource interception must not be treated
as universal proof that every local-file attempt raises that event.
Local-file resource references are rejected by the HTML preprocessing
boundary, while the top-level local-file navigation guard is verified
through the actual WebView2 navigation event.

The external URI-scheme launch handler remains registered as a
defense-in-depth control. Since the top-level navigation policy rejects
the external navigation first, a runtime test that observes cancellation
at `NavigationStarting` does not claim to exercise the later
`LaunchingExternalUriScheme` event.

The permission handler denies requests and sets `SavesInProfile` to
false. The opaque `NavigateToString` origin does not provide a suitable
secure-context API for directly raising the permission event in the current
runtime test harness. The handler's direct runtime event path therefore
remains a separately identified verification limitation; production
configuration must continue to deny the request and avoid persisting the
decision.

The HTML security policy and profile lifecycle remain independent of the
Markdown security policy. Changes to the HTML policy must not weaken,
reuse, or implicitly alter the Markdown rendering options, URI rules,
Content Security Policy, or WebView2 profile.

The HTML preview remains a format-specific renderer concern behind
`IDocumentContentRenderer`. It does not move WebView2 into the workspace
presenter or view contract and does not change the separation between
document extraction, canonical document semantics, knowledge processing,
and presentation.

### External Document Opening Lifecycle

External document opening remains an explicit workspace action for formats
without an appropriate in-app renderer.

The `IDocumentViewer` boundary receives document content through the
existing caller-owned stream contract. `DocumentWorkspacePresenter`
remains the authoritative source-stream owner and lends the current
stream to `IDocumentViewer.OpenAsync` for that operation only. The
presenter keeps the stream valid until the returned task completes,
because the viewer may still be reading it while materializing its
separately owned protected temporary artifact.

If replacement, removal, session revocation, or presenter disposal
invalidates the stream while that borrow is active, the presenter
requests cancellation and defers physical disposal until the operation
completion path has discharged the borrow. The source stream is never
transferred to the viewer.

Completion of `IDocumentViewer.OpenAsync` ends only the source-stream
borrow. It does not establish external-application consumption
completion or authorize temporary-artifact deletion.

Temporary plaintext materialization remains an implementation detail
of the external viewer and must remain outside the workspace presenter
and view lifecycle.

The temporary artifact storage and ownership contract is defined by
ADR-0006. The workspace presenter and view must not create temporary files,
choose temporary paths, apply filesystem access-control rules, or own
temporary artifact cleanup.

`DocumentWorkspacePresenter` delegates the explicit external-open request
through `IDocumentViewer`. The viewer owns each temporary artifact through
its `TemporaryDocumentArtifactStore` and `TemporaryDocumentArtifact`
collaborators. The store creates artifacts in the dedicated per-user
`%LOCALAPPDATA%\DeskVault\TemporaryDocuments` namespace, applies the
protected Windows user access-control policy, and assigns generated
GUID-based artifact identities with validated filename extensions.

The viewer creates an artifact exclusively without overwriting an existing
file, copies the caller-owned source stream, flushes the content, and closes
its write handle before passing the artifact path to the external launcher.
The source stream remains caller-owned and is not disposed by the viewer.

The external viewing lifecycle is:

```text
Caller-owned document stream
        ↓
DocumentWorkspacePresenter
        ↓
IDocumentViewer
        ↓
TemporaryDocumentArtifactStore
        ↓
Protected temporary artifact
        ↓
Fully written, flushed and closed
        ↓
External application launch
        ↓
External lifecycle observation
        ↓
Temporary artifact cleanup
```

A successful external application launch is not proof that the external
application has finished consuming the file. The viewer attempts
completion-based cleanup only when its launch contract supplies a
trustworthy task representing the lifecycle of the application actually
consuming the document, and only after that task completes successfully.
A missing completion task, or a task that faults or is cancelled, is
unobservable and does not authorize cleanup. Unobservable artifacts remain
retained beyond viewer disposal for the separate recovery capability
defined by ADR-0006.

The Windows Shell launch boundary does not infer consumer completion from
the `Process` returned by `Process.Start` with `UseShellExecute = true`.
That process may represent a short-lived Shell activation or handoff and
may exit while an already-running browser or other associated application
continues to consume the file. The current Shell launcher therefore reports
no trustworthy external-consumer completion task. Disposing its process
wrapper does not terminate the external application.

The external application owns its windows and tabs. DeskVault does not
track, close, or terminate external browser tabs or processes. Users close
those tabs through the external application. Closing a tab is not observed
by the workspace or viewer and does not establish that the temporary
artifact can safely be deleted. A workspace or document presentation
closing is likewise not an external-consumer completion signal.

The exact artifact ownership, cleanup eligibility, viewer-disposal, and
recovery semantics are defined by ADR-0006. The workspace UI and temporary
artifact store must not infer that a Shell process handle, application
shutdown, or browser-tab closure proves that the external application has
finished consuming the file.

Creation, cancellation before external launch, copy, and launch failures
cause the viewer to attempt cleanup at the artifact owner boundary. Cleanup
returns an explicit success or failure outcome. A failed cleanup is logged
using the generated artifact identity and bounded failure category;
physical paths, original filenames, and document content are not included
in the diagnostic. The viewer retains ownership registration when cleanup
fails and removes it only after successful deletion, allowing a later owner
cleanup attempt to retry. Viewer disposal must not delete an artifact while
an external-consumer lifecycle is active or unobservable. Faulted or
cancelled completion observation is not successful completion.

The generated namespace and naming convention allow the separately owned
startup recovery work to identify DeskVault viewing artifacts without
scanning or cleaning the general operating-system temporary directory.
Startup stale-artifact enumeration and reconciliation are not implemented
by this workspace interaction decision.

The canonical encrypted `.dvault` artifact remains unchanged and is never
replaced by the temporary external-viewing representation. Filesystem
deletion is not cryptographic zeroization, does not revoke copies already
consumed by an external application, and does not guarantee cleanup after
abrupt process termination. These limitations remain explicit.

This lifecycle must not change the canonical encrypted document artifact or
move external-viewing responsibilities into the Application layer,
Infrastructure layer, workspace presenter, or workspace view.

### Renderer Registration and Composition

Format-specific renderers are registered through dependency injection.

The intended composition is:

```text
IDocumentContentRenderer
├── TextDocumentContentRenderer
├── MarkdownDocumentContentRenderer
└── CsvDocumentContentRenderer
```

The resolver discovers the registered implementations and selects the first renderer whose `CanRender` method accepts the document filename.

Renderer-specific dependencies remain inside the renderer boundary. The workspace presenter and view contracts must not acquire dependencies on Markdig, WebView2, CsvHelper, or other format-specific presentation technologies.

This allows future formats to be introduced without modifying the workspace orchestration:

```text
IDocumentContentRenderer
├── TextDocumentContentRenderer
├── MarkdownDocumentContentRenderer
├── CsvDocumentContentRenderer
├── PdfDocumentContentRenderer       ← future
├── DocxDocumentContentRenderer      ← future
└── ...
```

### Renderer Resolution and Execution Failure Boundaries

Renderer resolution and renderer execution are distinct presentation
operations and must have distinct failure outcomes.

When the resolver cannot find a registered renderer whose
`CanRender` method accepts the requested filename, it reports a
dedicated no-renderer outcome. The document workspace presents the
existing unsupported-preview message for this outcome. External
viewing remains an explicit user-controlled fallback.

Once a renderer has been selected and its `RenderAsync` operation
has been invoked, an exception is a renderer execution failure,
not evidence that the document format is unsupported. This includes
`NotSupportedException` raised by the renderer. Renderer execution
failures follow the existing safe document-open error and diagnostic
logging path; exception details and document content must not be
exposed in the user-facing error.

Cancellation and operation invalidation remain separate outcomes.
Caller cancellation, superseding document-open operations, document
presentation disposal, and protected security-session revision
invalidation must not be converted into unsupported-preview or
generic renderer-failure feedback. A late completion from an
invalidated operation must not publish content or display a stale
error or unsupported-format message.

A document preview is committed only after rendering completes
successfully and the presentation operation remains current.
A failed or cancelled rendering operation must not leave a partial
render host or its child controls presented as a successful preview.
Uncommitted rendering resources are disposed through the document
view's host lifecycle. Document streams remain caller-owned and
must not be disposed by a renderer.

## Unsupported Formats

When no in-app renderer is available, DeskVault will not automatically
launch the operating system's associated application.

Instead, the workspace will display an in-app message explaining that the
format is not currently supported for preview.

The user will explicitly choose whether to open the document externally.

Conceptually:

```text
┌────────────────────────────────────────────────────────────────────┐
│                                                                    │
│  Preview unavailable for this file type.                           │
│                                                                    │
│  DeskVault does not currently support                              │
│  in-app preview for this format.                                   │
│                                                                    │
│                    [ Open Externally ]                             │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

The explicit external-open action is a user-controlled fallback and does
not change the workspace's responsibility for document lifecycle or
presentation.

## Loading State

Opening a document may involve document retrieval, decryption, renderer initialization, and future processing operations.

The workspace will therefore provide an explicit loading state.

The preferred initial behavior is:

```text
┌────────────────────────────────────────────────────────────────────┐
│ report.pdf                                      [ AI ] [⋯]          │
│ Loading document...                                                │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│                  Preparing document preview...                     │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

The state model may later support more specific status messages such as:

```text
Loading document...
Preparing preview...
Loading pages...
Indexing...
Ready
```

The initial implementation should keep these states simple while preserving a path for more detailed processing states later.

## Workspace Lifecycle

Closing a workspace will use conditional confirmation.

If the workspace has no unsaved state, it should close without unnecessary confirmation.

If meaningful unsaved workspace state exists, DeskVault will ask the user to confirm before closing.

Conceptually:

```text
Close workspace
      ↓
Has unsaved state?
   ┌──┴──┐
  No     Yes
  ↓       ↓
Close   Confirm
```

For MVP 1, the workspace is primarily a viewing experience and does not
introduce persistent editable workspace state. Therefore, close should not
require confirmation solely because a document is open.

The conditional confirmation rule is retained for future workspace state
such as related-document changes, AI notes, or other editable configuration.

## Document and Workspace Lifecycle

Document lifecycle, workspace membership lifecycle, and document presentation
lifecycle are separate concepts.

For a temporary document session:

```text
Documents
    ↓
Open Document
    ↓
Temporary Workspace Presentation
    ├── Close Document → presentation closes, document remains
    └── Remove Document → document removal pipeline runs and presentation closes
```

For a persistent workspace:

```text
Persistent Workspace
├── Document A
├── Document B
└── Document C

Close Document Presentation
        ↓
Presentation closes
        ↓
Workspace membership remains

Remove Document from Workspace
        ↓
Membership removed
        ↓
Underlying document remains in Documents
```

A closed workspace member can be reopened from the workspace without being
added again.

Removing a document from a persistent workspace is therefore distinct from
deleting the underlying document.

## Protected In-App Presentation During Locked Sessions

An authenticated application security session is required to present protected
document content and session-bound workspace information inside the
application. Workspace and document presentations consume the authoritative
Application-owned `IApplicationSecuritySession` contract; they do not
establish an independent authorization state and must not infer authorization
from an open form, retained presentation, previously rendered content, or
cached metadata.

The authoritative authorization observation is
`IApplicationSecuritySession.CurrentSnapshot`. The immutable
`ApplicationSecuritySessionSnapshot` includes the session state and revision.
Presentation operations that can retain, create, or publish protected
workspace content are bound to the revision under which they begin.

Session-state notifications are delivered synchronously by the Application
session notification mechanism, but a notification may already be older than
the current authoritative snapshot by the time a UI consumer processes it.
UI consumers therefore marshal presentation changes to the UI thread and
re-read the authoritative current snapshot before applying a notification.
The notification payload alone is not sufficient authority to apply a
locked or authenticated presentation state.

For a protected document or workspace operation, the presentation consumer
must verify that protected access remains available and that the authoritative
current snapshot still has the same revision captured when the operation
started. This check applies to synchronous presentation entry points and to
asynchronous operations after every awaited boundary that precedes publishing
protected state. An operation from an older revision must not create or
attach a document presentation, activate a document or workspace, publish
workspace metadata or document content, restore prior view state, show a
success result based on stale protected data, or re-enable controls for a
revoked or superseded session.

The main application shell and workspace presentation have coordinated but
distinct responsibilities:

- `MainFormPresenter` observes the authoritative application security
  session, immediately invalidates pending search operations, clears
  retained search state, and marshals the latest authoritative snapshot to
  the main view.
- The main application view enters its locked presentation state when the
  current authoritative session is not authenticated.
- The workspace presentation manager owns all application-managed temporary
  and persistent workspace presentations and closes the managed
  presentations when protected access is withdrawn.
- Each workspace presentation clears its workspace identity, document
  membership presentation, navigation state and document presentations.
- Each document workspace presenter invalidates in-flight open operations,
  clears the document view, and releases its owned active and pending
  document streams. If the current stream is still borrowed by an
  in-flight `IDocumentViewer.OpenAsync` operation, cancellation is
  requested and physical disposal is deferred until that operation
  completes and discharges the borrow. UI-dispatched presentation cleanup
  still clears the document view and requests release; it does not dispose
  a source stream that is still in use by the canceled operation.

Locking is an in-memory presentation invalidation boundary. It does not
delete canonical documents, delete persistent workspaces, or remove
persisted workspace membership. Those durable objects continue to be owned
by their existing Application and persistence workflows.

After a successful re-authentication, the application does not restore the
previous in-memory workspace or document presentation. Main application
metadata and workspace metadata are loaded again through the existing
Application query boundaries. A fresh workspace/document presentation is
created only under the newly authenticated session revision. Persistent
workspace membership may therefore be presented again from authoritative
repository data without reusing the disposed presentation objects.

The document renderer remains responsible for its renderer-owned resources,
and the workspace/document presentation lifecycle remains responsible for
its own views and streams. A renderer must not become a new authorization
authority. The renderer is invoked only through a document presentation
operation that has passed the workspace/document authorization checks.

Disposal is a best-effort cleanup operation. Cancellation callback failures
must not prevent the document presenter from attempting to clear its view and
dispose active and pending streams. A failure while disposing one document
presentation must not prevent the workspace presentation from attempting
cleanup of its remaining document presentations and its workspace view. A
failure while disposing one workspace must not prevent the workspace
presentation manager from attempting cleanup of the remaining managed
workspaces and clearing its registry.

Cleanup failures are safely logged at the application security-session
presentation boundary so that a workspace-disposal exception does not prevent
the main application from remaining locked. Disposal code may surface the
first failure after attempting the remaining cleanup operations; the
security-session consumer contains and records that failure rather than
allowing it to interrupt lock-state application.

These rules define the in-app presentation lifecycle only. They do not
redesign the Application security-session state machine, encryption/key
lifecycle, document persistence, or external-viewing lifecycle. In particular,
this ADR does not claim that a lock can revoke plaintext or temporary
artifacts already handed to an external application, nor does it claim
cryptographic zeroization of all copies of content that may have existed in
process memory. The implementation minimizes retained in-app presentation
state and disposes resources it owns within the established ownership
boundaries.

### Presentation Lifetime and Event Subscription Ownership

`MainForm` owns the `MainFormPresenter`, search-debounce timer, and
`WinFormsUserActivityMessageFilter` instances that it creates or receives
for its own UI lifetime. It releases those resources through one idempotent
cleanup path for both normal form closure and direct form disposal. This
form-level cleanup does not own or dispose the shared Generic Host; host
lifetime remains with `Program.Main` as specified by ADR-0002.

`MainFormPresenter` owns the event subscriptions it establishes on each
`WorkspacePresentation`. It tracks the presentations to which it subscribes
and detaches its workspace event handlers before the corresponding
presentation is released. This applies to successful workspace close and
delete, replacement by a presentation associated with a newer
security-session revision, protected-presentation cleanup, and presenter
disposal. A closed, disposed, or superseded presentation must not retain
callbacks into the presenter.

`WorkspacePresentationManager` maintains a registry of live,
non-disposed workspace presentation instances keyed by workspace identity.
It coordinates lookup, creation, reuse, activation, and closure of those
managed presentations. A disposed presentation must not be returned by
lookup, reported as present, found through temporary-document lookup,
reactivated, or reused by a subsequent creation operation. Factories remain
construction-only and do not retain created presentation instances.

The manager removes an individual presentation from its registry before
beginning that presentation's disposal. It clears the complete registry
before beginning `CloseAll` or manager disposal. If disposal of one
presentation fails, cleanup attempts continue for the remaining selected
presentations. The first cleanup failure may be surfaced after the
remaining cleanup attempts have been made.

`WorkspacePresentation` owns its workspace-view and application
security-session event subscriptions, workspace presentation state, and
workspace view lifetime. It also owns the collection of
`WorkspaceDocumentPresentation` child wrappers. A cancellation callback
failure during disposal must not prevent the remaining event detachment,
workspace-state clearing, child-presentation release, or workspace-view
disposal. A cancellation callback failure during session revocation must
not prevent the attempt to schedule workspace cleanup on the owning UI
thread.

`WorkspaceDocumentPresentation` remains the sole owner of its document
presenter/view pair. The workspace presentation, presentation manager,
and main-form presenter do not introduce a second disposal owner for those
child presenters or views.

Search cancellation sources remain owned by their individual search
operations. `MainFormPresenter` invalidates and cancels an active search
when the presenter is disposed or protected access is withdrawn, but it
does not dispose a cancellation source while its operation may still be
using the associated token. The operation disposes its source from its
completion/finally path after it has finished using that token.

These ownership rules preserve deterministic teardown without introducing
an application-wide lifecycle framework, changing the three-form WinForms
Passive View MVP architecture, or transferring shared-host ownership from
the application composition boundary.

### Session-Bound Modal Dialogs

Information, warning, error, and confirmation dialogs are protected
in-app presentation surfaces. Their lifetime must not extend beyond
the authenticated session revision that authorized their display.

The presenter requesting a dialog supplies the exact
`ApplicationSecuritySessionSnapshot.Revision` captured by the operation
that initiated the request. The dialog must not acquire a new session
revision at display time, because doing so could associate a result
from an earlier operation with a later authenticated session.

The UI delegates modal dialog lifetime to
`ISessionBoundDialogCoordinator`, implemented by
`SessionBoundDialogCoordinator`. The coordinator is registered as a
singleton in the UI composition root and owns the lifetime of active
session-bound modal forms.

Before displaying a dialog, the coordinator verifies that:

- the authoritative current snapshot permits protected access; and
- the current snapshot revision exactly matches the revision supplied
  by the requesting operation.

The coordinator registers active dialogs and subscribes to
`IApplicationSecuritySession.StateChanged` while at least one
session-bound dialog is active. It re-reads the authoritative
`CurrentSnapshot` when processing notifications because a notification
may already be older than the current session state when delivered.

A dialog is invalidated when protected access is withdrawn or the
authoritative session revision differs from the revision that authorized
the dialog. Any newer revision invalidates the dialog, including a
newer authenticated revision. Re-authentication must not revive an
existing dialog or preserve an earlier confirmation request.

Invalidated dialogs are closed on the coordinator's owning WinForms UI
thread. Multiple active or nested dialogs are tracked and closed during
revocation cleanup. The coordinator assigns `DialogResult.Cancel` when
requesting closure and returns `Cancel` when the dialog's owning
revision is no longer current.

The coordinator also revalidates the registration after the modal
dialog loop returns. Consequently, an affirmative result that races
with a lock or newer session transition is converted to a non-affirmative
result and cannot authorize the operation that requested the dialog.

The in-app message form supports the required OK and Yes/No interaction
patterns. Confirmation defaults to the non-affirmative choice. The
calling presenter remains responsible for revalidating its captured
session revision after a confirmation returns and before continuing
with the protected operation.

The dialog coordinator is a presentation-lifecycle component only.
It does not authenticate users, own authorization state, or replace
the authoritative Application security-session contract. It does not
change the Application, Infrastructure, encryption/key-management,
document persistence, or external-viewing lifecycle boundaries.

## Primary Document Removal

The current workspace implementation does not assign a special primary
document role to persistent workspace membership.

If a future multi-document workspace introduces primary-document semantics,
the existing architectural rule remains applicable: removing a primary
document must not silently change the workspace's meaning.

If related documents remain, a future implementation may ask the user to
choose a replacement primary document. If no related documents remain, the
workspace may close because it no longer has a document context.

This behavior is a future multi-document workspace rule and is not required
by the current implementation.

## Workspace Persistence

DeskVault distinguishes between a temporary workspace session and a
persistent workspace.

Opening a document directly creates a temporary workspace presentation.

The temporary workspace may remain temporary if the user is simply viewing
the document.

The user may explicitly choose `Save as Workspace` to convert the existing
temporary workspace into a persistent workspace:

```text
Open Document
      ↓
Temporary Workspace
      │
      ├── Read / inspect
      │       ↓
      │    Close
      │
      └── Save as Workspace
                  ↓
        Persistent Workspace
```

The conversion uses the existing workspace identity and keeps the current
presentation rather than creating a second workspace.

Persistent workspaces then support explicit document membership management.
Closing an individual document presentation does not automatically remove
that document from the workspace.

Persistent workspace changes use explicit user actions rather than silently
turning every temporary document session into a persistent workspace.

A persistent workspace also retains the identity of its last actively
selected member document as a navigation checkpoint. This checkpoint
preserves the user's most recent document context when the persistent
workspace is opened again. It is workspace metadata, not a second source
of document membership or document content.

The checkpoint is updated when the user selects a different document
member in an open persistent workspace. Programmatic document opening,
workspace initialization, activation, and restoration do not constitute
user selection and must not overwrite the checkpoint. Removing the active
member applies the existing domain rule that clears the checkpoint.
Activating a remaining member as an automatic presentation fallback after
that removal is not a new user selection and must not repopulate the
checkpoint. Re-selecting the already active member does not require a
persistence operation.

The checkpoint is valid only while its document remains a member of the
persistent workspace. Restoration must validate the stored identifier
against the workspace's current valid membership and document state.
An absent, stale, or invalid checkpoint must not prevent the workspace
from opening; the workspace follows its normal valid-document selection
behavior instead.

Checkpoint writes are ordered so that an older asynchronous selection
cannot overwrite a newer user selection. Pending writes must also respect
the active security-session revision and must not publish stale state after
the relevant presentation or authorization lifetime has ended.

This narrow navigation checkpoint is an intentional persistence boundary.
It does not mean that every presentation change or every workspace-session
state is automatically persisted.

## Workspace Recovery

Persistent workspace state and active session state will remain conceptually separate.

Users explicitly save persistent workspace changes.

DeskVault may maintain recovery information for an active workspace session so that unexpected application shutdown does not unnecessarily destroy working context.

The intended model is:

```text
Persistent Workspace
        ↑
    Explicit Save
        ↑
Active Workspace Session
        ↓
Unexpected shutdown
        ↓
Auto-recovery
```

Recovery information should not be treated as authoritative workspace data.

## Workspace Naming

When a temporary workspace is saved as a persistent workspace, DeskVault
requires a workspace name and allows an optional description.

The save flow is explicitly presented as `Save as Workspace`.

The user controls the resulting workspace identity, which is subsequently
shown in the persistent workspace presentation.

For example:

```text
Save as Workspace

Name:
[ Project Alpha                 ]

Description:
[ Customer onboarding review   ]

[ Cancel ]          [ Save ]
```

The same workspace presentation remains open after a successful conversion.

## Workspace Discovery

The application shell will provide both `Recent` and `Workspaces` navigation areas.

The intended navigation is:

```text
Left Navigation
├── Documents
├── Recent
├── Workspaces
├── Search
└── Settings
```

`Workspaces` represents intentionally saved and organized knowledge contexts.

`Recent` represents recently interacted-with application objects.

The distinction is:

```text
Documents
→ What exists

Recent
→ What I recently interacted with

Workspaces
→ What I intentionally organized and saved
```

## Recent Activity

Recent activity will be activity-based rather than limited to document opening.

Recent items may represent:

```text
Recent
├── Workspace: Project Alpha
├── Document: report.pdf
├── Workspace: Contract Review
├── Document: notes.md
└── ...
```

The ordering will be based on the most recent meaningful interaction.

Meaningful interactions may include opening, activating, adding related documents, saving, and future workspace or AI activity.

Recent retention will use a configurable or policy-based model rather than hard-coding an architectural limit such as ten or fifty items.

The initial implementation may use a sensible default policy.

## Recent Persistence and Ownership

Recent activity will persist locally across application restarts, but it will be treated as a cache rather than authoritative application data.

Conceptually:

```text
Documents / Workspaces
        ↓
Authoritative application data

Recent Activity
        ↓
Persisted local cache
        ↓
Retention / cleanup policy
```

Recent activity will be associated with the authenticated user and stored locally on the machine.

Conceptually:

```text
Authenticated User
        ↓
User-specific Recent Activity
        ↓
Local persistent cache
        ↓
Retention Policy
```

If the Recent cache is cleared, rebuilt, or becomes unavailable, the underlying documents and workspaces remain unaffected.

The detailed Recent implementation is deferred to a later navigation/activity phase.

## Workspace Concurrency

The workspace architecture supports multiple simultaneous workspace
presentation contexts.

The current model is:

```text
MainForm
├── Workspace A presentation
├── Workspace B presentation
└── Workspace C presentation
```

Each workspace presentation maintains its own workspace identity and document
presentations.

If a workspace already has an active presentation, DeskVault activates the
existing presentation rather than creating a duplicate.

Direct document opening follows the same reuse principle for an already
active temporary presentation of that document.

The presentation mechanism remains independent of the underlying workspace
model and is not coupled to MDI.

## Workspace State Ownership and Passive View Boundary

For each live workspace presentation context, `WorkspacePresentation` is
the authoritative owner of the in-memory workspace presentation state. This
includes the workspace identity and type, workspace name and description,
the workspace-member identifiers represented by the presentation, the set
of created document presentations, the selected persistent-workspace
navigation mode, and the active document identity.

This is an in-memory presentation-state boundary. It does not transfer
ownership of durable workspace entities or persisted workspace membership
from the Domain and Application persistence workflows to the UI. The
`WorkspacePresentation` coordinates presentation updates and invokes the
existing Application handlers for workspace-level mutations; durable state
continues to be governed by the existing workspace model and persistence
contracts.

The active document identity has two related but distinct roles. During a
live presentation, `WorkspacePresentation` owns the currently active
document identity and coordinates document activation. For a persistent
workspace, the Application workspace workflow also stores the last
user-selected member document as a durable navigation checkpoint. The
Application handler and workspace repository own that durable update;
the UI presentation does not write workspace persistence directly.

Only a user-driven change to the selected workspace member updates this
checkpoint. Programmatic opening and restoration of the stored active
document must not be interpreted as a new user selection. The stored
identifier is validated against the current workspace membership before
it is used during workspace restoration.

`NavigationMode` remains presentation-lifetime state owned by
`WorkspacePresentation`. When an existing workspace presentation is
activated, the presentation must project its authoritative navigation mode
to the view before activating the workspace's document presentation.
This reapplication prevents a view's transient control state from replacing
the presentation's authoritative state during reactivation. Navigation
mode is not persisted across application restarts by this decision.

`WorkspacePresentationManager` maintains the registry of live workspace
presentation instances keyed by workspace identity. It coordinates lookup,
creation, reuse, activation, and closure of those managed presentations.
The manager owns the registry of presentation instances; it does not
maintain a second copy of each presentation's identity, membership,
navigation, or active-document state.

Workspace identity is not copied into a child `DocumentWorkspacePresenter`
or propagated through the document-presentation factory contract.
`IWorkspaceDocumentPresentationFactory.Create` accepts only `documentId`
and `securitySessionRevision`. `DocumentWorkspacePresenter` has no
`WorkspaceId` property or workspace-identity constructor parameter and
exposes no workspace activation or closure operation. Workspace identity
and workspace lifecycle therefore remain at the workspace-presentation
and manager boundary.

`WorkspaceViewForm` implements `IWorkspacePresentationView` and remains a
WinForms Passive View. It is responsible for rendering workspace identity,
temporary-document identity, workspace-member navigation, tabs/sidebar
layout, contextual menu state, workspace-level presentation actions, and
the transient control-hosting arrangement required to display the current
document presentation. Its internal references to tab pages, a
sidebar-hosted document, selection synchronization, and
temporary/persistent visual mode are view and layout state only. They must
not become an independent source of authoritative workspace membership,
navigation mode, or active-document identity.

The view reports user interactions through `IWorkspacePresentationView`
events. `WorkspacePresentation` handles workspace-level interactions,
updates its in-memory state, invokes the appropriate Application workspace
handlers, and raises workspace lifecycle requests to the application shell
where required. It then projects the resulting presentation state back
through the view contract. The Form does not receive workspace repositories
or Application command handlers to perform these operations itself.

`WorkspacePresentation` retains the `WorkspaceDocumentPresentation`
objects in its document-presentation collection. When adding a child
presentation to the view, it passes only the document identifier, optional
document filename, and generic `System.Windows.Forms.Form` hosting surface
through `IWorkspacePresentationView.AddDocumentPresentation`. The view
contract does not expose or receive the child wrapper,
`DocumentWorkspacePresenter`, or `IDocumentView`. `WorkspaceViewForm`
attaches and detaches the supplied Form for its layout; it does not become
the lifecycle owner or dispose the child presenter/view pair.
`WorkspaceDocumentPresentation` remains the sole disposal owner for that
pair.

`DocumentViewForm` implements `IDocumentView` and remains a WinForms
Passive View for the document-specific content surface. It is responsible
for hosting the active rendered document content, displaying the
unsupported-format preview and explicit external-open action, and raising
document-specific presentation requests through its view events. It does
not own workspace identity, workspace membership, workspace navigation,
workspace management actions, or the workspace contextual menu. The
visible workspace chrome and the temporary/persistent workspace action menu
belong to `WorkspaceViewForm`.

The child document header is a separate document-level presentation surface.
`WorkspacePresentation` controls its visibility through
`IDocumentView.SetDocumentChromeVisible(bool)`, using the authoritative
workspace type as the mode source. After the document presenter completes
opening and the workspace operation revalidates its captured security-session
revision, the workspace presentation applies the child-header visibility
before attaching the generic Form hosting surface.

For a temporary workspace, the child document header is hidden because the
outer workspace presentation already identifies the directly opened document
and exposes the applicable document-level requests and temporary-session
actions. For a persistent workspace, the child document header remains
visible because the outer workspace header identifies the workspace while
the child header identifies the active document and exposes its
document-specific presentation actions.

A successful Save as Workspace conversion updates the existing
`WorkspacePresentation` in place. After applying the persistent workspace
identity to the outer view, it restores child document-header visibility on
each already-presented document view before publishing
`WorkspaceDetailsUpdated`. The existing child presenters and views are
reused; conversion does not recreate their presentations. Newly opened child
presentations receive visibility based on the current workspace type before
they are hosted.

`DocumentViewForm` renders the requested visibility through its explicit
view contract. It does not infer workspace mode from its WinForms parent,
inspect parent controls, or depend on a concrete workspace Form type.

The document-interaction contract and the WinForms hosting surface are
separate. `WorkspaceDocumentPresentation.View` exposes document-specific
behavior through `IDocumentView`, while its `HostedForm` property exposes
the generic `System.Windows.Forms.Form` surface used for workspace hosting.
Both properties refer to the same underlying view instance; the hosting
property does not create a second document view or a separate top-level
window.

`WorkspaceDocumentPresentationFactory` may construct the concrete
`DocumentViewForm`. It supplies that same instance to
`DocumentWorkspacePresenter` through `IDocumentView` and to
`WorkspaceDocumentPresentation` as the document view. This keeps concrete
`DocumentViewForm` construction knowledge within the factory rather than
exposing that implementation dependency to workspace hosting.

`WorkspaceDocumentPresentation` remains a non-activating wrapper for one
document presentation. It exposes the document presenter, document view,
and generic WinForms hosting surface; it does not forward a workspace
activation API to the document presenter. Its existing disposal path
continues to dispose the presenter and its view. The identity-contract
change does not create another disposal owner or alter that lifecycle.

`WorkspaceViewForm` uses only the generic `HostedForm` surface when
preparing, attaching, removing, activating, and moving the document
presentation between its tab and sidebar containers. It does not cast the
document interaction contract to `DocumentViewForm` or search hosted
controls for that concrete type. Moving between tab and sidebar hosting
reparents the same Form instance.

The generic Form hosting surface is a WinForms presentation boundary only.
It does not add Form, parent-control, or workspace-navigation members to
`IDocumentView`, move navigation ownership into the document view, or
change the authoritative workspace state held by `WorkspacePresentation`.
Exposing the host Form does not introduce another document view or another
window.

The hosting boundary does not create a new disposal owner or redefine the
document and workspace presentation lifetime contracts. The navigation-
restoration/persistence policy and broader presentation disposal and
lifetime contracts remain separate responsibilities.

Workspace membership and document presentation remain distinct. Closing
an individual document presentation removes that presentation from the
current workspace view but leaves persistent workspace membership intact.
Removing a document from a persistent workspace is a workspace-membership
operation; it does not delete the underlying document from the document
library. Global document removal is a separate document-level operation.
The document-specific removal pipeline remains owned by the document
workspace presentation. Upon successful global removal,
`DocumentWorkspacePresenter` raises its `DocumentRemoved` event.
`WorkspacePresentation` subscribes to that event when the child presentation
is registered. It identifies the affected child presentation, removes and
disposes that presentation, removes the document identifier from its
in-memory workspace membership, and publishes the corresponding workspace
removal and membership-update notifications. For a temporary workspace, it
also raises the workspace-close request. This keeps document removal
execution document-specific while workspace membership reconciliation and
temporary-workspace closure remain with the workspace owner.

Document information, global document removal, external viewing, and
format-specific rendering remain document-level behavior delegated to the
active document presentation and its document workspace presenter/view and
associated viewer or renderer collaborators. `WorkspacePresentation` may
select the active document presentation and route a document-specific
request to it; it must not absorb document metadata processing, document
removal execution, external-viewing lifecycle, or document rendering.

Temporary and persistent workspace behavior remains distinct. A temporary
workspace presents the directly opened document and exposes document-level
requests and temporary-session actions through `WorkspaceViewForm`, without
persistent-workspace navigation choices. Its child document header is
hidden because the outer temporary presentation already provides the
document identity and applicable document actions.

A persistent workspace supports multiple workspace members, workspace-level
membership and management actions, and the Tabs or Sidebar navigation
choice. Its outer header identifies the workspace, while the child document
header remains visible to identify the active document and expose its
document-specific presentation actions. These are separate identity and
action surfaces rather than duplicated workspace chrome.

The workspace presentation owns the navigation-mode value; the Form renders
the selected mode and reports user changes through its view event.
Conversion from temporary to persistent workspace continues to update the
existing presentation rather than creating a second presentation. After
the persistent workspace identity has been applied, the existing child
document headers are restored before `WorkspaceDetailsUpdated` is published.

This boundary defines state ownership, interaction flow, and the separation
between document interaction and WinForms hosting. Persistent active-document
checkpointing is performed through the Application workspace workflow and
is limited to user-driven member selection. Navigation-mode restoration
remains an in-memory presentation responsibility. These decisions do not
change the broader presentation disposal and lifetime contracts.

## Scope of Initial Implementation

The current implementation phase establishes the concrete workspace
presentation and rendering boundaries.

Implemented capabilities include:

- temporary single-document workspace sessions
- persistent named workspaces
- multiple documents within persistent workspaces
- Tabs default and Sidebar alternative navigation
- multiple simultaneous workspace presentation contexts
- activation/reuse of existing presentations
- separation of document presentation from workspace membership
- explicit Save as Workspace conversion
- document information and removal behavior
- TXT, Markdown, and CSV rendering
- imported HTML WebView2 security-policy, embedded-resource validation
  and isolated-profile lifecycle infrastructure; complete HTML renderer
  integration remains deferred
- explicit unsupported-format handling and external opening

The following remain architectural targets or deferred capabilities:

- functional local AI processing
- retrieval-augmented generation
- Recent activity implementation
- automatic workspace recovery
- PDF renderer
- DOCX renderer
- complete imported HTML/HTM renderer integration
- primary-document semantics in a future model that requires them

The implementation establishes the correct boundaries without prematurely
implementing all future functionality.

## Alternatives Considered

### Use a legacy WinForms `WebBrowser` control for Markdown

Rejected.

Although it would provide a quick HTML presentation path, it would tie the modern Markdown workspace to a legacy browser technology. WebView2 provides a more appropriate current Windows presentation surface while remaining an implementation detail of the renderer.

### Make `DocumentViewForm` directly depend on WebView2

Rejected.

The workspace form must remain independent of the Markdown presentation technology. Direct WebView2 coupling would make the workspace harder to extend and would blur the renderer boundary.

### Create a reusable `MarkdownViewerControl` immediately

Deferred.

The first implementation will keep WebView2 inside `MarkdownDocumentContentRenderer`. A reusable viewer control can be extracted when the Markdown presentation surface develops enough behavior to justify the additional abstraction.

### Render Markdown entirely with native WinForms controls

Rejected for the initial Markdown implementation.

A native renderer would require DeskVault to implement and maintain substantial formatting and layout behavior for headings, lists, code blocks, tables, links, and related Markdown features. Using Markdig plus WebView2 keeps that UI infrastructure focused while preserving the renderer boundary.

### Trust imported Markdown as executable/active content

Rejected.

Imported documents are treated as untrusted content. Rendering must use a controlled policy and must not automatically load remote resources.

### Automatically persist every opened document as a workspace

Rejected.

Most document openings are expected to be simple viewing sessions. Automatically persisting every document would create unnecessary workspace state.

A temporary workspace that becomes persistent when the user intentionally saves or enriches it provides a cleaner user experience.

### Automatically promote another document when the primary document is removed

Rejected.

Silently changing the primary document could change the meaning of the user's workspace.

The user should explicitly select the new primary document.

### Always confirm workspace closure

Rejected.

Confirmation for every workspace close would create unnecessary friction for simple document viewing.

Conditional confirmation provides protection only when meaningful unsaved state exists.

### Automatically save every workspace change

Rejected as a general workspace-persistence policy.

Automatic persistence of every presentation or workspace change would blur
the distinction between temporary session state and intentional persistent
workspace configuration.

A narrowly scoped exception is made for the last actively selected document
in an already persistent workspace. This is a navigation checkpoint rather
than automatic persistence of arbitrary workspace changes. It is updated
only for user-driven member selection, through the Application workspace
workflow, and is validated against current membership during restoration.

Explicit save remains the mechanism for converting a temporary workspace
into a persistent workspace and for intentional persistent workspace
configuration changes.

### Automatically open unsupported formats externally

Rejected.

Silent external application launches reduce user control and interrupt the unified workspace experience.

An explicit `Open Externally` action provides a safer and clearer fallback.

### Implement Recent immediately

Deferred.

Recent activity has been designed so that its future behavior is clear, but implementation is deferred to avoid expanding the current document workspace vertical slice unnecessarily.

### Implement multiple workspace windows immediately

Deferred.

The architecture will support multiple workspaces, but the first implementation will manage one active workspace to reduce initial UI and lifecycle complexity.

## Consequences

### Positive

- `WorkspaceViewForm` owns workspace identity, navigation, and contextual
  interaction, while `DocumentViewForm` owns document-content presentation.
- Document-specific presentation requests remain routed through the active
  document presentation and its presenter.
- Temporary document sessions remain focused on the opened document while persistent workspaces support multiple document members.
- The AI assistant has a defined adaptive interaction pattern.
- Document rendering is separated behind a resolver and renderer boundary.
- CSV is an implemented structured/grid renderer rather than a future renderer.
- CSV parsing preserves structural information before presentation.
- Bounded CSV previewing is represented explicitly through `HasMoreRows`.
- CSV parsing configuration is externally configurable through `CsvParsingOptions`.
- Unsupported formats have an explicit and user-controlled fallback.
- Loading states provide clear feedback during document preparation.
- Document lifecycle is separated from workspace lifecycle.
- Persistent workspace membership can survive closure of an individual document presentation.
- Workspace membership removal is distinct from deletion of the underlying document.
- Temporary document sessions do not automatically create persistent workspaces.
- Persistent workspace naming remains user-controlled.
- Recent activity has a clear future model without expanding the current implementation.
- Multiple simultaneous workspace presentation contexts are supported without coupling workspace identity to MDI.
- Workspace presentation state has one authoritative in-memory owner, while the WinForms workspace view remains Passive View and retains only transient UI hosting/layout state.
- The last user-selected document in a persistent workspace is retained as a validated navigation checkpoint through the Application workspace persistence boundary.
- Programmatic opening and restoration do not overwrite the user's persisted active-document checkpoint.
- Asynchronous checkpoint updates are ordered and fenced against stale selections and superseded security-session revisions.
- Existing workspace presentations reapply their authoritative in-memory navigation mode when activated.
- Navigation mode remains presentation-lifetime state and is not persisted across application restarts.
- Workspace-level actions and document-level requests remain routed through their respective presentation and Application boundaries.
- New document renderers can be added without changing workspace orchestration.
- Markdown rendering can evolve independently of the workspace contract.
- Markdig provides a mature Markdown parsing boundary while WebView2 provides a rich presentation surface.
- Markdown security/resource policy remains inside the renderer boundary.
- Imported HTML has a separate security policy and profile lifecycle rather than inheriting Markdown-specific rendering assumptions.
- The HTML policy validates embedded PNG/JPEG data, rejects unsupported active content and resource references, and applies dedicated WebView2 runtime restrictions.
- HTML preview sessions use unique InPrivate profiles with explicit browsing-data cleanup, browser-process exit coordination and physical user-data-folder deletion.
- Profile cleanup failures remain observable; incomplete control, process-exit or folder-deletion cleanup can be retried without treating an earlier failure as successful teardown.
- Rendering and future extraction/AI processing remain separate concerns.
- The current implementation remains intentionally scoped.
- Protected in-app document and workspace presentations are bound to the authoritative application security-session state and revision.
- Locking invalidates pending presentation operations and removes retained in-app protected presentation state.
- Stale asynchronous results cannot republish document or workspace content after their authorization revision is superseded.
- Re-authentication creates fresh presentation state from authoritative repositories rather than restoring prior in-memory presentations.
- Persistent document and workspace data remain separate from the in-memory presentation cleanup lifecycle.
- Presentation and manager disposal attempt remaining cleanup operations when an individual resource disposal fails.
- The main form releases its owned presenter and UI-lifetime resources through one idempotent cleanup path for normal closure and direct disposal.
- Main-form presenter workspace-event subscriptions are tracked and detached before their presentations are released, replaced, or cleared.
- The workspace presentation manager exposes only live, non-disposed presentations and removes registry entries before disposal.
- Workspace cancellation callback failures do not prevent remaining presentation cleanup or the attempt to schedule session-revocation cleanup.
- Search cancellation sources remain owned by their individual operations and are released only after those operations complete.
- External document materialization remains owned by `IDocumentViewer`, separate from workspace and document presentation ownership.
- The explicit external-open workflow uses the protected per-user artifact namespace and ownership contract defined by ADR-0006.
- Artifact cleanup failures are reported without silently relinquishing ownership, and a later owner cleanup attempt can retry deletion.
- Windows Shell process exit is not treated as consumer completion;
  unobservable external-viewing artifacts remain retained beyond viewer
  disposal for separate recovery.
- External browser tabs remain owned and closed by the external application;
  DeskVault does not terminate browser processes or treat tab closure as a
  cleanup signal.

### Negative

- Additional workspace state and lifecycle concepts increase application complexity.
- Renderer resolution introduces additional Application/UI abstractions.
- Persisting the active-document navigation checkpoint adds an Application workflow and ordered asynchronous persistence coordination.
- Navigation mode remains transient; restoring it across application restarts would require a separate persistence decision.
- Future broader workspace persistence and recovery will require additional storage design.
- Multi-document workspace management will require additional UI and domain modeling.
- Recent activity will require a separate persistence and retention implementation.
- AI functionality will require additional Application and AI-layer capabilities.
- Some document formats will initially lack in-app rendering.
- WebView2 adds a UI/runtime dependency for rich Markdown presentation.
- Markdown rendering requires an explicit security policy for HTML, links, and remote resources.
- Imported HTML requires a separate markup, embedded-resource and WebView2 runtime security policy; Markdown policy options cannot be reused as a substitute.
- InPrivate mode alone does not guarantee that profile data is absent from disk; the unique profile folder requires explicit cleanup and browser-process teardown.
- Profile cleanup introduces asynchronous resource ownership, process-exit coordination, physical deletion and observable failure/retry behavior.
- A filesystem deletion failure may leave preview-profile data on disk until a later cleanup attempt succeeds; the failure must remain observable and must not be represented as successful cleanup.
- Filesystem deletion is not cryptographic zeroization and cannot erase copies already made outside the preview profile.
- The WebView2 runtime does not expose every prohibited resource attempt through a single event. Preprocessing, navigation enforcement, CSP and resource interception must remain independent controls.
- Structured document representations add parsing and testing complexity before rendering.
- Bounded previews require explicit signaling so the UI does not mistake a partial representation for a complete document.
- Session-revision checks add lifecycle coordination to asynchronous presentation operations.
- Lock-time teardown requires coordinated cleanup across the main application view, workspace presentations, document presentations, and their owned streams.
- Best-effort disposal must preserve failure diagnostics while ensuring that one cleanup failure does not prevent attempts to release other owned resources.
- Temporary plaintext artifacts can remain on disk when external-consumer
  completion is unobservable, deletion fails, or DeskVault terminates before
  cleanup; deletion is not cryptographic erasure. External browser tabs
  remain outside DeskVault's control.
- The user-scoped filesystem access-control boundary does not isolate the temporary artifact from other processes running as the same Windows user.
- Startup stale-artifact discovery and reconciliation require a separate recovery capability and are not provided by the workspace interaction layer.

These trade-offs are acceptable because they establish a scalable foundation while keeping the first implementation focused.

## Result

DeskVault will implement an application-managed workspace presentation
that supports temporary single-document viewing and persistent
multi-document knowledge contexts.

The resulting UI direction is:

```text
Login
    ↓
Application Shell
    ↓
Document Library
    ↓
WorkspaceViewForm
    ├── Workspace/document identity and navigation
    ├── Workspace/document contextual actions
    ├── DocumentViewForm
    │   └── Document Renderer Boundary
    │       ├── Text
    │       ├── Markdown
    │       └── CSV
    └── Future Adaptive Local AI Assistant
             ↓
        Save as Workspace
             ↓
      Persistent Workspace
        ├── Document
        ├── Document
        └── Document
```

The architecture separates:

```text
Document lifecycle
        ≠
Workspace membership lifecycle
        ≠
Document presentation lifecycle
        ≠
Recent activity
        ≠
AI context
```

The renderer architecture additionally separates:

```text
Document parsing / extraction
        ≠
Document rendering
        ≠
AI / search processing
```

The existing Application and Infrastructure boundaries remain responsible
for document retrieval, decryption, persistence, and storage.

Markdown and imported HTML remain separate format-specific presentation
security boundaries. Imported HTML uses its dedicated WebView2 policy and
isolated profile lifecycle; completion of the renderer integration remains
a separate presentation concern. These controls do not change the canonical
document-processing and persistence boundaries.

This separation allows DeskVault to evolve toward multi-document workspaces,
local AI, retrieval, RAG, and enterprise knowledge workflows without
requiring the document-content view to own workspace behavior or future
AI-processing capabilities.
