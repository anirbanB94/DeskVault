using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.ProcessDocument;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Authentication;
using DeskVault.Application.Security.AutomaticLock;
using DeskVault.Application.Security.Sessions;
using DeskVault.Application.Security.Workstation;
using DeskVault.Application.Workspaces.Commands.CreateWorkspace;
using DeskVault.Application.Workspaces.Queries.GetWorkspace;
using DeskVault.Infrastructure.Persistence;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Security.Authentication;
using DeskVault.Infrastructure.Services;
using DeskVault.UI.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography;
using System.Text;
using Windows.Security.Credentials.UI;

namespace DeskVault.UI.Tests;

public sealed class ProductionSecuritySessionHostIntegrationTests
{
    [Fact]
    public async Task BuildForTesting_UsesProductionRegistrationsAndStartsWorkstationMonitoringBeforeHostStartupCompletes()
    {
        // Arrange
        await using ProductionSecuritySessionTestHost testHost =
            ProductionSecuritySessionTestHost.Create();

        IServiceProvider services =
            testHost.Services;

        IWorkstationSecurityMonitor workstationMonitor =
            services.GetRequiredService<IWorkstationSecurityMonitor>();

        IWorkstationSecurityReadiness workstationReadiness =
            services.GetRequiredService<IWorkstationSecurityReadiness>();

        // Act: inspect the fully composed production provider before startup.
        IHostedService[] hostedServices =
            [.. services.GetServices<IHostedService>()];

        int workstationMonitorIndex =
            Array.FindIndex(
                hostedServices,
                service =>
                    service is WorkstationSecurityMonitorHostedService);

        int automaticLockIndex =
            Array.FindIndex(
                hostedServices,
                service =>
                    service is ApplicationAutomaticLockHostedService);

        // Assert: production session and lifecycle contracts remain registered.
        Assert.IsType<ApplicationSecuritySession>(
            services.GetRequiredService<IApplicationSecuritySession>());

        Assert.IsType<ApplicationSecuritySessionLifecycleService>(
            services.GetRequiredService<IApplicationSecuritySessionLifecycleService>());

        Assert.IsType<ApplicationAuthenticationService>(
            services.GetRequiredService<IApplicationAuthenticationService>());

        IApplicationAutomaticLockService automaticLockService =
            services.GetRequiredService<IApplicationAutomaticLockService>();

        Assert.IsType<ApplicationAutomaticLockService>(
            automaticLockService);

        Assert.Same(
            automaticLockService,
            services.GetRequiredService<IApplicationUserActivityService>());

        Assert.IsType<WorkstationSecurityMonitor>(
            workstationMonitor);

        Assert.IsType<WindowsHelloAuthenticationProvider>(
            services.GetRequiredService<IApplicationAuthenticationProvider>());

        Assert.Same(
            testHost.WindowsHelloPlatformApi,
            services.GetRequiredService<IWindowsHelloPlatformApi>());

        Assert.Same(
            testHost.WindowHandleProvider,
            services.GetRequiredService<IWindowsHelloWindowHandleProvider>());

        Assert.Same(
            testHost.WorkstationEventSource,
            services.GetRequiredService<IWorkstationSecurityEventSource>());

        Assert.Same(
            workstationMonitor,
            workstationReadiness);

        // The retrieval adapters are internal Infrastructure types.
        // Verify their exact production type identities without widening
        // the Infrastructure assembly's internal visibility.
        Assert.Equal(
            "DeskVault.Infrastructure.Services.SessionAuthorizedEncryptionKeyService",
            services
                .GetRequiredService<IEncryptionKeyService>()
                .GetType()
                .FullName);

        Assert.Equal(
            "DeskVault.Infrastructure.Services.SessionAuthorizedDatabaseEncryptionKeyService",
            services
                .GetRequiredService<IDatabaseEncryptionKeyService>()
                .GetType()
                .FullName);

        Assert.IsType<EncryptedDeskVaultDbContextFactory>(
            services.GetRequiredService<IDbContextFactory<DeskVaultDbContext>>());

        Assert.IsType<DocumentEncryptionService>(
            services.GetRequiredService<DocumentEncryptionService>());

        Assert.Same(
            testHost.DataPaths,
            services.GetRequiredService<DeskVaultDataPaths>());

        Assert.True(workstationMonitorIndex >= 0);
        Assert.True(automaticLockIndex > workstationMonitorIndex);

        Assert.Equal(
            WorkstationSecurityReadinessState.NotStarted,
            workstationReadiness.CurrentSnapshot.State);

        // Act: start through the same lifecycle wrapper used by the application.
        await testHost.StartAsync(
            TestContext.Current.CancellationToken);

        // Assert: successful host startup means the initial monitoring
        // attempt has completed and workstation readiness is established.
        Assert.Equal(
            1,
            testHost.WorkstationEventSource.StartCallCount);

        Assert.Equal(
            WorkstationSecurityReadinessState.Ready,
            workstationReadiness.CurrentSnapshot.State);

        // Act: stop the production host through its lifecycle owner.
        await testHost.StopAsync(
            TestContext.Current.CancellationToken);

        // Assert: workstation monitoring is stopped by hosted-service shutdown.
        Assert.Equal(
            1,
            testHost.WorkstationEventSource.StopCallCount);

        Assert.Equal(
            WorkstationSecurityReadinessState.Stopped,
            workstationReadiness.CurrentSnapshot.State);
    }

    [Fact]
    public async Task HostStartup_WhenWorkstationMonitoringIsStillInitializing_DoesNotCompleteOrPermitAuthenticationUntilMonitoringIsReady()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using ProductionSecuritySessionTestHost testHost =
            ProductionSecuritySessionTestHost.Create();

        var monitoringStart =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        testHost.WorkstationEventSource.StartTask =
            monitoringStart.Task;

        IServiceProvider services =
            testHost.Services;

        IWorkstationSecurityReadiness workstationReadiness =
            services.GetRequiredService<IWorkstationSecurityReadiness>();

        IApplicationSecuritySession securitySession =
            services.GetRequiredService<IApplicationSecuritySession>();

        DeskVaultStartupCoordinator startupCoordinator =
            services.GetRequiredService<DeskVaultStartupCoordinator>();

        // Act: begin production host startup while holding the
        // controlled event source in its initialization state.
        Task hostStart =
            testHost.StartAsync(cancellationToken);

        try
        {
            await testHost.WorkstationEventSource.StartInvoked.WaitAsync(
                cancellationToken);

            // Assert: startup must remain pending until monitoring is ready.
            Assert.Equal(
                1,
                testHost.WorkstationEventSource.StartCallCount);

            Assert.Equal(
                WorkstationSecurityReadinessState.Starting,
                workstationReadiness.CurrentSnapshot.State);

            Assert.False(hostStart.IsCompleted);

            // Authentication must be rejected while monitoring is initializing.
            bool authenticatedDuringMonitoringInitialization =
                await startupCoordinator.AuthenticateAsync(
                    cancellationToken);

            Assert.False(authenticatedDuringMonitoringInitialization);

            Assert.Equal(
                0,
                testHost.WindowsHelloPlatformApi.AvailabilityCallCount);

            Assert.Equal(
                0,
                testHost.WindowsHelloPlatformApi.VerificationCallCount);

            // A rejected authentication attempt while readiness is Starting
            // must leave the application session unavailable.
            Assert.Equal(
                ApplicationSecuritySessionState.Unavailable,
                securitySession.CurrentSnapshot.State);

            // Act: complete event-source initialization.
            monitoringStart.TrySetResult(true);

            await hostStart.WaitAsync(cancellationToken);

            // Assert: successful monitoring initialization restores the
            // session to Locked, never to Authenticated.
            Assert.Equal(
                WorkstationSecurityReadinessState.Ready,
                workstationReadiness.CurrentSnapshot.State);

            Assert.Equal(
                ApplicationSecuritySessionState.Locked,
                securitySession.CurrentSnapshot.State);

            Assert.Equal(
                0,
                testHost.WindowsHelloPlatformApi.AvailabilityCallCount);

            Assert.Equal(
                0,
                testHost.WindowsHelloPlatformApi.VerificationCallCount);

            // Act: stop the production host.
            await testHost.StopAsync(cancellationToken);

            // Assert: hosted-service shutdown stops workstation monitoring.
            Assert.Equal(
                WorkstationSecurityReadinessState.Stopped,
                workstationReadiness.CurrentSnapshot.State);
        }
        finally
        {
            // Always release the controlled initialization task. This ensures
            // host startup can finish or fail and release its lifecycle gate
            // before ProductionSecuritySessionTestHost.DisposeAsync executes.
            monitoringStart.TrySetResult(true);

            try
            {
                await hostStart;
            }
            catch when (hostStart.IsFaulted || hostStart.IsCanceled)
            {
                // Preserve the original test failure while observing a failed
                // host-start operation during teardown.
            }
        }
    }

    [Fact]
    public async Task HostStartup_WhenWorkstationMonitoringInitiallyFails_BlocksAuthenticationAndDatabaseInitialization_UntilMonitoringRecoversAndFreshAuthenticationSucceeds()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using ProductionSecuritySessionTestHost testHost =
            ProductionSecuritySessionTestHost.Create();

        testHost.WorkstationEventSource.StartException =
            new InvalidOperationException(
                "Simulated initial workstation-monitoring failure.");

        IServiceProvider services =
            testHost.Services;

        IWorkstationSecurityMonitor workstationMonitor =
            services.GetRequiredService<IWorkstationSecurityMonitor>();

        IWorkstationSecurityReadiness workstationReadiness =
            services.GetRequiredService<IWorkstationSecurityReadiness>();

        IApplicationSecuritySession securitySession =
            services.GetRequiredService<IApplicationSecuritySession>();

        DeskVaultStartupCoordinator startupCoordinator =
            services.GetRequiredService<DeskVaultStartupCoordinator>();

        // Act: start the actual production host. A known monitoring
        // initialization failure must not establish authentication readiness.
        await testHost.StartAsync(cancellationToken);

        // Assert: the host remains available for recovery, but the
        // workstation prerequisite and application session fail closed.
        Assert.Equal(
            1,
            testHost.WorkstationEventSource.StartCallCount);

        Assert.Equal(
            1,
            testHost.WorkstationEventSource.StopCallCount);

        Assert.Equal(
            WorkstationSecurityReadinessState.Unavailable,
            workstationReadiness.CurrentSnapshot.State);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            securitySession.CurrentSnapshot.State);

        // Startup authentication must not invoke Windows Hello while
        // the workstation-monitoring prerequisite is unavailable.
        bool authenticatedWhileMonitoringUnavailable =
            await startupCoordinator.AuthenticateAsync(
                cancellationToken);

        Assert.False(authenticatedWhileMonitoringUnavailable);

        Assert.Equal(
            0,
            testHost.WindowsHelloPlatformApi.AvailabilityCallCount);

        Assert.Equal(
            0,
            testHost.WindowsHelloPlatformApi.VerificationCallCount);

        // Protected database initialization must remain blocked.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => startupCoordinator.InitializeDatabaseAsync(
                cancellationToken));

        Assert.False(
            File.Exists(testHost.DataPaths.DatabasePath));

        // Arrange recovery: the same external event-source instance is now
        // able to initialize successfully.
        testHost.WorkstationEventSource.StartException = null;

        // Act: recover through the real production monitor registered
        // in the production dependency-injection graph.
        bool monitoringRecovered =
            await workstationMonitor.TryStartAsync(
                cancellationToken);

        // Assert: recovery restores only the Locked state. It must not
        // reuse or synthesize a successful authentication result.
        Assert.True(monitoringRecovered);

        Assert.Equal(
            2,
            testHost.WorkstationEventSource.StartCallCount);

        Assert.Equal(
            WorkstationSecurityReadinessState.Ready,
            workstationReadiness.CurrentSnapshot.State);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            securitySession.CurrentSnapshot.State);

        Assert.False(
            securitySession.CurrentSnapshot.IsProtectedAccessAvailable);

        Assert.Equal(
            0,
            testHost.WindowsHelloPlatformApi.AvailabilityCallCount);

        Assert.Equal(
            0,
            testHost.WindowsHelloPlatformApi.VerificationCallCount);

        // Act: monitoring is ready, but Windows Hello is unavailable.
        testHost.WindowsHelloPlatformApi.Availability =
            UserConsentVerifierAvailability.NotConfiguredForUser;

        bool authenticatedWithoutAvailableProvider =
            await startupCoordinator.AuthenticateAsync(
                cancellationToken);

        // Assert: provider unavailability cannot establish authorization.
        Assert.False(authenticatedWithoutAvailableProvider);

        Assert.Equal(
            1,
            testHost.WindowsHelloPlatformApi.AvailabilityCallCount);

        Assert.Equal(
            0,
            testHost.WindowsHelloPlatformApi.VerificationCallCount);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            securitySession.CurrentSnapshot.State);

        Assert.False(
            securitySession.CurrentSnapshot.IsProtectedAccessAvailable);

        // Arrange: the approved provider is now available and will
        // return a successful Windows Hello verification result.
        testHost.WindowsHelloPlatformApi.Availability =
            UserConsentVerifierAvailability.Available;

        testHost.WindowsHelloPlatformApi.VerificationResult =
            UserConsentVerificationResult.Verified;

        // Act: a new authentication operation must perform fresh
        // availability checking and fresh Windows Hello verification.
        bool authenticatedAfterRecovery =
            await startupCoordinator.AuthenticateAsync(
                cancellationToken);

        // Assert: fresh authentication is the only operation that restores
        // protected access after monitoring and provider recovery.
        Assert.True(authenticatedAfterRecovery);

        Assert.Equal(
            2,
            testHost.WindowsHelloPlatformApi.AvailabilityCallCount);

        Assert.Equal(
            1,
            testHost.WindowsHelloPlatformApi.VerificationCallCount);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            securitySession.CurrentSnapshot.State);

        Assert.True(
            securitySession.CurrentSnapshot.IsProtectedAccessAvailable);

        // Startup database initialization is permitted only after
        // the actual production authentication path succeeds.
        await startupCoordinator.InitializeDatabaseAsync(
            cancellationToken);

        Assert.True(
            File.Exists(testHost.DataPaths.DatabasePath));
    }

    [Fact]
    public async Task AutomaticLock_WhenQualifyingActivityResetsBaseline_ExpiresThroughProductionLifecycleAndDeniesDocumentAndDatabaseAccessUntilFreshAuthentication()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var timeProvider =
            new ControlledTimeProvider();

        var configurationOverrides =
            new Dictionary<string, string?>
            {
                ["Security:AutomaticLock:Enabled"] = "true",
                ["Security:AutomaticLock:InactivityTimeoutMinutes"] = "1"
            };

        await using ProductionSecuritySessionTestHost testHost =
            ProductionSecuritySessionTestHost.Create(
                timeProvider,
                configurationOverrides);

        IServiceProvider services =
            testHost.Services;

        await testHost.StartAsync(cancellationToken);

        Assert.Same(
            timeProvider,
            services.GetRequiredService<TimeProvider>());

        DeskVaultStartupCoordinator startupCoordinator =
            services.GetRequiredService<DeskVaultStartupCoordinator>();

        IApplicationSecuritySession securitySession =
            services.GetRequiredService<IApplicationSecuritySession>();

        IApplicationAutomaticLockService automaticLockService =
            services.GetRequiredService<IApplicationAutomaticLockService>();

        IApplicationUserActivityService userActivityService =
            services.GetRequiredService<IApplicationUserActivityService>();

        IApplicationSecuritySessionLifecycleService lifecycleService =
            services.GetRequiredService<IApplicationSecuritySessionLifecycleService>();

        IEncryptionKeyService documentKeyService =
            services.GetRequiredService<IEncryptionKeyService>();

        IDatabaseEncryptionKeyService databaseKeyService =
            services.GetRequiredService<IDatabaseEncryptionKeyService>();

        IDbContextFactory<DeskVaultDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<DeskVaultDbContext>>();

        DocumentEncryptionService documentEncryptionService =
            services.GetRequiredService<DocumentEncryptionService>();

        Assert.IsType<ApplicationAutomaticLockService>(
            automaticLockService);

        Assert.Same(
            automaticLockService,
            userActivityService);

        Assert.IsType<ApplicationSecuritySessionLifecycleService>(
            lifecycleService);

        // Act: authenticate through the real production Windows Hello
        // provider and Application authentication service.
        bool authenticated =
            await startupCoordinator.AuthenticateAsync(
                cancellationToken);

        // Assert: protected access is established.
        Assert.True(authenticated);

        ApplicationSecuritySessionSnapshot authenticatedSnapshot =
            securitySession.CurrentSnapshot;

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            authenticatedSnapshot.State);

        Assert.True(
            authenticatedSnapshot.IsProtectedAccessAvailable);

        // Initialize an actual encrypted vault through the production
        // startup coordinator and DatabaseInitializer.
        await startupCoordinator.InitializeDatabaseAsync(
            cancellationToken);

        Assert.True(
            File.Exists(testHost.DataPaths.DatabasePath));

        string documentKeyPath =
            Path.Combine(
                testHost.DataPaths.SecurityDirectory,
                "master.key");

        string databaseKeyPath =
            Path.Combine(
                testHost.DataPaths.SecurityDirectory,
                "database.key");

        Assert.True(File.Exists(documentKeyPath));
        Assert.True(File.Exists(databaseKeyPath));

        // Capture non-secret key identity metadata. Key buffers returned
        // by the production services are caller-owned and must be cleared.
        Guid originalDocumentKeyId;
        int originalDocumentKeyVersion;

        EncryptionKeyMaterial originalDocumentKey =
            await documentKeyService.GetKeyAsync(
                cancellationToken);

        try
        {
            originalDocumentKeyId = originalDocumentKey.KeyId;
            originalDocumentKeyVersion = originalDocumentKey.KeyVersion;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                originalDocumentKey.KeyBytes);
        }

        Guid originalDatabaseKeyId;
        int originalDatabaseKeyVersion;

        EncryptionKeyMaterial originalDatabaseKey =
            await databaseKeyService.GetKeyAsync(
                cancellationToken);

        try
        {
            originalDatabaseKeyId = originalDatabaseKey.KeyId;
            originalDatabaseKeyVersion = originalDatabaseKey.KeyVersion;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                originalDatabaseKey.KeyBytes);
        }

        // Verify that the actual encrypted database can be opened through
        // the production IDbContextFactory registration.
        await using (DeskVaultDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(
                cancellationToken))
        {
            Assert.True(
                await dbContext.Database.CanConnectAsync(
                    cancellationToken));
        }

        // Verify a real document encryption/decryption round trip using
        // the production DocumentEncryptionService and authorized key path.
        Guid documentId =
            Guid.NewGuid();

        byte[] plaintext =
            Encoding.UTF8.GetBytes(
                "Id,Name\r\n1,DeskVault-Integration\r\n");

        byte[]? decryptedPlaintext = null;

        await using var encryptedDocument =
            new MemoryStream();

        await using var plaintextSource =
            new MemoryStream(plaintext);

        await documentEncryptionService.EncryptAsync(
            plaintextSource,
            encryptedDocument,
            documentId,
            cancellationToken);

        encryptedDocument.Position = 0;

        await using (var decryptedDocument =
            new MemoryStream())
        {
            await documentEncryptionService.DecryptAsync(
                encryptedDocument,
                decryptedDocument,
                documentId,
                cancellationToken);

            decryptedPlaintext =
                decryptedDocument.ToArray();
        }

        Assert.Equal(
            plaintext,
            decryptedPlaintext);

        CryptographicOperations.ZeroMemory(
            decryptedPlaintext);

        decryptedPlaintext = null;

        // Act: qualifying user activity at 30 seconds refreshes the
        // authenticated revision's inactivity baseline.
        timeProvider.Advance(
            TimeSpan.FromSeconds(30));

        bool activityAccepted =
            userActivityService.TryReportQualifyingActivity(
                authenticatedSnapshot.Revision);

        Assert.True(activityAccepted);

        // Background work does not report qualifying activity. Advancing
        // to 59 seconds after the accepted activity must not lock yet.
        timeProvider.Advance(
            TimeSpan.FromSeconds(59));

        automaticLockService.EvaluateInactivity();

        // Assert: the authenticated session remains available immediately
        // before the configured one-minute inactivity deadline.
        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            securitySession.CurrentSnapshot.State);

        Assert.True(
            securitySession.CurrentSnapshot.IsProtectedAccessAvailable);

        // Act: reach the exact inactivity deadline.
        timeProvider.Advance(
            TimeSpan.FromSeconds(1));

        automaticLockService.EvaluateInactivity();

        // Assert: the authoritative lifecycle has locked the session.
        ApplicationSecuritySessionSnapshot lockedSnapshot =
            securitySession.CurrentSnapshot;

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            lockedSnapshot.State);

        Assert.False(
            lockedSnapshot.IsProtectedAccessAvailable);

        Assert.True(
            lockedSnapshot.Revision > authenticatedSnapshot.Revision);

        // Strict document-key retrieval must fail through the actual
        // production session-authorized adapter.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => documentKeyService.GetKeyAsync(
                cancellationToken));

        // Strict database-key retrieval must also fail.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => databaseKeyService.GetKeyAsync(
                cancellationToken));

        // Document decryption must be denied before protected content
        // is returned through the actual encryption service.
        encryptedDocument.Position = 0;

        await using (var deniedDecryptionOutput =
            new MemoryStream())
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => documentEncryptionService.DecryptAsync(
                    encryptedDocument,
                    deniedDecryptionOutput,
                    documentId,
                    cancellationToken));

            Assert.Equal(
                0,
                deniedDecryptionOutput.Length);
        }

        // The production encrypted database factory must not create a
        // context after the session has been locked.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => dbContextFactory.CreateDbContextAsync(
                cancellationToken));

        // The existing canonical key artifacts must remain present.
        Assert.True(File.Exists(documentKeyPath));
        Assert.True(File.Exists(databaseKeyPath));

        // Act: reauthenticate through the same production startup
        // coordinator and Windows Hello authentication provider.
        bool reauthenticated =
            await startupCoordinator.AuthenticateAsync(
                cancellationToken);

        // Assert: fresh authentication restores protected access.
        Assert.True(reauthenticated);

        ApplicationSecuritySessionSnapshot reauthenticatedSnapshot =
            securitySession.CurrentSnapshot;

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            reauthenticatedSnapshot.State);

        Assert.True(
            reauthenticatedSnapshot.IsProtectedAccessAvailable);

        Assert.True(
            reauthenticatedSnapshot.Revision > lockedSnapshot.Revision);

        // Verify that the existing document key was reused rather than
        // replaced as part of the lock/unlock lifecycle.
        EncryptionKeyMaterial restoredDocumentKey =
            await documentKeyService.GetKeyAsync(
                cancellationToken);

        try
        {
            Assert.Equal(
                originalDocumentKeyId,
                restoredDocumentKey.KeyId);

            Assert.Equal(
                originalDocumentKeyVersion,
                restoredDocumentKey.KeyVersion);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                restoredDocumentKey.KeyBytes);
        }

        // Verify that the existing database key was reused.
        EncryptionKeyMaterial restoredDatabaseKey =
            await databaseKeyService.GetKeyAsync(
                cancellationToken);

        try
        {
            Assert.Equal(
                originalDatabaseKeyId,
                restoredDatabaseKey.KeyId);

            Assert.Equal(
                originalDatabaseKeyVersion,
                restoredDatabaseKey.KeyVersion);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                restoredDatabaseKey.KeyBytes);
        }

        // Verify that the same encrypted document remains readable
        // through a fresh authorized operation after reauthentication.
        encryptedDocument.Position = 0;

        await using (var restoredDecryptionOutput =
            new MemoryStream())
        {
            await documentEncryptionService.DecryptAsync(
                encryptedDocument,
                restoredDecryptionOutput,
                documentId,
                cancellationToken);

            decryptedPlaintext =
                restoredDecryptionOutput.ToArray();
        }

        try
        {
            Assert.Equal(
                plaintext,
                decryptedPlaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                decryptedPlaintext);
        }

        // Verify that the encrypted database can again be opened
        // through the production factory after fresh authentication.
        await using (DeskVaultDbContext restoredDbContext =
            await dbContextFactory.CreateDbContextAsync(
                cancellationToken))
        {
            Assert.True(
                await restoredDbContext.Database.CanConnectAsync(
                    cancellationToken));
        }

        CryptographicOperations.ZeroMemory(
            plaintext);
    }

    [Fact]
    public async Task ExistingDocumentWorkspaceSearchAndProcessingFlows_AfterFreshAuthentication_RemainUsableThroughProductionHost()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using ProductionSecuritySessionTestHost testHost =
            ProductionSecuritySessionTestHost.Create();

        await testHost.StartAsync(cancellationToken);

        IServiceProvider services =
            testHost.Services;

        DeskVaultStartupCoordinator startupCoordinator =
            services.GetRequiredService<DeskVaultStartupCoordinator>();

        IApplicationSecuritySession securitySession =
            services.GetRequiredService<IApplicationSecuritySession>();

        IApplicationSecuritySessionLifecycleService lifecycleService =
            services.GetRequiredService<
                IApplicationSecuritySessionLifecycleService>();

        ImportDocumentHandler importDocumentHandler =
            services.GetRequiredService<ImportDocumentHandler>();

        ProcessDocumentHandler processDocumentHandler =
            services.GetRequiredService<ProcessDocumentHandler>();

        SearchDocumentsHandler searchDocumentsHandler =
            services.GetRequiredService<SearchDocumentsHandler>();

        CreateWorkspaceHandler createWorkspaceHandler =
            services.GetRequiredService<CreateWorkspaceHandler>();

        GetWorkspaceHandler getWorkspaceHandler =
            services.GetRequiredService<GetWorkspaceHandler>();

        // Establish authorization and initialize the actual encrypted vault
        // through the production startup path.
        bool authenticated =
            await startupCoordinator.AuthenticateAsync(
                cancellationToken);

        Assert.True(authenticated);

        await startupCoordinator.InitializeDatabaseAsync(
            cancellationToken);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            securitySession.CurrentSnapshot.State);

        // Use a source file inside this test host's isolated data directory.
        string sourceFilePath =
            Path.Combine(
                testHost.DataPaths.RootDirectory,
                $"security-session-regression-{Guid.NewGuid():N}.txt");

        const string searchableText =
            """
            DeskVault security session integration regression.

            This document contains the cobalt regression marker.
            Document processing and search must remain available after
            a fresh application authentication.
            """;

        try
        {
            // Act: import a real source document through the production handler.
            await File.WriteAllTextAsync(
                sourceFilePath,
                searchableText,
                Encoding.UTF8,
                cancellationToken);

            ImportDocumentResult importResult =
                await importDocumentHandler.HandleAsync(
                    new ImportDocumentCommand(
                        sourceFilePath,
                        "Security Session Regression Document"),
                    cancellationToken);

            Assert.Equal(
                ImportDocumentResultStatus.Success,
                importResult.Status);

            Assert.NotNull(importResult.DocumentId);

            Guid documentId =
                importResult.DocumentId.Value;

            // Process the document through the production document reader,
            // processing pipeline, and encrypted persistence registrations.
            ProcessDocumentResult processingResult =
                await processDocumentHandler.HandleAsync(
                    new ProcessDocumentCommand(documentId),
                    cancellationToken);

            Assert.Equal(
                ProcessDocumentResultStatus.Success,
                processingResult.Status);

            Assert.Equal(
                documentId,
                processingResult.DocumentId);

            // Verify that the processed content is searchable.
            var searchQuery =
                new SearchDocumentsQuery(
                    "COBALT REGRESSION",
                    Limit: 20);

            SearchDocumentsPage initialSearchPage =
                await searchDocumentsHandler.HandleAsync(
                    searchQuery,
                    cancellationToken);

            Assert.Contains(
                initialSearchPage.Results,
                result => result.DocumentId == documentId);

            // Create a persistent workspace using the production workspace
            // handler and the already imported document.
            CreateWorkspaceResult createWorkspaceResult =
                await createWorkspaceHandler.HandleAsync(
                    new CreateWorkspaceCommand(
                        "Security Session Regression Workspace",
                        "Workspace used to verify protected access after re-authentication.",
                        IsPersistent: true,
                        DocumentIds: [documentId]),
                    cancellationToken);

            Assert.Equal(
                CreateWorkspaceResultStatus.Success,
                createWorkspaceResult.Status);

            var createdWorkspace =
                createWorkspaceResult.Workspace
                ?? throw new InvalidOperationException(
                    "The production workspace handler returned no workspace.");

            var workspaceQuery =
                new GetWorkspaceQuery(
                    createdWorkspace.Id);

            GetWorkspaceResult initialWorkspaceResult =
                await getWorkspaceHandler.HandleAsync(
                    workspaceQuery,
                    cancellationToken);

            Assert.Equal(
                GetWorkspaceResultStatus.Success,
                initialWorkspaceResult.Status);

            Assert.NotNull(initialWorkspaceResult.Workspace);

            Assert.Contains(
                initialWorkspaceResult.Workspace.Memberships,
                membership => membership.DocumentId == documentId);

            // Act: revoke access through the authoritative production
            // lock/unlock lifecycle.
            lifecycleService.Lock();

            Assert.Equal(
                ApplicationSecuritySessionState.Locked,
                securitySession.CurrentSnapshot.State);

            // Existing processing, search and workspace retrieval must not
            // continue using protected database/document access while locked.
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => processDocumentHandler.HandleAsync(
                    new ProcessDocumentCommand(documentId),
                    cancellationToken));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => searchDocumentsHandler.HandleAsync(
                    searchQuery,
                    cancellationToken));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => getWorkspaceHandler.HandleAsync(
                    workspaceQuery,
                    cancellationToken));

            // Re-authenticate through the same production Windows Hello path.
            bool reauthenticated =
                await startupCoordinator.AuthenticateAsync(
                    cancellationToken);

            Assert.True(reauthenticated);

            Assert.Equal(
                ApplicationSecuritySessionState.Authenticated,
                securitySession.CurrentSnapshot.State);

            // Existing document processing must remain available after
            // authorization is restored.
            ProcessDocumentResult reprocessedResult =
                await processDocumentHandler.HandleAsync(
                    new ProcessDocumentCommand(documentId),
                    cancellationToken);

            Assert.Equal(
                ProcessDocumentResultStatus.Success,
                reprocessedResult.Status);

            Assert.Equal(
                documentId,
                reprocessedResult.DocumentId);

            // Previously persisted processed content must remain searchable.
            SearchDocumentsPage restoredSearchPage =
                await searchDocumentsHandler.HandleAsync(
                    searchQuery,
                    cancellationToken);

            Assert.Contains(
                restoredSearchPage.Results,
                result => result.DocumentId == documentId);

            // The existing persistent workspace and its document membership
            // must remain retrievable after re-authentication.
            GetWorkspaceResult restoredWorkspaceResult =
                await getWorkspaceHandler.HandleAsync(
                    workspaceQuery,
                    cancellationToken);

            Assert.Equal(
                GetWorkspaceResultStatus.Success,
                restoredWorkspaceResult.Status);

            Assert.NotNull(restoredWorkspaceResult.Workspace);

            Assert.Contains(
                restoredWorkspaceResult.Workspace.Memberships,
                membership => membership.DocumentId == documentId);
        }
        finally
        {
            if (File.Exists(sourceFilePath))
            {
                File.Delete(sourceFilePath);
            }
        }
    }

    [Fact]
    public async Task WorkstationSecurityEvents_WhenReceivedByProductionMonitor_ApplyRequiredLockTransitionsWithoutTreatingUnlockOrResumeAsAuthentication()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using ProductionSecuritySessionTestHost testHost =
            ProductionSecuritySessionTestHost.Create();

        await testHost.StartAsync(cancellationToken);

        IServiceProvider services =
            testHost.Services;

        DeskVaultStartupCoordinator startupCoordinator =
            services.GetRequiredService<DeskVaultStartupCoordinator>();

        IApplicationSecuritySession securitySession =
            services.GetRequiredService<IApplicationSecuritySession>();

        IWorkstationSecurityReadiness workstationReadiness =
            services.GetRequiredService<IWorkstationSecurityReadiness>();

        // Establish initial authenticated access through the production
        // authentication and Windows Hello registration.
        bool initiallyAuthenticated =
            await startupCoordinator.AuthenticateAsync(
                cancellationToken);

        Assert.True(initiallyAuthenticated);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            securitySession.CurrentSnapshot.State);

        Assert.Equal(
            WorkstationSecurityReadinessState.Ready,
            workstationReadiness.CurrentSnapshot.State);

        // Act and assert: workstation unlock and connection events
        // must not authenticate or otherwise change the application session.
        WorkstationSecurityEventKind[] nonAuthenticatingEvents =
        [
            WorkstationSecurityEventKind.SessionUnlocked,
            WorkstationSecurityEventKind.SessionConnected
        ];

        foreach (WorkstationSecurityEventKind eventKind
            in nonAuthenticatingEvents)
        {
            ApplicationSecuritySessionSnapshot beforeEvent =
                securitySession.CurrentSnapshot;

            testHost.WorkstationEventSource.RaiseSecurityEvent(
                eventKind);

            ApplicationSecuritySessionSnapshot afterEvent =
                securitySession.CurrentSnapshot;

            Assert.Equal(
                ApplicationSecuritySessionState.Authenticated,
                afterEvent.State);

            Assert.True(
                afterEvent.IsProtectedAccessAvailable);

            Assert.Equal(
                beforeEvent.Revision,
                afterEvent.Revision);
        }

        // Act and assert: every supported workstation transition that
        // requires protection, including resume, must lock through the
        // production lifecycle.
        WorkstationSecurityEventKind[] lockingEvents =
        [
            WorkstationSecurityEventKind.SessionLocked,
            WorkstationSecurityEventKind.SessionLoggedOff,
            WorkstationSecurityEventKind.SessionDisconnected,
            WorkstationSecurityEventKind.SystemSuspending,
            WorkstationSecurityEventKind.SystemResumed
        ];

        foreach (WorkstationSecurityEventKind eventKind
            in lockingEvents)
        {
            ApplicationSecuritySessionSnapshot beforeEvent =
                securitySession.CurrentSnapshot;

            Assert.Equal(
                ApplicationSecuritySessionState.Authenticated,
                beforeEvent.State);

            testHost.WorkstationEventSource.RaiseSecurityEvent(
                eventKind);

            ApplicationSecuritySessionSnapshot afterEvent =
                securitySession.CurrentSnapshot;

            Assert.Equal(
                ApplicationSecuritySessionState.Locked,
                afterEvent.State);

            Assert.False(
                afterEvent.IsProtectedAccessAvailable);

            Assert.True(
                afterEvent.Revision > beforeEvent.Revision);

            // A supported workstation event withdraws access. Only a new
            // successful DeskVault authentication can restore it.
            bool reauthenticated =
                await startupCoordinator.AuthenticateAsync(
                    cancellationToken);

            Assert.True(reauthenticated);

            Assert.Equal(
                ApplicationSecuritySessionState.Authenticated,
                securitySession.CurrentSnapshot.State);

            Assert.True(
                securitySession.CurrentSnapshot.IsProtectedAccessAvailable);
        }

        Assert.Equal(
            WorkstationSecurityReadinessState.Ready,
            workstationReadiness.CurrentSnapshot.State);
    }

    [Fact]
    public async Task RuntimeMonitoringFailure_WhenAuthenticationIsRequested_BlocksWindowsHelloUntilMonitoringRecovers()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using ProductionSecuritySessionTestHost testHost =
            ProductionSecuritySessionTestHost.Create();

        await testHost.StartAsync(cancellationToken);

        IServiceProvider services =
            testHost.Services;

        DeskVaultStartupCoordinator startupCoordinator =
            services.GetRequiredService<DeskVaultStartupCoordinator>();

        IWorkstationSecurityMonitor workstationMonitor =
            services.GetRequiredService<IWorkstationSecurityMonitor>();

        IWorkstationSecurityReadiness workstationReadiness =
            services.GetRequiredService<IWorkstationSecurityReadiness>();

        IApplicationSecuritySession securitySession =
            services.GetRequiredService<IApplicationSecuritySession>();

        // Establish a successfully authenticated production session.
        bool authenticated =
            await startupCoordinator.AuthenticateAsync(
                cancellationToken);

        Assert.True(authenticated);

        int availabilityCallsBeforeFailure =
            testHost.WindowsHelloPlatformApi.AvailabilityCallCount;

        int verificationCallsBeforeFailure =
            testHost.WindowsHelloPlatformApi.VerificationCallCount;

        // Act: the already registered production monitor receives a
        // runtime failure notification from its event source.
        testHost.WorkstationEventSource.RaiseMonitoringFailure(
            new InvalidOperationException(
                "Simulated runtime workstation-monitoring failure."));

        // Assert: a monitoring failure uses the authoritative lifecycle
        // and makes the session unavailable.
        Assert.Equal(
            WorkstationSecurityReadinessState.Unavailable,
            workstationReadiness.CurrentSnapshot.State);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            securitySession.CurrentSnapshot.State);

        Assert.False(
            securitySession.CurrentSnapshot.IsProtectedAccessAvailable);

        // Even though Windows Hello remains available, another authentication
        // request must be rejected before checking the provider.
        bool authenticatedWhileUnavailable =
            await startupCoordinator.AuthenticateAsync(
                cancellationToken);

        Assert.False(authenticatedWhileUnavailable);

        Assert.Equal(
            availabilityCallsBeforeFailure,
            testHost.WindowsHelloPlatformApi.AvailabilityCallCount);

        Assert.Equal(
            verificationCallsBeforeFailure,
            testHost.WindowsHelloPlatformApi.VerificationCallCount);

        // Arrange recovery: the same event source can now initialize.
        testHost.WorkstationEventSource.StartException = null;

        // Act: recover through the real production monitor.
        bool recovered =
            await workstationMonitor.TryStartAsync(
                cancellationToken);

        // Assert: recovery restores only Locked.
        Assert.True(recovered);

        Assert.Equal(
            WorkstationSecurityReadinessState.Ready,
            workstationReadiness.CurrentSnapshot.State);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            securitySession.CurrentSnapshot.State);

        Assert.False(
            securitySession.CurrentSnapshot.IsProtectedAccessAvailable);

        // Fresh authentication is now required.
        bool authenticatedAfterRecovery =
            await startupCoordinator.AuthenticateAsync(
                cancellationToken);

        Assert.True(authenticatedAfterRecovery);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            securitySession.CurrentSnapshot.State);

        Assert.True(
            securitySession.CurrentSnapshot.IsProtectedAccessAvailable);

        Assert.Equal(
            availabilityCallsBeforeFailure + 1,
            testHost.WindowsHelloPlatformApi.AvailabilityCallCount);

        Assert.Equal(
            verificationCallsBeforeFailure + 1,
            testHost.WindowsHelloPlatformApi.VerificationCallCount);
    }

    [Fact]
    public async Task RuntimeMonitoringFailure_WhenWindowsHelloIsInFlight_RejectsDelayedAuthenticationAndIgnoresPreviousMonitoringGenerationCallback()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using ProductionSecuritySessionTestHost testHost =
            ProductionSecuritySessionTestHost.Create();

        await testHost.StartAsync(cancellationToken);

        IServiceProvider services =
            testHost.Services;

        IApplicationAuthenticationService authenticationService =
            services.GetRequiredService<IApplicationAuthenticationService>();

        DeskVaultStartupCoordinator startupCoordinator =
            services.GetRequiredService<DeskVaultStartupCoordinator>();

        IWorkstationSecurityMonitor workstationMonitor =
            services.GetRequiredService<IWorkstationSecurityMonitor>();

        IWorkstationSecurityReadiness workstationReadiness =
            services.GetRequiredService<IWorkstationSecurityReadiness>();

        IApplicationSecuritySession securitySession =
            services.GetRequiredService<IApplicationSecuritySession>();

        // Capture the callback belonging to the initial monitoring generation.
        EventHandler<WorkstationSecurityEventArgs>? staleEventHandler =
            testHost.WorkstationEventSource.CaptureSecurityEventHandler();

        Assert.NotNull(staleEventHandler);

        // Hold the next actual Windows Hello verification request so that
        // monitoring failure can race with an in-flight authentication.
        testHost.WindowsHelloPlatformApi.HoldNextVerificationRequest();

        // Act: begin authentication through the production Application service.
        Task<ApplicationAuthenticationServiceResult> authenticationTask =
            authenticationService.AuthenticateAsync(cancellationToken);

        // Wait until Windows Hello verification is actually in flight.
        await testHost.WindowsHelloPlatformApi.WaitForVerificationRequestAsync(
            cancellationToken);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticating,
            securitySession.CurrentSnapshot.State);

        // Trigger runtime monitoring failure before Windows Hello completes.
        testHost.WorkstationEventSource.RaiseMonitoringFailure(
            new InvalidOperationException(
                "Simulated monitoring failure during Windows Hello verification."));

        Assert.Equal(
            WorkstationSecurityReadinessState.Unavailable,
            workstationReadiness.CurrentSnapshot.State);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            securitySession.CurrentSnapshot.State);

        Assert.False(
            securitySession.CurrentSnapshot.IsProtectedAccessAvailable);

        // Recover monitoring before completing the old Windows Hello request.
        // This deliberately creates a newer Ready generation and Locked session.
        bool monitoringRecovered =
            await workstationMonitor.TryStartAsync(
                cancellationToken);

        Assert.True(monitoringRecovered);

        Assert.Equal(
            WorkstationSecurityReadinessState.Ready,
            workstationReadiness.CurrentSnapshot.State);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            securitySession.CurrentSnapshot.State);

        Assert.False(
            securitySession.CurrentSnapshot.IsProtectedAccessAvailable);

        long recoveredReadinessRevision =
            workstationReadiness.CurrentSnapshot.Revision;

        // Complete the previous Windows Hello request successfully.
        // Its success must be rejected because it belongs to an obsolete
        // workstation-readiness revision and authentication attempt.
        testHost.WindowsHelloPlatformApi.CompletePendingVerification(
            UserConsentVerificationResult.Verified);

        ApplicationAuthenticationServiceResult delayedResult =
            await authenticationTask.WaitAsync(cancellationToken);

        Assert.Equal(
            ApplicationAuthenticationServiceResult.Superseded,
            delayedResult);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            securitySession.CurrentSnapshot.State);

        Assert.False(
            securitySession.CurrentSnapshot.IsProtectedAccessAvailable);

        Assert.Equal(
            recoveredReadinessRevision,
            workstationReadiness.CurrentSnapshot.Revision);

        // Establish a fresh authenticated session under the new monitoring
        // generation. The old callback must not affect this session.
        bool freshlyAuthenticated =
            await startupCoordinator.AuthenticateAsync(
                cancellationToken);

        Assert.True(freshlyAuthenticated);

        ApplicationSecuritySessionSnapshot authenticatedSnapshot =
            securitySession.CurrentSnapshot;

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            authenticatedSnapshot.State);

        Assert.True(
            authenticatedSnapshot.IsProtectedAccessAvailable);

        // Deliver a lock event through the callback captured before failure
        // and recovery. The production monitor must reject the obsolete
        // source-generation callback.
        testHost.WorkstationEventSource.RaiseCapturedSecurityEvent(
            staleEventHandler,
            WorkstationSecurityEventKind.SessionLocked);

        // Assert: the current authenticated revision remains unchanged.
        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            securitySession.CurrentSnapshot.State);

        Assert.True(
            securitySession.CurrentSnapshot.IsProtectedAccessAvailable);

        Assert.Equal(
            authenticatedSnapshot.Revision,
            securitySession.CurrentSnapshot.Revision);
    }
}
