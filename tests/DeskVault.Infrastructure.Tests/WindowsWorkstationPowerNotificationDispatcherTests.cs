using DeskVault.Application.Security.Workstation;
using DeskVault.Infrastructure.Security.Workstation;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DeskVault.Infrastructure.Tests;

public sealed class WindowsWorkstationPowerNotificationDispatcherTests
{
    private const uint PowerSuspend = 0x0004;
    private const uint PowerResumeCritical = 0x0006;
    private const uint PowerResumeSuspend = 0x0007;
    private const uint PowerResumeAutomatic = 0x0012;

    [Fact]
    public async Task Callback_WhenSuspendAndResumeArrive_DispatchesSuspendBeforeResume()
    {
        // Arrange
        var receivedEvents =
            new ConcurrentQueue<WorkstationSecurityEventKind>();

        var bothEventsReceived =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var dispatcher =
            new WindowsWorkstationPowerNotificationDispatcher(
                kind =>
                {
                    receivedEvents.Enqueue(kind);

                    if (receivedEvents.Count >= 2)
                    {
                        bothEventsReceived.TrySetResult(true);
                    }
                });

        dispatcher.Start();

        try
        {
            // Act
            uint suspendResult =
                dispatcher.Callback(
                    IntPtr.Zero,
                    PowerSuspend,
                    IntPtr.Zero);

            uint resumeResult =
                dispatcher.Callback(
                    IntPtr.Zero,
                    PowerResumeSuspend,
                    IntPtr.Zero);

            await bothEventsReceived.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(0u, suspendResult);
            Assert.Equal(0u, resumeResult);

            Assert.Collection(
                receivedEvents,
                kind => Assert.Equal(
                    WorkstationSecurityEventKind.SystemSuspending,
                    kind),
                kind => Assert.Equal(
                    WorkstationSecurityEventKind.SystemResumed,
                    kind));
        }
        finally
        {
            await dispatcher.StopAsync(
                TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData(PowerResumeCritical)]
    [InlineData(PowerResumeSuspend)]
    [InlineData(PowerResumeAutomatic)]
    public async Task Callback_WhenResumeNotificationArrives_DispatchesResumeWithoutUnlock(
        uint notificationType)
    {
        // Arrange
        var receivedEvents =
            new ConcurrentQueue<WorkstationSecurityEventKind>();

        var resumeReceived =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var dispatcher =
            new WindowsWorkstationPowerNotificationDispatcher(
                kind =>
                {
                    receivedEvents.Enqueue(kind);

                    if (kind == WorkstationSecurityEventKind.SystemResumed)
                    {
                        resumeReceived.TrySetResult(true);
                    }
                });

        dispatcher.Start();

        try
        {
            // Act
            uint callbackResult =
                dispatcher.Callback(
                    IntPtr.Zero,
                    notificationType,
                    IntPtr.Zero);

            await resumeReceived.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(0u, callbackResult);

            Assert.Equal(
                [WorkstationSecurityEventKind.SystemResumed],
                receivedEvents);
        }
        finally
        {
            await dispatcher.StopAsync(
                TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Callback_WhenNotificationIsUnrelated_IgnoresIt()
    {
        // Arrange
        int receivedEventCount = 0;

        var dispatcher =
            new WindowsWorkstationPowerNotificationDispatcher(
                _ => Interlocked.Increment(ref receivedEventCount));

        dispatcher.Start();

        try
        {
            // Act
            uint callbackResult =
                dispatcher.Callback(
                    IntPtr.Zero,
                    notificationType: 0x000A,
                    IntPtr.Zero);

            await Task.Delay(
                TimeSpan.FromMilliseconds(50),
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(0u, callbackResult);
            Assert.Equal(0, Volatile.Read(ref receivedEventCount));
        }
        finally
        {
            await dispatcher.StopAsync(
                TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Callback_WhenEventHandlerBlocks_ReturnsWithoutBlockingNativeCallback()
    {
        // Arrange
        using var handlerEntered = new ManualResetEventSlim(false);
        using var releaseHandler = new ManualResetEventSlim(false);

        var dispatcher =
            new WindowsWorkstationPowerNotificationDispatcher(
                kind =>
                {
                    if (kind == WorkstationSecurityEventKind.SystemSuspending)
                    {
                        handlerEntered.Set();
                        releaseHandler.Wait();
                    }
                });

        dispatcher.Start();

        try
        {
            // Act
            Stopwatch stopwatch = Stopwatch.StartNew();

            uint callbackResult =
                dispatcher.Callback(
                    IntPtr.Zero,
                    PowerSuspend,
                    IntPtr.Zero);

            stopwatch.Stop();

            Assert.True(
                handlerEntered.Wait(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken));

            // Assert: the callback only queued work. The application
            // event handler is blocked on the dispatcher thread.
            Assert.Equal(0u, callbackResult);

            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(1),
                "The native callback performed blocking work.");
        }
        finally
        {
            releaseHandler.Set();

            await dispatcher.StopAsync(
                TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task StopAsync_WhenDispatcherHandlerIsBlocked_IsBoundedAndCanBeRetried()
    {
        // Arrange
        using var handlerEntered = new ManualResetEventSlim(false);
        using var releaseHandler = new ManualResetEventSlim(false);

        var dispatcher =
            new WindowsWorkstationPowerNotificationDispatcher(
                _ =>
                {
                    handlerEntered.Set();
                    releaseHandler.Wait();
                },
                shutdownTimeout: TimeSpan.FromMilliseconds(120));

        dispatcher.Start();

        try
        {
            dispatcher.Callback(
                IntPtr.Zero,
                PowerSuspend,
                IntPtr.Zero);

            Assert.True(
                handlerEntered.Wait(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken));

            // Act
            Stopwatch stopwatch = Stopwatch.StartNew();

            InvalidOperationException exception =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => dispatcher.StopAsync(
                        TestContext.Current.CancellationToken));

            stopwatch.Stop();

            // Assert
            Assert.Contains(
                "resources remain retained",
                exception.Message);

            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(2),
                "Dispatcher shutdown exceeded its bounded deadline.");
        }
        finally
        {
            releaseHandler.Set();

            // The same dispatcher instance must be retried. No replacement
            // worker or new synchronization objects are created.
            await dispatcher.StopAsync(
                TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Callback_AfterDispatcherStops_DoesNotInvokeEventHandler()
    {
        // Arrange
        int receivedEventCount = 0;

        var dispatcher =
            new WindowsWorkstationPowerNotificationDispatcher(
                _ => Interlocked.Increment(ref receivedEventCount));

        dispatcher.Start();

        await dispatcher.StopAsync(
            TestContext.Current.CancellationToken);

        // Act
        uint callbackResult =
            dispatcher.Callback(
                IntPtr.Zero,
                PowerSuspend,
                IntPtr.Zero);

        // Assert
        Assert.Equal(0u, callbackResult);
        Assert.Equal(0, Volatile.Read(ref receivedEventCount));
    }

    [Fact]
    public async Task Callback_WhenNotificationSignalFails_ReportsMonitoringFailure()
    {
        // Arrange
        var reportedFailure =
            new TaskCompletionSource<Exception>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var dispatcher =
            new WindowsWorkstationPowerNotificationDispatcher(
                _ => { },
                exception => reportedFailure.TrySetResult(exception),
                signalNotification: () =>
                    throw new InvalidOperationException(
                        "Synthetic notification signal failure."));

        dispatcher.Start();

        try
        {
            // Act
            uint callbackResult =
                dispatcher.Callback(
                    IntPtr.Zero,
                    PowerSuspend,
                    IntPtr.Zero);

            Exception actualFailure =
                await reportedFailure.Task.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(31u, callbackResult);
            Assert.IsType<InvalidOperationException>(actualFailure);
            Assert.Contains("Synthetic notification signal failure.", actualFailure.Message);
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Dispatch_WhenEventDeliveryThrows_ReportsMonitoringFailureAndStopsWorker()
    {
        // Arrange
        var reportedFailure =
            new TaskCompletionSource<Exception>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var dispatcher =
            new WindowsWorkstationPowerNotificationDispatcher(
                _ => throw new InvalidOperationException(
                    "Synthetic power event delivery failure."),
                exception => reportedFailure.TrySetResult(exception));

        dispatcher.Start();

        try
        {
            // Act
            uint callbackResult =
                dispatcher.Callback(
                    IntPtr.Zero,
                    PowerSuspend,
                    IntPtr.Zero);

            Exception actualFailure =
                await reportedFailure.Task.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(0u, callbackResult);
            Assert.IsType<InvalidOperationException>(actualFailure);
            Assert.Contains("Synthetic power event delivery failure.", actualFailure.Message);
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
        }
    }
}
