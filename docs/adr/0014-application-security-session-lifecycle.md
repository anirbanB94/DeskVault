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

Authentication-provider integration, application lock and unlock
lifecycle, protected document and database key availability,
automatic-lock policy, Windows workstation security transitions,
in-app presentation cleanup, suppression of late asynchronous results,
and integrated verification of the complete access boundary remain
separate responsibilities.

## Architectural Boundaries

The intended Application boundary is:

```text
Application Composition Root
        |
        +-- IApplicationSecuritySession
                |
                +-- CurrentSnapshot
                +-- StateChanged
                +-- BeginAuthentication
                +-- TryCompleteAuthentication
                +-- Lock
                +-- MarkUnavailable
                +-- RestoreAvailability
                        |
                        v
              ApplicationSecuritySession
                        |
                        +-- Authoritative state
                        +-- Monotonic revision
                        +-- Active attempt identity
                        +-- Serialized transitions
                        +-- Ordered notifications
```

Authentication integration supplies outcomes to the Application session.
Protected-access consumers observe the resulting authoritative state
through the Application contract.

The Application session does not depend on Infrastructure or UI
implementations.

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
  architectural boundaries.

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
  injection.

Automated Application tests cover the security-session state transitions,
authentication-attempt invalidation, concurrency behavior, notification
ordering, and dependency-injection registration. Full-solution validation
and integrated protected-access verification remain separate validation
concerns.

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
authentication, locking, key-availability, workstation, and presentation
integrations.
