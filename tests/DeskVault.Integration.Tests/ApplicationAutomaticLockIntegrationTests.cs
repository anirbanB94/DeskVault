using DeskVault.Application;
using DeskVault.Application.Configurations;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Authentication;
using DeskVault.Application.Security.Sessions;
using DeskVault.Application.Security.Workstation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskVault.Integration.Tests;

public sealed class ApplicationAutomaticLockIntegrationTests
{
    [Fact]
    public async Task AutomaticLock_WhenNewSessionAuthenticatesBeforeDelayedExpiry_DoesNotLockNewSession()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var timeProvider = new ManualTimeProvider();

        var services = new ServiceCollection();

        services.AddSingleton(
            new AutomaticLockSettings(
                enabled: true,
                inactivityTimeoutMinutes: 1));

        services.AddSingleton<TimeProvider>(timeProvider);

        services.AddSingleton<ILogger<ApplicationAuthenticationService>>(
            NullLogger<ApplicationAuthenticationService>.Instance);

        services.AddSingleton<IApplicationAuthenticationProvider>(
            new SuccessfulAuthenticationProvider());

        services.AddApplication();

        // Keep workstation readiness deterministic in this focused
        // automatic-lock integration test. The production monitor is
        // exercised separately by workstation security integration coverage.
        services.RemoveAll<IWorkstationSecurityReadiness>();

        services.AddSingleton<IWorkstationSecurityReadiness>(
            serviceProvider =>
                CreateReadyWorkstationSecurityReadiness(
                    serviceProvider.GetRequiredService<IApplicationSecuritySession>()));

        // Replace only the lifecycle interface with an interposing
        // decorator. Its inner lifecycle service is the real production
        // implementation and still owns every lock/unlock operation.
        services.RemoveAll<IApplicationSecuritySessionLifecycleService>();

        services.AddSingleton<InterposingLifecycleService>(
            serviceProvider =>
            {
                IApplicationSecuritySession session =
                    serviceProvider.GetRequiredService<IApplicationSecuritySession>();

                IApplicationAuthenticationService authenticationService =
                    serviceProvider.GetRequiredService<IApplicationAuthenticationService>();

                var actualLifecycle =
                    new ApplicationSecuritySessionLifecycleService(
                        session,
                        authenticationService);

                return new InterposingLifecycleService(actualLifecycle);
            });

        services.AddSingleton<IApplicationSecuritySessionLifecycleService>(
            serviceProvider =>
                serviceProvider.GetRequiredService<InterposingLifecycleService>());

        using ServiceProvider serviceProvider =
            services.BuildServiceProvider();

        IApplicationSecuritySession session =
            serviceProvider.GetRequiredService<IApplicationSecuritySession>();

        IApplicationAuthenticationService authenticationService =
            serviceProvider.GetRequiredService<IApplicationAuthenticationService>();

        IApplicationSecuritySessionLifecycleService lifecycleService =
            serviceProvider.GetRequiredService<IApplicationSecuritySessionLifecycleService>();

        InterposingLifecycleService interposingLifecycle =
            serviceProvider.GetRequiredService<InterposingLifecycleService>();

        IApplicationAutomaticLockService automaticLockService =
            serviceProvider.GetRequiredService<IApplicationAutomaticLockService>();

        automaticLockService.Start();

        try
        {
            // Establish the original authenticated revision.
            ApplicationAuthenticationServiceResult initialResult =
                await authenticationService.AuthenticateAsync(cancellationToken);

            Assert.Equal(
                ApplicationAuthenticationServiceResult.Succeeded,
                initialResult);

            ApplicationSecuritySessionSnapshot originalAuthenticated =
                session.CurrentSnapshot;

            Assert.Equal(
                ApplicationSecuritySessionState.Authenticated,
                originalAuthenticated.State);

            timeProvider.Advance(TimeSpan.FromMinutes(1));

            // Simulate another legitimate lifecycle lock followed by
            // successful re-authentication after expiry detection but
            // immediately before the old conditional lock is applied.
            interposingLifecycle.BeforeNextTryLock = () =>
            {
                _ = lifecycleService.Lock();

                ApplicationAuthenticationServiceResult reauthenticationResult =
                    lifecycleService.UnlockAsync(cancellationToken)
                        .GetAwaiter()
                        .GetResult();

                Assert.Equal(
                    ApplicationAuthenticationServiceResult.Succeeded,
                    reauthenticationResult);
            };

            // Act: the old expiry reaches the real revision-aware lifecycle
            // service only after the newer session has been authenticated.
            automaticLockService.EvaluateInactivity();

            ApplicationSecuritySessionSnapshot newerAuthenticated =
                session.CurrentSnapshot;

            // Assert: the old expiry did not lock the newer authenticated
            // revision, and its fresh inactivity baseline remains effective.
            Assert.True(
                newerAuthenticated.Revision > originalAuthenticated.Revision);

            Assert.Equal(
                ApplicationSecuritySessionState.Authenticated,
                newerAuthenticated.State);

            Assert.True(
                newerAuthenticated.IsProtectedAccessAvailable);

            timeProvider.Advance(TimeSpan.FromSeconds(59));

            automaticLockService.EvaluateInactivity();

            Assert.Equal(
                ApplicationSecuritySessionState.Authenticated,
                session.CurrentSnapshot.State);

            timeProvider.Advance(TimeSpan.FromSeconds(1));

            automaticLockService.EvaluateInactivity();

            // The new session is independently locked at its own deadline.
            Assert.Equal(
                ApplicationSecuritySessionState.Locked,
                session.CurrentSnapshot.State);

            Assert.False(
                session.CurrentSnapshot.IsProtectedAccessAvailable);

            Assert.Equal(
                2,
                interposingLifecycle.ConditionalLockCallCount);
        }
        finally
        {
            automaticLockService.Stop();
        }
    }

    [Fact]
    public async Task AutomaticLock_ExpiresSessionAndUnlockEstablishesFreshInactivityBaseline()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var timeProvider = new ManualTimeProvider();

        var services = new ServiceCollection();

        services.AddSingleton(
            new AutomaticLockSettings(
                enabled: true,
                inactivityTimeoutMinutes: 1));

        services.AddSingleton<TimeProvider>(timeProvider);

        services.AddSingleton<ILogger<ApplicationAuthenticationService>>(
            NullLogger<ApplicationAuthenticationService>.Instance);

        services.AddSingleton<IApplicationAuthenticationProvider>(
            new SuccessfulAuthenticationProvider());

        services.AddApplication();

        // Keep workstation readiness deterministic in this focused
        // automatic-lock integration test. The production monitor is
        // exercised separately by workstation security integration coverage.
        services.RemoveAll<IWorkstationSecurityReadiness>();

        services.AddSingleton<IWorkstationSecurityReadiness>(
            serviceProvider =>
                CreateReadyWorkstationSecurityReadiness(
                    serviceProvider.GetRequiredService<IApplicationSecuritySession>()));

        using ServiceProvider serviceProvider =
            services.BuildServiceProvider();

        IApplicationSecuritySession session =
            serviceProvider.GetRequiredService<IApplicationSecuritySession>();

        IApplicationAuthenticationService authenticationService =
            serviceProvider.GetRequiredService<IApplicationAuthenticationService>();

        IApplicationSecuritySessionLifecycleService lifecycleService =
            serviceProvider.GetRequiredService<IApplicationSecuritySessionLifecycleService>();

        IApplicationAutomaticLockService automaticLockService =
            serviceProvider.GetRequiredService<IApplicationAutomaticLockService>();

        IApplicationUserActivityService userActivityService =
            serviceProvider.GetRequiredService<IApplicationUserActivityService>();

        // Both public contracts must use the same application singleton.
        Assert.Same(
            automaticLockService,
            userActivityService);

        // Subscribe before authentication so the authenticated transition
        // establishes the initial inactivity baseline.
        automaticLockService.Start();

        // Act: establish the first authenticated session.
        ApplicationAuthenticationServiceResult initialAuthenticationResult =
            await authenticationService.AuthenticateAsync(cancellationToken);

        Assert.Equal(
            ApplicationAuthenticationServiceResult.Succeeded,
            initialAuthenticationResult);

        ApplicationSecuritySessionSnapshot firstAuthenticatedSnapshot =
            session.CurrentSnapshot;

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            firstAuthenticatedSnapshot.State);

        // A qualifying activity at 45 seconds resets the baseline.
        timeProvider.Advance(TimeSpan.FromSeconds(45));

        bool activityAccepted =
            userActivityService.TryReportQualifyingActivity(
                firstAuthenticatedSnapshot.Revision);

        Assert.True(activityAccepted);

        // At 59 seconds after the new baseline, the session remains
        // authenticated.
        timeProvider.Advance(TimeSpan.FromSeconds(59));

        automaticLockService.EvaluateInactivity();

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            session.CurrentSnapshot.State);

        // At exactly 60 seconds after the new baseline, automatic locking
        // must delegate to the authoritative lifecycle service.
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        automaticLockService.EvaluateInactivity();

        ApplicationSecuritySessionSnapshot lockedSnapshot =
            session.CurrentSnapshot;

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            lockedSnapshot.State);

        Assert.False(
            lockedSnapshot.IsProtectedAccessAvailable);

        Assert.True(
            lockedSnapshot.Revision > firstAuthenticatedSnapshot.Revision);

        // Re-authenticate through the production lifecycle service.
        ApplicationAuthenticationServiceResult unlockResult =
            await lifecycleService.UnlockAsync(cancellationToken);

        Assert.Equal(
            ApplicationAuthenticationServiceResult.Succeeded,
            unlockResult);

        ApplicationSecuritySessionSnapshot secondAuthenticatedSnapshot =
            session.CurrentSnapshot;

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            secondAuthenticatedSnapshot.State);

        Assert.True(
            secondAuthenticatedSnapshot.IsProtectedAccessAvailable);

        Assert.True(
            secondAuthenticatedSnapshot.Revision > lockedSnapshot.Revision);

        // Activity associated with the first session must not refresh
        // the newly authenticated session.
        bool staleActivityAccepted =
            userActivityService.TryReportQualifyingActivity(
                firstAuthenticatedSnapshot.Revision);

        Assert.False(staleActivityAccepted);

        // The fresh authentication transition establishes a new baseline.
        timeProvider.Advance(TimeSpan.FromSeconds(59));

        automaticLockService.EvaluateInactivity();

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            session.CurrentSnapshot.State);

        timeProvider.Advance(TimeSpan.FromSeconds(1));

        automaticLockService.EvaluateInactivity();

        // Assert: the second authenticated session is independently
        // subject to the configured inactivity policy.
        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            session.CurrentSnapshot.State);

        Assert.False(
            session.CurrentSnapshot.IsProtectedAccessAvailable);

        automaticLockService.Stop();
    }

    private static IWorkstationSecurityReadiness CreateReadyWorkstationSecurityReadiness(
        IApplicationSecuritySession session)
    {
        return new ReadyWorkstationSecurityReadiness(session);
    }

    private sealed class ReadyWorkstationSecurityReadiness
        : IWorkstationSecurityReadiness
    {
        private const long ReadyRevision = 1;

        private readonly IApplicationSecuritySession _session;

        public ReadyWorkstationSecurityReadiness(
            IApplicationSecuritySession session)
        {
            ArgumentNullException.ThrowIfNull(session);

            _session = session;
        }

        public WorkstationSecurityReadinessSnapshot CurrentSnapshot { get; } =
            new(
                WorkstationSecurityReadinessState.Ready,
                revision: ReadyRevision);

        // This fake represents permanently ready monitoring. It never changes
        // readiness, so subscribers are not registered or notified.
        public event Action<WorkstationSecurityReadinessSnapshot>? ReadinessChanged
        {
            add { }
            remove { }
        }

        public bool TryRestoreSessionAvailability(
            long expectedReadinessRevision)
        {
            if (!CurrentSnapshot.IsReady ||
                expectedReadinessRevision != CurrentSnapshot.Revision)
            {
                return false;
            }

            if (_session.CurrentSnapshot.State ==
                ApplicationSecuritySessionState.Unavailable)
            {
                _session.RestoreAvailability();
            }

            return true;
        }
    }

    private sealed class InterposingLifecycleService :
        IApplicationSecuritySessionLifecycleService
    {
        private readonly IApplicationSecuritySessionLifecycleService _inner;

        private Action? _beforeNextTryLock;

        private int _conditionalLockCallCount;

        public InterposingLifecycleService(
            IApplicationSecuritySessionLifecycleService inner)
        {
            ArgumentNullException.ThrowIfNull(inner);

            _inner = inner;
        }

        public int ConditionalLockCallCount =>
            Volatile.Read(ref _conditionalLockCallCount);

        public Action? BeforeNextTryLock
        {
            set => Interlocked.Exchange(
                ref _beforeNextTryLock,
                value);
        }

        public bool TryLock(
            long expectedRevision,
            out ApplicationSecuritySessionSnapshot snapshot)
        {
            Interlocked.Increment(ref _conditionalLockCallCount);

            Action? callback =
                Interlocked.Exchange(
                    ref _beforeNextTryLock,
                    null);

            callback?.Invoke();

            return _inner.TryLock(
                expectedRevision,
                out snapshot);
        }

        public ApplicationSecuritySessionSnapshot Lock() =>
            _inner.Lock();

        public Task<ApplicationAuthenticationServiceResult> UnlockAsync(
            CancellationToken cancellationToken = default) =>
            _inner.UnlockAsync(cancellationToken);
    }

    private sealed class SuccessfulAuthenticationProvider :
        IApplicationAuthenticationProvider
    {
        public Task<ApplicationAuthenticationAvailability> CheckAvailabilityAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                ApplicationAuthenticationAvailability.Available);
        }

        public Task<ApplicationAuthenticationProviderResult> RequestAuthenticationAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                ApplicationAuthenticationProviderResult.Succeeded);
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency =>
            TimeSpan.TicksPerSecond;

        public override long GetTimestamp() =>
            Interlocked.Read(ref _timestamp);

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
}
