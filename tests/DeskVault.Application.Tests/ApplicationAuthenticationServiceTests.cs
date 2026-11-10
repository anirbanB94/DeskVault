using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Authentication;
using DeskVault.Application.Security.Sessions;
using DeskVault.Application.Security.Workstation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class ApplicationAuthenticationServiceTests
{
    [Fact]
    public async Task AuthenticateAsync_WhenProviderSucceeds_AuthenticatesSession()
    {
        // Arrange
        var provider =
            CreateAvailableProvider();

        var session =
            new ApplicationSecuritySession();

        ApplicationAuthenticationService service =
            CreateService(
                provider.Object,
                session);

        // Act
        ApplicationAuthenticationServiceResult result =
            await service.AuthenticateAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.Succeeded,
            result);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            session.CurrentSnapshot.State);

        Assert.True(
            session.CurrentSnapshot.IsProtectedAccessAvailable);

        provider.Verify(
            x => x.CheckAvailabilityAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);

        provider.Verify(
            x => x.RequestAuthenticationAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenWorkstationMonitoringIsUnavailable_DoesNotCheckWindowsHelloOrGrantAccess()
    {
        // Arrange
        var provider =
            CreateAvailableProvider();

        var session =
            new ApplicationSecuritySession();

        var readiness =
            new Mock<IWorkstationSecurityReadiness>();

        readiness
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(
                new WorkstationSecurityReadinessSnapshot(
                    WorkstationSecurityReadinessState.Unavailable,
                    revision: 2));

        ApplicationAuthenticationService service =
            CreateService(
                provider.Object,
                session,
                readiness.Object);

        // Act
        ApplicationAuthenticationServiceResult result =
            await service.AuthenticateAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.Unavailable,
            result);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            session.CurrentSnapshot.State);

        Assert.False(session.CurrentSnapshot.IsProtectedAccessAvailable);

        provider.Verify(
            x => x.CheckAvailabilityAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        provider.Verify(
            x => x.RequestAuthenticationAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenReadinessChangesDuringProviderAvailability_DoesNotBeginAuthentication()
    {
        // Arrange
        var provider =
            CreateAvailableProvider();

        var session =
            new ApplicationSecuritySession();

        WorkstationSecurityReadinessSnapshot currentReadiness =
            new(
                WorkstationSecurityReadinessState.Ready,
                revision: 1);

        var readiness =
            new Mock<IWorkstationSecurityReadiness>();

        readiness
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentReadiness);

        provider
            .Setup(x => x.CheckAvailabilityAsync(
                It.IsAny<CancellationToken>()))
            .Returns((CancellationToken _) =>
            {
                // Simulate monitoring failure while Windows Hello availability
                // is being checked.
                currentReadiness =
                    new WorkstationSecurityReadinessSnapshot(
                        WorkstationSecurityReadinessState.Unavailable,
                        revision: 2);

                session.MarkUnavailable();

                return Task.FromResult(
                    ApplicationAuthenticationAvailability.Available);
            });

        ApplicationAuthenticationService service =
            CreateService(
                provider.Object,
                session,
                readiness.Object);

        // Act
        ApplicationAuthenticationServiceResult result =
            await service.AuthenticateAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.Unavailable,
            result);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            session.CurrentSnapshot.State);

        Assert.False(session.CurrentSnapshot.IsProtectedAccessAvailable);

        provider.Verify(
            x => x.RequestAuthenticationAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        // The readiness revision changed while the provider availability
        // check was pending. The stale revision must not be used to restore
        // session availability.
        readiness.Verify(
            x => x.TryRestoreSessionAvailability(1),
            Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenReadinessChangesDuringProviderAuthentication_RejectsSuccessfulProviderResult()
    {
        // Arrange
        var provider =
            CreateAvailableProvider();

        var session =
            new ApplicationSecuritySession();

        WorkstationSecurityReadinessSnapshot currentReadiness =
            new(
                WorkstationSecurityReadinessState.Ready,
                revision: 1);

        var readiness =
            new Mock<IWorkstationSecurityReadiness>();

        readiness
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentReadiness);

        readiness
            .Setup(x => x.TryRestoreSessionAvailability(1))
            .Returns(true);

        var requestStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var requestCompletion =
            new TaskCompletionSource<ApplicationAuthenticationProviderResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        provider
            .Setup(x => x.RequestAuthenticationAsync(
                It.IsAny<CancellationToken>()))
            .Returns((CancellationToken _) =>
            {
                requestStarted.TrySetResult(true);
                return requestCompletion.Task;
            });

        ApplicationAuthenticationService service =
            CreateService(
                provider.Object,
                session,
                readiness.Object);

        Task<ApplicationAuthenticationServiceResult> authenticationTask =
            service.AuthenticateAsync(
                TestContext.Current.CancellationToken);

        await requestStarted.Task.WaitAsync(
            TestContext.Current.CancellationToken);

        // Act
        // Simulate the monitor invalidating the session while Windows Hello
        // verification is still in progress.
        currentReadiness =
            new WorkstationSecurityReadinessSnapshot(
                WorkstationSecurityReadinessState.Unavailable,
                revision: 2);

        session.MarkUnavailable();

        requestCompletion.TrySetResult(
            ApplicationAuthenticationProviderResult.Succeeded);

        ApplicationAuthenticationServiceResult result =
            await authenticationTask.WaitAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.Unavailable,
            result);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            session.CurrentSnapshot.State);

        Assert.False(session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Theory]
    [InlineData(
        ApplicationAuthenticationProviderResult.Failed,
        ApplicationSecuritySessionState.Unauthenticated)]
    [InlineData(
        ApplicationAuthenticationProviderResult.Cancelled,
        ApplicationSecuritySessionState.Unauthenticated)]
    [InlineData(
        ApplicationAuthenticationProviderResult.Failed,
        ApplicationSecuritySessionState.Locked)]
    [InlineData(
        ApplicationAuthenticationProviderResult.Cancelled,
        ApplicationSecuritySessionState.Locked)]
    public async Task AuthenticateAsync_WhenProviderDoesNotSucceed_PreservesPreviousNonAuthorizedState(
        ApplicationAuthenticationProviderResult providerResult,
        ApplicationSecuritySessionState expectedState)
    {
        // Arrange
        var provider =
            CreateAvailableProvider(providerResult);

        var session =
            new ApplicationSecuritySession();

        if (expectedState ==
            ApplicationSecuritySessionState.Locked)
        {
            session.Lock();
        }

        ApplicationAuthenticationService service =
            CreateService(
                provider.Object,
                session);

        // Act
        ApplicationAuthenticationServiceResult result =
            await service.AuthenticateAsync(
                TestContext.Current.CancellationToken);

        // Assert
        ApplicationAuthenticationServiceResult expectedResult =
            providerResult ==
                ApplicationAuthenticationProviderResult.Failed
                ? ApplicationAuthenticationServiceResult.Failed
                : ApplicationAuthenticationServiceResult.Cancelled;

        Assert.Equal(
            expectedResult,
            result);

        Assert.Equal(
            expectedState,
            session.CurrentSnapshot.State);

        Assert.False(
            session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenProviderIsUnavailable_MarksSessionUnavailableWithoutRequestingVerification()
    {
        // Arrange
        var provider =
            CreateAvailableProvider();

        provider
            .Setup(x => x.CheckAvailabilityAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                ApplicationAuthenticationAvailability.Unavailable);

        var session =
            new ApplicationSecuritySession();

        ApplicationAuthenticationService service =
            CreateService(
                provider.Object,
                session);

        // Act
        ApplicationAuthenticationServiceResult result =
            await service.AuthenticateAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.Unavailable,
            result);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            session.CurrentSnapshot.State);

        Assert.False(
            session.CurrentSnapshot.IsProtectedAccessAvailable);

        provider.Verify(
            x => x.RequestAuthenticationAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenAvailabilityRecovers_RestoresLockedStateBeforeAuthentication()
    {
        // Arrange
        var provider =
            CreateAvailableProvider(
                ApplicationAuthenticationProviderResult.Failed);

        var session =
            new ApplicationSecuritySession();

        session.MarkUnavailable();

        var observedStates =
            new List<ApplicationSecuritySessionState>();

        session.StateChanged +=
            snapshot => observedStates.Add(snapshot.State);

        ApplicationAuthenticationService service =
            CreateService(
                provider.Object,
                session);

        // Act
        ApplicationAuthenticationServiceResult result =
            await service.AuthenticateAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.Failed,
            result);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            session.CurrentSnapshot.State);

        Assert.Equal(
            [
                ApplicationSecuritySessionState.Locked,
                ApplicationSecuritySessionState.Authenticating,
                ApplicationSecuritySessionState.Locked
            ],
            observedStates);

        Assert.False(
            session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenProviderReturnsUnavailableAfterAttempt_MarksSessionUnavailable()
    {
        // Arrange
        var provider =
            CreateAvailableProvider(
                ApplicationAuthenticationProviderResult.Unavailable);

        var session =
            new ApplicationSecuritySession();

        ApplicationAuthenticationService service =
            CreateService(
                provider.Object,
                session);

        // Act
        ApplicationAuthenticationServiceResult result =
            await service.AuthenticateAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.Unavailable,
            result);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            session.CurrentSnapshot.State);

        Assert.False(
            session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenAvailabilityCheckThrows_MarksSessionUnavailable()
    {
        // Arrange
        var provider =
            CreateAvailableProvider();

        provider
            .Setup(x => x.CheckAvailabilityAsync(
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    "Synthetic provider failure."));

        var session =
            new ApplicationSecuritySession();

        ApplicationAuthenticationService service =
            CreateService(
                provider.Object,
                session);

        // Act
        ApplicationAuthenticationServiceResult result =
            await service.AuthenticateAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.Unavailable,
            result);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            session.CurrentSnapshot.State);

        provider.Verify(
            x => x.RequestAuthenticationAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenVerificationRequestThrows_MarksSessionUnavailable()
    {
        // Arrange
        var provider =
            CreateAvailableProvider();

        provider
            .Setup(x => x.RequestAuthenticationAsync(
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    "Synthetic provider failure."));

        var session =
            new ApplicationSecuritySession();

        ApplicationAuthenticationService service =
            CreateService(
                provider.Object,
                session);

        // Act
        ApplicationAuthenticationServiceResult result =
            await service.AuthenticateAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.Unavailable,
            result);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            session.CurrentSnapshot.State);

        Assert.False(
            session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenProviderReturnsUnknownResult_FailsClosed()
    {
        // Arrange
        var provider =
            CreateAvailableProvider(
                (ApplicationAuthenticationProviderResult)999);

        var session =
            new ApplicationSecuritySession();

        ApplicationAuthenticationService service =
            CreateService(
                provider.Object,
                session);

        // Act
        ApplicationAuthenticationServiceResult result =
            await service.AuthenticateAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.Unavailable,
            result);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            session.CurrentSnapshot.State);

        Assert.False(
            session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenAvailabilityIsUnknown_FailsClosed()
    {
        // Arrange
        var provider =
            CreateAvailableProvider();

        provider
            .Setup(x => x.CheckAvailabilityAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (ApplicationAuthenticationAvailability)999);

        var session =
            new ApplicationSecuritySession();

        ApplicationAuthenticationService service =
            CreateService(
                provider.Object,
                session);

        // Act
        ApplicationAuthenticationServiceResult result =
            await service.AuthenticateAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.Unavailable,
            result);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            session.CurrentSnapshot.State);

        provider.Verify(
            x => x.RequestAuthenticationAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenAttemptIsInvalidatedByLock_RejectsLateSuccess()
    {
        // Arrange
        var provider =
            CreateAvailableProvider();

        var requestStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var requestCompletion =
            new TaskCompletionSource<ApplicationAuthenticationProviderResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        provider
            .Setup(x => x.RequestAuthenticationAsync(
                It.IsAny<CancellationToken>()))
            .Returns(
                (CancellationToken _) =>
                {
                    requestStarted.TrySetResult(true);

                    return requestCompletion.Task;
                });

        var session =
            new ApplicationSecuritySession();

        ApplicationAuthenticationService service =
            CreateService(
                provider.Object,
                session);

        Task<ApplicationAuthenticationServiceResult> authenticationTask =
            service.AuthenticateAsync(
                TestContext.Current.CancellationToken);

        await requestStarted.Task;

        // Act
        session.Lock();

        requestCompletion.SetResult(
            ApplicationAuthenticationProviderResult.Succeeded);

        ApplicationAuthenticationServiceResult result =
            await authenticationTask;

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.Superseded,
            result);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            session.CurrentSnapshot.State);

        Assert.False(
            session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenCallerCancelsDuringNonCooperativeProviderRequest_InvalidatesAttempt()
    {
        // Arrange
        var provider =
            CreateAvailableProvider();

        var requestStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var requestCompletion =
            new TaskCompletionSource<ApplicationAuthenticationProviderResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        provider
            .Setup(x => x.RequestAuthenticationAsync(
                It.IsAny<CancellationToken>()))
            .Returns(
                (CancellationToken _) =>
                {
                    requestStarted.TrySetResult(true);

                    // Deliberately ignore cancellation to verify that
                    // the Application service independently invalidates
                    // its session-bound authentication attempt.
                    return requestCompletion.Task;
                });

        var session =
            new ApplicationSecuritySession();

        ApplicationAuthenticationService service =
            CreateService(
                provider.Object,
                session);

        using var cancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);

        Task<ApplicationAuthenticationServiceResult> authenticationTask =
            service.AuthenticateAsync(
                cancellationTokenSource.Token);

        await requestStarted.Task;

        // Act
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await authenticationTask);

        // A late successful provider result must not restore access.
        requestCompletion.SetResult(
            ApplicationAuthenticationProviderResult.Succeeded);

        // Assert
        Assert.Equal(
            ApplicationSecuritySessionState.Unauthenticated,
            session.CurrentSnapshot.State);

        Assert.False(
            session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenAnotherRequestIsInProgress_ReturnsAlreadyInProgress()
    {
        // Arrange
        var provider =
            CreateAvailableProvider();

        var requestStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var requestCompletion =
            new TaskCompletionSource<ApplicationAuthenticationProviderResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        provider
            .Setup(x => x.RequestAuthenticationAsync(
                It.IsAny<CancellationToken>()))
            .Returns(
                (CancellationToken _) =>
                {
                    requestStarted.TrySetResult(true);

                    return requestCompletion.Task;
                });

        var session =
            new ApplicationSecuritySession();

        ApplicationAuthenticationService service =
            CreateService(
                provider.Object,
                session);

        Task<ApplicationAuthenticationServiceResult> firstRequest =
            service.AuthenticateAsync(
                TestContext.Current.CancellationToken);

        await requestStarted.Task;

        // Act
        ApplicationAuthenticationServiceResult secondResult =
            await service.AuthenticateAsync(
                TestContext.Current.CancellationToken);

        requestCompletion.SetResult(
            ApplicationAuthenticationProviderResult.Succeeded);

        ApplicationAuthenticationServiceResult firstResult =
            await firstRequest;

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.AlreadyInProgress,
            secondResult);

        Assert.Equal(
            ApplicationAuthenticationServiceResult.Succeeded,
            firstResult);

        provider.Verify(
            x => x.CheckAvailabilityAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);

        provider.Verify(
            x => x.RequestAuthenticationAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            session.CurrentSnapshot.State);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenSessionIsAlreadyAuthenticated_ReturnsNotPermitted()
    {
        // Arrange
        var provider =
            CreateAvailableProvider();

        var session =
            new ApplicationSecuritySession();

        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        session.TryCompleteAuthentication(
            attempt,
            ApplicationAuthenticationOutcome.Succeeded);

        ApplicationAuthenticationService service =
            CreateService(
                provider.Object,
                session);

        // Act
        ApplicationAuthenticationServiceResult result =
            await service.AuthenticateAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.NotPermitted,
            result);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            session.CurrentSnapshot.State);

        provider.Verify(
            x => x.CheckAvailabilityAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        provider.Verify(
            x => x.RequestAuthenticationAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenCallerIsAlreadyCancelled_DoesNotChangeSession()
    {
        // Arrange
        var provider =
            CreateAvailableProvider();

        var session =
            new ApplicationSecuritySession();

        ApplicationAuthenticationService service =
            CreateService(
                provider.Object,
                session);

        using var cancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);

        cancellationTokenSource.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                service.AuthenticateAsync(
                    cancellationTokenSource.Token));

        // Assert
        Assert.Equal(
            ApplicationSecuritySessionState.Unauthenticated,
            session.CurrentSnapshot.State);

        provider.Verify(
            x => x.CheckAvailabilityAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static Mock<IApplicationAuthenticationProvider>
        CreateAvailableProvider(
            ApplicationAuthenticationProviderResult result =
                ApplicationAuthenticationProviderResult.Succeeded)
    {
        var provider =
            new Mock<IApplicationAuthenticationProvider>();

        provider
            .Setup(x => x.CheckAvailabilityAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                ApplicationAuthenticationAvailability.Available);

        provider
            .Setup(x => x.RequestAuthenticationAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        return provider;
    }

    private static ApplicationAuthenticationService CreateService(
        IApplicationAuthenticationProvider provider,
        IApplicationSecuritySession session,
        IWorkstationSecurityReadiness? workstationReadiness = null)
    {
        workstationReadiness ??=
            CreateReadyWorkstationReadiness(session).Object;

        return new ApplicationAuthenticationService(
            provider,
            session,
            workstationReadiness,
            NullLogger<ApplicationAuthenticationService>.Instance);
    }

    private static Mock<IWorkstationSecurityReadiness> CreateReadyWorkstationReadiness(
        IApplicationSecuritySession session)
    {
        var readiness =
            new Mock<IWorkstationSecurityReadiness>();

        var snapshot =
            new WorkstationSecurityReadinessSnapshot(
                WorkstationSecurityReadinessState.Ready,
                revision: 1);

        readiness
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(snapshot);

        readiness
            .Setup(x => x.TryRestoreSessionAvailability(
                It.IsAny<long>()))
            .Returns((long expectedRevision) =>
            {
                if (expectedRevision != snapshot.Revision ||
                    !snapshot.IsReady)
                {
                    return false;
                }

                if (session.CurrentSnapshot.State ==
                    ApplicationSecuritySessionState.Unavailable)
                {
                    try
                    {
                        session.RestoreAvailability();
                    }
                    catch (InvalidOperationException)
                    {
                        return false;
                    }
                }

                return session.CurrentSnapshot.State !=
                    ApplicationSecuritySessionState.Unavailable;
            });

        return readiness;
    }
}
