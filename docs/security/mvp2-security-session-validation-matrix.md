# MVP2 Security Session Validation Matrix

**Parent PBI:** [#185 — Protect Vault Access During an Active DeskVault Session](https://github.com/anirbanB94/DeskVault/issues/185)
**Validation owner:** [#192 — Validate locked-session security boundaries](https://github.com/anirbanB94/DeskVault/issues/192)
**Evidence snapshot:** 2026-11-13
**Status:** Integrated automated evidence is recorded below, including #351 current-session reconciliation and independent suspend/resume callback handling. Manual Windows checks now record successful workstation lock, sign-out, Modern Standby resume, and hibernate resume on the documented x64 host. Remote-session disconnect remains unexecuted on the available host, and residual detection and event-delivery limitations remain explicit.

## 1. Purpose and scope

This matrix is the parent-outcome traceability record required by Technical Task #192. It maps each of the 14 observable acceptance criteria in #185 to the implementing Technical Task(s), the strongest available automated or manual validation evidence, its current evidence status, and any remaining gap.

#192 owns integrated verification across the production dependency-injection graph and the parent traceability record. It does not take ownership of the production features implemented by its component tasks:

- #186 owns the framework-neutral Application security-session contract.
- #187 owns Windows Hello authentication integration.
- #188 owns the authoritative application lock/unlock lifecycle.
- #189 owns session-authorized document and database key retrieval.
- #190 owns automatic-lock configuration, qualifying activity, monotonic inactivity measurement, and expiry triggering.
- #191 owns the original Windows workstation-event integration, monitoring readiness, and known monitoring-failure handling.
- #351 extends #191 with current-process WTS session-state reconciliation, independent suspend/resume callback observation, reconciliation-worker liveness, bounded native-query lifecycle, and additional monitoring-reliability evidence.
- #320 owns in-app presentation protection, retained-content cleanup, and suppression of stale asynchronous presentation results.
- #193 remains authoritative for secure external document viewing and its temporary plaintext lifecycle.
- #214 remains authoritative for active-vault concurrency between application instances.
- #220 remains authoritative for cryptographic key provisioning/retrieval semantics and key identity/version.
- #221 remains authoritative for cross-cutting security hardening outside this session/access capability.

The matrix does not claim to validate the implementation scope of #193, #214, #220, or #221. It records their responsibility boundaries where they constrain the parent outcomes.

## 2. Evidence-status definitions

| Status | Meaning |
|---|---|
| **PASS — automated** | The mapped behavior has relevant automated evidence recorded in a completed component task and/or the current #192 integrated test run. |
| **PASS — manual and automated** | Automated coverage exists and the specified Windows interactive behavior was also recorded as manually verified. |
| **PARTIAL — manual/platform evidence outstanding** | Automated event or boundary behavior is covered, but the recorded evidence does not establish every required native Windows delivery scenario or platform distinction. |
| **NOT EXECUTED** | A documented manual check has not been performed. This is not a pass. |

Historical solution-wide totals below are evidence recorded by the corresponding completed component task. They are not presented as a new #192 full-solution run. #192's own latest executed checks are listed separately in Section 5.

## 3. Parent #185 acceptance-criteria traceability

**Current evidence summary:** 12 of 14 parent criteria have PASS evidence. AC9 and AC10 remain PARTIAL for the explicit Windows native-delivery and unobservable-notification limitations described below. These partial outcomes must remain visible in any parent closure assessment.

### AC1 — Authentication must precede protected vault access

**Parent outcome:** Given DeskVault has not established successful authentication for vault access, when the application starts or protected access is requested, protected vault content remains unavailable until supported DeskVault authentication succeeds.

- **Implementing tasks:** #186 (authoritative session state), #187 (Windows Hello authentication and startup gate), #188 (lock/unlock lifecycle), #192 (integrated verification).
- **Automated evidence:** `ApplicationSecuritySessionTests`, `ApplicationSecuritySessionSnapshotTests`, `ApplicationDependencyInjectionTests`, `WindowsHelloAuthenticationIntegrationTests`, `DeskVaultStartupCompositionTests`, `DeskVaultStartupCoordinatorTests`, and `ProgramStartupTests`. #192 adds `BuildForTesting_UsesProductionRegistrationsAndStartsWorkstationMonitoringBeforeHostStartupCompletes` and `HostStartup_WhenWorkstationMonitoringInitiallyFails_BlocksAuthenticationAndDatabaseInitialization_UntilMonitoringRecoversAndFreshAuthenticationSucceeds` in `ProductionSecuritySessionHostIntegrationTests`.
- **Manual evidence:** #187 records the native Windows Security prompt, successful authentication and vault startup, cancellation without protected startup, and successful retry.
- **Evidence status:** **PASS — manual and automated.**
- **Gap:** None identified for this outcome. The #192 full-solution build and test suite passed on 2026-11-11; see Section 5 for the task-specific execution results.

### AC2 — Failed, cancelled, unavailable, or unsafe authentication must fail closed

**Parent outcome:** Given authentication fails, is cancelled or unavailable, or a required security condition cannot be established safely, protected vault access remains unavailable and none of these outcomes is treated as successful authentication.

- **Implementing tasks:** #186, #187, #188, #191, #192.
- **Automated evidence:** `ApplicationAuthenticationServiceTests`, `WindowsHelloAuthenticationProviderTests`, `WindowsHelloAuthenticationIntegrationTests`, `ApplicationSecuritySessionLifecycleServiceTests`, `WorkstationSecurityMonitorFailurePathTests`, and `WorkstationSecurityMonitorTests`. #192's initial-monitoring-failure and runtime-monitoring-failure integration tests verify that Windows Hello is not invoked while monitoring is unavailable; the initial-failure test also verifies database initialization is blocked and the database file is not created.
- **Manual evidence:** #187 records successful Windows Hello verification, cancellation without protected startup, and a successful retry.
- **Evidence status:** **PASS — manual and automated.**
- **Gap:** None identified for the authentication and monitoring outcomes covered by the owning tasks.

### AC3 — All in-app protected-access paths must be unavailable while locked

**Parent outcome:** Given the session is unauthenticated or locked, protected content remains unavailable through in-app paths, including already-open document/workspace views, rendered content, cached results, sensitive metadata, and further protected actions.

- **Implementing tasks:** #186 (authorization state), #189 (document/database key boundary), #320 (in-app presentation enforcement), #192 (integrated consistency verification).
- **Automated evidence:** `SessionAuthorizedKeyRetrievalTests`, `SessionAuthorizedKeyServicesTests`, `SessionAuthorizedKeyAvailabilityIntegrationTests`; #320's `SessionRevocationPresentationCleanupTests`, `MainFormPresenterTests`, `DocumentWorkspacePresenterTests`, `DocumentWorkspacePresenterSessionRevisionTests`, `WorkspacePresentationSessionRevisionTests`, `WorkspacePresentationManagerSessionRevisionTests`, and `WorkspacePresentationFactorySessionRevisionTests`. #192 adds `DocumentPresentation_WhenSessionLocksDuringPendingRender_ClearsRetainedContentAndRejectsLateResultAfterReauthentication` and `WorkspacePresentation_WhenOwningSessionIsRevoked_DisposesWorkspaceAndChildPresentationOnTheirUiDispatchers`.
- **Manual evidence:** The #191 Windows+L check verified the locked DeskVault experience and that an in-app Unlock action followed by successful Windows Hello authentication was required before protected use resumed. The focused retained-content and presenter behavior is primarily verified automatically by #320 and #192.
- **Evidence status:** **PASS — automated**, with the recorded Windows+L interactive check as supplementary evidence.
- **Gap:** No automated gap identified in the mapped in-app presentation/key boundaries. This does not claim forensic erasure of every managed-memory copy; #320 explicitly limits its cleanup guarantee to the defined presentation/resource lifecycle.

### AC4 — In-flight protected operations must not publish results after lock

**Parent outcome:** Given a protected operation is in progress when the session becomes locked, its later completion or asynchronous result is not displayed or made available while access is unauthorized.

- **Implementing tasks:** #188 (authoritative lock), #189 (protected-key retrieval authorization), #320 (presentation-operation invalidation and cleanup), #192 (cross-component race verification).
- **Automated evidence:** #189's `SessionAuthorizedKeyRetrievalTests` and `SessionAuthorizedKeyServicesTests` cover retrieval-versus-lock races and acquired-buffer clearing. #320's `DocumentWorkspacePresenterSessionRevisionTests`, `WorkspacePresentationSessionRevisionTests`, and `SessionRevocationPresentationCleanupTests` cover stale presentation operations. #192's pending-render presentation integration test locks the production session while `ShowDocumentAsync` is pending and verifies cleanup and late-result rejection.
- **Manual evidence:** No separate manual asynchronous-race check is claimed; deterministic automated tests provide the evidence for this race.
- **Evidence status:** **PASS — automated.**
- **Gap:** None identified for the in-app asynchronous presentation and key-retrieval boundaries covered by these tasks.

### AC5 — Invalidated results must not return after re-authentication

**Parent outcome:** Given a result was invalidated by locking or a newer session transition, later successful authentication must not automatically present or reuse that stale result; protected content must be obtained through a new authorized operation.

- **Implementing tasks:** #186 (session revisions and stale-attempt rejection), #188 (lock invalidates earlier unlock operations), #320 (presentation revision fencing), #192 (integrated verification).
- **Automated evidence:** `ApplicationSecuritySessionTests`, `ApplicationSecuritySessionLifecycleServiceTests`, `DocumentWorkspacePresenterSessionRevisionTests`, `WorkspacePresentationSessionRevisionTests`, and `WorkspacePresentationManagerSessionRevisionTests`. #192's pending-render test completes the old presentation only after a newer authenticated revision exists and verifies the old presenter remains invalidated, retains no document metadata/stream, and does not present or activate the stale document.
- **Manual evidence:** No separate manual stale-result test is claimed; the race is deterministic and automatically tested.
- **Evidence status:** **PASS — automated.**
- **Gap:** None identified.

### AC6 — Overlapping or repeated lock/authentication operations must not restore stale authorization

**Parent outcome:** Given lock, authentication, or unlock operations overlap or repeat, protected access is not restored by a stale or out-of-order outcome; access is restored only after successful DeskVault authentication following the most recent lock.

- **Implementing tasks:** #186 (session transition/revision contract), #187 (authentication outcome integration), #188 (lock/unlock operation generation), #191 (monitor-generation fencing), #192 (integrated race verification).
- **Automated evidence:** `ApplicationSecuritySessionTests`, `ApplicationSecuritySessionLifecycleServiceTests`, `WindowsHelloAuthenticationIntegrationTests`, `WorkstationSecurityMonitorTests`, and `WorkstationSecurityMonitorFailurePathTests`. #192's `RuntimeMonitoringFailure_WhenWindowsHelloIsInFlight_RejectsDelayedAuthenticationAndIgnoresPreviousMonitoringGenerationCallback` holds a real production-provider verification request pending, triggers monitor failure and recovery, completes the old successful result, and verifies it is superseded. It then delivers an old-generation callback after a newer successful authentication and verifies the authenticated revision is unchanged.
- **Manual evidence:** #187 and #191 have separate Windows interactive checks for native authentication and workstation lock; these do not replace the automated concurrency evidence.
- **Evidence status:** **PASS — automated.**
- **Gap:** None identified for the stale authentication and monitoring-generation races covered by the integrated test.

### AC7 — Automatic inactivity locking must use qualifying user activity and the configured timeout

**Parent outcome:** Given automatic locking is enabled with valid settings, qualifying user inactivity reaching the configured timeout locks the session and requires authentication again; background processing and other non-user activity do not extend inactivity.

- **Implementing tasks:** #188 (lock lifecycle), #190 (automatic-lock policy, qualifying activity, monotonic time and expiry), #192 (integrated key/access verification).
- **Automated evidence:** `ApplicationAutomaticLockServiceTests`, `ApplicationAutomaticLockIntegrationTests`, `ApplicationAutomaticLockHostedServiceTests`, and #192's `AutomaticLock_WhenQualifyingActivityResetsBaseline_ExpiresThroughProductionLifecycleAndDeniesDocumentAndDatabaseAccessUntilFreshAuthentication`. The #192 test uses controlled monotonic time, resets the inactivity baseline with qualifying activity, checks immediately before the expiry boundary, evaluates at the exact deadline, verifies the production lifecycle locks, checks document/database key and protected-access denial, and verifies fresh authentication restores access.
- **Manual evidence:** No manual clock-wait test is claimed; the deadline and activity behavior are verified deterministically with controlled time.
- **Evidence status:** **PASS — automated.**
- **Gap:** None identified for the configured expiry and production key-withdrawal path.

### AC8 — Automatic-lock configuration must follow the documented fail-closed contract

**Parent outcome:** An entirely absent effective `Security:AutomaticLock` section preserves the disabled default. A present section requires a Boolean `Enabled`; enabled policy requires an integer timeout from 1 through 1,440 minutes; disabled policy may omit the timeout but any supplied timeout must be valid. Missing, malformed, or unevaluable required configuration prevents authentication and keeps protected access unavailable.

- **Implementing tasks:** #190 (effective configuration contract and validation), #192 (production-host configuration integration).
- **Automated evidence:** `AutomaticLockSettingsFactoryTests` (23/23 in #190's recorded focused result), `ProgramStartupTests`, `DeskVaultStartupCompositionTests`, and the #192 production test host, which passes explicit `Security:AutomaticLock:Enabled` and `InactivityTimeoutMinutes` overrides to the shared production composition before the policy is validated. The #190 validation record covers the absent-section default and malformed/present-section behavior.
- **Manual evidence:** Not required for deterministic configuration parsing and startup validation; no manual test is claimed.
- **Evidence status:** **PASS — automated.**
- **Gap:** None identified from the #190 configuration evidence and #192 production composition usage.

### AC9 — Supported workstation security transitions must withdraw access

**Parent outcome:** Supported workstation lock, logoff/sign-out, suspend, or hibernate withdraw protected access and require DeskVault re-authentication. Windows session unlock or workstation resume alone must not restore access.

- **Implementing tasks:** #188 (authoritative lock lifecycle), #191 (original Windows workstation event-source mapping and monitor), #351 (current-session reconciliation and independent suspend/resume callback), and #192 (integrated monitor/lifecycle verification).
- **Automated evidence:** Existing `WorkstationSecurityMonitorTests`, `WindowsWorkstationSecurityEventSourceTests`, `WindowsWorkstationSecurityEventSourceFailurePathTests`, `WindowsWorkstationSecurityNativeApiTests`, and #192 production integration coverage remain applicable. #351 adds the following focused selections, all passed on 2026-11-12:
  - `WindowsWorkstationSecurityNativeApiTests`, `WindowsWorkstationSecuritySessionStateParserTests`, and `WindowsWorkstationSecuritySessionReconciliationWorkerTests`: **45/45 passed**. This covers process-session identity, WTS structure/state validation, native buffer cleanup, initial-query failure, persistent locked/disconnected state handling, query timeout/no overlap, worker liveness, polling/cadence measurements, bounded shutdown, and cancellation-related native-buffer cleanup.
  - `WindowsWorkstationSecurityEventSourceTests` and `WindowsWorkstationPowerNotificationDispatcherTests`: **35/35 passed**. This covers independent power callback registration, event mapping, bounded dispatch outside the callback stack, suspend-before-resume ordering, failure paths, unregistration, and cleanup.
  - `WorkstationSecurityMonitorTests` and `WorkstationSecurityMonitorFailurePathTests`: **20/20 passed**. These verify that lock-triggering events, including `SystemResumed`, use the existing lifecycle lock; Windows session unlock and connect do not grant authorization.
  - `ProductionSecuritySessionHostIntegrationTests`: **8/8 passed**. Production-host integration verifies readiness gating, authentication fencing, monitoring-failure behavior, required workstation transitions, and continued protected workflow behavior after fresh authentication.
  - Two #351 reconciliation integration cases intentionally suppress the primary WTS notification and verify that a persistent locked state or disconnected state is independently discovered and locks the authenticated application session.
- **Manual evidence:** #191 records Windows+L followed by Windows sign-in: DeskVault remained locked, protected actions remained unavailable, and the in-app Unlock action required fresh Windows Hello authentication. On 2026-11-13, additional interactive checks on Windows 11 Home Single Language 26H2, build 26300.9974 (x64), passed for Windows sign-out/sign-in, Modern Standby (S0) resume, and hibernate/resume. DeskVault required fresh in-app authentication after each transition. Remote-session disconnect/reconnect was not executed because a suitable remote-session host was not available in this test environment.
- **Platform limitations:** The current-session query detects persistent locked or disconnected state at a later poll when the corresponding primary WTS window notification is missed. It does not reconstruct complete Windows session history and cannot guarantee observation of a transient transition that begins and ends between polling samples. The selected power notification API does not independently distinguish suspend from hibernate. The manual Modern Standby and hibernate checks were initiated as separate Windows power operations; they do not establish separate native power-event classifications.
- **Evidence status:** **PARTIAL — Windows workstation lock, sign-out/sign-in, Modern Standby resume, and hibernate/resume have passed manual validation on the available x64 host. Remote-session disconnect/reconnect remains unexecuted because a suitable remote-session host was unavailable. The selected notification mechanism also does not independently distinguish suspend from hibernate.**
- **Gap:** Execute remote-session disconnect/reconnect on a suitable supported remote Windows host when available. No numeric detection-latency measurement or native event-source trace capture was recorded during the November 13 manual checks. Preserve the transient-state and power-notification limitations; do not claim that automated notification mapping proves native delivery.

### AC10 — Workstation monitoring must fail closed when protection cannot be established reliably

**Parent outcome:** Given required workstation security protection cannot be established or monitored reliably, DeskVault must not silently continue exposing protected content under an assumed-secure session.

- **Implementing tasks:** #191 (original monitor readiness and known failure handling), #351 (WTS reconciliation, independent power callback, query/worker/callback failure handling), and #192 (production host startup, runtime failure and recovery integration).
- **Automated evidence:** Existing `WorkstationSecurityMonitorFailurePathTests`, `WindowsWorkstationSecurityEventSourceFailurePathTests`, `WindowsWorkstationSecurityEventSourceTests`, `DeskVaultStartupCompositionTests`, and #192's initial and runtime monitoring-failure host integration tests verify known failures leave the session unavailable, prevent Windows Hello authentication while monitoring is unavailable, block protected database initialization on initial failure, and withdraw access on runtime failure.

  #351 adds focused automated evidence executed on 2026-11-12:
  - The native API/parser/reconciliation selection passed **45/45**. It covers WTS query failure, malformed/unsupported state, parser rejection, native buffer cleanup, initial query failure, query timeout without overlap, process-session identity changes, worker exit/liveness failure, and bounded shutdown.
  - The event-source/power-dispatcher selection passed **35/35**. It covers WTS and independent power registration failures, callback signalling and dispatch failure, callback unregistration failure, bounded dispatcher shutdown, and cleanup/retry behavior.
  - The Application monitor and failure-path selection passed **20/20**. It verifies lifecycle lock ordering and that a monitoring failure supersedes an in-flight unlock before the session becomes unavailable.
  - The production security-session host integration selection passed **8/8**. It verifies monitoring readiness before authentication, initial and runtime failure handling, recovery only to Locked, and stale authentication rejection.
  - The two suppressed-primary-WTS-notification integration cases verify that persistent locked and disconnected process-session states are found through independent reconciliation and withdraw protected access.
- **Manual evidence:** The #191 Windows+L check and the #351 sign-out/sign-in, Modern Standby resume, and hibernate/resume checks in Section 6 are supplementary evidence of normal transition outcomes. They are not evidence of native event-source failure injection. The #351 native registration, query, worker, callback, and failure paths have separate automated evidence.
- **Evidence status:** **PARTIAL — known initialization, query, worker, callback, dispatcher, and cleanup failures fail closed, and persistent locked/disconnected process-session state can be reconciled when the primary WTS window notification is suppressed. The implementation cannot guarantee observation of every silently missed or transient operating-system transition.**
- **Gap:** A transient locked or disconnected state that begins and ends between WTS reconciliation samples may remain undetected if the corresponding WTS notification is also missed. A power transition may remain unobservable if both the window power-broadcast path and the independent callback fail to report it. AC10 must not be marked fully satisfied without an explicit product-level acceptance of these residual platform limitations or a separately approved mitigation.

### AC11 — Failed protected-access checks must not replace or recreate encrypted data

**Parent outcome:** Given a required protected-access check fails, including document or database access that cannot be established safely, protected content remains unavailable and existing encrypted vault data is not silently replaced or unnecessarily recreated.

- **Implementing tasks:** #189 (strict session-authorized key retrieval and provisioning separation), #192 (production database/document denial and artifact-reuse integration). #220 remains the broader key provisioning/retrieval semantics owner.
- **Automated evidence:** `SessionAuthorizedKeyRetrievalTests`, `SessionAuthorizedKeyServicesTests`, `SessionAuthorizedKeyAvailabilityIntegrationTests`, `EncryptedDatabasePipelineIntegrationTests`, and #192's `AutomaticLock_WhenQualifyingActivityResetsBaseline_ExpiresThroughProductionLifecycleAndDeniesDocumentAndDatabaseAccessUntilFreshAuthentication`. #189 records denied retrieval without provisioning or mutation of existing key artifacts. #192 verifies both production key services and the encrypted database factory deny protected access after lock.
- **Manual evidence:** Not required for deterministic key-denial and artifact-preservation checks; no manual test is claimed.
- **Evidence status:** **PASS — automated.**
- **Gap:** None identified for the document/database key and artifact-preservation paths covered by #189 and #192.

### AC12 — Re-authentication must restore access without unnecessary vault recreation or storage-model changes

**Parent outcome:** Given DeskVault is locked, successful re-authentication restores vault access without unnecessary vault re-creation, data loss, or changes to the existing encrypted storage model.

- **Implementing tasks:** #187 (authentication), #188 (unlock lifecycle), #189 (existing key retrieval without provisioning side effects), #192 (production vault reuse verification). #220 remains authoritative for key identity/version and provisioning semantics.
- **Automated evidence:** `SessionAuthorizedKeyAvailabilityIntegrationTests`, `DatabaseEncryptionIntegrationTests`, `EncryptedDatabasePipelineIntegrationTests`, `WindowsDatabaseEncryptionKeyServiceIntegrationTests`, and #192's automatic-lock integration test. The #192 test verifies that document/database key identity and version metadata are unchanged across lock and re-authentication, the existing encrypted document can be decrypted again, and the existing encrypted database can be opened again.
- **Manual evidence:** #187 records successful Windows Hello authentication and access to existing vault content after authentication.
- **Evidence status:** **PASS — automated and supplementary manual evidence.**
- **Gap:** None identified for the existing-key and existing-database paths covered. This is not a claim of testing backup/restore, multi-instance concurrency (#214), or a new cryptographic design.

### AC13 — Existing document, workspace, search and processing behavior must remain usable after access is restored

**Parent outcome:** Given vault access has been validly restored, existing document, workspace, search and processing behavior remains usable.

- **Implementing tasks:** #192 owns the integrated regression proof. The underlying document-processing, search, and workspace features remain owned by their existing application capabilities; #187/#188 provide the authentication and lock/unlock prerequisite.
- **Automated evidence:** #192 adds `ExistingDocumentWorkspaceSearchAndProcessingFlows_AfterFreshAuthentication_RemainUsableThroughProductionHost`. Through handlers resolved from the production host, the test imports a source document, processes it, searches processed content, creates a persistent workspace, and retrieves its document membership. It then locks the production session and verifies processing, search, and workspace retrieval are denied. After fresh production authentication, it verifies processing, search, and workspace retrieval succeed again for the existing document/workspace.
- **Manual evidence:** Not required for the integrated application-handler regression; no separate manual workflow is claimed.
- **Evidence status:** **PASS — automated.** The test passed 1/1 in the latest #192 verification run.
- **Gap:** None identified for the tested import/process/search/persistent-workspace flow.

### AC14 — No sensitive vault content may be exposed in the locked-session experience

**Parent outcome:** No sensitive vault content is exposed as part of the locked-session experience.

- **Implementing tasks:** #189 (protected-key boundary), #320 (in-app retained-content and presentation protection), #192 (integrated consistency verification).
- **Automated evidence:** #189's key retrieval and availability tests; #320's `SessionRevocationPresentationCleanupTests`, `DocumentWorkspacePresenterSessionRevisionTests`, `WorkspacePresentationSessionRevisionTests`, `WorkspacePresentationManagerSessionRevisionTests`, and `WorkspacePresentationFactorySessionRevisionTests`; #192's document and workspace production-session presentation integration tests. The #192 tests verify that protected keys are denied, document content/metadata is cleared, retained streams are disposed, workspace state is removed, and stale presentation work cannot publish after re-authentication.
- **Manual evidence:** The #191 Windows+L check verified DeskVault remains in its locked state until in-app re-authentication. This is supplementary and does not replace presentation-content assertions in automated tests.
- **Evidence status:** **PASS — automated**, with the recorded Windows+L check as supplementary evidence.
- **Gap:** None identified for the in-app presentation and protected-key boundaries covered by #189/#320/#192. External viewing and temporary plaintext remain outside this matrix's implementation scope and are governed by #193.

## 4. Reused component-level regression evidence

The following results are historical evidence recorded by each completed task. They are referenced to demonstrate reuse and ownership; they must not be added together or presented as the current #192 full-suite result.

| Task | Recorded validation evidence | Role in #192 traceability |
|---|---|---|
| [#186](https://github.com/anirbanB94/DeskVault/issues/186) | Full solution 1,594/1,594; `ApplicationSecuritySessionTests` 32/32; `ApplicationSecuritySessionSnapshotTests` 8/8; `ApplicationDependencyInjectionTests` 6/6. | Authoritative session states, authorization availability, revision ordering and stale authentication behavior. |
| [#187](https://github.com/anirbanB94/DeskVault/issues/187) | Full solution 1,663/1,663; Application authentication/session integration 57/57; Windows Hello Infrastructure/provider/DI 26/26; HWND/composition 6/6; startup composition 10/10; Windows Hello integration 15/15. Manual Windows Hello success/cancellation/retry recorded. | Approved authentication provider, startup authentication gate and failure/cancellation/unavailability outcomes. |
| [#188](https://github.com/anirbanB94/DeskVault/issues/188) | Full solution 1,684/1,684; Application lifecycle tests 16/16; Application DI 1/1; Windows Hello runtime unlock integration 4/4. | Authoritative lock/unlock lifecycle and invalidation of competing or stale operations. |
| [#189](https://github.com/anirbanB94/DeskVault/issues/189) | Full solution 1,711/1,711; `SessionAuthorizedKeyRetrievalTests` 8/8; `SessionAuthorizedKeyServicesTests` 6/6; `SessionAuthorizedKeyAvailabilityIntegrationTests` 2/2; encrypted database pipeline 9/9; aggregate focused selections 59/59. | Production session-authorized document/database key retrieval, lock/retrieval race handling, buffer clearing, no provisioning on denied retrieval, and preservation of existing key artifacts. |
| [#190](https://github.com/anirbanB94/DeskVault/issues/190) | Full solution 1,892/1,892; `ApplicationAutomaticLockServiceTests` 11/11; `ApplicationAutomaticLockIntegrationTests` 2/2; `ApplicationAutomaticLockHostedServiceTests` 4/4; `AutomaticLockSettingsFactoryTests` 23/23; aggregate focused selections 125/125. | Configuration policy, monotonic inactivity timing, qualifying activity, production host lifetime and automatic-lock triggering. |
| [#191](https://github.com/anirbanB94/DeskVault/issues/191) | Full solution 1,943/1,943; `WorkstationSecurityMonitorFailurePathTests` 2/2; `WorkstationSecurityMonitorTests` 17/17; `WindowsWorkstationSecurityEventSourceFailurePathTests` 1/1; `WindowsWorkstationSecurityEventSourceTests` 18/18; native API test 1/1. Manual Windows+L check recorded. | Workstation event mapping, monitoring readiness, failure/recovery, native event-source lifecycle and stale callback handling. |
| [#320](https://github.com/anirbanB94/DeskVault/issues/320) | Full solution 1,814/1,814; UI suite 416/416; aggregate changed UI test classes 222/222. Relevant classes include `SessionRevocationPresentationCleanupTests`, `DocumentWorkspacePresenterSessionRevisionTests`, `WorkspacePresentationSessionRevisionTests`, `WorkspacePresentationManagerSessionRevisionTests`, and `WorkspacePresentationFactorySessionRevisionTests`. | In-app protected-content cleanup, session-revision fencing, cancellation/suppression of late presentation results and presentation-resource disposal. |

## 5. #192-specific integrated execution evidence

The following checks were run for #192 on 2026-11-11. They are distinct from the historical child-task results in Section 4.

| Executed check | Result | Evidence meaning |
|---|---:|---|
| `dotnet build .\DeskVault.slnx --no-restore` | **Passed** | Full solution build succeeded. |
| `dotnet test .\DeskVault.slnx --no-restore` | **1,952/1,952 passed; 0 failed; 0 skipped** | Full solution test suite passed. |
| #192 production-host and presentation integration selection | **9/9 passed** | Production composition, readiness, automatic lock, workstation transitions/failures/races, integrated workflow and presentation behavior. |
| Existing #320 presentation regression selection | **19/19 passed** | Existing presentation cleanup and session-revision regression classes remain green. |
| Application authentication, automatic-lock, workstation-monitor, lifecycle and dependency-injection selection | **77/77 passed** | Reused Application-level security-session and automatic-lock regression coverage. |
| #189 protected-key retrieval regression selection | **16/16 passed** | Protected document/database key retrieval regression coverage. |
| Windows Hello and automatic-lock integration regression selection | **21/21 passed** | Reused Windows Hello and automatic-lock integration coverage. |
| UI startup and automatic-lock regression selection | **30/30 passed** | UI startup and automatic-lock regression coverage. |
| Aggregate focused #192 and reused component regression selections | **172/172 passed; 0 failed; 0 not executed** | Sum of the six focused selections above; these executions are not added to the full solution test count. |
| `git diff --check` | **Passed** | No whitespace errors in the verified implementation change set. |
| `git show --format= --check HEAD` | **Passed** | The committed implementation diff passed the whitespace/error check. |

The full solution build and test commands were executed for #192 and passed. The focused selections provide additional targeted evidence; their counts are reported separately and are not added to the full solution total.

The parent #185 manual/platform limitations in AC9 and AC10 remain outstanding independently of these automated test results. See Sections 3 and 7.

### 5.1 #351 focused workstation-transition monitoring execution evidence

The following selections were executed on 2026-11-12 on
`feat/351-harden-workstation-transition-monitoring-reliability`.
They are focused #351 implementation and regression checks. They are
separate from the historical #192 full-solution result above and are not
a new full-solution test run.

| Executed check | Result | Evidence meaning |
|---|---:|---|
| `WindowsWorkstationSecurityNativeApiTests`, `WindowsWorkstationSecuritySessionStateParserTests`, and `WindowsWorkstationSecuritySessionReconciliationWorkerTests` | **45/45 passed; 0 failed; 0 skipped** | Native WTS query, process-session identity, parser validation, buffer cleanup, current-state reconciliation, polling, query timeout/no overlap, worker liveness, cancellation, and bounded shutdown. |
| `WindowsWorkstationSecurityEventSourceTests` and `WindowsWorkstationPowerNotificationDispatcherTests` | **35/35 passed; 0 failed; 0 skipped** | WTS event-source lifecycle, independent suspend/resume callback registration and dispatch, callback ordering, failure handling, unregistration, and cleanup. |
| `WorkstationSecurityMonitorTests` and `WorkstationSecurityMonitorFailurePathTests` | **20/20 passed; 0 failed; 0 skipped** | Application lock policy, resume-triggered lock, monitoring failure ordering, readiness, recovery, and stale callback fencing. |
| `ProductionSecuritySessionHostIntegrationTests` | **8/8 passed; 0 failed; 0 skipped** | Production shared-host readiness, authentication gating, runtime monitoring failure, stale authentication rejection, workstation transitions, and existing protected workflow regression. |
| `git diff --check` | **Passed** | Working-tree whitespace validation succeeded for the reviewed change set. |

The test project builds succeeded for all four focused selections.
The four focused test results are reported separately and are not
represented as a full-solution total.

The two reconciliation integration cases deliberately suppress the
primary WTS session notification while retaining the independently
queried current-session state. One case supplies a persistent locked
active session; the other supplies a persistent disconnected session.
Both verify that reconciliation publishes the protective transition
and the authenticated application session is locked.

The automated tests establish the parser, native boundary, controlled
notification paths, reconciliation behavior, and Application/production
host response. They do not establish that an actual supported Windows
installation delivers every WTS, logoff, remote-session, suspend, resume,
or hibernate event. Those interactive scenarios remain separately
tracked in Section 6. The focused results do not resolve the limitations
recorded for AC9 and AC10.

### 5.2 #351 follow-up gap-remediation regression evidence

The following consolidated regression selections were executed on
2026-11-12 after the Step 7 gap-remediation changes. They supersede the
earlier individual Infrastructure subset totals in Section 5.1 as the
latest combined Infrastructure execution. These are focused test runs,
not a new full-solution test run.

| Executed check | Result | Evidence meaning |
|---|---:|---|
| `WindowsWorkstationSecurityNativeApiTests`, `WindowsWorkstationSecuritySessionStateParserTests`, `WindowsWorkstationSecuritySessionReconciliationWorkerTests`, `WindowsWorkstationSecurityEventSourceTests`, `WindowsWorkstationSecurityEventSourceFailurePathTests`, and `WindowsWorkstationPowerNotificationDispatcherTests` | **85/85 passed; 0 failed; 0 skipped** | Consolidated Infrastructure regression covering native WTS operations, ABI layout, process-session identity and lookup failure, state parsing, memory cleanup, reconciliation, power callback dispatch, worker liveness, and resource lifecycle. |
| `WorkstationSecurityMonitorTests` and `WorkstationSecurityMonitorFailurePathTests` | **20/20 passed; 0 failed; 0 skipped** | Application monitor policy, protective lock transitions, failure ordering, readiness, recovery, and stale callback handling. |
| `ProductionSecuritySessionHostIntegrationTests` | **8/8 passed; 0 failed; 0 skipped** | Production shared-host readiness, authentication gating, monitoring failure and recovery, stale authentication rejection, and protected workflow regression. |
| `git diff --check` | **Passed** | Final working-tree whitespace validation succeeded. |

All three test-project builds succeeded. The reported selections are
separate test-project executions and are not represented as a
full-solution total.

#### Native ABI architecture evidence

The ABI layout test asserts the `WTSINFOEXW` outer structure size
(232 bytes), union offset (8 bytes), `WTSINFOEX_LEVEL1_W` structure
size (224 bytes), and the offsets of its declared members.

The test process architecture and operating-system architecture were
both **X64**, with an eight-byte pointer. The Infrastructure and
Infrastructure.Tests project files do not declare `PlatformTarget`,
`RuntimeIdentifier`, `RuntimeIdentifiers`, or `Platforms` overrides.
Consequently, the executed evidence establishes the asserted layout
under the x64 test host only. It does not establish compatibility for
ARM64 or another process architecture. #351's native-layout acceptance
criterion remains PARTIAL until the supported architecture policy is
explicit and every architecture in that policy has corresponding
validation evidence, or the product support scope is explicitly limited
and documented.

#### Transient-state observation limitation

The added deterministic reconciliation test starts with an active,
unlocked process session, changes the simulated state to locked and
back to unlocked between polling samples, and suppresses the primary WTS
notification. The worker does not observe the transient lock and does
not publish a lock event for that interval.

This is an intentional demonstration of the documented polling boundary,
not a test failure. Current-session polling detects persistent state
that remains present at a later sample; it is not a complete event
history and cannot guarantee observation of a transition that begins
and ends between samples when the corresponding notification is missed.

#### Remaining manual/platform evidence

The additional #351 automated tests do not replace interactive Windows
validation. Section 6 now records successful manual Windows+L,
sign-out/sign-in, Modern Standby resume, and hibernate/resume checks
performed on 2026-11-13 on a Windows 11 Home Single Language x64 host.
Remote-session disconnect/reconnect was not executed because a suitable
remote-session host was unavailable. No numeric event-detection latency
or native event-source trace capture was recorded during these checks.

Parent #185 AC9 remains PARTIAL pending the unexecuted remote-session
scenario. AC10 remains PARTIAL because transient state changes between
reconciliation samples and silently missed power notifications may
remain unobservable. The suspend/hibernate classification limitation
must also remain explicit.

## 6. Windows-specific manual validation

### 6.1 Test environment and prerequisites

The November 13, 2026 manual checks were performed on:

- Operating system: Microsoft Windows 11 Home Single Language, Version 26H2.
- OS build: 26300.9974.
- OS and process architecture: x64.
- .NET SDK reported by the host: 10.0.404.
- DeskVault UI target framework: `net10.0-windows10.0.26100.0`.
- Automatic-lock configuration: `Security.AutomaticLock.Enabled` was `false` in the source and matching output configuration. No process, user, or machine environment overrides were present during preflight.

The checks used a non-sensitive test vault and existing document/workspace content. The operator confirmed the expected security behavior after each completed transition. No numeric event-detection latency or native event-source trace capture was recorded.

### 6.2 Windows Hello native prompt — recorded as completed under #187

**Evidence status:** **PASS — manually recorded by #187**, in addition to automated authentication result tests. This is historical component-task evidence, not a new manual run performed for #351.

### 6.3 Windows workstation lock — recorded as completed under #191

**Steps and expected result:** Windows+L followed by Windows sign-in must leave DeskVault locked. Protected actions remain unavailable until the user invokes the in-app Unlock action and completes fresh Windows Hello verification.

**Evidence status:** **PASS — manually recorded by #191.** The task record states that DeskVault remained locked after Windows+L and Windows sign-in and required the in-app Unlock/Windows Hello flow. Existing documents and workspace records were usable after successful application authentication.

### 6.4 Windows logoff/sign-out delivery — completed 2026-11-13

**Steps performed**

1. Authenticate to DeskVault and verify the test vault is usable.
2. Sign out/log off the Windows user session.
3. Sign back into Windows and relaunch or return to DeskVault.
4. Verify that Windows sign-in alone does not restore the previous DeskVault authorization and that fresh in-app authentication is required.

**Expected result:** The supported logoff/sign-out transition withdraws protected access through the existing application lock lifecycle.

**Evidence status:** **PASS — manually confirmed on 2026-11-13.** The operator confirmed that the transition behaved as expected and that fresh in-app authentication was required.

### 6.5 Remote-session disconnect/reconnect — not executed

**Expected scenario**

1. In a supported remote Windows session, authenticate to DeskVault and verify the vault is usable.
2. Disconnect the remote session without signing out.
3. Reconnect to the same Windows session.
4. Verify DeskVault remains locked and requires fresh in-app Windows Hello authentication.

**Evidence status:** **NOT EXECUTED.** The available preflight host was Windows 11 Home Single Language, `TermService` was stopped, and the session-query utilities were unavailable. No separate suitable remote-session host was used. Do not treat this scenario as passed.

### 6.6 Modern Standby resume — completed 2026-11-13

**Steps performed**

1. Authenticate to DeskVault and verify the test vault and protected content are usable.
2. Put the laptop into Sleep using the Windows power menu. The host reports `S0 Low Power Idle` (Modern Standby) as its available standby state.
3. Resume the laptop and return to DeskVault.
4. Verify DeskVault remains locked and that protected operations require fresh in-app Windows Hello authentication.

**Expected result:** Resuming Windows does not restore DeskVault authorization.

**Evidence status:** **PASS — manually confirmed on 2026-11-13.** The operator confirmed the expected protective behavior after Modern Standby resume.

### 6.7 Hibernate/resume — completed 2026-11-13

**Steps performed**

1. Authenticate to DeskVault and verify the test vault and protected content are usable.
2. Select Hibernate from the Windows power menu and allow the machine to hibernate.
3. Power the laptop back on and sign into Windows if prompted.
4. Return to DeskVault and verify protected access remains locked until fresh in-app Windows Hello authentication.

**Expected result:** Windows sign-in after hibernation does not restore DeskVault authorization.

**Evidence status:** **PASS — manually confirmed on 2026-11-13.** The operator confirmed the expected protective behavior after hibernate/resume.

### 6.8 Manual-validation summary

| Scenario | Automated evidence | Manual evidence | Current status |
|---|---|---|---|
| Windows Hello success, cancellation and retry | Yes — #187 and #192 integration coverage | Recorded under #187 | **PASS** |
| Windows workstation lock (`Windows+L`) | Yes — #191 and #192 event/lifecycle coverage | Recorded under #191 | **PASS** |
| Windows logoff/sign-out | Yes — event mapping/lifecycle coverage | Confirmed 2026-11-13 | **PASS** |
| Remote-session disconnect/reconnect | Yes — event mapping and reconciliation coverage | Not executed; no suitable remote host available | **NOT EXECUTED** |
| Modern Standby (`S0`) resume | Yes — event mapping and lifecycle coverage | Confirmed 2026-11-13 | **PASS** |
| Hibernate/resume | Yes — event mapping and lifecycle coverage | Confirmed 2026-11-13 | **PASS** |
| Independent native classification of suspend versus hibernate | No separate classification is exposed by the selected power-notification source | Separate manual power operations passed; native event classification not established | **NOT INDEPENDENTLY DISTINGUISHABLE** |
## 7. Outstanding coverage and closure controls

1. **Parent #185 AC9 remains partial for the unexecuted remote-session scenario.** Windows+L, Windows sign-out/sign-in, Modern Standby resume, and hibernate/resume have passed the recorded manual checks. Remote-session disconnect/reconnect remains unexecuted because a suitable remote-session host was unavailable. Do not represent that scenario as passed.
2. **Parent #185 AC10 remains partial for residual event-delivery limitations.** Known initialization, query, parser, worker, callback, dispatcher, and cleanup failures use the established fail-closed boundary. Persistent locked/disconnected state can be independently reconciled when a primary WTS window notification is suppressed. However, a transient lock/disconnect that begins and ends between polls may remain undetected if the notification is also missed. A power transition may remain unobservable if neither power-notification path reports it. These limitations must not be silently represented as full coverage.
3. **#351 focused execution evidence completed on 2026-11-12.** Infrastructure native API/parser/reconciliation tests passed 45/45; event-source/power-dispatcher tests passed 35/35; Application workstation monitor/failure-path tests passed 20/20; production security-session host integration tests passed 8/8. These focused selections are recorded separately and are not added together as a substitute for the full-solution result.
4. **#351 final solution validation completed on 2026-11-13.** `dotnet build .\DeskVault.slnx --no-restore` succeeded. `dotnet test .\DeskVault.slnx --no-restore` passed **2,029/2,029**, with 0 failed and 0 skipped. `git diff --check HEAD --` and the staged-diff whitespace check passed. This result is the #351 final solution validation and is separate from historical #192 evidence.
5. **The #192 full-solution evidence remains historical and separate.** The full solution build and test suite recorded for #192 on 2026-11-11 passed 1,952/1,952, with 0 failed and 0 skipped. Do not combine or overwrite that historical result with the #351 run.
6. **Do not mark parent #185 closed solely because this matrix or #351 exists.** Each parent acceptance criterion must be evaluated against its evidence status. AC9 retains the unexecuted remote-session scenario; AC10 retains the documented transient and silent power-notification limitations.
7. **Do not expand #192 or #351 into #193, #214, #220 or #221 implementation.** Those remain separate responsibility boundaries.
## 8. ADR and architecture impact reference

The #192 implementation change set consists of a shared production-host test-composition seam and integration-test/support files. `HostConfigurator.Build()` continues to invoke the common composition path without test service overrides. The test entry point applies substitutions only after production registrations have been composed. The integrated tests retain the production Application session, authentication, lifecycle, monitoring, session-authorized key adapters, encrypted database factory, and document encryption registrations.

The ADR impact review for this change set considered the shared host/composition and session boundary (ADR-0002 and ADR-0014), the document/database encryption and key-retrieval boundaries (ADR-0004 and ADR-0009), the in-app workspace/presentation lifecycle (ADR-0006 and ADR-0007), and the Windows Hello/workstation monitoring decisions (ADR-0015). The existing decisions already describe the responsibilities and invariants exercised by #192. This task adds integration evidence and traceability, not a new production architectural decision; no ADR update is indicated by the current #192 implementation diff.

### #351 ADR impact review

The #351 implementation changes the durable Windows workstation-monitoring
contract and updates ADR-0014. The updated decision records:

- initial WTS current-process state validation and independent power callback
  registration as workstation-monitor readiness prerequisites;
- process-session identity acquisition and `WTSSessionInfoEx` state
  reconciliation;
- native WTS buffer release and parser validation requirements;
- periodic polling, the one-second timer configuration, skipped ticks while
  a native query remains active, and actual query-start cadence measurement;
- query timeout, worker liveness, bounded shutdown, and retained resource
  behavior;
- independent suspend/resume callback dispatch and deterministic
  unregistration/cleanup;
- resume as a protective lifecycle lock transition;
- the supported Windows platform baseline and the modern WTS session-flag
  mapping, without implementing the legacy Windows 7/Windows Server 2008 R2
  flag inversion;
- residual transient-state and silent power-notification limitations.

ADR-0015 was reviewed and remains unchanged because the authentication
provider, Windows Hello verification contract, and fresh-authentication
requirement are unchanged. ADR-0002 was reviewed and remains unchanged
because the shared Generic Host owner and composition responsibility
established by #190 are unchanged. The new reconciliation and callback
components remain part of the existing Infrastructure workstation
monitoring lifecycle; no competing host or security-session state machine
is introduced.

## 9. Evidence source references

- [#185 — Parent PBI and its 14 acceptance criteria](https://github.com/anirbanB94/DeskVault/issues/185)
- [#186 — Application security-session contract and validation evidence](https://github.com/anirbanB94/DeskVault/issues/186)
- [#187 — Windows Hello authentication integration and manual prompt validation](https://github.com/anirbanB94/DeskVault/issues/187)
- [#188 — Authoritative lock/unlock lifecycle](https://github.com/anirbanB94/DeskVault/issues/188)
- [#189 — Session-authorized document/database key retrieval and key-race evidence](https://github.com/anirbanB94/DeskVault/issues/189)
- [#190 — Automatic-lock configuration and inactivity lifecycle](https://github.com/anirbanB94/DeskVault/issues/190)
- [#191 — Workstation transitions, monitoring readiness/failure and Windows+L manual evidence](https://github.com/anirbanB94/DeskVault/issues/191)
- [#192 — Integrated validation and traceability owner](https://github.com/anirbanB94/DeskVault/issues/192)
- [#351 — Workstation transition monitoring reliability, current-session reconciliation, and independent power callback](https://github.com/anirbanB94/DeskVault/issues/351)
- [#320 — In-app presentation protection and retained-content cleanup](https://github.com/anirbanB94/DeskVault/issues/320)
