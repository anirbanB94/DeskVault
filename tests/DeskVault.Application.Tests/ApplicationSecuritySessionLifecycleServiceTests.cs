using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Authentication;
using DeskVault.Application.Security.Sessions;
using DeskVault.Application.Security.Workstation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class ApplicationSecuritySessionLifecycleServiceTests
{
    [Fact]
    public void Lock_WhenSessionIsAuthenticated_WithdrawsProtectedAccess()
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateAuthenticatedSession();

        var notifications =
            new List<ApplicationSecuritySessionSnapshot>();

        session.StateChanged += notifications.Add;

        var authenticationService =
            new StubAuthenticationService(
                _ => throw new InvalidOperationException());

        var lifecycle =
            CreateLifecycle(session, authenticationService);

        // Act
        ApplicationSecuritySessionSnapshot snapshot =
            lifecycle.Lock();

        // Assert
        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            snapshot.State);

        Assert.False(snapshot.IsProtectedAccessAvailable);

        Assert.Same(snapshot, session.CurrentSnapshot);

        Assert.Single(notifications);
        Assert.Same(snapshot, notifications[0]);

        Assert.Equal(0, authenticationService.CallCount);
    }

    [Fact]
    public async Task TryLock_WhenExpectedRevisionIsStale_DoesNotSupersedeNewerInFlightUnlock()
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateLockedSession();

        long staleRevision =
            session.CurrentSnapshot.Revision;

        var authenticationReached =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var allowAuthenticationCompletion =
            new TaskCompletionSource<ApplicationAuthenticationServiceResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var authenticationService =
            new StubAuthenticationService(
                async cancellationToken =>
                {
                    CompleteAuthentication(
                        session,
                        ApplicationAuthenticationOutcome.Succeeded);

                    authenticationReached.TrySetResult(true);

                    return await allowAuthenticationCompletion.Task
                        .WaitAsync(cancellationToken);
                });

        var lifecycle =
            CreateLifecycle(session, authenticationService);

        try
        {
            Task<ApplicationAuthenticationServiceResult> unlockTask =
                lifecycle.UnlockAsync(
                    TestContext.Current.CancellationToken);

            await authenticationReached.Task.WaitAsync(
                TestContext.Current.CancellationToken);

            ApplicationSecuritySessionSnapshot authenticated =
                session.CurrentSnapshot;

            // Act
            bool locked = lifecycle.TryLock(
                staleRevision,
                out ApplicationSecuritySessionSnapshot snapshot);

            // Complete the previously started unlock after the stale
            // conditional lock has been rejected.
            allowAuthenticationCompletion.TrySetResult(
                ApplicationAuthenticationServiceResult.Succeeded);

            ApplicationAuthenticationServiceResult unlockResult =
                await unlockTask.WaitAsync(
                    TestContext.Current.CancellationToken);

            // Assert
            Assert.False(locked);
            Assert.Same(authenticated, snapshot);
            Assert.Same(authenticated, session.CurrentSnapshot);

            Assert.Equal(
                ApplicationAuthenticationServiceResult.Succeeded,
                unlockResult);

            Assert.Equal(
                ApplicationSecuritySessionState.Authenticated,
                session.CurrentSnapshot.State);

            Assert.True(
                session.CurrentSnapshot.IsProtectedAccessAvailable);
        }
        finally
        {
            allowAuthenticationCompletion.TrySetResult(
                ApplicationAuthenticationServiceResult.Succeeded);
        }
    }

    [Fact]
    public async Task TryLock_WhenExpectedRevisionMatches_SupersedesInFlightUnlock()
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateLockedSession();

        var authenticationReached =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var authenticationService =
            new StubAuthenticationService(
                async cancellationToken =>
                {
                    CompleteAuthentication(
                        session,
                        ApplicationAuthenticationOutcome.Succeeded);

                    authenticationReached.TrySetResult(true);

                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);

                    return ApplicationAuthenticationServiceResult.Succeeded;
                });

        var lifecycle =
            CreateLifecycle(session, authenticationService);

        Task<ApplicationAuthenticationServiceResult> unlockTask =
            lifecycle.UnlockAsync(
                TestContext.Current.CancellationToken);

        await authenticationReached.Task.WaitAsync(
            TestContext.Current.CancellationToken);

        ApplicationSecuritySessionSnapshot authenticated =
            session.CurrentSnapshot;

        // Act
        bool locked = lifecycle.TryLock(
            authenticated.Revision,
            out ApplicationSecuritySessionSnapshot snapshot);

        ApplicationAuthenticationServiceResult unlockResult =
            await unlockTask.WaitAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.True(locked);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            snapshot.State);

        Assert.Same(snapshot, session.CurrentSnapshot);

        Assert.Equal(
            ApplicationAuthenticationServiceResult.Superseded,
            unlockResult);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            session.CurrentSnapshot.State);

        Assert.False(
            session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Theory]
    [InlineData(
        ApplicationAuthenticationOutcome.Succeeded,
        ApplicationAuthenticationServiceResult.Succeeded,
        ApplicationSecuritySessionState.Authenticated)]
    [InlineData(
        ApplicationAuthenticationOutcome.Failed,
        ApplicationAuthenticationServiceResult.Failed,
        ApplicationSecuritySessionState.Locked)]
    [InlineData(
        ApplicationAuthenticationOutcome.Cancelled,
        ApplicationAuthenticationServiceResult.Cancelled,
        ApplicationSecuritySessionState.Locked)]
    [InlineData(
        ApplicationAuthenticationOutcome.Unavailable,
        ApplicationAuthenticationServiceResult.Unavailable,
        ApplicationSecuritySessionState.Unavailable)]
    public async Task UnlockAsync_UsesAuthenticationOutcome(
        ApplicationAuthenticationOutcome outcome,
        ApplicationAuthenticationServiceResult expectedResult,
        ApplicationSecuritySessionState expectedState)
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateLockedSession();

        var notifications =
            new List<ApplicationSecuritySessionSnapshot>();

        session.StateChanged += notifications.Add;

        var authenticationService =
            new StubAuthenticationService(
                _ => Task.FromResult(
                    CompleteAuthentication(session, outcome)));

        var lifecycle =
            CreateLifecycle(session, authenticationService);

        // Act
        ApplicationAuthenticationServiceResult result =
            await lifecycle.UnlockAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedResult, result);

        Assert.Equal(
            expectedState,
            session.CurrentSnapshot.State);

        Assert.Equal(
            expectedState == ApplicationSecuritySessionState.Authenticated,
            session.CurrentSnapshot.IsProtectedAccessAvailable);

        Assert.Equal(
            new[]
            {
                ApplicationSecuritySessionState.Authenticating,
                expectedState
            },
            notifications.Select(snapshot => snapshot.State));

        Assert.Equal(
            new long[] { 2, 3 },
            notifications.Select(snapshot => snapshot.Revision));
    }

    [Fact]
    public async Task UnlockAsync_WhenWorkstationMonitoringIsUnavailable_BlocksUnlockAndDoesNotInvokeProvider()
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateLockedSession();

        var provider =
            new Mock<IApplicationAuthenticationProvider>(MockBehavior.Strict);

        provider
            .Setup(x => x.CheckAvailabilityAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                ApplicationAuthenticationAvailability.Available);

        provider
            .Setup(x => x.RequestAuthenticationAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                ApplicationAuthenticationProviderResult.Succeeded);

        var workstationReadiness =
            new Mock<IWorkstationSecurityReadiness>(MockBehavior.Strict);

        workstationReadiness
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(new WorkstationSecurityReadinessSnapshot(
                WorkstationSecurityReadinessState.Unavailable,
                revision: 1));

        var authenticationService =
            new ApplicationAuthenticationService(
                provider.Object,
                session,
                workstationReadiness.Object,
                NullLogger<ApplicationAuthenticationService>.Instance);

        var lifecycle =
            CreateLifecycle(session, authenticationService);

        // Act
        ApplicationAuthenticationServiceResult result =
            await lifecycle.UnlockAsync(
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

        // Windows Hello being Available cannot bypass the unavailable
        // workstation-monitoring prerequisite.
        provider.Verify(
            x => x.CheckAvailabilityAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        provider.Verify(
            x => x.RequestAuthenticationAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        workstationReadiness.Verify(
            x => x.TryRestoreSessionAvailability(
                It.IsAny<long>()),
            Times.Never);
    }
    [Fact]
    public async Task UnlockAsync_WhenSessionIsUnavailable_RequiresSuccessfulAuthentication()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        session.MarkUnavailable();

        var authenticationService =
            new StubAuthenticationService(
                _ => Task.FromResult(
                    CompleteAuthentication(
                        session,
                        ApplicationAuthenticationOutcome.Succeeded)));

        var lifecycle =
            CreateLifecycle(session, authenticationService);

        // Act
        ApplicationAuthenticationServiceResult result =
            await lifecycle.UnlockAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.Succeeded,
            result);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            session.CurrentSnapshot.State);

        Assert.True(session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public async Task Lock_WhenAvailabilityCheckIsPending_SupersedesUnlock()
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateLockedSession();

        var availabilityStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var authenticationService =
            new StubAuthenticationService(
                async cancellationToken =>
                {
                    availabilityStarted.TrySetResult(true);

                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);

                    return ApplicationAuthenticationServiceResult.Succeeded;
                });

        var lifecycle =
            CreateLifecycle(session, authenticationService);

        Task<ApplicationAuthenticationServiceResult> unlockTask =
            lifecycle.UnlockAsync(
                TestContext.Current.CancellationToken);

        await availabilityStarted.Task.WaitAsync(
            TestContext.Current.CancellationToken);

        // Act
        ApplicationSecuritySessionSnapshot snapshot =
            lifecycle.Lock();

        ApplicationAuthenticationServiceResult result =
            await unlockTask.WaitAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.Superseded,
            result);

        Assert.Same(snapshot, session.CurrentSnapshot);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            session.CurrentSnapshot.State);

        Assert.False(session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public async Task Lock_WhenAuthenticationAttemptIsPending_RejectsLateSuccess()
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateLockedSession();

        var authenticationStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var authenticationCompletion =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var authenticationService =
            new StubAuthenticationService(
                async _ =>
                {
                    ApplicationSecuritySessionAttempt attempt =
                        session.BeginAuthentication();

                    authenticationStarted.TrySetResult(true);

                    await authenticationCompletion.Task;

                    bool accepted =
                        session.TryCompleteAuthentication(
                            attempt,
                            ApplicationAuthenticationOutcome.Succeeded);

                    return accepted
                        ? ApplicationAuthenticationServiceResult.Succeeded
                        : ApplicationAuthenticationServiceResult.Superseded;
                });

        var lifecycle =
            CreateLifecycle(session, authenticationService);

        Task<ApplicationAuthenticationServiceResult> unlockTask =
            lifecycle.UnlockAsync(
                TestContext.Current.CancellationToken);

        await authenticationStarted.Task.WaitAsync(
            TestContext.Current.CancellationToken);

        // Act
        lifecycle.Lock();

        authenticationCompletion.TrySetResult(true);

        ApplicationAuthenticationServiceResult result =
            await unlockTask.WaitAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.Superseded,
            result);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            session.CurrentSnapshot.State);

        Assert.False(session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public async Task UnlockAsync_WhenAnotherUnlockIsInProgress_ReturnsAlreadyInProgress()
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateLockedSession();

        var authenticationStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var authenticationCompletion =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var authenticationService =
            new StubAuthenticationService(
                async cancellationToken =>
                {
                    authenticationStarted.TrySetResult(true);

                    await authenticationCompletion.Task.WaitAsync(
                        cancellationToken);

                    return CompleteAuthentication(
                        session,
                        ApplicationAuthenticationOutcome.Succeeded);
                });

        var lifecycle =
            CreateLifecycle(session, authenticationService);

        Task<ApplicationAuthenticationServiceResult> firstUnlock =
            lifecycle.UnlockAsync(
                TestContext.Current.CancellationToken);

        await authenticationStarted.Task.WaitAsync(
            TestContext.Current.CancellationToken);

        // Act
        ApplicationAuthenticationServiceResult secondResult =
            await lifecycle.UnlockAsync(
                TestContext.Current.CancellationToken);

        authenticationCompletion.TrySetResult(true);

        ApplicationAuthenticationServiceResult firstResult =
            await firstUnlock.WaitAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.AlreadyInProgress,
            secondResult);

        Assert.Equal(
            ApplicationAuthenticationServiceResult.Succeeded,
            firstResult);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            session.CurrentSnapshot.State);

        Assert.Equal(1, authenticationService.CallCount);
    }

    [Fact]
    public async Task UnlockAsync_WhenCallerCancels_PropagatesCancellation()
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateLockedSession();

        var authenticationStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var authenticationService =
            new StubAuthenticationService(
                async cancellationToken =>
                {
                    authenticationStarted.TrySetResult(true);

                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);

                    return ApplicationAuthenticationServiceResult.Succeeded;
                });

        var lifecycle =
            CreateLifecycle(session, authenticationService);

        using var cancellationSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);

        Task<ApplicationAuthenticationServiceResult> unlockTask =
            lifecycle.UnlockAsync(cancellationSource.Token);

        await authenticationStarted.Task.WaitAsync(
            TestContext.Current.CancellationToken);

        // Act
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await unlockTask);

        // Assert
        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            session.CurrentSnapshot.State);

        Assert.False(session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public async Task Lock_WhenAvailabilityCheckIgnoresCancellation_SupersedesUnlock()
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateLockedSession();

        var availabilityStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var availabilityCompletion =
            new TaskCompletionSource<ApplicationAuthenticationAvailability>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var provider =
            new Mock<IApplicationAuthenticationProvider>();

        provider
            .Setup(x => x.CheckAvailabilityAsync(
                It.IsAny<CancellationToken>()))
            .Returns(
                (CancellationToken _) =>
                {
                    availabilityStarted.TrySetResult(true);

                    return availabilityCompletion.Task;
                });

        provider
            .Setup(x => x.RequestAuthenticationAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                ApplicationAuthenticationProviderResult.Succeeded);

        var workstationReadiness =
            new Mock<IWorkstationSecurityReadiness>();

        workstationReadiness
            .Setup(x => x.CurrentSnapshot)
            .Returns(new WorkstationSecurityReadinessSnapshot(
                WorkstationSecurityReadinessState.Ready,
                revision: 1));

        workstationReadiness
            .Setup(x => x.TryRestoreSessionAvailability(1))
            .Returns(true);

        var authenticationService =
            new ApplicationAuthenticationService(
                provider.Object,
                session,
                workstationReadiness.Object,
                NullLogger<ApplicationAuthenticationService>.Instance);

        var lifecycle =
            CreateLifecycle(session, authenticationService);

        Task<ApplicationAuthenticationServiceResult> unlockTask =
            lifecycle.UnlockAsync(
                TestContext.Current.CancellationToken);

        await availabilityStarted.Task.WaitAsync(
            TestContext.Current.CancellationToken);

        // Act
        lifecycle.Lock();

        ApplicationAuthenticationServiceResult result =
            await unlockTask.WaitAsync(
                TestContext.Current.CancellationToken);

        availabilityCompletion.TrySetResult(
            ApplicationAuthenticationAvailability.Available);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.Superseded,
            result);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            session.CurrentSnapshot.State);

        Assert.False(session.CurrentSnapshot.IsProtectedAccessAvailable);

        provider.Verify(
            x => x.RequestAuthenticationAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UnlockAsync_DuringLockNotification_ReturnsNotPermitted()
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateAuthenticatedSession();

        var notificationStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        using var notificationRelease =
            new ManualResetEventSlim();

        session.StateChanged += snapshot =>
        {
            if (snapshot.State ==
                ApplicationSecuritySessionState.Locked)
            {
                notificationStarted.TrySetResult(true);
                notificationRelease.Wait();
            }
        };

        var authenticationService =
            new StubAuthenticationService(
                _ => Task.FromResult(
                    ApplicationAuthenticationServiceResult.Succeeded));

        var lifecycle =
            CreateLifecycle(session, authenticationService);

        Task<ApplicationSecuritySessionSnapshot> lockTask =
            Task.Run(lifecycle.Lock);

        try
        {
            await notificationStarted.Task.WaitAsync(
                TestContext.Current.CancellationToken);

            // Act
            ApplicationAuthenticationServiceResult result =
                await lifecycle.UnlockAsync(
                    TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(
                ApplicationAuthenticationServiceResult.NotPermitted,
                result);

            Assert.Equal(0, authenticationService.CallCount);
        }
        finally
        {
            notificationRelease.Set();
        }

        await lockTask.WaitAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            session.CurrentSnapshot.State);

        Assert.False(session.CurrentSnapshot.IsProtectedAccessAvailable);
    }


    [Fact]
    public async Task UnlockAsync_WhenLockNotificationIsQueuedBehindActiveDispatch_PreservesRevisionOrder()
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateLockedSession();

        var notificationStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        using var notificationRelease =
            new ManualResetEventSlim();

        var notifications =
            new List<ApplicationSecuritySessionSnapshot>();

        int firstAuthenticatingNotification = 0;

        session.StateChanged += snapshot =>
        {
            if (snapshot.State ==
                    ApplicationSecuritySessionState.Authenticating &&
                Interlocked.CompareExchange(
                    ref firstAuthenticatingNotification,
                    1,
                    0) == 0)
            {
                notificationStarted.TrySetResult(true);
                notificationRelease.Wait();
            }

            notifications.Add(snapshot);
        };

        var authenticationService =
            new StubAuthenticationService(
                _ => Task.FromResult(
                    CompleteAuthentication(
                        session,
                        ApplicationAuthenticationOutcome.Succeeded)));

        var lifecycle =
            CreateLifecycle(session, authenticationService);

        Task<ApplicationSecuritySessionAttempt> initialAttemptTask =
            Task.Run(() => session.BeginAuthentication());

        ApplicationSecuritySessionSnapshot lockedSnapshot;

        ApplicationAuthenticationServiceResult unlockResult;

        try
        {
            await notificationStarted.Task.WaitAsync(
                TestContext.Current.CancellationToken);

            // Act
            lockedSnapshot =
                lifecycle.Lock();

            unlockResult =
                await lifecycle.UnlockAsync(
                    TestContext.Current.CancellationToken);
        }
        finally
        {
            // Allow the existing notification dispatcher to drain its queue.
            notificationRelease.Set();
        }

        ApplicationSecuritySessionAttempt initialAttempt =
            await initialAttemptTask.WaitAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            lockedSnapshot.State);

        Assert.Equal(3, lockedSnapshot.Revision);

        Assert.Equal(
            ApplicationAuthenticationServiceResult.Succeeded,
            unlockResult);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            session.CurrentSnapshot.State);

        Assert.True(session.CurrentSnapshot.IsProtectedAccessAvailable);

        Assert.False(
            session.TryCompleteAuthentication(
                initialAttempt,
                ApplicationAuthenticationOutcome.Succeeded));

        Assert.Equal(
            new[]
            {
                ApplicationSecuritySessionState.Authenticating,
                ApplicationSecuritySessionState.Locked,
                ApplicationSecuritySessionState.Authenticating,
                ApplicationSecuritySessionState.Authenticated
            },
            notifications.Select(snapshot => snapshot.State));

        Assert.Equal(
            new long[] { 2, 3, 4, 5 },
            notifications.Select(snapshot => snapshot.Revision));

        Assert.Equal(1, authenticationService.CallCount);
    }


    [Fact]
    public async Task Lock_WhenSessionIsAlreadyLocked_RepeatedRequestSupersedesLatestUnlock()
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateLockedSession();

        var notifications =
            new List<ApplicationSecuritySessionSnapshot>();

        session.StateChanged += notifications.Add;

        var firstUnlockStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var secondUnlockStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var firstUnlockCompletion =
            new TaskCompletionSource<ApplicationAuthenticationServiceResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var secondUnlockCompletion =
            new TaskCompletionSource<ApplicationAuthenticationServiceResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        int invocationCount = 0;

        var authenticationService =
            new StubAuthenticationService(
                _ =>
                {
                    int invocation =
                        Interlocked.Increment(ref invocationCount);

                    return invocation switch
                    {
                        1 => StartFirstUnlockAsync(),
                        2 => StartSecondUnlockAsync(),
                        _ => throw new InvalidOperationException(
                            "Unexpected authentication invocation.")
                    };
                });

        var lifecycle =
            CreateLifecycle(session, authenticationService);

        // Act
        Task<ApplicationAuthenticationServiceResult> firstUnlock =
            lifecycle.UnlockAsync(
                TestContext.Current.CancellationToken);

        await firstUnlockStarted.Task.WaitAsync(
            TestContext.Current.CancellationToken);

        ApplicationSecuritySessionSnapshot firstLockSnapshot =
            lifecycle.Lock();

        Task<ApplicationAuthenticationServiceResult> secondUnlock =
            lifecycle.UnlockAsync(
                TestContext.Current.CancellationToken);

        await secondUnlockStarted.Task.WaitAsync(
            TestContext.Current.CancellationToken);

        ApplicationSecuritySessionSnapshot secondLockSnapshot =
            lifecycle.Lock();

        firstUnlockCompletion.TrySetResult(
            ApplicationAuthenticationServiceResult.Succeeded);

        secondUnlockCompletion.TrySetResult(
            ApplicationAuthenticationServiceResult.Succeeded);

        ApplicationAuthenticationServiceResult[] results =
            await Task.WhenAll(firstUnlock, secondUnlock);

        // Assert
        Assert.All(
            results,
            result => Assert.Equal(
                ApplicationAuthenticationServiceResult.Superseded,
                result));

        Assert.Same(firstLockSnapshot, secondLockSnapshot);
        Assert.Same(secondLockSnapshot, session.CurrentSnapshot);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            session.CurrentSnapshot.State);

        Assert.False(session.CurrentSnapshot.IsProtectedAccessAvailable);

        Assert.Empty(notifications);

        Assert.Equal(2, authenticationService.CallCount);

        Task<ApplicationAuthenticationServiceResult> StartFirstUnlockAsync()
        {
            firstUnlockStarted.TrySetResult(true);
            return firstUnlockCompletion.Task;
        }

        Task<ApplicationAuthenticationServiceResult> StartSecondUnlockAsync()
        {
            secondUnlockStarted.TrySetResult(true);
            return secondUnlockCompletion.Task;
        }
    }

    [Fact]
    public void Lock_WhenCalledConcurrently_LeavesSessionLocked()
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateAuthenticatedSession();

        var lifecycle =
            CreateLifecycle(
                session,
                new StubAuthenticationService(
                    _ => throw new InvalidOperationException()));

        // Act
        Parallel.For(
            0,
            64,
            _ => lifecycle.Lock());

        // Assert
        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            session.CurrentSnapshot.State);

        Assert.False(session.CurrentSnapshot.IsProtectedAccessAvailable);

        Assert.Equal(3, session.CurrentSnapshot.Revision);
    }

    [Fact]
    public async Task UnlockAsync_WhenSessionIsUnauthenticated_ReturnsNotPermitted()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        var authenticationService =
            new StubAuthenticationService(
                _ => Task.FromResult(
                    ApplicationAuthenticationServiceResult.Succeeded));

        var lifecycle =
            CreateLifecycle(session, authenticationService);

        // Act
        ApplicationAuthenticationServiceResult result =
            await lifecycle.UnlockAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.NotPermitted,
            result);

        Assert.Equal(0, authenticationService.CallCount);

        Assert.Equal(
            ApplicationSecuritySessionState.Unauthenticated,
            session.CurrentSnapshot.State);
    }

    private static ApplicationSecuritySessionLifecycleService CreateLifecycle(
        IApplicationSecuritySession session,
        IApplicationAuthenticationService authenticationService)
    {
        return new ApplicationSecuritySessionLifecycleService(
            session,
            authenticationService);
    }

    private static ApplicationSecuritySession CreateLockedSession()
    {
        var session =
            new ApplicationSecuritySession();

        session.Lock();

        return session;
    }

    private static ApplicationSecuritySession CreateAuthenticatedSession()
    {
        var session =
            new ApplicationSecuritySession();

        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        Assert.True(
            session.TryCompleteAuthentication(
                attempt,
                ApplicationAuthenticationOutcome.Succeeded));

        return session;
    }

    private static ApplicationAuthenticationServiceResult CompleteAuthentication(
        ApplicationSecuritySession session,
        ApplicationAuthenticationOutcome outcome)
    {
        if (session.CurrentSnapshot.State ==
            ApplicationSecuritySessionState.Unavailable)
        {
            session.RestoreAvailability();
        }

        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        bool accepted =
            session.TryCompleteAuthentication(
                attempt,
                outcome);

        if (!accepted)
        {
            return ApplicationAuthenticationServiceResult.Superseded;
        }

        return outcome switch
        {
            ApplicationAuthenticationOutcome.Succeeded =>
                ApplicationAuthenticationServiceResult.Succeeded,

            ApplicationAuthenticationOutcome.Failed =>
                ApplicationAuthenticationServiceResult.Failed,

            ApplicationAuthenticationOutcome.Cancelled =>
                ApplicationAuthenticationServiceResult.Cancelled,

            ApplicationAuthenticationOutcome.Unavailable =>
                ApplicationAuthenticationServiceResult.Unavailable,

            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
    }

    private sealed class StubAuthenticationService(
        Func<CancellationToken, Task<ApplicationAuthenticationServiceResult>> authenticate)
        : IApplicationAuthenticationService
    {
        private int _callCount;

        public int CallCount =>
            Volatile.Read(ref _callCount);

        public Task<ApplicationAuthenticationServiceResult> AuthenticateAsync(
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);

            return authenticate(cancellationToken);
        }
    }
}
