using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Workstation;
using DeskVault.Shared.Resources;
using DeskVault.UI.Forms;
using DeskVault.UI.Hosting;
using DeskVault.UI.Services.Dialogs;
using DeskVault.UI.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace DeskVault.UI.Tests;

public sealed class DeskVaultStartupCompositionTests
{
    [Fact]
    public void Build_RegistersStartupCoordinatorAsSingletonAndFormAsTransient()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        RunOnStaThread(
            () =>
            {
                // Arrange
                using IHost host = HostConfigurator.Build();

                IHostedService[] hostedServices =
                    host.Services.GetServices<IHostedService>().ToArray();

                // Act
                DeskVaultStartupCoordinator firstCoordinator =
                    host.Services.GetRequiredService<DeskVaultStartupCoordinator>();

                DeskVaultStartupCoordinator secondCoordinator =
                    host.Services.GetRequiredService<DeskVaultStartupCoordinator>();

                IStartupAuthenticationDialogService firstReauthenticationDialogService =
                    host.Services.GetRequiredService<IStartupAuthenticationDialogService>();

                IStartupAuthenticationDialogService secondReauthenticationDialogService =
                    host.Services.GetRequiredService<IStartupAuthenticationDialogService>();

                Assert.Same(
                    firstReauthenticationDialogService,
                    secondReauthenticationDialogService);

                Assert.IsType<StartupAuthenticationDialogService>(
                    firstReauthenticationDialogService);

                using var firstForm =
                    host.Services.GetRequiredService<StartupAuthenticationForm>();

                using var secondForm =
                    host.Services.GetRequiredService<StartupAuthenticationForm>();

                IWorkstationSecurityMonitor workstationMonitor =
                    host.Services.GetRequiredService<IWorkstationSecurityMonitor>();

                IWorkstationSecurityReadiness workstationReadiness =
                    host.Services.GetRequiredService<IWorkstationSecurityReadiness>();

                // Assert
                Assert.Single(
                    hostedServices.OfType<WorkstationSecurityMonitorHostedService>());

                Assert.Single(
                    hostedServices.OfType<ApplicationAutomaticLockHostedService>());

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

                Assert.True(workstationMonitorIndex >= 0);
                Assert.True(automaticLockIndex >= 0);
                Assert.True(workstationMonitorIndex < automaticLockIndex);

                Assert.Same(
                    workstationMonitor,
                    workstationReadiness);

                Assert.Same(
                    TimeProvider.System,
                    host.Services.GetRequiredService<TimeProvider>());

                Assert.Same(firstCoordinator, secondCoordinator);

                Assert.IsType<StartupAuthenticationForm>(firstForm);
                Assert.IsType<StartupAuthenticationForm>(secondForm);
                Assert.NotSame(firstForm, secondForm);

                // Resolving the startup coordinator and form must not initialize
                // the database before an authenticated session exists.
                Assert.Throws<InvalidOperationException>(
                    () =>
                        firstCoordinator
                            .InitializeDatabaseAsync(cancellationToken)
                            .GetAwaiter()
                            .GetResult());
            });
    }

    private static void RunOnStaThread(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Exception? failure = null;

        var thread = new Thread(
            () =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo
                .Capture(failure)
                .Throw();
        }
    }
}

public sealed class WorkstationSecurityMonitorHostedServiceTests
{
    private static readonly TimeSpan TestRetryInterval =
        TimeSpan.FromMilliseconds(10);

    [Fact]
    public async Task StartAsync_WhenMonitoringStartsSuccessfully_EstablishesReadinessBeforeReturning()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var snapshot = new WorkstationSecurityReadinessSnapshot(
            WorkstationSecurityReadinessState.NotStarted,
            revision: 0);

        var monitor =
            new Mock<IWorkstationSecurityMonitor>(MockBehavior.Strict);

        monitor
            .SetupGet(service => service.CurrentSnapshot)
            .Returns(() => snapshot);

        monitor
            .Setup(service => service.TryStartAsync(
                It.IsAny<CancellationToken>()))
            .Returns((CancellationToken _) =>
            {
                snapshot = new WorkstationSecurityReadinessSnapshot(
                    WorkstationSecurityReadinessState.Ready,
                    revision: 1);

                return Task.FromResult(true);
            });

        monitor
            .Setup(service => service.StopAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var hostedService = CreateHostedService(monitor.Object);

        // Act
        await hostedService.StartAsync(cancellationToken);

        // Assert
        Assert.Equal(
            WorkstationSecurityReadinessState.Ready,
            monitor.Object.CurrentSnapshot.State);

        monitor.Verify(
            service => service.TryStartAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);

        await hostedService.StopAsync(cancellationToken);

        monitor.Verify(
            service => service.StopAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_WhenInitialMonitoringAttemptFails_RetriesUntilReadinessIsRestored()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var snapshot = new WorkstationSecurityReadinessSnapshot(
            WorkstationSecurityReadinessState.NotStarted,
            revision: 0);

        var retryCompleted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        int startAttemptCount = 0;

        var monitor =
            new Mock<IWorkstationSecurityMonitor>(MockBehavior.Strict);

        monitor
            .SetupGet(service => service.CurrentSnapshot)
            .Returns(() => snapshot);

        monitor
            .Setup(service => service.TryStartAsync(
                It.IsAny<CancellationToken>()))
            .Returns((CancellationToken _) =>
            {
                int attempt =
                    Interlocked.Increment(ref startAttemptCount);

                if (attempt == 1)
                {
                    snapshot = new WorkstationSecurityReadinessSnapshot(
                        WorkstationSecurityReadinessState.Unavailable,
                        revision: 1);

                    return Task.FromResult(false);
                }

                snapshot = new WorkstationSecurityReadinessSnapshot(
                    WorkstationSecurityReadinessState.Ready,
                    revision: 2);

                retryCompleted.TrySetResult(true);

                return Task.FromResult(true);
            });

        monitor
            .Setup(service => service.StopAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var hostedService = CreateHostedService(monitor.Object);

        // Act
        await hostedService.StartAsync(cancellationToken);

        // Initial failure must not make the monitor ready.
        Assert.Equal(
            WorkstationSecurityReadinessState.Unavailable,
            monitor.Object.CurrentSnapshot.State);

        await retryCompleted.Task.WaitAsync(
            TimeSpan.FromSeconds(3),
            cancellationToken);

        // Assert
        Assert.Equal(2, Volatile.Read(ref startAttemptCount));

        Assert.Equal(
            WorkstationSecurityReadinessState.Ready,
            monitor.Object.CurrentSnapshot.State);

        await hostedService.StopAsync(cancellationToken);

        monitor.Verify(
            service => service.TryStartAsync(
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        monitor.Verify(
            service => service.StopAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMonitoringBecomesUnavailable_RetriesMonitoring()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var snapshot = new WorkstationSecurityReadinessSnapshot(
            WorkstationSecurityReadinessState.NotStarted,
            revision: 0);

        var recoveryCompleted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        int startAttemptCount = 0;

        var monitor =
            new Mock<IWorkstationSecurityMonitor>(MockBehavior.Strict);

        monitor
            .SetupGet(service => service.CurrentSnapshot)
            .Returns(() => snapshot);

        monitor
            .Setup(service => service.TryStartAsync(
                It.IsAny<CancellationToken>()))
            .Returns((CancellationToken _) =>
            {
                int attempt =
                    Interlocked.Increment(ref startAttemptCount);

                snapshot = new WorkstationSecurityReadinessSnapshot(
                    WorkstationSecurityReadinessState.Ready,
                    revision: attempt);

                if (attempt == 2)
                {
                    recoveryCompleted.TrySetResult(true);
                }

                return Task.FromResult(true);
            });

        monitor
            .Setup(service => service.StopAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var hostedService = CreateHostedService(monitor.Object);

        await hostedService.StartAsync(cancellationToken);

        Assert.Equal(
            WorkstationSecurityReadinessState.Ready,
            monitor.Object.CurrentSnapshot.State);

        // Simulate a runtime monitoring failure.
        snapshot = new WorkstationSecurityReadinessSnapshot(
            WorkstationSecurityReadinessState.Unavailable,
            revision: 2);

        // Act
        await recoveryCompleted.Task.WaitAsync(
            TimeSpan.FromSeconds(3),
            cancellationToken);

        // Assert
        Assert.Equal(2, Volatile.Read(ref startAttemptCount));

        Assert.Equal(
            WorkstationSecurityReadinessState.Ready,
            monitor.Object.CurrentSnapshot.State);

        await hostedService.StopAsync(cancellationToken);

        monitor.Verify(
            service => service.TryStartAsync(
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        monitor.Verify(
            service => service.StopAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_WhenInitializationIsCancelled_CleansUpAndPropagatesCancellation()
    {
        // Arrange
        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        var snapshot = new WorkstationSecurityReadinessSnapshot(
            WorkstationSecurityReadinessState.NotStarted,
            revision: 0);

        var monitor =
            new Mock<IWorkstationSecurityMonitor>(MockBehavior.Strict);

        monitor
            .SetupGet(service => service.CurrentSnapshot)
            .Returns(() => snapshot);

        monitor
            .Setup(service => service.TryStartAsync(
                It.IsAny<CancellationToken>()))
            .Returns((CancellationToken token) =>
                Task.FromCanceled<bool>(token));

        monitor
            .Setup(service => service.StopAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var hostedService = CreateHostedService(monitor.Object);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => hostedService.StartAsync(cancellationSource.Token));

        // Assert
        monitor.Verify(
            service => service.TryStartAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);

        monitor.Verify(
            service => service.StopAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StopAsync_WhenCalledRepeatedly_StopsMonitoringOnlyOnce()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var snapshot = new WorkstationSecurityReadinessSnapshot(
            WorkstationSecurityReadinessState.NotStarted,
            revision: 0);

        var monitor =
            new Mock<IWorkstationSecurityMonitor>(MockBehavior.Strict);

        monitor
            .SetupGet(service => service.CurrentSnapshot)
            .Returns(() => snapshot);

        monitor
            .Setup(service => service.TryStartAsync(
                It.IsAny<CancellationToken>()))
            .Returns((CancellationToken _) =>
            {
                snapshot = new WorkstationSecurityReadinessSnapshot(
                    WorkstationSecurityReadinessState.Ready,
                    revision: 1);

                return Task.FromResult(true);
            });

        monitor
            .Setup(service => service.StopAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var hostedService = CreateHostedService(monitor.Object);

        await hostedService.StartAsync(cancellationToken);

        // Act
        await hostedService.StopAsync(cancellationToken);
        await hostedService.StopAsync(cancellationToken);

        // Assert
        monitor.Verify(
            service => service.TryStartAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);

        monitor.Verify(
            service => service.StopAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StopAsync_WhenMonitoringCleanupFails_RetriesCleanupOnNextCall()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var snapshot = new WorkstationSecurityReadinessSnapshot(
            WorkstationSecurityReadinessState.NotStarted,
            revision: 0);

        int stopAttemptCount = 0;

        var monitor =
            new Mock<IWorkstationSecurityMonitor>(MockBehavior.Strict);

        monitor
            .SetupGet(service => service.CurrentSnapshot)
            .Returns(() => snapshot);

        monitor
            .Setup(service => service.TryStartAsync(
                It.IsAny<CancellationToken>()))
            .Returns((CancellationToken _) =>
            {
                snapshot = new WorkstationSecurityReadinessSnapshot(
                    WorkstationSecurityReadinessState.Ready,
                    revision: 1);

                return Task.FromResult(true);
            });

        monitor
            .Setup(service => service.StopAsync(
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                int attempt =
                    Interlocked.Increment(ref stopAttemptCount);

                return attempt == 1
                    ? Task.FromException(
                        new InvalidOperationException(
                            "Simulated monitoring cleanup failure."))
                    : Task.CompletedTask;
            });

        using var hostedService = CreateHostedService(monitor.Object);

        await hostedService.StartAsync(cancellationToken);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => hostedService.StopAsync(cancellationToken));

        await hostedService.StopAsync(cancellationToken);

        // Assert
        Assert.Equal(2, Volatile.Read(ref stopAttemptCount));

        monitor.Verify(
            service => service.StopAsync(
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task StartAsync_WhenInitializationThrows_LogsSafeFailureAndContinuesRetrying()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        const string sensitiveFailureSentinel =
            "PBI191_WORKSTATION_MONITOR_EXCEPTION_PAYLOAD_SENTINEL";

        var snapshot = new WorkstationSecurityReadinessSnapshot(
            WorkstationSecurityReadinessState.NotStarted,
            revision: 0);

        var retryCompleted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        int startAttemptCount = 0;

        var monitor =
            new Mock<IWorkstationSecurityMonitor>(MockBehavior.Strict);

        monitor
            .SetupGet(service => service.CurrentSnapshot)
            .Returns(() => snapshot);

        monitor
            .Setup(service => service.TryStartAsync(
                It.IsAny<CancellationToken>()))
            .Returns((CancellationToken _) =>
            {
                int attempt =
                    Interlocked.Increment(ref startAttemptCount);

                if (attempt == 1)
                {
                    return Task.FromException<bool>(
                        new InvalidOperationException(
                            sensitiveFailureSentinel));
                }

                snapshot = new WorkstationSecurityReadinessSnapshot(
                    WorkstationSecurityReadinessState.Ready,
                    revision: 2);

                retryCompleted.TrySetResult(true);

                return Task.FromResult(true);
            });

        monitor
            .Setup(service => service.StopAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var logger =
            new RecordingLogger<WorkstationSecurityMonitorHostedService>();

        using var hostedService =
            new WorkstationSecurityMonitorHostedService(
                monitor.Object,
                TimeProvider.System,
                logger,
                TestRetryInterval);

        // Act
        await hostedService.StartAsync(cancellationToken);

        try
        {
            await retryCompleted.Task.WaitAsync(
                TimeSpan.FromSeconds(3),
                cancellationToken);
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }

        // Assert
        Assert.Equal(2, Volatile.Read(ref startAttemptCount));

        Assert.Equal(
            WorkstationSecurityReadinessState.Ready,
            monitor.Object.CurrentSnapshot.State);

        // The successful retry also logs Information, so select only
        // the safe error diagnostic generated by the failed attempt.
        CapturedLogEntry entry =
            Assert.Single(
                logger.Entries,
                captured => captured.Level == LogLevel.Error);

        Assert.Null(entry.Exception);

        Assert.Contains(
            DiagnosticOperationCodes.WorkstationSecurityMonitoringFailure,
            entry.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            nameof(InvalidOperationException),
            entry.Message,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            sensitiveFailureSentinel,
            entry.Message,
            StringComparison.Ordinal);

        monitor.Verify(
            service => service.TryStartAsync(
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        monitor.Verify(
            service => service.StopAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private sealed record CapturedLogEntry(
        LogLevel Level,
        string Message,
        Exception? Exception);

    private sealed class RecordingLogger<TCategory> : ILogger<TCategory>
    {
        private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();

        public CapturedLogEntry[] Entries =>
            _entries.ToArray();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) =>
            true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _entries.Enqueue(
                new CapturedLogEntry(
                    logLevel,
                    formatter(state, exception),
                    exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }

    private static WorkstationSecurityMonitorHostedService CreateHostedService(
        IWorkstationSecurityMonitor monitor)
    {
        return new WorkstationSecurityMonitorHostedService(
            monitor,
            TimeProvider.System,
            NullLogger<WorkstationSecurityMonitorHostedService>.Instance,
            TestRetryInterval);
    }
}
