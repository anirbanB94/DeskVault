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

If a capability previously marked unavailable becomes available again, the Application authentication service must restore the session from `Unavailable` to `Locked` through `IApplicationSecuritySession.RestoreAvailability()` before beginning a new authentication attempt.

Recovery from an unavailable condition never transitions directly to `Authenticated`.

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
```

A failed, cancelled, unavailable, unsupported, or otherwise unsuccessful authentication attempt does not permit database initialization or creation of `MainForm`.

Database initialization failure remains a separate startup failure. It is reported through the existing safe diagnostic boundary and a fixed generic user-facing message.

The startup coordinator does not change database initialization, migration, cryptographic key provisioning, strict key retrieval, or encrypted database provider semantics.

### No-Fallback Policy

MVP 2 will not fall back to:

- Windows account password collection or `LogonUser`;
- the presence of a Windows account or user profile;
- DPAPI availability or successful DPAPI unprotection alone;
- workstation lock/unlock or resume events;
- an automatically selected network identity provider;
- a locally stored application password or passphrase;
- another unapproved authentication mechanism.

When Windows Hello is unavailable or verification does not succeed, protected access remains unavailable.

The user may retry after Windows Hello availability is independently re-established. A retry must still complete a new successful verification.

No authentication failure or unavailable outcome authorizes reinitialization, replacement, or recreation of existing vault keys.

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

The application must not log biometric data, credential material, authentication secrets, or sensitive exception details.

Unavailable, failed, or cancelled verification must remain non-authorized and must not be treated as a successful security condition.

## Consequences

### Positive

- DeskVault uses a familiar Windows-owned user-verification prompt.
- The application does not collect or persist the Windows password.
- Authentication remains local and does not introduce a network identity-provider dependency.
- Explicit availability and verification outcomes map to the existing Application security-session contract.
- Successful verification is required before protected database initialization.
- Application, Infrastructure, and UI responsibilities remain separated.
- Existing document/database encryption and key-protection responsibilities remain separate.
- The supported product baseline excludes Windows releases and editions that are no longer within Microsoft's servicing lifecycle.
- A future application-specific authentication mechanism can be evaluated independently without changing the authoritative session contract.

### Negative

- Authentication depends on Windows Hello being configured, available, and permitted by policy.
- Users without an available Windows Hello verification capability cannot access the vault through an alternative MVP 2 method.
- Users on unsupported Windows releases or editions cannot rely on DeskVault receiving supported product compatibility.
- The product support baseline is more restrictive than the Windows Hello API's own minimum supported build.
- The implementation requires a valid application window handle for the desktop verification prompt.
- Actual Windows Hello hardware and policy behavior requires manual validation on a supported Windows host.
- Windows Hello does not isolate the application from arbitrary same-user code or replace protected-key and presentation enforcement.

These trade-offs are acceptable for MVP 2 because the selected mechanism provides local user verification without introducing a separate credential store or changing the existing encryption architecture.

## Verification

The implementation is supported by automated regression coverage at the Application, Infrastructure, UI, and Integration boundaries.

Application-level verification covers:

- authentication availability and outcome handling;
- successful, failed, cancelled, and unavailable authentication;
- restoration from an unavailable condition before a new attempt;
- authentication-attempt identity and stale completion rejection;
- cancellation and concurrent authentication behavior;
- safe handling of unexpected provider failures;
- singleton registration of the authentication service and security session.

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
- unavailable Windows Hello capability and an unavailable window handle preventing verification and protected access.

UI-level verification covers:

- active-form HWND availability;
- absent, disposed, or handle-less forms;
- use of the current active form handle;
- production registration and composition of the HWND provider;
- startup coordinator behavior when the session is not authenticated;
- prevention of database initialization before authenticated access;
- database initialization after successful authentication;
- cancellation and initialization failure handling;
- production startup dependency-injection composition.

The implementation also rejects a form during disposal; that transient state is guarded in production code but is not independently exercised by a dedicated automated test.

Build and test coverage establishes compile-time and automated regression behavior for the implemented boundaries. It does not, by itself, establish that Windows Hello is correctly configured or operational on every supported Windows device.

Manual validation on supported Windows 11 hardware and configurations remains necessary to verify the actual operating-system prompt, device availability, policy behavior, cancellation behavior, and HWND association.

The supported Windows servicing baseline must be reviewed when the deployed .NET runtime or Microsoft's Windows servicing lifecycle changes.

## Related Decisions

- ADR-0001 establishes DeskVault's offline-first product vision.
- ADR-0002 establishes the Domain, Application, Infrastructure, and UI boundaries and the composition-root model.
- ADR-0004 establishes document encryption, DPAPI key protection, strict retrieval, and the authorized provisioning boundary.
- ADR-0009 establishes encrypted SQLite and database-key management.
- ADR-0014 establishes the authoritative Application security-session contract, authentication outcomes, state transitions, and stale authentication-attempt rejection.
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

The supported product baseline is Windows 11 version 25H2 or later while serviced and supported by the deployed .NET 10 runtime, together with Windows 11 version 24H2 Enterprise and Education editions only while serviced. Windows 10 and Windows 11 version 24H2 Home/Pro-family editions are not supported by this MVP 2 policy.

A DeskVault master passphrase remains a possible future MVP 3 direction and is not an MVP 2 fallback or implementation scope.
