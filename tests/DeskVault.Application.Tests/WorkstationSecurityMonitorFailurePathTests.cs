using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.Application.Security.Workstation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class WorkstationSecurityMonitorFailurePathTests
{
    [Fact]
    public async Task TryStartAsync_WhenCancellationOccursDuringSourceStart_TransitionsToUnavailableAndStopsSource()
    {
        // Arrange
        var session = new ApplicationSecuritySession();
        var eventSource = new TestWorkstationSecurityEventSource();

        var startEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        eventSource.StartOperation = async cancellationToken =>
        {
            startEntered.TrySetResult(true);

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);
        };

        var lifecycle = new Mock<IApplicationSecuritySessionLifecycleService>();

        var monitor = new WorkstationSecurityMonitor(
            eventSource,
            session,
            () => lifecycle.Object,
            NullLogger<WorkstationSecurityMonitor>.Instance);

        var readinessNotifications =
            new List<WorkstationSecurityReadinessSnapshot>();

        monitor.ReadinessChanged += readinessNotifications.Add;

        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        Task<bool> startTask =
            monitor.TryStartAsync(cancellationTokenSource.Token);

        await startEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => startTask);

        // Assert
        Assert.Equal(
            WorkstationSecurityReadinessState.Unavailable,
            monitor.CurrentSnapshot.State);

        Assert.False(monitor.CurrentSnapshot.IsReady);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            session.CurrentSnapshot.State);

        Assert.Equal(1, eventSource.StartCallCount);
        Assert.Equal(1, eventSource.StopCallCount);

        Assert.Equal(
            new[]
            {
                WorkstationSecurityReadinessState.Starting,
                WorkstationSecurityReadinessState.Unavailable
            },
            readinessNotifications.Select(snapshot => snapshot.State));

        lifecycle.Verify(
            service => service.Lock(),
            Times.Once);
    }

    [Fact]
    public async Task MonitoringFailure_WhenSessionLockThrows_StillMarksSessionUnavailable()
    {
        // Arrange
        ApplicationSecuritySession session = CreateAuthenticatedSession();

        var eventSource = new TestWorkstationSecurityEventSource();

        var lifecycle =
            new Mock<IApplicationSecuritySessionLifecycleService>();

        lifecycle
            .Setup(service => service.Lock())
            .Throws(new InvalidOperationException(
                "Synthetic session-lock failure."));

        var monitor = new WorkstationSecurityMonitor(
            eventSource,
            session,
            () => lifecycle.Object,
            NullLogger<WorkstationSecurityMonitor>.Instance);

        Assert.True(
            await monitor.TryStartAsync(
                TestContext.Current.CancellationToken));

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            session.CurrentSnapshot.State);

        // Act
        eventSource.RaiseMonitoringFailure(
            new InvalidOperationException(
                "Synthetic workstation-monitoring failure."));

        // Assert
        Assert.Equal(
            WorkstationSecurityReadinessState.Unavailable,
            monitor.CurrentSnapshot.State);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            session.CurrentSnapshot.State);

        Assert.False(
            session.CurrentSnapshot.IsProtectedAccessAvailable);

        lifecycle.Verify(
            service => service.Lock(),
            Times.Once);
    }

    private static ApplicationSecuritySession CreateAuthenticatedSession()
    {
        var session = new ApplicationSecuritySession();

        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        Assert.True(
            session.TryCompleteAuthentication(
                attempt,
                ApplicationAuthenticationOutcome.Succeeded));

        return session;
    }

    private sealed class TestWorkstationSecurityEventSource
        : IWorkstationSecurityEventSource
    {
        public event EventHandler<WorkstationSecurityEventArgs>? SecurityEventReceived;

        public event EventHandler<WorkstationSecurityEventSourceFailureEventArgs>? MonitoringFailed;

        public int StartCallCount { get; private set; }

        public int StopCallCount { get; private set; }

        public Func<CancellationToken, Task>? StartOperation { get; set; }

        public Func<CancellationToken, Task>? StopOperation { get; set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCallCount++;

            cancellationToken.ThrowIfCancellationRequested();

            return StartOperation?.Invoke(cancellationToken)
                ?? Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCallCount++;

            cancellationToken.ThrowIfCancellationRequested();

            return StopOperation?.Invoke(cancellationToken)
                ?? Task.CompletedTask;
        }

        public void RaiseSecurityEvent(WorkstationSecurityEventKind kind)
        {
            SecurityEventReceived?.Invoke(
                this,
                new WorkstationSecurityEventArgs(kind));
        }

        public void RaiseMonitoringFailure(Exception exception)
        {
            MonitoringFailed?.Invoke(
                this,
                new WorkstationSecurityEventSourceFailureEventArgs(exception));
        }
    }
}
