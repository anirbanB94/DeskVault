using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Authentication;
using DeskVault.Application.Security.Sessions;
using DeskVault.Application.Security.Workstation;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskVault.Application.Tests;

public sealed class WorkstationSecurityMonitorTests
{
    [Fact]
    public async Task TryStartAsync_WhenEventSourceStarts_SetsReadinessToReady()
    {
        // Arrange
        var session = new ApplicationSecuritySession();
        var eventSource = new TestWorkstationSecurityEventSource();
        WorkstationSecurityMonitor monitor = CreateMonitor(
            eventSource,
            session);

        var readinessNotifications =
            new List<WorkstationSecurityReadinessSnapshot>();

        monitor.ReadinessChanged += readinessNotifications.Add;

        // Act
        bool started = await monitor.TryStartAsync(
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(started);
        Assert.Equal(
            WorkstationSecurityReadinessState.Ready,
            monitor.CurrentSnapshot.State);

        Assert.Equal(2, monitor.CurrentSnapshot.Revision);
        Assert.Equal(
            new[]
            {
                WorkstationSecurityReadinessState.Starting,
                WorkstationSecurityReadinessState.Ready
            },
            readinessNotifications.Select(snapshot => snapshot.State));

        Assert.Equal(
            ApplicationSecuritySessionState.Unauthenticated,
            session.CurrentSnapshot.State);

        Assert.Equal(1, eventSource.StartCallCount);
    }

    [Fact]
    public async Task TryStartAsync_WhenEventSourceFails_LeavesSessionUnavailable()
    {
        // Arrange
        var session = new ApplicationSecuritySession();
        var eventSource = new TestWorkstationSecurityEventSource
        {
            StartOperation = _ =>
                Task.FromException(
                    new InvalidOperationException("Synthetic registration failure."))
        };

        WorkstationSecurityMonitor monitor = CreateMonitor(
            eventSource,
            session);

        // Act
        bool started = await monitor.TryStartAsync(
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(started);
        Assert.Equal(
            WorkstationSecurityReadinessState.Unavailable,
            monitor.CurrentSnapshot.State);

        Assert.False(monitor.CurrentSnapshot.IsReady);
        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            session.CurrentSnapshot.State);

        Assert.Equal(1, eventSource.StartCallCount);
        Assert.Equal(1, eventSource.StopCallCount);
    }

    [Fact]
    public async Task TryStartAsync_WhenMonitoringRecovers_RestoresUnavailableSessionOnlyToLocked()
    {
        // Arrange
        var session = new ApplicationSecuritySession();
        var eventSource = new TestWorkstationSecurityEventSource();
        int startAttempt = 0;

        eventSource.StartOperation = _ =>
        {
            startAttempt++;

            return startAttempt == 1
                ? Task.FromException(
                    new InvalidOperationException("Synthetic first registration failure."))
                : Task.CompletedTask;
        };

        WorkstationSecurityMonitor monitor = CreateMonitor(
            eventSource,
            session);

        // Act
        bool firstStart = await monitor.TryStartAsync(
            TestContext.Current.CancellationToken);

        Assert.False(firstStart);
        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            session.CurrentSnapshot.State);

        bool recovered = await monitor.TryStartAsync(
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(recovered);
        Assert.Equal(
            WorkstationSecurityReadinessState.Ready,
            monitor.CurrentSnapshot.State);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            session.CurrentSnapshot.State);

        Assert.False(session.CurrentSnapshot.IsProtectedAccessAvailable);
        Assert.Equal(2, eventSource.StartCallCount);
    }

    [Fact]
    public async Task TryStartAsync_WhenAlreadyReady_DoesNotRegisterEventSourceAgain()
    {
        // Arrange
        var session = new ApplicationSecuritySession();
        var eventSource = new TestWorkstationSecurityEventSource();
        WorkstationSecurityMonitor monitor = CreateMonitor(
            eventSource,
            session);

        // Act
        bool firstStart = await monitor.TryStartAsync(
            TestContext.Current.CancellationToken);

        bool secondStart = await monitor.TryStartAsync(
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(firstStart);
        Assert.True(secondStart);
        Assert.Equal(1, eventSource.StartCallCount);
        Assert.Equal(
            WorkstationSecurityReadinessState.Ready,
            monitor.CurrentSnapshot.State);
    }

    [Theory]
    [InlineData(WorkstationSecurityEventKind.SessionLocked)]
    [InlineData(WorkstationSecurityEventKind.SessionLoggedOff)]
    [InlineData(WorkstationSecurityEventKind.SessionDisconnected)]
    [InlineData(WorkstationSecurityEventKind.SystemSuspending)]
    [InlineData(WorkstationSecurityEventKind.SystemResumed)]
    public async Task SecurityEvent_WhenTransitionRequiresProtection_LocksAuthenticatedSession(
        WorkstationSecurityEventKind eventKind)
    {
        // Arrange
        ApplicationSecuritySession session = CreateAuthenticatedSession();
        var eventSource = new TestWorkstationSecurityEventSource();
        WorkstationSecurityMonitor monitor = CreateMonitor(
            eventSource,
            session);

        Assert.True(await monitor.TryStartAsync(
            TestContext.Current.CancellationToken));

        // Act
        eventSource.RaiseSecurityEvent(eventKind);

        // Assert
        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            session.CurrentSnapshot.State);

        Assert.False(session.CurrentSnapshot.IsProtectedAccessAvailable);
        Assert.True(monitor.CurrentSnapshot.IsReady);
    }

    [Theory]
    [InlineData(WorkstationSecurityEventKind.SessionUnlocked)]
    [InlineData(WorkstationSecurityEventKind.SessionConnected)]
    [InlineData(WorkstationSecurityEventKind.SystemResumed)]
    public async Task SecurityEvent_WhenOperatingSystemReportsUnlockOrResume_DoesNotRestoreAccess(
        WorkstationSecurityEventKind eventKind)
    {
        // Arrange
        ApplicationSecuritySession session = CreateAuthenticatedSession();
        var eventSource = new TestWorkstationSecurityEventSource();
        WorkstationSecurityMonitor monitor = CreateMonitor(
            eventSource,
            session);

        Assert.True(await monitor.TryStartAsync(
            TestContext.Current.CancellationToken));

        var lifecycle = CreateLifecycle(session);
        lifecycle.Lock();

        long lockedRevision = session.CurrentSnapshot.Revision;

        // Act
        eventSource.RaiseSecurityEvent(eventKind);

        // Assert
        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            session.CurrentSnapshot.State);

        Assert.Equal(lockedRevision, session.CurrentSnapshot.Revision);
        Assert.False(session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public async Task SecurityEvent_WhenRepeatedLockArrives_DoesNotCreateContradictorySessionState()
    {
        // Arrange
        ApplicationSecuritySession session = CreateAuthenticatedSession();
        var eventSource = new TestWorkstationSecurityEventSource();
        WorkstationSecurityMonitor monitor = CreateMonitor(
            eventSource,
            session);

        Assert.True(await monitor.TryStartAsync(
            TestContext.Current.CancellationToken));

        // Act
        eventSource.RaiseSecurityEvent(WorkstationSecurityEventKind.SessionLocked);
        long firstLockedRevision = session.CurrentSnapshot.Revision;

        eventSource.RaiseSecurityEvent(WorkstationSecurityEventKind.SessionLocked);
        eventSource.RaiseSecurityEvent(WorkstationSecurityEventKind.SessionLoggedOff);

        // Assert
        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            session.CurrentSnapshot.State);

        Assert.Equal(firstLockedRevision, session.CurrentSnapshot.Revision);
        Assert.True(monitor.CurrentSnapshot.IsReady);
    }

    [Fact]
    public async Task MonitoringFailure_WhenUnlockIsInFlight_SupersedesUnlockBeforeSessionBecomesUnavailable()
    {
        // Arrange
        ApplicationSecuritySession session = CreateAuthenticatedSession();
        var eventSource = new TestWorkstationSecurityEventSource();
        var authenticationStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var authenticationService = new PendingAuthenticationService(
            session,
            authenticationStarted);

        var lifecycle = new ApplicationSecuritySessionLifecycleService(
            session,
            authenticationService);

        var monitor = new WorkstationSecurityMonitor(
            eventSource,
            session,
            () => lifecycle,
            NullLogger<WorkstationSecurityMonitor>.Instance);

        Assert.True(await monitor.TryStartAsync(
            TestContext.Current.CancellationToken));

        lifecycle.Lock();

        var sessionTransitions =
            new List<ApplicationSecuritySessionState>();

        session.StateChanged += snapshot =>
            sessionTransitions.Add(snapshot.State);

        Task<ApplicationAuthenticationServiceResult> unlockTask =
            lifecycle.UnlockAsync(TestContext.Current.CancellationToken);

        await authenticationStarted.Task.WaitAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            session.CurrentSnapshot.State);

        // Act
        eventSource.RaiseMonitoringFailure(
            new InvalidOperationException("Synthetic runtime monitoring failure."));

        ApplicationAuthenticationServiceResult unlockResult =
            await unlockTask.WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.Superseded,
            unlockResult);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            session.CurrentSnapshot.State);

        Assert.False(session.CurrentSnapshot.IsProtectedAccessAvailable);
        Assert.Equal(
            new[]
            {
                ApplicationSecuritySessionState.Authenticating,
                ApplicationSecuritySessionState.Authenticated,
                ApplicationSecuritySessionState.Locked,
                ApplicationSecuritySessionState.Unavailable
            },
            sessionTransitions);

        Assert.Equal(
            WorkstationSecurityReadinessState.Unavailable,
            monitor.CurrentSnapshot.State);
    }

    [Fact]
    public async Task TryRestoreSessionAvailability_WhenReadinessRevisionIsStale_DoesNotRestoreSession()
    {
        // Arrange
        var session = new ApplicationSecuritySession();
        var eventSource = new TestWorkstationSecurityEventSource();
        WorkstationSecurityMonitor monitor = CreateMonitor(
            eventSource,
            session);

        Assert.True(await monitor.TryStartAsync(
            TestContext.Current.CancellationToken));

        long firstReadyRevision = monitor.CurrentSnapshot.Revision;

        eventSource.RaiseMonitoringFailure(
            new InvalidOperationException("Synthetic runtime monitoring failure."));

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            session.CurrentSnapshot.State);

        Assert.True(await monitor.TryStartAsync(
            TestContext.Current.CancellationToken));

        // Act
        bool restoredWithStaleRevision =
            monitor.TryRestoreSessionAvailability(firstReadyRevision);

        // Assert
        Assert.False(restoredWithStaleRevision);
        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            session.CurrentSnapshot.State);
    }

    [Fact]
    public async Task MonitoringFailure_WhenMonitoringRecovers_RejectsCallbacksFromPreviousGenerationAndDetachesHandlers()
    {
        // Arrange
        ApplicationSecuritySession session = CreateAuthenticatedSession();
        var eventSource = new TestWorkstationSecurityEventSource();

        WorkstationSecurityMonitor monitor = CreateMonitor(
            eventSource,
            session);

        Assert.True(await monitor.TryStartAsync(
            TestContext.Current.CancellationToken));

        EventHandler<WorkstationSecurityEventArgs> staleHandler =
            eventSource.CaptureSecurityEventHandler();

        Assert.Equal(1, eventSource.SecurityEventSubscriberCount);
        Assert.Equal(1, eventSource.MonitoringFailureSubscriberCount);

        // Act: trigger a known runtime source failure.
        eventSource.RaiseMonitoringFailure(
            new InvalidOperationException(
                "Synthetic runtime monitoring failure."));

        // The failed generation must be detached before recovery.
        Assert.Equal(
            WorkstationSecurityReadinessState.Unavailable,
            monitor.CurrentSnapshot.State);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            session.CurrentSnapshot.State);

        Assert.Equal(0, eventSource.SecurityEventSubscriberCount);
        Assert.Equal(0, eventSource.MonitoringFailureSubscriberCount);

        // Recover into a new monitoring generation.
        bool recovered = await monitor.TryStartAsync(
            TestContext.Current.CancellationToken);

        Assert.True(recovered);

        Assert.Equal(
            WorkstationSecurityReadinessState.Ready,
            monitor.CurrentSnapshot.State);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            session.CurrentSnapshot.State);

        Assert.Equal(1, eventSource.SecurityEventSubscriberCount);
        Assert.Equal(1, eventSource.MonitoringFailureSubscriberCount);

        // Establish a fresh authenticated revision after recovery.
        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        Assert.True(
            session.TryCompleteAuthentication(
                attempt,
                ApplicationAuthenticationOutcome.Succeeded));

        long authenticatedRevision =
            session.CurrentSnapshot.Revision;

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            session.CurrentSnapshot.State);

        // Invoke the captured callback from the previous source generation.
        // It must not lock or otherwise mutate the recovered session.
        staleHandler(
            eventSource,
            new WorkstationSecurityEventArgs(
                WorkstationSecurityEventKind.SessionLocked));

        // Assert: the obsolete callback was fenced by its old generation.
        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            session.CurrentSnapshot.State);

        Assert.True(
            session.CurrentSnapshot.IsProtectedAccessAvailable);

        Assert.Equal(
            authenticatedRevision,
            session.CurrentSnapshot.Revision);

        // Stop must detach the current generation's subscriptions.
        await monitor.StopAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(
            WorkstationSecurityReadinessState.Stopped,
            monitor.CurrentSnapshot.State);

        Assert.Equal(0, eventSource.SecurityEventSubscriberCount);
        Assert.Equal(0, eventSource.MonitoringFailureSubscriberCount);
    }
    [Fact]
    public async Task StopAsync_WhenCalled_StopsSourceAndIgnoresCapturedStaleCallbacks()
    {
        // Arrange
        ApplicationSecuritySession session = CreateAuthenticatedSession();
        var eventSource = new TestWorkstationSecurityEventSource();
        WorkstationSecurityMonitor monitor = CreateMonitor(
            eventSource,
            session);

        Assert.True(await monitor.TryStartAsync(
            TestContext.Current.CancellationToken));

        EventHandler<WorkstationSecurityEventArgs> staleHandler =
            eventSource.CaptureSecurityEventHandler();

        long authenticatedRevision = session.CurrentSnapshot.Revision;

        // Act
        await monitor.StopAsync(TestContext.Current.CancellationToken);
        staleHandler(
            eventSource,
            new WorkstationSecurityEventArgs(
                WorkstationSecurityEventKind.SessionLocked));

        // Assert
        Assert.Equal(
            WorkstationSecurityReadinessState.Stopped,
            monitor.CurrentSnapshot.State);

        Assert.Equal(1, eventSource.StopCallCount);
        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            session.CurrentSnapshot.State);

        Assert.Equal(authenticatedRevision, session.CurrentSnapshot.Revision);
    }

    [Fact]
    public async Task StopAsync_WhenCalledRepeatedly_StopsEventSourceOnlyOnce()
    {
        // Arrange
        var session = new ApplicationSecuritySession();
        var eventSource = new TestWorkstationSecurityEventSource();
        WorkstationSecurityMonitor monitor = CreateMonitor(
            eventSource,
            session);

        Assert.True(await monitor.TryStartAsync(
            TestContext.Current.CancellationToken));

        // Act
        await monitor.StopAsync(TestContext.Current.CancellationToken);
        await monitor.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, eventSource.StopCallCount);
    }

    private static WorkstationSecurityMonitor CreateMonitor(
        IWorkstationSecurityEventSource eventSource,
        IApplicationSecuritySession session)
    {
        return new WorkstationSecurityMonitor(
            eventSource,
            session,
            () => CreateLifecycle(session),
            NullLogger<WorkstationSecurityMonitor>.Instance);
    }

    private static ApplicationSecuritySessionLifecycleService CreateLifecycle(
        IApplicationSecuritySession session)
    {
        return new ApplicationSecuritySessionLifecycleService(
            session,
            new RejectingAuthenticationService());
    }

    private static ApplicationSecuritySession CreateAuthenticatedSession()
    {
        var session = new ApplicationSecuritySession();
        ApplicationSecuritySessionAttempt attempt = session.BeginAuthentication();

        Assert.True(
            session.TryCompleteAuthentication(
                attempt,
                ApplicationAuthenticationOutcome.Succeeded));

        return session;
    }

    private sealed class RejectingAuthenticationService
        : IApplicationAuthenticationService
    {
        public Task<ApplicationAuthenticationServiceResult> AuthenticateAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                ApplicationAuthenticationServiceResult.NotPermitted);
        }
    }

    private sealed class PendingAuthenticationService
        : IApplicationAuthenticationService
    {
        private readonly IApplicationSecuritySession _session;
        private readonly TaskCompletionSource<bool> _authenticationStarted;

        public PendingAuthenticationService(
            IApplicationSecuritySession session,
            TaskCompletionSource<bool> authenticationStarted)
        {
            _session = session;
            _authenticationStarted = authenticationStarted;
        }

        public async Task<ApplicationAuthenticationServiceResult> AuthenticateAsync(
            CancellationToken cancellationToken = default)
        {
            ApplicationSecuritySessionAttempt attempt =
                _session.BeginAuthentication();

            _session.TryCompleteAuthentication(
                attempt,
                ApplicationAuthenticationOutcome.Succeeded);

            _authenticationStarted.TrySetResult(true);

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);

            return ApplicationAuthenticationServiceResult.Succeeded;
        }
    }

    private sealed class TestWorkstationSecurityEventSource
        : IWorkstationSecurityEventSource
    {
        private EventHandler<WorkstationSecurityEventArgs>? _securityEventReceived;

        private EventHandler<WorkstationSecurityEventSourceFailureEventArgs>? _monitoringFailed;

        public event EventHandler<WorkstationSecurityEventArgs>? SecurityEventReceived
        {
            add => _securityEventReceived += value;
            remove => _securityEventReceived -= value;
        }

        public event EventHandler<WorkstationSecurityEventSourceFailureEventArgs>? MonitoringFailed
        {
            add => _monitoringFailed += value;
            remove => _monitoringFailed -= value;
        }

        public int SecurityEventSubscriberCount =>
            _securityEventReceived?.GetInvocationList().Length ?? 0;

        public int MonitoringFailureSubscriberCount =>
            _monitoringFailed?.GetInvocationList().Length ?? 0;

        public int StartCallCount { get; private set; }

        public int StopCallCount { get; private set; }

        public Func<CancellationToken, Task>? StartOperation { get; set; }

        public Func<CancellationToken, Task>? StopOperation { get; set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCallCount++;
            cancellationToken.ThrowIfCancellationRequested();

            return StartOperation?.Invoke(cancellationToken) ?? Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCallCount++;
            cancellationToken.ThrowIfCancellationRequested();

            return StopOperation?.Invoke(cancellationToken) ?? Task.CompletedTask;
        }

        public void RaiseSecurityEvent(WorkstationSecurityEventKind kind)
        {
            _securityEventReceived?.Invoke(
                this,
                new WorkstationSecurityEventArgs(kind));
        }

        public void RaiseMonitoringFailure(Exception exception)
        {
            _monitoringFailed?.Invoke(
                this,
                new WorkstationSecurityEventSourceFailureEventArgs(exception));
        }

        public EventHandler<WorkstationSecurityEventArgs> CaptureSecurityEventHandler()
        {
            return _securityEventReceived
                ?? throw new InvalidOperationException(
                    "No workstation security event handler is attached.");
        }
    }
}
