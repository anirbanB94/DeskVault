# ADR-0014: Application Security Session Lifecycle and Authorization Boundary

## Status

Accepted

## Context

DeskVault stores document content and database data using separate
encryption and key-management boundaries. Those protections establish
confidentiality at rest, but they do not by themselves define whether the
running application is currently authorized to expose protected vault
content.

The application requires an authoritative runtime security-session
boundary that can coordinate authentication, re-authentication, locking,
and the withdrawal of protected access.

This boundary is consumed by multiple application capabilities and must
not depend on WinForms, a particular authentication provider, or the
underlying document and database encryption implementations.

The lifecycle also has to account for asynchronous authentication
operations. An authentication attempt may remain in flight while another
operation locks the application or establishes that a required security
capability is unavailable. A late successful completion must not restore
access after the later security transition.

Repeated and competing transitions must therefore have deterministic
semantics, and consumers must be able to observe state changes without
relying on timing assumptions.

## Decision

DeskVault will maintain one authoritative, in-memory application security
session per Application dependency-injection service provider.

The Application layer owns the session contract and its state-transition
coordination. The session is registered as a singleton through the
Application composition root.

The boundary consists of:

- `IApplicationSecuritySession`, defined in
  `DeskVault.Application.Interfaces`;
- `ApplicationSecuritySession`, implementing the contract in
  `DeskVault.Application.Security.Sessions`;
- immutable session snapshots;
- explicit authentication outcomes and session-bound authentication
  attempt tokens;
- revision-ordered state-change notifications.

The session remains framework-neutral. It does not depend on WinForms,
Windows session APIs, Infrastructure implementations, or cryptographic
key services.

### Authoritative Session States

The session supports the following states:

| State | Meaning | Protected access |
|---|---|---|
| `Unauthenticated` | No successful authentication has been established for the current application session. | Denied |
| `Authenticating` | An authentication or re-authentication attempt is in progress. | Denied |
| `Authenticated` | Authentication has succeeded and the application session authorizes protected access. | Allowed |
| `Locked` | Protected access has been explicitly withdrawn and authentication is required again. | Denied |
| `Unavailable` | A required security capability or condition cannot be established reliably. | Denied |

Protected access is available exclusively in the `Authenticated` state.

The absence of an authentication failure does not constitute authorization.
Unauthenticated, authenticating, locked, and unavailable states all remain
non-authorized.

The session starts in `Unauthenticated` with revision `0`. Session state is
runtime-only and is not restored as authenticated from persisted workspace
or application data after a new application session is created.

### Allowed State Transitions

Authentication can begin only from `Unauthenticated` or `Locked`.

Authentication from `Locked` is re-authentication and is the operation
that can restore protected access after a lock.

The allowed transitions are:

| Current state | Operation or outcome | Resulting state |
|---|---|---|
| `Unauthenticated` | Begin authentication | `Authenticating` |
| `Locked` | Begin authentication | `Authenticating` |
| `Authenticating` | Authentication succeeds | `Authenticated` |
| `Authenticating` | Authentication fails | Previous non-authorized state |
| `Authenticating` | Authentication is cancelled | Previous non-authorized state |
| `Authenticating` | Authentication capability is unavailable | `Unavailable` |
| Any non-unavailable state | Lock | `Locked` |
| Any state | Required security capability becomes unreliable | `Unavailable` |
| `Unavailable` | Availability is independently re-established | `Locked` |

A failed or cancelled authentication attempt returns to the state from
which that attempt began. Consequently, failure or cancellation during
re-authentication leaves the application locked.

An authentication outcome of `Unavailable` does not grant access.

An `Unavailable` session cannot be unlocked by starting authentication
directly. The caller must first establish that the required security
capability or condition has recovered and explicitly restore availability.
Restoration moves the session to `Locked`, never directly to
`Authenticated`. A new successful authentication is still required.

A lock request does not clear an unresolved `Unavailable` condition.
Repeated lock and unavailable requests are idempotent when the
authoritative state does not change.

### Immutable Snapshots and Revisions

Consumers obtain the current state through an immutable
`ApplicationSecuritySessionSnapshot`.

Every actual state transition receives a monotonically increasing
revision. The initial revision is `0`; a transition increments the
revision exactly once.

An operation that leaves the authoritative state unchanged does not
increment the revision and does not emit another state-change
notification.

The revision identifies the ordering of session-state observations. It is
not a persisted processing generation, document identity, encryption-key
version, or account identifier.

### Authentication Attempt Identity and Stale Completion

Each authentication attempt receives an opaque token bound to:

- the security-session instance that created it;
- the revision at which authentication began;
- the non-authorized state from which the attempt originated.

Only the currently active attempt may complete the current
authentication operation.

Locking or marking the session unavailable invalidates the active
attempt. A completion from an invalidated attempt, an earlier revision,
an already completed attempt, or another session instance is rejected
without changing the current state.

A stale successful authentication result must never restore protected
access after a later lock or unavailable transition.

The session coordinator does not depend on cancellation being successfully
observed by an external authentication provider to preserve this invariant.
Attempt identity and authoritative state determine whether a completion
can be accepted.

### Concurrency and State Notifications

State transitions are serialized by the session coordinator's private
synchronization boundary.

The coordinator commits the new state and revision before publishing its
notification. State-change notifications are queued and dispatched in
increasing revision order.

Each notification carries the immutable snapshot associated with that
transition. A consumer must not use an older notification to overwrite a
newer revision it has already observed.

A notification observer is not permitted to reverse or roll back a
committed state transition by throwing an exception. Observer failures
must not prevent the committed transition from remaining authoritative
or prevent other observers from receiving subsequent notifications.

Repeated lock requests and competing authentication completions must
therefore converge on one authoritative state rather than relying on
thread scheduling or UI event ordering.

Notification handlers execute synchronously on the thread that drains the
notification queue. The coordinator does not capture or marshal to a UI
synchronization context. UI consumers are responsible for marshaling
presentation updates to the appropriate thread and must compare snapshot
revisions before applying state.

A transition may be committed and its notification queued while another
thread is already draining the notification queue. In that situation,
the operation that commits the transition does not wait for the existing
dispatcher to finish processing all queued notifications. The active
dispatcher continues to deliver the queued snapshots in revision order.

Consequently, a lock operation can return after the authoritative session
has become `Locked` even when its notification is still queued behind an
earlier notification. A subsequent lifecycle operation may begin after
the lock operation returns. Its state transitions and notifications are
still committed and delivered in revision order after the lock transition.

Notification delivery is therefore an ordered observation mechanism,
not a requirement that every observer finish processing a transition
before another valid session transition may begin.

### Authentication and Cryptographic Responsibilities

The security session represents runtime authorization state. It does not
perform authentication itself and does not implement password validation,
credential storage, Windows account authentication, or another
authentication mechanism.

The authentication integration is responsible for obtaining an
authentication outcome and submitting it against the corresponding
session-bound attempt.

The session boundary is also separate from document and database
encryption.

It does not:

- generate, replace, rotate, or provision encryption keys;
- decrypt document content;
- change document or database encryption formats;
- replace strict existing-key retrieval;
- treat authentication failure as permission to recreate key material;
- persist authentication state as a substitute for protected key storage.

Existing document and database encryption and key-protection decisions
remain authoritative.

### Application and Presentation Boundaries

The Application layer owns the session contract and state coordination.

The UI and other protected-access consumers may observe the session
through the Application abstraction. They must not implement separate
authoritative authentication state or bypass the Application boundary
to determine whether protected access is available.

The session coordinator establishes the shared authorization state, but
it does not by itself implement every enforcement action required when
access is withdrawn.

The Application layer provides
`IApplicationSecuritySessionLifecycleService`, implemented by
`ApplicationSecuritySessionLifecycleService`. It coordinates runtime
lock and unlock operations using the authoritative session and the
existing `IApplicationAuthenticationService`. The service is registered
as a singleton through the Application composition root.

The lifecycle service does not introduce another authorization state or
replace the session's state-transition rules. Its operation generation is
private coordination state used only to determine whether an unlock
operation has been superseded by a newer lock request.

An unconditional lifecycle lock request advances that generation even
when the session is already `Locked` or remains `Unavailable`. It
invalidates and cancels any active unlock operation before applying
`IApplicationSecuritySession.Lock()`.

The revision-conditional `TryLock(expectedRevision, out snapshot)`
operation advances the generation and invalidates an active unlock only
when the expected revision still identifies the current authenticated
session and the conditional session transition succeeds. A stale
conditional request returns the current snapshot without superseding a
newer unlock operation.

A competing unlock is rejected while the lifecycle lock request is
executing. This includes notification dispatch performed synchronously
within that lock call. The lock operation does not, however, wait for an
already-active notification dispatcher to finish draining notifications
that were queued by the lock transition.

Once the lifecycle lock call returns, another unlock may begin even if
the lock notification remains queued. The authoritative session state
has already been committed, and the session notification coordinator
preserves revision-ordered delivery of the lock and subsequent unlock
transitions.

Unlock is permitted only from `Locked` or `Unavailable` and delegates
verification to the existing authentication service. That service remains
responsible for provider availability, authentication-attempt creation,
outcome mapping, and session completion. A failed or cancelled
re-authentication therefore remains non-authorized. Recovery from
`Unavailable` still requires restoration to `Locked` followed by a new
successful authentication.

An unlock result is accepted by the lifecycle service only while its
operation generation remains current. A late success from an operation
superseded by a newer lock request is reported as superseded and cannot be
treated as completion of the current unlock operation. Cancellation is a
coordination signal; the authoritative session's attempt identity and
state-transition rules remain the final protection against stale
authentication completion.

The lifecycle service does not own protected document or database key
availability, automatic-lock policy, Windows workstation security
events, in-app presentation cleanup, suppression of late asynchronous
presentation results, or integrated verification of the complete access
boundary. Those remain separate responsibilities.

### Workstation Security Monitoring and Readiness

Workstation security monitoring is a required runtime security capability
for MVP 2. It observes supported security transitions affecting the Windows
session in which DeskVault is running and routes access-withdrawal events
through the existing Application lifecycle. It is not an authentication
provider, an independent authorization state machine, or an alternative
owner of the application security session.

The Application layer defines the framework-neutral
`IWorkstationSecurityEventSource`, `IWorkstationSecurityMonitor`, and
`IWorkstationSecurityReadiness` contracts. The Application
`WorkstationSecurityMonitor` implements the monitor and readiness
contracts through one singleton instance. Infrastructure supplies the
Windows notification adapter. The UI composition root registers the
monitoring hosted service with the existing shared Generic Host; it does
not create another host or another application session.

The readiness snapshot is immutable and revisioned. Its states are:

| State | Meaning | Authentication permitted |
|---|---|---|
| `NotStarted` | Monitoring has not been initialized. | No |
| `Starting` | WTS window registration, independent power-callback registration, and initial current-process session reconciliation are being initialized. | No |
| `Ready` | WTS notification registration and independent power-callback registration have succeeded, the initial current-process WTS snapshot has been validated, and the reconciliation worker is operational. | Yes, subject to Windows Hello availability and successful verification |
| `Unavailable` | Monitoring initialization or an established monitoring capability has failed. | No |
| `Stopped` | Monitoring has stopped for application shutdown. | No |

Only `Ready` permits authentication. The Application authentication
service checks readiness before Windows Hello availability is evaluated,
and fences the availability result, session-bound authentication attempt,
provider invocation, and provider completion to the same readiness
revision. A result obtained while readiness is unavailable or has changed
cannot establish authenticated access. Windows Hello reporting
`Available` does not override a workstation readiness state other than
`Ready`.

The shared host starts the workstation monitoring hosted service before
startup authentication. The hosted service awaits WTS window registration,
independent suspend/resume callback registration, and the initial validated
current-process session query. The monitor does not report `Ready` merely
because a native notification window has been registered. The initial
current-session observation and the required independent notification
registration must also succeed, and the reconciliation worker must be
operational. A known initialization failure leaves readiness non-ready,
keeps authentication blocked, and is retried while the shared host remains
active. The startup authentication interface may be composed, but no
authentication attempt can begin until the monitor reaches `Ready`.

If an established event source reports a runtime failure, the monitor
invalidates its source generation and detaches that generation's event
handlers. It then invokes the existing unconditional
`IApplicationSecuritySessionLifecycleService.Lock()` before marking the
authoritative application security session `Unavailable`. The lifecycle
lock supersedes an in-flight unlock and invalidates its authentication
attempt; the unavailable transition keeps access denied even if Windows
Hello remains available. A callback from an earlier source generation is
ignored and cannot restore readiness or authorization.

When monitoring successfully recovers, the monitor advances readiness to
a new `Ready` revision and restores an `Unavailable` application
security session only to `Locked`. Recovery never authenticates or
unlocks the application. A fresh successful Windows Hello verification
through the existing authentication and lifecycle services is required.

#### MVP 2 Windows Notification Policy

The Windows Infrastructure adapter uses a hidden top-level native window
on a dedicated background thread. It registers that window for WTS
notifications with `NOTIFY_FOR_THIS_SESSION`; it does not register for
all Windows sessions. The same top-level window receives power-broadcast
messages. A message-only window is not used because it does not receive
system power broadcasts.

The supported notification mapping is:

| Windows notification | Application event | Application response |
|---|---|---|
| `WM_WTSSESSION_CHANGE / WTS_SESSION_LOCK` | `SessionLocked` | Call the existing lifecycle `Lock()`. |
| `WM_WTSSESSION_CHANGE / WTS_SESSION_LOGOFF` | `SessionLoggedOff` | Call the existing lifecycle `Lock()`. |
| `WM_WTSSESSION_CHANGE / WTS_CONSOLE_DISCONNECT` | `SessionDisconnected` | Call the existing lifecycle `Lock()`. |
| `WM_WTSSESSION_CHANGE / WTS_REMOTE_DISCONNECT` | `SessionDisconnected` | Call the existing lifecycle `Lock()`. Remote-session disconnect is supported for the DeskVault process session. |
| `WM_POWERBROADCAST / PBT_APMSUSPEND` | `SystemSuspending` | Call the existing lifecycle `Lock()`. |
| `WM_WTSSESSION_CHANGE / WTS_SESSION_UNLOCK` | `SessionUnlocked` | No authorization transition. A Windows unlock is not DeskVault authentication. |
| `WM_WTSSESSION_CHANGE / WTS_CONSOLE_CONNECT` | `SessionConnected` | No authorization transition. |
| `WM_WTSSESSION_CHANGE / WTS_REMOTE_CONNECT` | `SessionConnected` | No authorization transition. |
| `WM_POWERBROADCAST / PBT_APMRESUMEAUTOMATIC` | `SystemResumed` | Call the existing lifecycle `Lock()`. Fresh DeskVault authentication is required. |
| `WM_POWERBROADCAST / PBT_APMRESUMESUSPEND` | `SystemResumed` | Call the existing lifecycle `Lock()`. Fresh DeskVault authentication is required. |
| `WM_POWERBROADCAST / PBT_APMRESUMECRITICAL` | `SystemResumed` | Call the existing lifecycle `Lock()`. Fresh DeskVault authentication is required. |

The power-broadcast API reports entry into and resumption from a low-power
state; it does not identify the exact low-power state. DeskVault therefore
treats the supported suspend and resume notifications as protective
security transitions and does not claim to distinguish sleep from
hibernation through these notifications.

The adapter also translates the legacy `PBT_APMRESUMECRITICAL` value when
received. Microsoft documents that support for this event was removed
starting with Windows Vista. It is retained as a defensive translation
and is not relied upon as a supported Windows 11 notification.

Notifications not listed in the mapping are not interpreted as an
authorization or lock signal by this MVP 2 adapter. This includes WTS
session logon, remote-control status, session creation or termination,
and desktop-ready notifications. The current-session registration does
not provide workstation monitoring for other Windows user sessions.

A lock-triggering notification is routed through the existing lifecycle
service. Repeated notifications therefore remain lock requests against
the same authoritative session; the lifecycle operation invalidates an
active unlock even when the session is already locked. Notification order
cannot grant authorization: Windows session unlock and connect events do
not change the application security session, while both suspend and
resume events invoke the existing lifecycle lock. A delayed lock
notification remains a lock request and can only withdraw access. The
monitor's source-generation fence rejects callbacks belonging to an
earlier monitoring generation.

#### Independent Current-Session Reconciliation

The WTS window notification path is supplemented by an independent query
of the actual Windows session that owns the DeskVault process. The adapter
obtains the process session identifier through
`ProcessIdToSessionId(Environment.ProcessId)`. It does not substitute the
active console session identifier, which would not identify the correct
session when DeskVault is running in a supported remote session.

Infrastructure queries the local WTS server through
`WTSQuerySessionInformationW`, requesting the `WTSSessionInfoEx`
information class (value `25`). The returned `WTSINFOEXW` structure is
marshalled using the explicitly declared native layout. The state parser
requires the supported information level, the expected structure size,
the expected process-session identifier, a recognized WTS connection
state, and one of the supported locked/unlocked session-flag values.

**Supported process architecture and native ABI.** The Windows
workstation native session-query boundary supports 64-bit x64 and
64-bit ARM64 processes. The runtime checks
`RuntimeInformation.ProcessArchitecture` and `IntPtr.Size` before
calling `ProcessIdToSessionId`. Only `Architecture.X64` and
`Architecture.Arm64` with an eight-byte pointer are supported. A
32-bit process, including x86 or Arm, and any unsupported architecture
or pointer-width combination is rejected with `ERROR_NOT_SUPPORTED`
(50) before the process-session lookup is invoked. The initial
workstation-monitoring query therefore cannot establish readiness on
an unsupported process architecture; the existing monitoring
initialization failure boundary keeps authentication unavailable.

The declared `WTSINFOEXW` and `WTSINFOEX_LEVEL1_W` layouts must be
validated on both supported process architectures. The expected
64-bit ABI assertions include a 232-byte `WTSINFOEXW`, an eight-byte
union offset, a 224-byte level-one structure, and the declared
member offsets. An x64 test result is not evidence of ARM64 layout
compatibility. Architecture-specific Windows CI execution is required
for both x64 and ARM64. The support policy is process-architecture
based; it does not broaden the Windows product OS or edition baseline
defined by ADR-0015.

**Supported Windows baseline and WTS flag compatibility.** The
Windows-targeted production and test projects retain the technical
platform target `net10.0-windows10.0.26100.0` and supported platform
version `10.0.26100.0`. The product-support policy is defined by
ADR-0015: supported Windows 11 releases and editions must remain within
Microsoft's servicing lifecycle and the deployed .NET 10 support matrix.
Windows 7 and Windows Server 2008 R2 are outside the DeskVault MVP 2
supported product baseline.

Microsoft documents a legacy defect on Windows 7 and Windows Server
2008 R2 in which `WTS_SESSIONSTATE_LOCK` and
`WTS_SESSIONSTATE_UNLOCK` have reversed meanings. The current parser
intentionally uses the documented modern mapping:
`WTS_SESSIONSTATE_LOCK = 0` means locked, and
`WTS_SESSIONSTATE_UNLOCK = 1` means unlocked. No legacy flag inversion
is implemented because those legacy operating systems are not supported.
The minimum supported client version documented for
`PowerRegisterSuspendResumeNotification` is Windows 8 desktop; the
DeskVault Windows 11 technical baseline is higher.

The accepted state interpretation is:

| WTS current-process state | Reconciliation result | Application response |
|---|---|---|
| Active and locked | `Locked` | Publish `SessionLocked`; invoke the existing lifecycle `Lock()`. |
| Active and unlocked | `Unlocked` | Observational only. Never authenticate or unlock DeskVault. |
| Any recognized non-active connection state | `Disconnected` | Publish `SessionDisconnected`; invoke the existing lifecycle `Lock()`. |
| Unknown, malformed, mismatched, or unsupported state | Invalid | Report a monitoring failure and fail closed. |

Every nonzero buffer returned by the WTS query is released through
`WTSFreeMemory` in an unconditional cleanup path. This includes successful
queries, unsuccessful native queries that return a buffer, short or
malformed responses, and exceptions during the native query or
marshalling path. The managed state parser operates on the copied
snapshot after the native WTS buffer has been released.

The reconciliation worker performs its synchronous WTS queries on one
dedicated thread. The production poll timer is configured with a one-second
due time and period. A timer tick that occurs while a native query is in
progress is skipped rather than queued; actual query-start intervals can
therefore exceed one second when a query takes longer or Windows scheduling
is delayed. Polling uses an injectable `TimeProvider` scheduling boundary
for deterministic automated tests, and the implementation records query
duration and actual intervals between query starts. The measured values are
diagnostics; they do not establish a hard real-time guarantee for Windows
scheduling.

A single query is allowed to execute at a time. A delayed or blocked
native call is not replaced with another query, so query overlap and
unbounded worker accumulation are not used as recovery mechanisms. An
independent watchdog checks both the active query deadline and the
reconciliation thread's liveness. A query that exceeds its deadline, an
unexpected worker termination, a failed process-session lookup, a failed
WTS query, or an invalid state result is reported through the existing
monitoring-failure contract. Shutdown is bounded; resources that may
still be used by an unreturned synchronous native query remain retained
until cleanup can safely complete.

The initial validated process-session query is part of startup readiness.
A successful WTS window registration without a successful initial query
does not establish `Ready`. During active monitoring, a persistent locked
or disconnected process-session state is published through the existing
Application event-source boundary even when the corresponding WTS window
notification was not delivered. The existing Application monitor then
uses the authoritative lifecycle `Lock()` operation. An unlocked query
result never invokes an Application unlock operation.

Reconciliation provides a current-state observation, not a complete
Windows session-event history. It can discover a persistent locked or
disconnected state that remains present at a later poll. It cannot guarantee
observation of a transition that begins and ends between polling samples.
That transient-state limitation remains explicit.

#### Independent Suspend/Resume Callback

In addition to the hidden-window power-broadcast path, Infrastructure
registers an independent callback through
`PowerRegisterSuspendResumeNotification` using
`DEVICE_NOTIFY_CALLBACK`. The callback recognizes the supported suspend
and resume event values and signals a dedicated bounded dispatcher.

The native callback performs only bounded notification bookkeeping and
signalling. It does not synchronously run the Application lifecycle lock,
wait for a worker, or perform other blocking application work on the
native callback stack. The dispatcher processes the notification outside
the callback and routes it through the existing event-source publisher.
When suspend and resume notifications are pending together, the dispatcher
preserves suspend-before-resume delivery.

The independent callback and the existing `WM_POWERBROADCAST` path are
both retained. Duplicate notifications remain harmless because each
protective event invokes the existing idempotent lifecycle lock operation.
Resume is not an authorization signal: `SystemResumed` invokes `Lock()`
and leaves the application requiring fresh Windows Hello authentication.

Shutdown first unregisters the native callback through
`PowerUnregisterSuspendResumeNotification`, then stops and drains the
managed dispatcher so callbacks already in progress can complete before
their resources are released. If native unregistration fails, the callback
and dispatcher remain retained; the source must not discard a delegate or
worker that Windows may still invoke. Cleanup can be retried after the
native registration is successfully removed. Callback signalling failure,
dispatcher failure, and event-delivery failure are monitoring failures.

#### Notification Delivery and Failure Limits

Successful WTS registration establishes that the current DeskVault
window is registered to receive WTS session-change messages. Each
successful registration is paired with an explicit unregister before
the native window is disposed. Independent power callback registration
is also paired with explicit unregistration and dispatcher draining.
Startup registration failure, initial query failure, native worker
failure, reconciliation timeout or liveness failure, unexpected
message-loop termination, callback/dispatcher failure, and reported
native cleanup or unregistration failure are observable by the adapter
and are handled as monitoring failures. A failure after monitoring has
become ready is surfaced through the event-source failure contract.

For an unexpected message-loop termination after startup has succeeded,
the event source raises `MonitoringFailed` before beginning potentially
blocking native unregistration or window cleanup. The Application monitor
can therefore apply the existing fail-closed lifecycle transition without
waiting for native cleanup to finish. Intentional stop is not reported as
an unexpected runtime failure.

The native window and WTS registration remain owned by the original
worker until unregistration succeeds. A failed unregister does not permit
the source to discard the window or create a replacement registration.
Cleanup remains retryable; the window is disposed only after successful
WTS unregistration, and a failed window disposal is retried by its
owning worker thread. A caller-facing shutdown operation is bounded even
when native cleanup has not completed; the owner retains resources until
they can be safely released.

The WTS reconciliation path improves detection of persistent current
session state when the primary WTS window notification is missed. It does
not poll the complete Windows session history and does not guarantee
observation of every transition. A transient lock or disconnect that
begins and ends between polling samples may remain undetected when the
corresponding notification is also missed. A silently undelivered power
callback or window power-broadcast message cannot be inferred merely
from the current WTS lock/connection state when that state does not
expose the missed power transition. The adapter must not claim to detect
all silently missed notifications or guarantee observation of every
operating-system transition.

The application therefore fails closed for failures it can establish:
a failed initial WTS query or required registration prevents readiness,
and a reported runtime monitoring, query, worker, callback, or cleanup
failure locks and marks the application session unavailable. A persistent
locked or disconnected WTS state is independently reconciled even when
the primary WTS window notification is suppressed. Platform delivery
limitations remain explicit and require interactive validation on
supported Windows configurations. Automated tests validate the query
boundary, state parsing, buffer cleanup, polling/liveness behavior,
suppressed-notification reconciliation, callback registration and
dispatch, and reported failure handling; they cannot prove that the
operating system will deliver every event in every environment.

Workstation transitions change only the runtime application
authorization state through the existing lifecycle. This monitor does
not modify encrypted document data, encrypted database data, key
artifacts, key identity, or key versions. Existing protected-key and
presentation enforcement boundaries remain responsible for their own
withdrawal and cleanup behavior.

### Automatic Inactivity Lock Coordination

Automatic inactivity locking is an Application-level policy coordinator
that observes the authoritative session and delegates expiry locking to
the existing lifecycle service. It does not add an authorization state,
change the session transition rules, or provide a second lock/unlock
implementation.

The Application layer defines:

- `IApplicationAutomaticLockService`, which controls monitoring and
  inactivity evaluation;
- `IApplicationUserActivityService`, which accepts qualifying activity
  associated with an observed session revision;
- `ApplicationAutomaticLockService`, which implements both contracts
  through one shared singleton instance;
- `AutomaticLockSettings`, an immutable, validated policy snapshot
  supplied by the UI composition root;
- an injected `TimeProvider` used for monotonic timestamp measurement.

The Application service does not read JSON, environment variables,
`IConfiguration`, WinForms messages, Windows workstation events, or
hosting APIs. Configuration construction and validation belong to the
UI composition root. Periodic scheduling belongs to a UI-hosted service.
The Application engine remains framework-neutral.

When automatic locking is enabled, the coordinator establishes a fresh
inactivity baseline for each newly observed authenticated session
revision. A qualifying activity is accepted only when:

- the current authoritative session is `Authenticated`;
- the activity source supplies the current session revision; and
- the inactivity deadline has not already been reached.

Accepted activity advances the baseline using a monotonic timestamp.
Activity associated with a prior or otherwise stale session revision is
rejected and cannot refresh the current authenticated session.

Qualifying activity represents deliberate user input, such as key
presses, pointer-button actions, touch or pointer-down actions, gestures,
or explicit user-invoked commands. Passive pointer movement, painting,
timer messages, background work, AI processing, indexing, and search
execution are not qualifying activity. The UI input adapter is
responsible for reporting only deliberate input; programmatic work must
not manufacture user activity.

Inactivity is evaluated using `TimeProvider.GetTimestamp()` and
`TimeProvider.GetElapsedTime()`. Wall-clock time and UI timer intervals
must not determine whether the inactivity timeout has elapsed. The hosted
scheduler only requests evaluations; the Application engine measures
elapsed inactivity independently of scheduler drift.

The expiry boundary is inclusive. When elapsed inactivity is greater
than or equal to the configured timeout, the authenticated session is
expired. An activity event observed at or after that deadline is rejected
and cannot extend the session.

Activity acceptance and expiry evaluation are serialized by the
Application coordinator. If expiry wins that synchronization boundary,
later activity for the same revision cannot reset the baseline. If
qualifying activity is accepted before the deadline, it advances the
baseline and subsequent evaluation measures from that accepted activity.
A pending expiry is fenced to its authenticated session revision so a
delayed lock request cannot lock a later authenticated session.

When expiry is established, the automatic-lock coordinator invokes only
`IApplicationSecuritySessionLifecycleService.TryLock(expectedRevision, out snapshot)`,
using the authenticated revision whose inactivity baseline expired. It
must not perform a separate revision check followed by an unconditional
`Lock()` call.

The lifecycle service serializes the conditional request against its
unlock-operation coordination. The authoritative
`IApplicationSecuritySession.TryLock(expectedRevision, out snapshot)`
operation checks the expected revision and protected-access state within
the same session synchronization boundary that commits the transition.
A stale expected revision is rejected without locking a newer authenticated
session.

A rejected stale conditional lock does not advance the lifecycle operation
generation, detach or cancel a newer in-flight unlock, or alter the current
session. A matching conditional lock commits the transition to `Locked`,
advances the lifecycle operation generation, and invalidates and cancels
any active unlock operation. Protected access is withdrawn through the
authoritative session state transition.

The automatic-lock coordinator does not authenticate, unlock, restore an
unavailable session, or directly mutate authoritative session state.
After a later successful authentication establishes a new authenticated
revision, a fresh inactivity baseline is created. Activity from an
earlier revision remains stale.

When the validated policy is disabled, the coordinator does not establish
an inactivity deadline or lock the session through automatic inactivity
evaluation. Explicit manual, lifecycle, or workstation-driven locking
continues to use its separately established owner and contract.

An absent effective automatic-lock configuration section produces the
disabled policy. An explicitly present section must be valid; malformed,
partial, or invalid explicit configuration prevents host construction
rather than silently weakening the policy. The UI composition root
registers one immutable validated settings snapshot. Changes to external
configuration sources require application restart and do not mutate an
active policy snapshot.

## Architectural Boundaries

The intended Application boundary is:

```text
Application Composition Root
        |
        +-- IApplicationSecuritySession
        |       |
        |       +-- CurrentSnapshot
        |       +-- StateChanged
        |       +-- BeginAuthentication
        |       +-- TryCompleteAuthentication
        |       +-- Lock
        |       +-- MarkUnavailable
        |       +-- RestoreAvailability
        |               |
        |               v
        |     ApplicationSecuritySession
        |       +-- Authoritative state
        |       +-- Monotonic revision
        |       +-- Active attempt identity
        |       +-- Serialized transitions
        |       +-- Ordered notifications
        |
        +-- IApplicationAuthenticationService
        |       |
        |       +-- Provider availability and verification
        |       +-- Session-bound authentication completion
        |
        +-- IApplicationSecuritySessionLifecycleService
        |       |
        |       +-- Lock
        |       +-- TryLock(expectedRevision)
        |       +-- UnlockAsync
        |       +-- Unlock-operation generation
        |       +-- Active unlock cancellation
        |       +-- Delegates state changes to the session
        |       +-- Delegates verification to authentication
        |
        +-- IApplicationAutomaticLockService
        |       |
        |       +-- Start / EvaluateInactivity / Stop
        |       +-- Immutable automatic-lock policy
        |       +-- Monotonic inactivity measurement
        |       +-- Authenticated revision baseline
        |       +-- Exact deadline enforcement
        |       +-- Delegates expiry to lifecycle TryLock(expectedRevision)
        |
        +-- IApplicationUserActivityService
                |
                +-- Qualifying user activity
                +-- Current session revision fencing
                +-- Rejects stale or post-deadline activity
```

Runtime lock/unlock orchestration delegates authoritative state changes
to the Application session and authentication to the existing
authentication service. The lifecycle service does not own a separate
authorization state.

Authentication integration supplies outcomes to the Application session.
Protected-access consumers observe the resulting authoritative state
through the Application contract.

The Application session, lifecycle service, and automatic-lock coordinator
do not depend on Infrastructure or UI implementations. UI adapters supply
validated configuration, deliberate input events, and hosted evaluation
scheduling through the Application contracts.

## Alternatives Considered

### Keep authoritative authentication state inside WinForms

Rejected.

A UI-owned state model would make authorization dependent on presentation
lifecycle and would prevent non-UI Application consumers from depending
on one authoritative session boundary.

It would also allow multiple presentation components to develop
contradictory views of the current authorization state.

### Register the session as transient or scoped

Rejected.

Separate coordinator instances could maintain different states and
independently accept or reject authentication completions.

The desktop application's root Application provider must therefore
supply one shared singleton session instance.

### Restore the previous state based only on authentication completion time

Rejected.

A late successful completion could restore access after a lock request
had already withdrawn it.

Authentication attempts must be bound to the session identity and
revision so that stale completions can be rejected deterministically.

### Reuse document-processing generations for authentication attempts

Rejected.

Document-processing generations belong to a separate lifecycle and
persistence boundary. They identify authoritative document-processing
attempts and protect derived-content publication.

Authentication attempts are runtime security-session operations and
must not change document-processing lineage or persistence semantics.

### Store authentication state as part of the encrypted document or database key

Rejected.

Authentication and authorization state are distinct from encryption
key identity, key retrieval, provisioning, and encrypted data format.

Combining them would blur the responsibilities already established by
the document and database encryption decisions.

### Place the session state machine in the Domain layer

Rejected.

The security session coordinates application runtime authorization,
authentication outcomes, and consumer notifications. It is an
Application-level orchestration boundary rather than a document or
workspace business entity.

## Consequences

### Positive

- Protected access has one authoritative Application-owned state.
- Access is granted only after an accepted successful authentication.
- Unauthenticated, authenticating, locked, and unavailable states fail
  closed.
- Authentication attempts cannot restore access after a later lock or
  unavailable transition.
- Repeated transitions have deterministic idempotent behavior.
- Immutable snapshots provide consistent state observations.
- Monotonic revisions allow consumers to identify stale notifications.
- State transitions are serialized independently of UI event ordering.
- Application consumers remain independent of WinForms and
  authentication-provider implementation details.
- Automatic inactivity expiry reuses the existing lifecycle lock operation
  instead of introducing another authoritative authorization state.
- Monotonic timestamps and authenticated session revisions make inactivity
  expiry deterministic in the presence of clock changes and stale input.
- Existing document and database encryption responsibilities remain
  separate from runtime authorization.
- Workstation monitoring readiness is an independent prerequisite for
  authentication; Windows Hello availability alone cannot authorize access.
- Supported workstation lock, logoff, disconnect, suspend, and resume
  transitions reuse the existing lifecycle lock rather than creating a
  second authorization state or authentication mechanism.
- Persistent locked or disconnected process-session state can be
  independently reconciled when the primary WTS window notification is
  missed.
- Initial WTS state validation and independent power-callback registration
  are prerequisites for workstation-monitor readiness.
- The native workstation session-query boundary has an explicit 64-bit
  x64 and ARM64 process-architecture policy; unsupported architectures
  fail closed before process-session lookup.
- Query, worker-liveness, callback, and registration failures use the
  existing fail-closed monitoring boundary.
- Monitoring recovery returns an unavailable session to `Locked`, so a
  fresh Windows Hello verification remains mandatory.

### Negative

- The Application layer now owns an additional cross-cutting runtime
  lifecycle abstraction.
- Consumers must handle state notifications and respect revision ordering.
- Authentication and lock integration must correctly use the session
  contract rather than creating local authoritative state.
- The complete protected-access guarantee requires coordinated
  enforcement by the authentication, key, workstation, and presentation
  boundaries.
- The UI composition root must maintain the validated automatic-lock
  settings snapshot and the hosted evaluation-service lifetime.
- Qualifying input must be classified correctly at the UI boundary; an
  incorrect activity source could otherwise extend or prematurely expire
  an authenticated session.
- Additional concurrency and lifecycle regression coverage is required.
- WTS and power notifications are operating-system messages, not a
  tamper-proof or complete event-audit stream.
- Periodic current-session reconciliation adds native query work and
  cannot prove observation of a transient state that begins and ends
  between polling samples.
- A silently missed power transition may remain unobservable when neither
  power-notification path reports it; this limitation must remain explicit.
- A synchronous native WTS query cannot be forcibly interrupted. A timed-out
  query therefore makes monitoring unavailable and retains its resources
  until the native call returns and cleanup can complete.
- The native ABI support claim requires architecture-specific Windows
  validation. An x64 pass cannot be treated as ARM64 evidence, and the
  ARM64 CI execution remains outstanding until that job has run.
- Actual notification and reconciliation behavior still requires
  interactive validation on supported Windows configurations.

These trade-offs are acceptable because protected vault access requires
one deterministic authorization boundary that is independent of the UI
and existing encryption implementation.

## Implementation Constraints

The implementation must:

- begin every new Application security session in `Unauthenticated`;
- grant protected access only in `Authenticated`;
- reject authentication initiation from `Authenticating`,
  `Authenticated`, or `Unavailable`;
- require re-authentication from `Locked`;
- return failed and cancelled authentication attempts to their original
  non-authorized state;
- treat unavailable authentication or security capabilities as
  non-authorized;
- invalidate active authentication attempts when locking or marking
  the session unavailable;
- reject stale, duplicate, or foreign-session authentication completions;
- keep state transitions serialized and revisions monotonically increasing;
- make repeated lock and unavailable operations idempotent;
- restore an unavailable session only to `Locked`;
- publish state-change notifications in revision order;
- prevent observer failures from changing committed authorization state;
- keep the session contract independent of WinForms and Infrastructure;
- preserve existing document and database encryption and key-management
  boundaries;
- avoid persisting authenticated session state for restoration into a
  new application session;
- keep authentication-provider, workstation-monitoring, automatic-lock,
  protected-key, and UI-enforcement responsibilities at their respective
  architectural boundaries;
- expose Windows workstation notifications to Application only through the
  framework-neutral event-source contract, with the native implementation
  remaining in Infrastructure;
- require workstation readiness to be `Ready` before initial or runtime
  authentication and fence each authentication operation to one readiness
  revision;
- route supported lock, logoff, disconnect, suspend, and resume
  notifications through the existing lifecycle `Lock()` operation;
- treat Windows session unlock and session connect only as observations,
  never as proof of DeskVault authentication;
- treat system resume as a protective lock transition and require fresh
  DeskVault authentication after resume;
- identify the actual DeskVault process session through
  `ProcessIdToSessionId(Environment.ProcessId)`, not the active console
  session identifier;
- permit the native process-session query only for 64-bit x64 and ARM64
  processes with an eight-byte pointer; reject other process architectures
  and pointer widths with `ERROR_NOT_SUPPORTED` before invoking
  `ProcessIdToSessionId`;
- validate the declared WTSINFOEX native structure sizes and member offsets
  on both supported process architectures; an x64-only run must not be
  represented as ARM64 validation;
- query `WTSSessionInfoEx` for the actual process session and validate the
  supported structure level, exact structure size, process-session
  identity, connection state, and locked/unlocked flags;
- release every nonzero native WTS query buffer through `WTSFreeMemory`,
  including unsuccessful, malformed, exceptional, and cancellation-related
  completion paths;
- configure the production poll timer with a one-second due time and period;
- skip a poll tick while a native query is in progress rather than queueing
  an overlapping query, and record query duration and actual query-start
  cadence;
- prevent overlapping native session queries and unbounded worker
  accumulation;
- publish `MonitoringFailed` immediately after an unexpected message-loop
  exit following successful startup, before potentially blocking native
  unregistration or window cleanup; do not report intentional stop as an
  unexpected runtime monitoring failure;
- retain the original WTS registration, native window, and owning worker
  when unregistration fails; do not create a replacement registration
  until the previous native resource ownership has been safely discharged;
- dispose the native window only after successful WTS unregistration,
  retry failed window disposal on its owning worker thread, and keep
  caller-facing shutdown bounded while retaining resources needed for
  safe cleanup completion;
- monitor reconciliation query deadlines and worker liveness independently
  of the query operation;
- report failed, unavailable, malformed, unknown, or unsupported
  current-session state through the existing fail-closed monitoring
  boundary;
- register the independent suspend/resume callback through
  `PowerRegisterSuspendResumeNotification` and dispatch its events outside
  the native callback stack;
- unregister the power callback and drain in-progress callback dispatch
  before releasing callback and worker resources; retain those resources
  when native unregistration fails;
- on a known runtime monitoring failure, supersede an in-flight unlock
  through the lifecycle lock before marking the application session
  `Unavailable`;
- recover monitoring only to a new `Ready` generation and restore an
  unavailable application session only to `Locked`;
- detect persistent locked or disconnected process-session state through
  independent reconciliation when the primary WTS window notification is
  missed, while documenting that transient transitions between polls and
  silently missed power events may remain unobservable;
- preserve the documented inability of the selected power notification
  API to distinguish suspend from hibernate;
- preserve encrypted document, database, and key artifacts across
  workstation-driven session transitions;
- keep automatic inactivity policy coordination in the Application layer
  without introducing hosting, configuration-provider, WinForms, or
  operating-system dependencies there;
- supply automatic-lock policy as one immutable validated settings
  snapshot from the UI composition root;
- treat an absent effective automatic-lock section as disabled, but fail
  host construction for an explicitly present invalid policy;
- establish a new inactivity baseline for each authenticated session
  revision;
- accept qualifying activity only for the current authenticated revision
  and only before the inactivity deadline;
- measure inactivity with monotonic timestamp and elapsed-time APIs,
  never wall-clock time;
- treat elapsed inactivity equal to the configured timeout as expired;
- reject activity at or after the deadline and serialize activity
  acceptance with expiry evaluation;
- fence a pending expiry to the authenticated revision that expired;
- delegate automatic expiry exclusively to the revision-conditional
  `IApplicationSecuritySessionLifecycleService.TryLock(expectedRevision, out snapshot)`;
- require the authoritative session to compare the expected revision and
  commit the conditional lock within one synchronization boundary;
- reject stale conditional expiry without advancing the lifecycle
  generation or superseding a newer in-flight unlock;
- invalidate an in-flight unlock when a matching conditional expiry
  successfully commits the lock transition;
- leave authentication, re-authentication, unavailable-state recovery,
  and authoritative session transitions to their existing owners;
- establish a fresh inactivity baseline after each successful
  authentication transition;
- exclude passive pointer movement, timer messages, painting, background
  processing, search, indexing, and AI work from qualifying activity;
- keep periodic evaluation scheduling in the UI-hosted adapter while
  retaining policy and monotonic deadline decisions in Application;
- use the lifecycle service for runtime lock/unlock coordination while
  keeping the authoritative authorization state in the session;
- preserve unconditional `Lock()` semantics: advance the lifecycle
  operation generation for every request, including a repeated request
  that does not change session state, and invalidate any active unlock
  before applying the session lock;
- for `TryLock(expectedRevision, out snapshot)`, advance the lifecycle
  generation and invalidate an active unlock only when the expected
  revision identifies the current authenticated session and the
  conditional session transition succeeds;
- reject stale conditional lock requests without changing the lifecycle
  generation or detaching or cancelling a newer in-flight unlock;
- for a successful conditional lock, establish the lifecycle generation
  fence and detach the superseded unlock before requesting cancellation;
  request cancellation outside the lifecycle synchronization gate;
- reject a competing unlock while a lifecycle lock request is executing,
  including any state-change notification dispatch performed
  synchronously within that lock call;
- preserve revision-ordered state transitions and notification delivery
  when a lock notification is queued behind an already-active dispatcher;
- permit runtime unlock only from `Locked` or `Unavailable`;
- accept an unlock result only while its lifecycle operation remains
  current, and never treat a superseded result as authorization.

## Verification

The Application security-session implementation is covered by automated
tests for:

- initial unauthenticated state and protected-access availability;
- authentication success, failure, cancellation, and unavailability;
- authentication from both unauthenticated and locked states;
- rejection of authentication initiation from disallowed states;
- lock and unavailable transitions;
- idempotent repeated lock and unavailable requests;
- invalidation of an in-flight authentication attempt after lock;
- invalidation of an in-flight authentication attempt after
  unavailability;
- rejection of authentication attempts from another session;
- rejection of duplicate authentication completion;
- restoration from unavailable to locked and the requirement for
  subsequent successful authentication;
- concurrent lock requests;
- re-entrant state-change handling and revision-ordered notifications;
- observer failure isolation;
- invalid authentication outcomes and null attempt arguments;
- singleton registration and resolution through Application dependency
  injection;
- lifecycle lock and unlock coordination through the Application
  lifecycle service;
- authoritative session conditional locking for a matching revision and
  rejection of a stale expected revision;
- lifecycle conditional locking that preserves a newer in-flight unlock
  for stale expiry and supersedes it for a matching expiry;
- deterministic automatic-lock race coverage where a newer authenticated
  revision is established immediately before an older expiry is applied;
- document-workspace compatibility before expiry, presentation cleanup
  after lock, rejection of the old presenter after re-authentication, and
  successful document opening through a new presenter;
- automatic-lock service and WinForms activity-filter disposal, including
  rejection of activity after disposal;
- controlled host-construction failure that prevents startup
  authentication and vault initialization while presenting a safe error;
- singleton registration and shared resolution of the automatic-lock
  service through both its monitoring and user-activity contracts;
- automatic-lock behavior when the policy is disabled;
- initial inactivity baseline creation on an authenticated session
  transition;
- qualifying activity resetting the inactivity baseline before expiry;
- rejection of activity at the exact expiry boundary;
- exact-deadline automatic locking through the authoritative lifecycle
  service;
- rejection of stale activity from an earlier authenticated revision;
- fresh inactivity baseline creation after successful re-authentication;
- repeated inactivity evaluation without duplicate expiry locking;
- stopping and disposing automatic-lock monitoring;
- monotonic-time evaluation through an injected deterministic
  `TimeProvider`;
- hosted-service startup and shutdown, including start failure cleanup;
- shared host start-before-authentication ordering and host disposal;
- integration of successful authentication, inactivity reset, automatic
  expiry, lifecycle re-authentication, stale activity rejection, and a
  second expiry against the real Application services;
- lock invalidation of an unlock still checking provider availability,
  before a session-bound authentication attempt exists;
- lock invalidation of an active authentication attempt and rejection of
  its delayed successful completion;
- repeated lock requests superseding an unlock even when the authoritative
  session remains in the same state;
- rejection of a competing unlock during synchronous lock-notification
  dispatch;
- unlock and subsequent state transitions while the lock notification
  is queued behind an already-active notification dispatcher;
- revision-ordered delivery of queued lock and subsequent unlock
  notifications;
- successful runtime re-authentication and failed, cancelled, and
  unavailable re-authentication through the production Windows Hello
  composition.

Automated Application tests cover the security-session state transitions,
authentication-attempt invalidation, concurrency behavior, notification
ordering, dependency-injection registration, and automatic-lock policy
coordination. The automatic-lock integration test exercises authentication,
qualifying activity, monotonic expiry, lifecycle locking, re-authentication,
stale activity rejection, and a subsequent expiry through the real
Application services. Full-solution validation and the complete integrated
protected-access verification remain separate validation concerns.

Workstation-security verification additionally covers:

- readiness lifecycle transitions, immutable readiness snapshots, and
  readiness revision fencing;
- rejection of initial authentication and subsequent unlock when
  workstation monitoring is not `Ready`, including when Windows Hello
  reports `Available`;
- rejection of provider availability and verification results obtained
  across a readiness-generation change;
- mapping of supported WTS lock, logoff, console disconnect, and remote
  disconnect notifications to the existing lifecycle lock;
- mapping of suspend and resume power notifications to the protective
  lifecycle lock, including the rule that resume never restores
  authorization;
- no authorization transition for Windows session unlock or console/remote
  connect notifications;
- current-process session identity acquisition through
  `ProcessIdToSessionId` and WTS query of the actual process session;
- WTSINFOEX level, structure-size, process-session identity, connection
  state, and session-flag validation;
- native WTSINFOEX ABI layout assertions on both supported process
  architectures, x64 and ARM64: `WTSINFOEXW` size of 232 bytes, union
  offset of 8 bytes, level-one structure size of 224 bytes, and the
  declared offsets of all level-one members; each architecture-specific
  test host must verify its process architecture, operating-system
  architecture, and eight-byte pointer width;
- process-architecture guard coverage for supported x64/8-byte and
  ARM64/8-byte combinations, and rejection of unsupported x86, Arm,
  and mismatched pointer-width combinations with `ERROR_NOT_SUPPORTED`
  before native process-session lookup;
- process-session lookup failure through the injected
  `ProcessIdToSessionId` boundary, including preservation of Win32 error
  information and rejection before issuing a WTS session-information
  query;
- deterministic coverage of a lock followed by unlock entirely between
  reconciliation samples while the primary WTS notification is
  suppressed, demonstrating the documented transient-state observation
  limitation;
- native WTS buffer release through `WTSFreeMemory` on successful query,
  malformed response, failed query, parser rejection, cancellation-related
  native-query completion, and exceptional completion paths;
- persistent locked and disconnected current-session reconciliation when
  the primary WTS window notification is intentionally suppressed;
- deterministic polling interval, query duration and cadence measurement,
  non-overlapping native queries, and rejection of stale process-session
  identity;
- bounded query-timeout handling, independent worker-liveness detection,
  bounded shutdown, and retained resources while a synchronous native
  query remains active;
- independent suspend/resume callback registration and event mapping;
- bounded callback dispatch outside the native callback stack, suspend
  before resume ordering, callback signalling failure, dispatcher failure,
  and event-delivery failure handling;
- native callback unregistration, dispatcher draining, retry after
  unregistration failure, and callback/delegate lifetime retention;
- repeated lock notifications, out-of-generation callbacks, and stale
  callbacks after a monitoring failure;
- runtime failure ordering that supersedes an in-flight unlock before the
  session becomes `Unavailable`;
- recovery to `Locked` only, followed by a required fresh authentication;
- native window creation, WTS registration and unregistration, message-loop
  failure, stop retry, and native resource cleanup;
- immediate `MonitoringFailed` publication after an unexpected
  post-start message-loop exit while native WTS unregistration is blocked;
- retention and retry of the same WTS registration after unregistration
  failure, prevention of replacement registration while ownership remains,
  and retry of native window disposal on the owning worker thread;
- hosted-service startup, initialization retry, and deterministic shutdown.

These tests establish the Application policy and the controlled native
boundary. They verify that persistent locked or disconnected process-session
state is reconciled even when the primary WTS window notification is
suppressed. The deterministic scheduling tests and measured query values
do not establish a hard real-time guarantee for Windows scheduling. Manual
validation on supported Windows hardware and session configurations
remains necessary for actual WTS and power-notification delivery. The
documented limitations remain that a transient state change may begin
and end between polls, and a silently missed power transition cannot be
inferred when neither power-notification path reports it.

## Related Decisions

- ADR-0001 establishes DeskVault's offline-first product vision and
  security direction.
- ADR-0002 establishes the Domain, Application, Infrastructure, and UI
  boundaries and the Application composition-root model.
- ADR-0004 establishes document encryption, strict existing-key
  retrieval, and the authorized key-provisioning boundary.
- ADR-0006 establishes the in-app document workspace and its separation
  from Application and Infrastructure responsibilities.
- ADR-0007 establishes the document workspace UI, rendering, and
  presentation boundaries.
- ADR-0009 establishes encrypted SQLite and database-key management.
- ADR-0010 establishes document-processing generation authority and
  stale-result protection for document processing.
- ADR-0015 establishes Windows Hello as the selected authentication
  provider and its integration with the Application security session,
  including the workstation-monitor readiness prerequisite.

## References

- Microsoft Learn — WTSRegisterSessionNotification:
  https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsregistersessionnotification

- Microsoft Learn — WM_WTSSESSION_CHANGE:
  https://learn.microsoft.com/en-us/windows/win32/termserv/wm-wtssession-change

- Microsoft Learn — WM_POWERBROADCAST Messages:
  https://learn.microsoft.com/en-us/windows/win32/power/wm-powerbroadcast-messages

- Microsoft Learn — PBT_APMSUSPEND:
  https://learn.microsoft.com/en-us/windows/win32/power/pbt-apmsuspend

- Microsoft Learn — PBT_APMRESUMEAUTOMATIC:
  https://learn.microsoft.com/en-us/windows/win32/power/pbt-apmresumeautomatic

- Microsoft Learn — PBT_APMRESUMESUSPEND:
  https://learn.microsoft.com/en-us/windows/win32/power/pbt-apmresumesuspend

- Microsoft Learn — PBT_APMRESUMECRITICAL:
  https://learn.microsoft.com/en-us/windows/win32/power/pbt-apmresumecritical

- Microsoft Learn — ProcessIdToSessionId:
  https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-processidtosessionid

- Microsoft Learn — WTSQuerySessionInformationW:
  https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsquerysessioninformationw

- Microsoft Learn — WTSINFOEXW:
  https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/ns-wtsapi32-wtsinfoexw

- Microsoft Learn — WTSINFOEX_LEVEL1_W:
  https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/ns-wtsapi32-wtsinfoex_level1_w

- Microsoft Learn — WTSFreeMemory:
  https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsfreememory

- Microsoft Learn — PowerRegisterSuspendResumeNotification:
  https://learn.microsoft.com/en-us/windows/desktop/api/powerbase/nf-powerbase-powerregistersuspendresumenotification

- Microsoft Learn — PowerUnregisterSuspendResumeNotification:
  https://learn.microsoft.com/en-us/windows/desktop/api/powerbase/nf-powerbase-powerunregistersuspendresumenotification

## Result

DeskVault has one authoritative, framework-neutral Application security
session that governs the runtime authorization state for protected vault
access.

Authentication attempts are bound to the session and its revision.
Locking or establishing a security-unavailable condition invalidates
earlier attempts, and stale completions cannot restore access.

Only the `Authenticated` state permits protected access. All other
states remain non-authorized, and recovery from an unavailable condition
returns to `Locked` so that successful authentication is still required.

The resulting boundary preserves the existing document and database
encryption architecture while providing a deterministic foundation for
authentication, runtime lock/unlock orchestration, automatic inactivity
locking, key availability, workstation transitions, and presentation
protection. Workstation notifications and independently reconciled
current-session state are runtime evidence routed through the existing
lifecycle; neither is an authentication signal.

Workstation monitoring readiness requires successful WTS notification
registration, independent suspend/resume callback registration, and an
initial validated WTS snapshot for the actual DeskVault process session.
The current-session reconciliation worker observes persistent locked and
disconnected process-session state independently of the primary WTS
window-notification path. It uses bounded single-query execution,
periodic scheduling, query-timeout detection, and independent worker
liveness monitoring. Unknown, malformed, unavailable, or unsupported
session state fails closed through the existing monitoring-failure
boundary.

The independent power callback supplements the existing window-message
power path. Suspend and resume both withdraw protected access through the
existing lifecycle lock. Resume never restores authorization; fresh
Windows Hello verification remains mandatory. Callback unregistration
and dispatcher draining are part of the monitoring resource lifecycle.

The native WTS session-query boundary supports 64-bit x64 and ARM64
processes and rejects unsupported process architectures or pointer
widths before native process-session lookup. Native structure layout
must be validated independently on both supported architectures.
This process-architecture policy does not change the Windows product
OS and edition baseline established by ADR-0015.

The monitoring design does not claim to reconstruct complete Windows
session history. Persistent locked or disconnected state can be detected
by later reconciliation when a WTS window notification is missed, but a
transient state change between polls may remain undetected. A silently
missed power transition may also remain unobservable when neither power
notification path reports it. The selected power-notification semantics
do not independently distinguish suspend from hibernate. These limits
remain explicit and require manual Windows validation.

Monitoring recovery returns the Application security session only to
`Locked`. WTS remote-session disconnect remains scoped to the DeskVault
process session. Automatic inactivity policy is coordinated separately
from the authoritative session and delegates expiry to the existing
lifecycle lock operation. Monotonic time, authenticated revision fencing,
and the inclusive expiry boundary prevent stale activity from extending
an expired or newer session. Unconditional lock requests and successful
revision-conditional expiry locks supersede earlier unlock operations
without changing the authoritative session-state contract.
