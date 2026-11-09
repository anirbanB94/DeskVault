using DeskVault.Application.Configurations;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Authentication;
using DeskVault.Application.Security.AutomaticLock;
using DeskVault.Application.Security.Sessions;

namespace DeskVault.Application.Tests;

public sealed class ApplicationAutomaticLockServiceTests
{
    [Fact]
    public void EvaluateInactivity_WhenNewAuthenticatedRevisionAppearsBeforeLifecycleLock_DoesNotLockNewSession()
    {
        // Arrange
        TestContext context = CreateContext();

        context.Service.Start();

        ApplicationSecuritySessionSnapshot expiredSession =
            context.Session.Transition(
                ApplicationSecuritySessionState.Authenticated);

        context.TimeProvider.Advance(TimeSpan.FromMinutes(1));

        // Simulate a newer authenticated session winning immediately
        // after the automatic-lock revision check but before the
        // lifecycle lock operation is applied.
        context.Lifecycle.BeforeLock = () =>
            context.Session.Transition(
                ApplicationSecuritySessionState.Authenticated);

        // Act
        context.Service.EvaluateInactivity();

        // Assert
        ApplicationSecuritySessionSnapshot current =
            context.Session.CurrentSnapshot;

        Assert.NotEqual(
            expiredSession.Revision,
            current.Revision);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            current.State);

        Assert.True(current.IsProtectedAccessAvailable);

        Assert.Equal(1, context.Lifecycle.LockCallCount);
    }
    [Fact]
    public void EvaluateInactivity_WhenWallClockChanges_DoesNotChangeMonotonicDeadline()
    {
        // Arrange
        TestContext context = CreateContext();

        context.Service.Start();

        context.Session.Transition(
            ApplicationSecuritySessionState.Authenticated);

        // Move wall-clock time substantially forward without advancing
        // the monotonic timestamp.
        context.TimeProvider.SetUtcNow(
            new DateTimeOffset(2040, 1, 1, 0, 0, 0, TimeSpan.Zero));

        // Act
        context.Service.EvaluateInactivity();

        // Assert: a UTC jump alone cannot expire the session.
        Assert.Equal(0, context.Lifecycle.LockCallCount);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            context.Session.CurrentSnapshot.State);

        // Advance monotonic time to one second before the deadline,
        // then move wall-clock time substantially backward.
        context.TimeProvider.Advance(TimeSpan.FromSeconds(59));

        context.TimeProvider.SetUtcNow(
            new DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero));

        context.Service.EvaluateInactivity();

        Assert.Equal(0, context.Lifecycle.LockCallCount);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            context.Session.CurrentSnapshot.State);

        // The session must still expire at exactly 60 monotonic seconds.
        context.TimeProvider.Advance(TimeSpan.FromSeconds(1));

        context.Service.EvaluateInactivity();

        Assert.Equal(1, context.Lifecycle.LockCallCount);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            context.Session.CurrentSnapshot.State);
    }

    [Fact]
    public void EvaluateInactivity_BeforeDeadline_DoesNotLockSession()
    {
        // Arrange
        TestContext context = CreateContext();

        context.Service.Start();

        context.Session.Transition(
            ApplicationSecuritySessionState.Authenticated);

        context.TimeProvider.Advance(TimeSpan.FromSeconds(59));

        // Act
        context.Service.EvaluateInactivity();

        // Assert
        Assert.Equal(0, context.Lifecycle.LockCallCount);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            context.Session.CurrentSnapshot.State);
    }

    [Fact]
    public void EvaluateInactivity_AtDeadline_LocksSession()
    {
        // Arrange
        TestContext context = CreateContext();

        context.Service.Start();

        context.Session.Transition(
            ApplicationSecuritySessionState.Authenticated);

        context.TimeProvider.Advance(TimeSpan.FromMinutes(1));

        // Act
        context.Service.EvaluateInactivity();

        // Assert
        Assert.Equal(1, context.Lifecycle.LockCallCount);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            context.Session.CurrentSnapshot.State);
    }

    [Fact]
    public void TryReportQualifyingActivity_BeforeDeadline_ResetsBaseline()
    {
        // Arrange
        TestContext context = CreateContext();

        context.Service.Start();

        ApplicationSecuritySessionSnapshot authenticated =
            context.Session.Transition(
                ApplicationSecuritySessionState.Authenticated);

        context.TimeProvider.Advance(TimeSpan.FromSeconds(30));

        // Act
        bool accepted =
            context.Service.TryReportQualifyingActivity(
                authenticated.Revision);

        context.TimeProvider.Advance(TimeSpan.FromSeconds(59));

        context.Service.EvaluateInactivity();

        // Assert
        Assert.True(accepted);
        Assert.Equal(0, context.Lifecycle.LockCallCount);

        // The new baseline was established at the qualifying activity.
        context.TimeProvider.Advance(TimeSpan.FromSeconds(1));

        context.Service.EvaluateInactivity();

        Assert.Equal(1, context.Lifecycle.LockCallCount);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            context.Session.CurrentSnapshot.State);
    }

    [Fact]
    public void TryReportQualifyingActivity_AtDeadline_IsRejectedAndLocksSession()
    {
        // Arrange
        TestContext context = CreateContext();

        context.Service.Start();

        ApplicationSecuritySessionSnapshot authenticated =
            context.Session.Transition(
                ApplicationSecuritySessionState.Authenticated);

        context.TimeProvider.Advance(TimeSpan.FromMinutes(1));

        // Act
        bool accepted =
            context.Service.TryReportQualifyingActivity(
                authenticated.Revision);

        // Assert
        Assert.False(accepted);
        Assert.Equal(1, context.Lifecycle.LockCallCount);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            context.Session.CurrentSnapshot.State);
    }

    [Fact]
    public void TryReportQualifyingActivity_FromStaleSessionRevision_DoesNotResetNewBaseline()
    {
        // Arrange
        TestContext context = CreateContext();

        context.Service.Start();

        ApplicationSecuritySessionSnapshot originalSession =
            context.Session.Transition(
                ApplicationSecuritySessionState.Authenticated);

        context.TimeProvider.Advance(TimeSpan.FromSeconds(30));

        context.Session.Transition(
            ApplicationSecuritySessionState.Locked);

        context.Session.Transition(
            ApplicationSecuritySessionState.Authenticated);

        context.TimeProvider.Advance(TimeSpan.FromSeconds(30));

        // Act
        bool accepted =
            context.Service.TryReportQualifyingActivity(
                originalSession.Revision);

        context.TimeProvider.Advance(TimeSpan.FromSeconds(30));

        context.Service.EvaluateInactivity();

        // Assert
        Assert.False(accepted);
        Assert.Equal(1, context.Lifecycle.LockCallCount);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            context.Session.CurrentSnapshot.State);
    }

    [Fact]
    public void EvaluateInactivity_RepeatedlyAfterExpiry_InvokesLifecycleLockOnlyOnce()
    {
        // Arrange
        TestContext context = CreateContext();

        context.Service.Start();

        context.Session.Transition(
            ApplicationSecuritySessionState.Authenticated);

        context.TimeProvider.Advance(TimeSpan.FromMinutes(1));

        // Act
        context.Service.EvaluateInactivity();
        context.Service.EvaluateInactivity();
        context.Service.EvaluateInactivity();

        // Assert
        Assert.Equal(1, context.Lifecycle.LockCallCount);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            context.Session.CurrentSnapshot.State);
    }

    [Fact]
    public void EvaluateInactivity_WhenPolicyIsDisabled_DoesNotLockSession()
    {
        // Arrange
        TestContext context = CreateContext(
            enabled: false,
            timeoutMinutes: null);

        context.Service.Start();

        ApplicationSecuritySessionSnapshot authenticated =
            context.Session.Transition(
                ApplicationSecuritySessionState.Authenticated);

        context.TimeProvider.Advance(TimeSpan.FromHours(2));

        // Act
        context.Service.EvaluateInactivity();

        bool activityAccepted =
            context.Service.TryReportQualifyingActivity(
                authenticated.Revision);

        // Assert
        Assert.False(activityAccepted);
        Assert.Equal(0, context.Lifecycle.LockCallCount);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            context.Session.CurrentSnapshot.State);
    }

    [Fact]
    public void Stop_AfterMonitoringStarted_RejectsFurtherActivityAndEvaluation()
    {
        // Arrange
        TestContext context = CreateContext();

        context.Service.Start();

        ApplicationSecuritySessionSnapshot authenticated =
            context.Session.Transition(
                ApplicationSecuritySessionState.Authenticated);

        context.Service.Stop();

        context.TimeProvider.Advance(TimeSpan.FromMinutes(2));

        // Act
        bool activityAccepted =
            context.Service.TryReportQualifyingActivity(
                authenticated.Revision);

        context.Service.EvaluateInactivity();

        // Assert
        Assert.False(activityAccepted);
        Assert.Equal(0, context.Lifecycle.LockCallCount);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            context.Session.CurrentSnapshot.State);
    }

    [Fact]
    public void Dispose_AfterMonitoringStarted_RejectsActivityAndEvaluation()
    {
        // Arrange
        TestContext context = CreateContext();

        context.Service.Start();

        ApplicationSecuritySessionSnapshot authenticated =
            context.Session.Transition(
                ApplicationSecuritySessionState.Authenticated);

        // Act
        context.Service.Dispose();

        // Disposal is idempotent.
        context.Service.Dispose();

        context.TimeProvider.Advance(TimeSpan.FromMinutes(2));

        bool activityAccepted =
            context.Service.TryReportQualifyingActivity(
                authenticated.Revision);

        context.Service.EvaluateInactivity();

        // Assert
        Assert.False(activityAccepted);
        Assert.Equal(0, context.Lifecycle.LockCallCount);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            context.Session.CurrentSnapshot.State);

        Assert.Throws<ObjectDisposedException>(
            () => context.Service.Start());
    }
    private static TestContext CreateContext(
        bool enabled = true,
        int? timeoutMinutes = 1)
    {
        var session = new TestSecuritySession();
        var lifecycle = new TestSecuritySessionLifecycleService(session);
        var timeProvider = new ManualTimeProvider();

        var settings = new AutomaticLockSettings(
            enabled,
            timeoutMinutes);

        var service = new ApplicationAutomaticLockService(
            session,
            lifecycle,
            settings,
            timeProvider);

        return new TestContext(
            service,
            session,
            lifecycle,
            timeProvider);
    }

    private sealed record TestContext(
        ApplicationAutomaticLockService Service,
        TestSecuritySession Session,
        TestSecuritySessionLifecycleService Lifecycle,
        ManualTimeProvider TimeProvider);

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        private DateTimeOffset _utcNow =
            new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override long TimestampFrequency =>
            TimeSpan.TicksPerSecond;

        public override long GetTimestamp() =>
            Interlocked.Read(ref _timestamp);

        public override DateTimeOffset GetUtcNow() =>
            _utcNow;

        public void SetUtcNow(DateTimeOffset value)
        {
            _utcNow = value;
        }

        public void Advance(TimeSpan elapsed)
        {
            if (elapsed < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsed));
            }

            Interlocked.Add(
                ref _timestamp,
                elapsed.Ticks);
        }
    }

    private sealed class TestSecuritySession : IApplicationSecuritySession
    {
        public ApplicationSecuritySessionSnapshot CurrentSnapshot { get; private set; } =
            new(
                ApplicationSecuritySessionState.Unauthenticated,
                revision: 0);

        public event Action<ApplicationSecuritySessionSnapshot>? StateChanged;

        public ApplicationSecuritySessionAttempt BeginAuthentication() =>
            throw new NotSupportedException(
                "Authentication is not part of this test fixture.");

        public bool TryCompleteAuthentication(
            ApplicationSecuritySessionAttempt attempt,
            ApplicationAuthenticationOutcome outcome) =>
            throw new NotSupportedException(
                "Authentication is not part of this test fixture.");

        public bool TryLock(
            long expectedRevision,
            out ApplicationSecuritySessionSnapshot snapshot)
        {
            if (CurrentSnapshot.Revision != expectedRevision ||
                !CurrentSnapshot.IsProtectedAccessAvailable)
            {
                snapshot = CurrentSnapshot;
                return false;
            }

            snapshot =
                Transition(ApplicationSecuritySessionState.Locked);

            return true;
        }

        public ApplicationSecuritySessionSnapshot Lock() =>
            Transition(ApplicationSecuritySessionState.Locked);

        public ApplicationSecuritySessionSnapshot MarkUnavailable() =>
            Transition(ApplicationSecuritySessionState.Unavailable);

        public ApplicationSecuritySessionSnapshot RestoreAvailability() =>
            Transition(ApplicationSecuritySessionState.Locked);

        public ApplicationSecuritySessionSnapshot Transition(
            ApplicationSecuritySessionState state)
        {
            CurrentSnapshot = new ApplicationSecuritySessionSnapshot(
                state,
                checked(CurrentSnapshot.Revision + 1));

            StateChanged?.Invoke(CurrentSnapshot);

            return CurrentSnapshot;
        }
    }

    private sealed class TestSecuritySessionLifecycleService :
        IApplicationSecuritySessionLifecycleService
    {
        private readonly TestSecuritySession _session;

        public TestSecuritySessionLifecycleService(
            TestSecuritySession session)
        {
            _session = session;
        }

        public int LockCallCount { get; private set; }

        public Action? BeforeLock { get; set; }

        public bool TryLock(
            long expectedRevision,
            out ApplicationSecuritySessionSnapshot snapshot)
        {
            LockCallCount++;

            BeforeLock?.Invoke();

            return _session.TryLock(
                expectedRevision,
                out snapshot);
        }

        public ApplicationSecuritySessionSnapshot Lock()
        {
            LockCallCount++;

            BeforeLock?.Invoke();

            return _session.Lock();
        }

        public Task<ApplicationAuthenticationServiceResult> UnlockAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "Unlock is not part of this test fixture.");
    }
}
