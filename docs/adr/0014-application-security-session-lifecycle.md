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
  provider and its integration with the Application security session.

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
protection. Automatic inactivity policy is coordinated separately from
the authoritative session and delegates expiry to the existing lifecycle
lock operation. Monotonic time, authenticated revision fencing, and the
inclusive expiry boundary prevent stale activity from extending an
expired or newer session. Unconditional lock requests and successful
revision-conditional expiry locks supersede earlier unlock operations
without changing the authoritative session-state contract.
