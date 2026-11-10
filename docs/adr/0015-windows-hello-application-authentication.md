# ADR-0015: Windows Hello for MVP 2 Application Authentication

## Status

Accepted

## Context

DeskVault requires fresh user verification before allowing protected vault access during an application session.

The Application security-session contract is defined by ADR-0014. It provides the authoritative runtime authorization state, but deliberately does not implement authentication or depend on a specific authentication provider.

The MVP 2 authentication mechanism must:

- operate within DeskVault's local/offline product model;
- verify the currently signed-in Windows user through an operating-system authentication experience;
- distinguish successful verification from failure, cancellation, and unavailable authentication capability;
- avoid collecting or persisting the user's Windows password;
- integrate with the Application security-session contract;
- prevent protected database initialization from occurring before successful authentication;
- preserve the existing document and database encryption and key-protection architecture.

An application-specific master passphrase is a possible future direction, but it would introduce a separate credential lifecycle, enrollment, verification, recovery, and potentially key-wrapping decisions. It is not required to establish the MVP 2 authentication boundary.

## Decision

DeskVault MVP 2 will use Windows Hello through the Windows `UserConsentVerifier` desktop interop API as its sole application authentication mechanism.

The desktop implementation will use:

`Windows.Security.Credentials.UI.UserConsentVerifierInterop.RequestVerificationForWindowAsync`

The request will be associated with the active DeskVault application window handle. Availability will be checked through `UserConsentVerifier.CheckAvailabilityAsync()` before requesting verification.

The application will accept protected-access authorization only when Windows reports the `Verified` result and the corresponding session-bound authentication attempt is accepted by `IApplicationSecuritySession`.

No alternative authentication method will be selected automatically or silently when Windows Hello is unavailable, disabled, cancelled, or fails.

### Supported Windows Baseline

DeskVault MVP 2 will support the following Windows client baseline:

- Windows 11 version 25H2 or later, provided that the Windows release and edition remain within Microsoft's servicing lifecycle and the release is included in the supported operating-system matrix for the deployed .NET 10 runtime.
- Windows 11 version 24H2 Enterprise and Education editions, only while those editions remain within Microsoft's servicing lifecycle.
- Windows 11 version 24H2 Home and Pro-family editions are not supported. Microsoft ended servicing for these editions in October 2026.
- Windows 10 is not part of the DeskVault MVP 2 supported product baseline, even though .NET 10 supports certain Windows 10 Enterprise and LTSC editions.

As of November 2, 2026, Windows 11 version 24H2 Enterprise and Education remain within their servicing lifecycle through October 12, 2027. The corresponding 24H2 Home and Pro-family editions have already reached end of servicing.

The supported Windows baseline is a product-support policy, not merely a Windows API availability requirement. Future Windows releases must remain within Microsoft's servicing lifecycle and the supported operating system matrix for the deployed .NET 10 runtime to be considered supported by DeskVault.

The Windows Hello desktop interop API itself documents Windows build `22000` as its minimum supported client. DeskVault's minimum technical Windows platform version is higher because the selected MVP 2 product baseline includes Windows 11 version 24H2 Enterprise and Education (build `26100`) as its oldest supported Windows client release.

Windows-targeted production and test projects will use the explicit Windows platform target:

`net10.0-windows10.0.26100.0`

Their supported OS platform version will be:

`10.0.26100.0`

The Windows-targeted projects are:

- `DeskVault.Infrastructure`
- `DeskVault.UI`
- `DeskVault.Infrastructure.Tests`
- `DeskVault.UI.Tests`
- `DeskVault.Integration.Tests`

This platform target preserves compatibility with the oldest technically supported Windows release in the DeskVault product baseline. It does not by itself express Windows edition eligibility or Microsoft's servicing status. Those remain separate product-support requirements.

Cross-platform projects that do not require Windows APIs remain on their existing `net10.0` target.

### Windows Hello Availability

The provider will check authentication capability availability before starting an authentication attempt.

The following availability outcomes are treated as unavailable:

- no authentication device is present;
- the current Windows user has not configured an authentication verifier;
- verification is disabled by policy;
- the authentication device is busy;
- an unsupported platform or missing API prevents verification;
- an unexpected API failure prevents the capability from being established.

Availability does not constitute successful authentication.

If a capability previously marked unavailable becomes available again, the Application authentication service may proceed only when workstation security monitoring is also `Ready`. The readiness contract owns restoration of the session from `Unavailable` to `Locked` through `IWorkstationSecurityReadiness.TryRestoreSessionAvailability(expectedReadinessRevision)`. Restoration never transitions directly to `Authenticated`.

Recovery from an unavailable condition never transitions directly to `Authenticated`.

### Workstation Security Readiness Prerequisite

Workstation security-monitor readiness is an independent and mandatory
prerequisite for Windows Hello authentication. A Windows Hello
availability result of `Available` is insufficient when workstation
security monitoring is `NotStarted`, `Starting`, `Unavailable`, or
`Stopped`.

Before checking Windows Hello availability, the Application
authentication service reads the current workstation readiness snapshot.
Only the `Ready` state permits the authentication workflow to continue.

The authentication workflow is fenced to the readiness revision observed
at its start. The Application authentication service revalidates that
revision after provider-availability evaluation, before creating the
session-bound authentication attempt, before requesting Windows Hello
verification, and before accepting the provider result. A readiness
transition or a changed readiness revision prevents a delayed provider
result from establishing authenticated access.

This requirement applies to both initial authentication and runtime
re-authentication through the existing Application lifecycle service.
It does not add another authentication provider, authorization state,
or unlock path.

When a known workstation-monitoring failure occurs, the monitor first
uses the existing lifecycle lock operation to supersede an in-flight
unlock and then marks the authoritative application security session
`Unavailable`. Windows Hello remaining `Available` cannot bypass that
unavailable monitoring condition.

After monitoring successfully recovers, readiness becomes `Ready` under
a new readiness revision. Recovery restores the application session only
to `Locked`; a fresh successful Windows Hello verification is required
before protected access can be restored.

The detailed Windows event mapping, supported remote-session behavior,
native registration lifecycle, and notification-delivery limitations are
defined by ADR-0014.


### Verification Outcome Mapping

The Windows `UserConsentVerificationResult` is mapped as follows:

| Windows result | Application session outcome |
|---|---|
| `Verified` | `Succeeded` |
| `RetriesExhausted` | `Failed` |
| `Canceled` | `Cancelled` |
| `DeviceNotPresent` | `Unavailable` |
| `NotConfiguredForUser` | `Unavailable` |
| `DisabledByPolicy` | `Unavailable` |
| `DeviceBusy` | `Unavailable` |
| Unknown or unsupported result | `Unavailable` |
| Unexpected API failure | `Unavailable` |

Only `Verified` can produce a successful authentication outcome.

The Application authentication service must submit the outcome against the exact attempt returned by `BeginAuthentication()`. The session coordinator remains authoritative and may reject stale, invalidated, or out-of-order completions.

### Authentication and Authorization Boundary

Windows Hello verifies the currently signed-in Windows user through the operating-system authentication experience.

It does not create a separate DeskVault user identity or credential database.

Windows chooses the supported verification method exposed by the current device and Windows configuration. The application does not require biometric-only verification and does not collect biometric data.

Successful verification establishes the Application security-session state through ADR-0014. It does not independently provision, retrieve, replace, rotate, or derive encryption keys.

Authentication and runtime authorization remain distinct from document/database encryption and key-protection responsibilities.

Existing DPAPI `CurrentUser` protection, strict key retrieval, key identity/version semantics, encrypted document format, and encrypted SQLite behavior remain governed by ADR-0004, ADR-0009, and the established key-management decisions.

Protected document/database key availability enforcement remains a separate responsibility and must not be replaced by this authentication provider.

### Startup Authorization Gate

The production DeskVault startup flow must complete successful Windows Hello verification and establish the authenticated Application security session before invoking `DatabaseInitializer.InitializeAsync()`.

This ordering ensures that database initialization, initial key provisioning, and subsequent protected-vault operations do not run before the required application authentication boundary is established.

The production dependency-injection composition must register the selected Windows Hello provider. Test-only provider registration is not evidence that the production startup path uses the selected mechanism.

The authentication prompt must be associated with a valid active DeskVault window. UI window-handle ownership remains in the UI composition boundary; the Application security-session contract must not depend on WinForms or native window types.

### Shared Host Lifecycle and Automatic Inactivity Lock

The production UI composition root owns one shared Generic Host for the lifetime of the DeskVault application process.

The host is built and started before the production startup authentication form is resolved or displayed. Starting the host starts its registered hosted services and establishes the runtime service lifecycle before the authentication workflow begins.

If host construction fails because the startup configuration is missing, malformed, or invalid, `Program.Main` presents a fixed actionable startup-configuration message and returns before startup authentication, database initialization, or creation of the main application interface. The failure message does not expose exception details. A host that was not successfully constructed is not started.

The same host and service provider remain in use throughout:

- startup authentication;
- protected database initialization;
- creation and execution of the main WinForms interface;
- normal application shutdown.

The production startup path must not build a second host for authentication, database initialization, or the main application interface. Doing so could create separate singleton security sessions, lifecycle services, authentication services, or automatic-lock coordinators.

The UI-owned `DeskVaultHostLifetime` coordinates host startup, shutdown, and disposal. It is responsible for:

- starting the production host before startup authentication;
- propagating host-start failures rather than proceeding to authentication;
- stopping the host when startup is cancelled or fails;
- stopping the host when the main WinForms message loop exits;
- disposing the host after the application lifecycle has ended.

The host must be stopped and disposed on every applicable exit path, including successful application exit, startup cancellation, authentication failure, database initialization failure, and exceptions during startup or normal application execution.

The Application layer owns automatic inactivity-lock policy coordination through `ApplicationAutomaticLockService`. The service implements `IApplicationAutomaticLockService` and `IApplicationUserActivityService` through one shared singleton instance.

The UI composition root supplies the validated immutable `AutomaticLockSettings` snapshot and the system `TimeProvider`. The Application service remains independent of configuration providers, WinForms, operating-system events, and hosting APIs.

The UI registers `ApplicationAutomaticLockHostedService` as the hosted scheduling adapter. It starts and stops the Application automatic-lock service with the shared host and periodically requests inactivity evaluation. Scheduling intervals do not determine the expiry deadline; the Application service measures elapsed inactivity through monotonic timestamp APIs.

The automatic-lock service can start before startup authentication without starting an inactivity countdown for an unauthenticated session. It establishes its baseline only when the authoritative Application security session transitions to a new `Authenticated` revision.

The UI input adapter reports qualifying deliberate user activity to `IApplicationUserActivityService` with the observed session revision. Activity is accepted only for the current authenticated revision and before the configured inactivity deadline. Passive pointer movement, painting, timers, and background operations do not extend the inactivity baseline.

When the inactivity deadline is reached, the Application automatic-lock service delegates expiry to the revision-conditional `IApplicationSecuritySessionLifecycleService.TryLock(expectedRevision, out snapshot)`, using the authenticated session revision whose inactivity baseline expired.

The lifecycle service and authoritative Application security session continue to own lock coordination and the resulting authorization-state transition. The expected revision is checked atomically with the session lock transition. A delayed expiry from an earlier revision cannot lock a newer authenticated session or supersede its in-flight unlock. A matching expiry commits the lock and invalidates any in-flight unlock through the existing lifecycle generation and cancellation coordination.

Automatic inactivity locking does not introduce another authentication provider, authentication outcome, authorization state, or unlock path. Following a lock, protected access can be restored only through the existing authentication service and the selected Windows Hello provider.

The `Security:AutomaticLock` configuration is resolved and validated by the UI composition root before startup authentication is allowed to proceed. An absent effective section produces a disabled policy. Explicit invalid configuration prevents successful host construction or startup; it must not silently be treated as a valid enabled or disabled policy.

The validated automatic-lock settings are an immutable runtime snapshot. Changes to external configuration sources do not modify an active policy; the application must be restarted for configuration changes to take effect.

The host lifecycle, automatic-lock policy, activity classification, monotonic expiry, and session-revision behavior are governed by ADR-0002 and ADR-0014. This ADR establishes how those services coexist with the Windows Hello startup authorization boundary.

### Implementation

The authentication integration is implemented across the Application, Infrastructure, and UI boundaries while preserving the separation defined by ADR-0002 and ADR-0014.

#### Application Authentication Orchestration

The Application layer defines:

- `IApplicationAuthenticationProvider`;
- `IApplicationAuthenticationService`;
- `ApplicationAuthenticationService`;
- `ApplicationAuthenticationAvailability`;
- `ApplicationAuthenticationProviderResult`;
- `ApplicationAuthenticationServiceResult`.

`ApplicationAuthenticationService` coordinates authentication using the existing `IApplicationSecuritySession` contract.

It is responsible for:

- checking authentication capability availability before beginning an authentication attempt;
- restoring an unavailable session to `Locked` when capability availability is re-established;
- beginning authentication through the authoritative session contract;
- submitting the provider outcome against the exact session-bound authentication attempt;
- preserving the distinction between success, failure, cancellation, and unavailability;
- rejecting or containing concurrent authentication requests;
- preventing an authentication completion from restoring protected access after its attempt has been invalidated;
- preserving cancellation semantics;
- treating unexpected provider failures as an unavailable authentication condition and recording them through safe diagnostic logging.

The Application layer does not reference WinForms, Windows Hello APIs, native window handles, or Infrastructure implementations.

The authentication service and application security session are registered as singletons through the Application dependency-injection composition root.

#### Windows Hello Infrastructure Provider

Infrastructure implements the Application authentication provider through `WindowsHelloAuthenticationProvider`.

The Windows-specific implementation is separated behind a narrow platform API abstraction:

- `IWindowsHelloPlatformApi`;
- `WindowsHelloPlatformApi`;
- `IWindowsHelloWindowHandleProvider`.

`WindowsHelloPlatformApi` uses `UserConsentVerifier.CheckAvailabilityAsync()` to establish whether Windows Hello verification is available.

For a verification request, it uses the desktop interop API `UserConsentVerifierInterop.RequestVerificationForWindowAsync` and supplies the active DeskVault window handle and verification message.

`WindowsHelloAuthenticationProvider` maps the Windows availability and verification results to the provider-neutral Application outcomes defined by this decision.

A missing or invalid window handle cannot be treated as successful verification. Unexpected platform failures are allowed to reach the Application authentication service, which is responsible for failing closed and recording a safe diagnostic.

The provider does not collect Windows account passwords, persist credentials, manage biometric information, or alter document or database encryption keys.

Infrastructure registers the Windows Hello platform implementation and the Application authentication provider through its production dependency-injection configuration.

#### UI Window Ownership

The UI layer owns the association between the Windows Hello request and the active WinForms window.

The implementation uses:

- `IActiveWindowsHelloFormAccessor`;
- `WinFormsActiveWindowsHelloFormAccessor`;
- `WindowsHelloWindowHandleProvider`.

The active-form accessor isolates access to the WinForms active-form state. The window-handle provider returns a usable handle only when an appropriate active form exists and its handle is available. Missing, disposed, disposing, or handle-less forms do not provide a usable verification handle.

The UI composition root registers the active-form accessor and window-handle provider for production use.

This keeps Windows Forms and native window ownership outside the Application authentication contract.

The UI layer also owns the WinForms user-activity adapter used by automatic inactivity locking. `WinFormsUserActivityMessageFilter` observes the application's input-message boundary and reports qualifying deliberate input through `IApplicationUserActivityService`.

The input adapter must not report passive mouse movement, painting, timer messages, or background processing as qualifying user activity. It associates activity with the currently observed authenticated session revision so that stale input cannot extend a newer session.

The message filter is started and stopped with the main form's UI lifecycle. It does not own the inactivity timeout, calculate the authoritative expiry deadline, or perform a lock transition. Those responsibilities remain in the Application automatic-lock service and the existing lifecycle service.

#### Startup Authentication Experience

`StartupAuthenticationForm` presents the Windows Hello verification request before the main application interface is created.

The form provides the user with the ability to initiate verification, retry after an unsuccessful attempt, or cancel startup. Cancellation is propagated to the authentication service. Closing the form while verification is in progress requests cancellation rather than allowing an unfinished authentication operation to continue as an accepted startup.

`DeskVaultStartupCoordinator` coordinates authentication and protected database initialization.

It uses the authoritative `IApplicationSecuritySession` snapshot to determine whether protected access is available. Database initialization is permitted only when the session is in the `Authenticated` state.

The database initializer is supplied to the coordinator through a deferred delegate. It is not resolved or invoked as a side effect of constructing the coordinator or displaying the authentication form.

The production startup sequence is:

```text
Application Startup
        |
        v
Build Production Host
        |
        v
Start Shared IHost and Hosted Services
        |
        v
Resolve StartupAuthenticationForm
        |
        v
Display Windows Hello Authentication Prompt
        |
        v
Check Windows Hello Availability
        |
        v
Request Verification for Active DeskVault HWND
        |
        v
Accept Verified Outcome Against Session Attempt
        |
        v
Confirm Authenticated Application Security Session
        |
        v
Invoke DatabaseInitializer.InitializeAsync()
        |
        v
Create MainForm
        |
        v
Run Main Application
        |
        v
Stop and Dispose Shared IHost
```

The shared host is started before the startup authentication form is resolved or displayed. This ensures that the production hosted-service lifecycle is established before authentication begins and that the same singleton service graph is retained throughout the application lifecycle.

Automatic inactivity monitoring is available as a hosted service during this lifetime. It does not establish an inactivity baseline or cause an automatic lock until the authoritative security session becomes authenticated.

A failed, cancelled, unavailable, unsupported, or otherwise unsuccessful authentication attempt does not permit database initialization or creation of `MainForm`.

Database initialization failure remains a separate startup failure. It is reported through the existing safe diagnostic boundary and a fixed generic user-facing message.

Database initialization and all protected-vault operations remain gated by the authoritative authenticated session state and the existing protected-key enforcement boundary.
The startup coordinator does not change database initialization, migration, cryptographic key provisioning, strict key retrieval, or encrypted database provider semantics.

If startup authentication is cancelled, authentication fails, or database initialization fails, the shared host must still be stopped and disposed.
The same cleanup requirement applies when normal WinForms execution terminates or raises an exception.

### No-Fallback Policy

MVP 2 will not fall back to:

- Windows account password collection or `LogonUser`;
- the presence of a Windows account or user profile;
- DPAPI availability or successful DPAPI unprotection alone;
- workstation lock, unlock, connect, disconnect, suspend, or resume events;
- an automatically selected network identity provider;
- a locally stored application password or passphrase;
- another unapproved authentication mechanism.

When Windows Hello is unavailable or verification does not succeed, protected access remains unavailable.

Workstation lock, logoff, disconnect, and suspend notifications withdraw
protected access through the existing Application lifecycle. Windows
session unlock, console or remote connect, and system resume notifications
do not restore access, begin authentication, or establish a successful
DeskVault authentication outcome. They are not substitutes for a fresh
Windows Hello verification.

The user may retry after Windows Hello availability is independently re-established. A retry must still complete a new successful verification.

No authentication failure or unavailable outcome authorizes reinitialization, replacement, or recreation of existing vault keys.

Automatic inactivity locking is not an authentication fallback. It delegates to the existing Application lifecycle lock operation and leaves re-authentication to the Windows Hello authentication boundary.

### Deferred Authentication Direction

A DeskVault master passphrase is retained as a possible MVP 3 authentication direction for separate evaluation.

It is not adopted by this ADR as an MVP 2 mechanism, secondary provider, fallback, recovery credential, or key-derivation input.

Any future master-passphrase capability requires a separate approved architectural decision covering credential enrollment, secure verification, password changes, recovery, throttling, credential storage, and whether the passphrase is merely an application authorization factor or participates in cryptographic key protection.

WebAuthn/FIDO2, smart-card/certificate authentication, and other platform or application-specific authentication methods are also not selected for MVP 2.

## Alternatives Considered

### Windows account password verification

Rejected for MVP 2.

A Windows account credential prompt followed by `LogonUser` would require the application to handle a plaintext password buffer and introduce additional credential-handling and local/domain account behavior. Windows Hello provides an operating-system-owned verification experience without requiring DeskVault to collect the Windows password.

### DeskVault master passphrase

Deferred to possible MVP 3 evaluation.

An application-specific passphrase would create an independent credential lifecycle and recovery requirement. It would also require a separate decision about whether it is only an application authorization factor or participates in unlocking cryptographic key material.

It is not included as an automatic fallback when Windows Hello is unavailable.

### Windows WebAuthn / FIDO2

Not selected for MVP 2.

WebAuthn can support phishing-resistant public-key credentials and external FIDO2 security keys. However, application credential registration, local challenge verification, credential storage, additional authenticator enrollment, loss/replacement, and recovery would add scope beyond the current MVP 2 requirement.

It remains a possible future authentication alternative.

### Smart-card or certificate authentication

Not selected for MVP 2.

Certificate enrollment, trust-chain management, expiration, revocation, and hardware availability introduce additional operational requirements not needed by the current local desktop authentication scope.

### Windows account context, DPAPI availability, or workstation unlock

Rejected as authentication.

These conditions do not establish a fresh Windows Hello user verification for the current DeskVault access attempt.

## Security and Threat-Model Implications

The selected mechanism establishes fresh verification through the current Windows user context. It does not provide a separate DeskVault identity or isolate DeskVault from arbitrary code already running with the same user's effective privileges.

The mechanism does not establish universal device binding, protect against a compromised Windows operating system, or prove that the user used a biometric rather than a configured PIN.

DPAPI `CurrentUser` remains a key-protection mechanism and must not be represented as proof of fresh authentication or universal device binding.

Protected content remains dependent on enforcement at the Application, protected-key, and presentation boundaries. Windows Hello verification alone does not guarantee that previously opened content, cached results, or in-flight operations are withdrawn after a later lock transition.

Automatic inactivity locking adds a runtime trigger for the existing Application lock lifecycle. It does not replace Windows Hello authentication, change the authentication provider, or independently authorize protected access.

An inactivity timeout must be measured using monotonic elapsed time and fenced to the authenticated session revision. Qualifying activity must be deliberate user input associated with the current session revision. Activity at or after the expiry deadline must not extend the authenticated session.

When automatic inactivity expiry occurs, it delegates to the existing lifecycle lock operation. The resulting non-authorized state is established by the authoritative Application security session. A later successful Windows Hello authentication remains necessary before protected access is restored.

The application must not log biometric data, credential material, authentication secrets, or sensitive exception details.

Unavailable, failed, or cancelled verification must remain non-authorized and must not be treated as a successful security condition.

Workstation monitoring is an independent security prerequisite. A
reported monitor failure blocks authentication even when Windows Hello
is available. A silently undelivered operating-system notification
cannot be inferred by the notification adapter; the implementation
documents this platform delivery limitation and does not claim complete
operating-system event capture.


## Consequences

### Positive

- DeskVault uses a familiar Windows-owned user-verification prompt.
- The application does not collect or persist the Windows password.
- Authentication remains local and does not introduce a network identity-provider dependency.
- Explicit availability and verification outcomes map to the existing Application security-session contract.
- Workstation-monitor readiness is an additional required authentication
  prerequisite and cannot be bypassed by Windows Hello availability.
- Workstation monitoring recovery restores an unavailable session only
  to `Locked`, preserving the requirement for fresh Windows Hello
  verification.
- Successful verification is required before protected database initialization.
- Application, Infrastructure, and UI responsibilities remain separated.
- Existing document/database encryption and key-protection responsibilities remain separate.
- The supported product baseline excludes Windows releases and editions that are no longer within Microsoft's servicing lifecycle.
- A future application-specific authentication mechanism can be evaluated independently without changing the authoritative session contract.
- Production hosted services start before startup authentication and remain associated with the same shared host throughout the application lifetime.
- Host shutdown and disposal are part of the application exit path, including startup cancellation and startup failure.
- Automatic inactivity expiry delegates to the existing lifecycle lock operation rather than introducing a separate authentication or authorization mechanism.
- A new inactivity baseline is established only after successful authentication of a new session revision.

### Negative

- Authentication depends on Windows Hello being configured, available, and permitted by policy.
- Users without an available Windows Hello verification capability cannot access the vault through an alternative MVP 2 method.
- Users on unsupported Windows releases or editions cannot rely on DeskVault receiving supported product compatibility.
- The product support baseline is more restrictive than the Windows Hello API's own minimum supported build.
- The implementation requires a valid application window handle for the desktop verification prompt.
- Actual Windows Hello hardware and policy behavior requires manual validation on a supported Windows host.
- Actual workstation WTS session and power-notification delivery requires
  interactive validation on supported Windows configurations.
- A notification silently not delivered to the process cannot be detected
  by the workstation event adapter.
- Windows Hello does not isolate the application from arbitrary same-user code or replace protected-key and presentation enforcement.
- The UI composition root must maintain the shared host lifetime and validated automatic-lock policy configuration.
- Qualifying activity must be classified correctly at the UI input boundary; incorrectly reported activity could extend an inactivity baseline.
- The periodic hosted evaluator must remain alive while the application session is active, and its lifecycle must be stopped and disposed reliably.
- Automatic-lock timing, activity acceptance, and expiry behavior require deterministic Application tests and integrated validation.

These trade-offs are acceptable for MVP 2 because the selected mechanism provides local user verification without introducing a separate credential store or changing the existing encryption architecture.

## Verification

The implementation is supported by automated regression coverage at the Application, Infrastructure, UI, and Integration boundaries.

Application-level verification covers:

- authentication availability and outcome handling;
- rejection of authentication when workstation monitoring is not `Ready`,
  including when Windows Hello reports `Available`;
- readiness-revision fencing across provider availability checks,
  authentication-attempt creation, provider invocation, and result
  acceptance;
- rejection of stale Windows Hello results after a workstation-monitoring
  failure or readiness-generation change;
- restoration of an unavailable session to `Locked` only after the
  workstation readiness prerequisite is re-established;
- successful, failed, cancelled, and unavailable authentication;
- restoration from an unavailable condition before a new attempt;
- authentication-attempt identity and stale completion rejection;
- cancellation and concurrent authentication behavior;
- safe handling of unexpected provider failures;
- singleton registration of the authentication service and security session;
- automatic-lock policy validation and immutable settings behavior;
- automatic-lock evaluation using monotonic time;
- qualification of activity by authenticated session revision;
- rejection of stale activity and activity at or after the inactivity deadline;
- delegation of automatic expiry to the existing lifecycle lock service.

Infrastructure-level verification covers:

- Windows Hello availability-result mapping;
- verification-result mapping;
- successful verification and unsuccessful outcomes;
- missing or invalid window handles;
- unexpected platform failures;
- production registration of the Windows Hello provider and platform adapter.

Integration-level verification covers:

- production Application and Infrastructure dependency-injection registration with Windows Hello as the sole authentication provider;
- mapping Windows availability and verification results through the production Windows Hello provider and Application authentication service into the authoritative session;
- successful, failed, cancelled, unavailable, and unknown verification outcomes;
- unavailable Windows Hello capability and an unavailable window handle preventing verification and protected access;
- Windows Hello availability being insufficient when workstation
  monitoring is not ready;
- authentication remaining blocked after a known monitoring failure,
  including when Windows Hello becomes available again;
- monitoring recovery returning the application session only to `Locked`
  and requiring fresh Windows Hello verification;
- automatic inactivity locking integrated with the real Application security-session and lifecycle services;
- accepted activity resetting the inactivity baseline before expiry;
- exact-deadline expiry locking the authenticated session;
- rejection of activity associated with an earlier authenticated session revision;
- successful re-authentication followed by a fresh inactivity baseline and a subsequent expiry.

UI-level verification covers:

- active-form HWND availability;
- absent, disposed, or handle-less forms;
- use of the current active form handle;
- production registration and composition of the HWND provider;
- startup coordinator behavior when the session is not authenticated;
- prevention of database initialization before authenticated access;
- database initialization after successful authentication;
- cancellation and initialization failure handling;
- production startup dependency-injection composition;
- shared host startup before startup authentication;
- hosted automatic-lock service startup and shutdown;
- workstation-security monitor hosted-service startup before
  startup authentication;
- authentication remaining blocked after initial workstation-monitor
  initialization failure while retry continues;
- workstation-monitor retry, recovery, and shutdown through the
  production shared Generic Host lifecycle;
- host stop and disposal after startup cancellation or failure;
- host stop and disposal after normal application termination;
- WinForms activity-filter lifecycle and reporting of qualifying input;
- exclusion of passive pointer movement, timer messages, and painting from qualifying activity.

The implementation also rejects a form during disposal; that transient state is guarded in production code but is not independently exercised by a dedicated automated test.

Build and test coverage establishes compile-time and automated regression behavior for the implemented boundaries. It does not, by itself, establish that Windows Hello is correctly configured or operational on every supported Windows device.

Manual validation on supported Windows 11 hardware and configurations remains necessary to verify the actual operating-system prompt, device availability, policy behavior, cancellation behavior, and HWND association.

The supported Windows servicing baseline must be reviewed when the deployed .NET runtime or Microsoft's Windows servicing lifecycle changes.

## Related Decisions

- ADR-0001 establishes DeskVault's offline-first product vision.
- ADR-0002 establishes the Domain, Application, Infrastructure, and UI boundaries, the composition-root model, and the shared Generic Host lifecycle.
- ADR-0004 establishes document encryption, DPAPI key protection, strict retrieval, and the authorized provisioning boundary.
- ADR-0009 establishes encrypted SQLite and database-key management.
- ADR-0014 establishes the authoritative Application security-session contract, authentication outcomes, state transitions, stale authentication-attempt rejection, automatic inactivity-lock coordination, and mandatory workstation-monitor readiness.
- The existing cryptographic key lifecycle remains governed by the relevant key-management decisions.

## References

- Microsoft Learn — UserConsentVerifier:
  https://learn.microsoft.com/en-us/uwp/api/windows.security.credentials.ui.userconsentverifier

- Microsoft Learn — IUserConsentVerifierInterop:
  https://learn.microsoft.com/en-us/windows/win32/api/userconsentverifierinterop/nn-userconsentverifierinterop-iuserconsentverifierinterop

- Microsoft Learn — RequestVerificationForWindowAsync:
  https://learn.microsoft.com/en-us/windows/win32/api/userconsentverifierinterop/nf-userconsentverifierinterop-iuserconsentverifierinterop-requestverificationforwindowasync

- Microsoft Learn — UserConsentVerificationResult:
  https://learn.microsoft.com/en-us/uwp/api/windows.security.credentials.ui.userconsentverificationresult

- Microsoft Learn — UserConsentVerifierAvailability:
  https://learn.microsoft.com/en-us/uwp/api/windows.security.credentials.ui.userconsentverifieravailability

- Microsoft Learn — Install .NET on Windows:
  https://learn.microsoft.com/en-us/dotnet/core/install/windows

- Microsoft Learn — Windows 11 release information:
  https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information

- Microsoft Lifecycle — Windows 11 Home and Pro:
  https://learn.microsoft.com/en-us/lifecycle/products/windows-11-home-and-pro

- Microsoft Lifecycle — Windows 11 Enterprise and Education:
  https://learn.microsoft.com/en-us/lifecycle/products/windows-11-enterprise-and-education-version-21h2

- Microsoft Learn — Windows versions and SDK overview:
  https://learn.microsoft.com/en-us/windows/apps/get-started/versioning-overview

## Result

DeskVault MVP 2 will use Windows Hello as its sole authentication mechanism for protected application access.

The Application security session remains the authoritative authorization boundary. Authentication must succeed before protected database initialization, and unavailable, failed, cancelled, or unsupported verification must fail closed.

The implementation preserves the separation between Application authentication orchestration, Infrastructure Windows Hello integration, and UI window ownership. The startup coordinator ensures that database initialization and creation of the main application interface occur only after the Application security session confirms authenticated protected access.

The production application uses one shared Generic Host. The host and its hosted services start before startup authentication and remain active throughout authentication, database initialization, and the WinForms application lifetime. The host is stopped and disposed on startup cancellation, startup failure, and normal application termination.

Automatic inactivity locking is coordinated by the Application layer using an immutable validated policy snapshot, monotonic time, and authenticated session revision fencing. The UI supplies deliberate activity events and hosted evaluation scheduling. Expiry delegates to the existing Application lifecycle lock operation, and Windows Hello remains the sole authentication mechanism for restoring protected access.

The supported product baseline is Windows 11 version 25H2 or later while serviced and supported by the deployed .NET 10 runtime, together with Windows 11 version 24H2 Enterprise and Education editions only while serviced. Windows 10 and Windows 11 version 24H2 Home/Pro-family editions are not supported by this MVP 2 policy.

A DeskVault master passphrase remains a possible future MVP 3 direction and is not an MVP 2 fallback or implementation scope.
