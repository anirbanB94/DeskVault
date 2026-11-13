using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.Application.Security.Workstation;
using DeskVault.Infrastructure.Security.Workstation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Collections.Concurrent;
using System.ComponentModel;

namespace DeskVault.Infrastructure.Tests;

public sealed class WindowsWorkstationSecurityEventSourceTests
{
    private const uint WtsSessionChangeMessage = 0x02B1;
    private const uint PowerBroadcastMessage = 0x0218;

    [Fact]
    public async Task StartAsync_RegistersCurrentSessionAndRunsNativeWindowOnDedicatedThread()
    {
        // Arrange
        var nativeApi = new FakeNativeApi();
        var source = new WindowsWorkstationSecurityEventSource(nativeApi);

        // Act
        await source.StartAsync(TestContext.Current.CancellationToken);

        FakeNativeWindow window = Assert.Single(nativeApi.Windows);

        // Assert
        Assert.Equal(1, nativeApi.RegisterCount);
        Assert.Equal(0u, nativeApi.LastRegistrationFlags);
        Assert.NotEqual(IntPtr.Zero, nativeApi.LastRegisteredWindowHandle);
        Assert.NotEqual(Environment.CurrentManagedThreadId, window.CreationThreadId);
        Assert.Equal(window.CreationThreadId, window.MessageLoopThreadId);
        Assert.Equal(window.CreationThreadId, nativeApi.RegistrationThreadId);

        await source.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, nativeApi.UnregisterCount);
        Assert.False(nativeApi.WindowWasDisposedWhenUnregistered);
        Assert.True(window.IsDisposed);
        Assert.Equal(window.CreationThreadId, window.DisposeThreadId);
    }

    [Fact]
    public async Task StartAsync_WhenWtsRegistrationFails_CleansPartialWindowAndAllowsRetry()
    {
        // Arrange
        var nativeApi = new FakeNativeApi
        {
            RegisterSucceeds = false,
            RegistrationErrorCode = 5
        };

        var source = new WindowsWorkstationSecurityEventSource(nativeApi);

        // Act
        Win32Exception exception = await Assert.ThrowsAsync<Win32Exception>(
            () => source.StartAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(5, exception.NativeErrorCode);
        Assert.Single(nativeApi.Windows);
        Assert.True(nativeApi.Windows[0].IsDisposed);
        Assert.Equal(1, nativeApi.RegisterCount);
        Assert.Equal(0, nativeApi.UnregisterCount);

        // A failed native registration must not poison the source instance
        // after partial resources have been released.
        nativeApi.RegisterSucceeds = true;

        await source.StartAsync(TestContext.Current.CancellationToken);
        await source.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, nativeApi.RegisterCount);
        Assert.Equal(1, nativeApi.UnregisterCount);
        Assert.Equal(2, nativeApi.Windows.Count);
        Assert.True(nativeApi.Windows[1].IsDisposed);
    }

    [Theory]
    [InlineData(WtsSessionChangeMessage, 0x0007UL, WorkstationSecurityEventKind.SessionLocked)]
    [InlineData(WtsSessionChangeMessage, 0x0008UL, WorkstationSecurityEventKind.SessionUnlocked)]
    [InlineData(WtsSessionChangeMessage, 0x0006UL, WorkstationSecurityEventKind.SessionLoggedOff)]
    [InlineData(WtsSessionChangeMessage, 0x0001UL, WorkstationSecurityEventKind.SessionConnected)]
    [InlineData(WtsSessionChangeMessage, 0x0003UL, WorkstationSecurityEventKind.SessionConnected)]
    [InlineData(WtsSessionChangeMessage, 0x0002UL, WorkstationSecurityEventKind.SessionDisconnected)]
    [InlineData(WtsSessionChangeMessage, 0x0004UL, WorkstationSecurityEventKind.SessionDisconnected)]
    [InlineData(PowerBroadcastMessage, 0x0004UL, WorkstationSecurityEventKind.SystemSuspending)]
    [InlineData(PowerBroadcastMessage, 0x0006UL, WorkstationSecurityEventKind.SystemResumed)]
    [InlineData(PowerBroadcastMessage, 0x0007UL, WorkstationSecurityEventKind.SystemResumed)]
    [InlineData(PowerBroadcastMessage, 0x0012UL, WorkstationSecurityEventKind.SystemResumed)]
    public async Task StartAsync_WhenNativeNotificationArrives_PublishesNormalizedSecurityEvent(
        uint nativeMessage,
        ulong nativeEvent,
        WorkstationSecurityEventKind expectedKind)
    {
        // Arrange
        var nativeApi = new FakeNativeApi();
        var source = new WindowsWorkstationSecurityEventSource(nativeApi);

        var receivedEvent = new TaskCompletionSource<WorkstationSecurityEventKind>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        source.SecurityEventReceived += (_, eventArgs) =>
            receivedEvent.TrySetResult(eventArgs.Kind);

        await source.StartAsync(TestContext.Current.CancellationToken);

        FakeNativeWindow window = Assert.Single(nativeApi.Windows);

        // Act
        await window.PublishMessageAsync(
            nativeMessage,
            (nuint)nativeEvent,
            0,
            TestContext.Current.CancellationToken);

        WorkstationSecurityEventKind actualKind =
            await receivedEvent.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedKind, actualKind);

        await source.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StartAsync_WhenNativeNotificationIsNotSecurityRelevant_IgnoresIt()
    {
        // Arrange
        var nativeApi = new FakeNativeApi();
        var source = new WindowsWorkstationSecurityEventSource(nativeApi);
        int receivedEventCount = 0;

        source.SecurityEventReceived += (_, _) =>
            Interlocked.Increment(ref receivedEventCount);

        await source.StartAsync(TestContext.Current.CancellationToken);

        FakeNativeWindow window = Assert.Single(nativeApi.Windows);

        // Act: logon and remote-control notifications do not imply a
        // DeskVault authentication state transition. Generic power-status
        // changes are not suspend/resume notifications.
        await window.PublishMessageAsync(
            WtsSessionChangeMessage,
            0x0005,
            1,
            TestContext.Current.CancellationToken);

        await window.PublishMessageAsync(
            WtsSessionChangeMessage,
            0x0009,
            1,
            TestContext.Current.CancellationToken);

        await window.PublishMessageAsync(
            PowerBroadcastMessage,
            0x000A,
            0,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, Volatile.Read(ref receivedEventCount));

        await source.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StopAsync_WhenCalledRepeatedly_UnregistersAndDisposesOnlyOnce()
    {
        // Arrange
        var nativeApi = new FakeNativeApi();
        var source = new WindowsWorkstationSecurityEventSource(nativeApi);

        await source.StartAsync(TestContext.Current.CancellationToken);

        FakeNativeWindow window = Assert.Single(nativeApi.Windows);

        // Act
        await source.StopAsync(TestContext.Current.CancellationToken);
        await source.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, nativeApi.UnregisterCount);
        Assert.Equal(1, window.DisposeCount);
    }

    [Fact]
    public async Task StopAsync_WhenWtsUnregistrationFails_RetainsWindowAndRetriesSameRegistration()
    {
        // Arrange
        var nativeApi = new FakeNativeApi
        {
            UnregisterSucceeds = false,
            UnregistrationErrorCode = 6
        };

        var source = new WindowsWorkstationSecurityEventSource(
            nativeApi,
            TimeSpan.FromMilliseconds(150));

        await source.StartAsync(TestContext.Current.CancellationToken);

        FakeNativeWindow window = Assert.Single(nativeApi.Windows);

        // Act
        System.Diagnostics.Stopwatch stopwatch =
            System.Diagnostics.Stopwatch.StartNew();

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => source.StopAsync(TestContext.Current.CancellationToken));

        stopwatch.Stop();

        // Assert: shutdown is bounded and the original HWND is retained.
        Assert.Contains("bounded", exception.Message);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));

        Assert.False(window.IsDisposed);
        Assert.False(nativeApi.WindowWasDisposedWhenUnregistered);
        Assert.True(nativeApi.UnregisterCount >= 1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => source.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(1, nativeApi.RegisterCount);
        Assert.Single(nativeApi.Windows);

        // Allow the same registration to be released.
        nativeApi.UnregisterSucceeds = true;

        await source.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(window.IsDisposed);
        Assert.False(nativeApi.WindowWasDisposedWhenUnregistered);
        Assert.Equal(1, nativeApi.RegisterCount);
        Assert.Single(nativeApi.Windows);
        Assert.True(nativeApi.UnregisterCount >= 2);
    }
    [Fact]
    public async Task StopAsync_WhenWtsUnregistrationBlocks_IsBoundedAndRetainsNativeWindowUntilNativeCallReturns()
    {
        // Arrange
        using var unregisterEntered = new ManualResetEventSlim(false);
        using var releaseUnregister = new ManualResetEventSlim(false);

        var nativeApi = new FakeNativeApi
        {
            UnregisterEntered = unregisterEntered,
            ContinueUnregister = releaseUnregister
        };

        var source = new WindowsWorkstationSecurityEventSource(
            nativeApi,
            TimeSpan.FromMilliseconds(150));

        await source.StartAsync(TestContext.Current.CancellationToken);

        FakeNativeWindow window = Assert.Single(nativeApi.Windows);

        try
        {
            // Act
            Task stopTask = source.StopAsync(
                TestContext.Current.CancellationToken);

            Assert.True(
                unregisterEntered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken),
                "The native WTS unregistration was not entered.");

            InvalidOperationException exception =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => stopTask);

            // Assert: the public operation returned while the native call
            // remained blocked, and the worker retained the original HWND.
            Assert.Contains("bounded", exception.Message);
            Assert.False(window.IsDisposed);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => source.StartAsync(TestContext.Current.CancellationToken));

            Assert.Equal(1, nativeApi.RegisterCount);
            Assert.Single(nativeApi.Windows);
        }
        finally
        {
            // Always release the fake native operation so the test cannot
            // leave a background worker blocked.
            releaseUnregister.Set();
        }

        // The same retained worker completes cleanup after native return.
        await source.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(window.IsDisposed);
        Assert.Equal(1, nativeApi.RegisterCount);
        Assert.Single(nativeApi.Windows);
    }
    [Fact]
    public async Task StopAsync_WhenWindowDisposalFails_RetriesDisposalBeforeCompleting()
    {
        // Arrange
        var nativeApi = new FakeNativeApi();

        var source = new WindowsWorkstationSecurityEventSource(
            nativeApi,
            TimeSpan.FromMilliseconds(150));

        await source.StartAsync(TestContext.Current.CancellationToken);

        FakeNativeWindow window = Assert.Single(nativeApi.Windows);
        window.FailNextDispose();

        // Act
        await source.StopAsync(TestContext.Current.CancellationToken);

        // Assert: WTS was unregistered once. The original window's
        // failed disposal was retried on its owning worker thread.
        Assert.Equal(1, nativeApi.UnregisterCount);
        Assert.Equal(2, window.DisposeCount);
        Assert.True(window.IsDisposed);
        Assert.Equal(window.CreationThreadId, window.DisposeThreadId);
    }
    [Fact]
    public async Task MessageLoop_WhenItTerminatesUnexpectedly_RaisesMonitoringFailureAndCanRestartAfterCleanup()
    {
        // Arrange
        var nativeApi = new FakeNativeApi();
        var source = new WindowsWorkstationSecurityEventSource(nativeApi);

        var monitoringFailure = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        source.MonitoringFailed += (_, eventArgs) =>
            monitoringFailure.TrySetResult(eventArgs.Exception);

        await source.StartAsync(TestContext.Current.CancellationToken);

        FakeNativeWindow firstWindow = Assert.Single(nativeApi.Windows);

        // Act
        firstWindow.CompleteMessageLoopUnexpectedly();

        Exception failure =
            await monitoringFailure.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<InvalidOperationException>(failure);

        // MonitoringFailed is raised before native cleanup. StopAsync waits
        // for the original worker to complete cleanup before resource assertions.
        await source.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(firstWindow.IsDisposed);
        Assert.Equal(1, nativeApi.UnregisterCount);

        // A fresh generation can start after cleanup completes.
        await source.StartAsync(TestContext.Current.CancellationToken);
        await source.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, nativeApi.RegisterCount);
        Assert.Equal(2, nativeApi.UnregisterCount);
        Assert.Equal(2, nativeApi.Windows.Count);
    }

    [Fact]
    public async Task SecurityEventReceived_WhenOneSubscriberThrows_StillNotifiesOtherSubscribers()
    {
        // Arrange
        var nativeApi = new FakeNativeApi();
        var source = new WindowsWorkstationSecurityEventSource(nativeApi);

        var secondSubscriberReceived = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        source.SecurityEventReceived += (_, _) =>
            throw new InvalidOperationException("Subscriber test failure.");

        source.SecurityEventReceived += (_, _) =>
            secondSubscriberReceived.TrySetResult(true);

        await source.StartAsync(TestContext.Current.CancellationToken);

        FakeNativeWindow window = Assert.Single(nativeApi.Windows);

        // Act
        await window.PublishMessageAsync(
            WtsSessionChangeMessage,
            0x0007,
            1,
            TestContext.Current.CancellationToken);

        // Assert
        await secondSubscriberReceived.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        await source.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StartAsync_WhenInitialSessionQueryFails_DoesNotBecomeReadyAndCleansNativeResources()
    {
        // Arrange
        var nativeApi = new FakeNativeApi
        {
            SessionQuerySucceeds = false,
            SessionQueryErrorCode = 5
        };

        var source =
            new WindowsWorkstationSecurityEventSource(nativeApi);

        // Act
        Win32Exception exception =
            await Assert.ThrowsAsync<Win32Exception>(
                () => source.StartAsync(
                    TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(5, exception.NativeErrorCode);
        Assert.Equal(1, nativeApi.RegisterCount);
        Assert.Equal(1, nativeApi.UnregisterCount);
        Assert.Equal(1, nativeApi.SessionQueryCount);

        FakeNativeWindow window =
            Assert.Single(nativeApi.Windows);

        Assert.True(window.IsDisposed);

        // A clean failed-start path permits a fresh registration attempt.
        nativeApi.SessionQuerySucceeds = true;

        await source.StartAsync(
            TestContext.Current.CancellationToken);

        await source.StopAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(2, nativeApi.RegisterCount);
        Assert.Equal(2, nativeApi.UnregisterCount);
    }

    [Fact]
    public async Task StartAsync_WhenInitialSessionIsLocked_PublishesLockBeforeReturning()
    {
        // Arrange
        var nativeApi = new FakeNativeApi
        {
            SessionConnectionState = 0,
            SessionFlags = 0
        };

        var source =
            new WindowsWorkstationSecurityEventSource(nativeApi);

        var lockReceived =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        source.SecurityEventReceived += (_, eventArgs) =>
        {
            if (eventArgs.Kind ==
                WorkstationSecurityEventKind.SessionLocked)
            {
                lockReceived.TrySetResult(true);
            }
        };

        // Act
        await source.StartAsync(
            TestContext.Current.CancellationToken);

        await lockReceived.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, nativeApi.SessionQueryCount);

        await source.StopAsync(
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StartAsync_WhenIndependentPowerNotificationsArrive_PublishesSuspendAndResume()
    {
        // Arrange
        var nativeApi = new FakeNativeApi();
        var source = new WindowsWorkstationSecurityEventSource(nativeApi);

        var receivedEvents =
            new ConcurrentQueue<WorkstationSecurityEventKind>();

        var suspendReceived =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var resumeReceived =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        source.SecurityEventReceived += (_, eventArgs) =>
        {
            receivedEvents.Enqueue(eventArgs.Kind);

            if (eventArgs.Kind == WorkstationSecurityEventKind.SystemSuspending)
            {
                suspendReceived.TrySetResult(true);
            }

            if (eventArgs.Kind == WorkstationSecurityEventKind.SystemResumed)
            {
                resumeReceived.TrySetResult(true);
            }
        };

        // Act
        await source.StartAsync(TestContext.Current.CancellationToken);

        WindowsWorkstationPowerNotificationCallback callback =
            Assert.IsType<WindowsWorkstationPowerNotificationCallback>(
                nativeApi.LastRegisteredPowerCallback);

        uint suspendResult =
            callback(IntPtr.Zero, 0x0004, IntPtr.Zero);

        await suspendReceived.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        uint resumeResult =
            callback(IntPtr.Zero, 0x0007, IntPtr.Zero);

        await resumeReceived.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0u, suspendResult);
        Assert.Equal(0u, resumeResult);

        Assert.Equal(
            [
                WorkstationSecurityEventKind.SystemSuspending,
                WorkstationSecurityEventKind.SystemResumed
            ],
            receivedEvents);

        Assert.Equal(1, nativeApi.PowerRegistrationCount);
        Assert.NotEqual(
            IntPtr.Zero,
            nativeApi.ActivePowerRegistrationHandle);

        await source.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, nativeApi.PowerUnregistrationCount);
        Assert.Equal(
            IntPtr.Zero,
            nativeApi.ActivePowerRegistrationHandle);
    }

    [Fact]
    public async Task StartAsync_WhenIndependentPowerRegistrationFails_DoesNotBecomeReadyAndCleansResources()
    {
        // Arrange
        var nativeApi = new FakeNativeApi
        {
            PowerRegisterSucceeds = false,
            PowerRegistrationErrorCode = 5
        };

        var source = new WindowsWorkstationSecurityEventSource(nativeApi);

        // Act
        Win32Exception exception =
            await Assert.ThrowsAsync<Win32Exception>(
                () => source.StartAsync(
                    TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(5, exception.NativeErrorCode);
        Assert.Equal(1, nativeApi.RegisterCount);
        Assert.Equal(1, nativeApi.UnregisterCount);
        Assert.Equal(1, nativeApi.PowerRegistrationCount);
        Assert.Equal(0, nativeApi.PowerUnregistrationCount);
        Assert.Equal(0, nativeApi.SessionQueryCount);

        FakeNativeWindow window = Assert.Single(nativeApi.Windows);
        Assert.True(window.IsDisposed);
    }

    [Fact]
    public async Task StopAsync_WhenPowerUnregistrationFails_RetainsDispatcherAndRetriesSameRegistration()
    {
        // Arrange
        var nativeApi = new FakeNativeApi();

        var source = new WindowsWorkstationSecurityEventSource(nativeApi);

        await source.StartAsync(TestContext.Current.CancellationToken);

        IntPtr originalRegistrationHandle =
            nativeApi.ActivePowerRegistrationHandle;

        WindowsWorkstationPowerNotificationCallback originalCallback =
            Assert.IsType<WindowsWorkstationPowerNotificationCallback>(
                nativeApi.LastRegisteredPowerCallback);

        nativeApi.PowerUnregisterSucceeds = false;

        // Act — the first cleanup and its immediate retry both fail.
        InvalidOperationException firstStopFailure =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => source.StopAsync(TestContext.Current.CancellationToken));

        // Assert — neither failed attempt may discard the registration.
        Assert.Contains("retained", firstStopFailure.Message);
        Assert.Equal(
            originalRegistrationHandle,
            nativeApi.ActivePowerRegistrationHandle);

        Assert.Same(
            originalCallback,
            nativeApi.LastRegisteredPowerCallback);

        Assert.Equal(2, nativeApi.PowerUnregistrationCount);

        // The source must not create a replacement worker while the old
        // registration still owns a callback and dispatcher.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => source.StartAsync(TestContext.Current.CancellationToken));

        // Act — retry after the native unregister operation can succeed.
        nativeApi.PowerUnregisterSucceeds = true;

        await source.StopAsync(TestContext.Current.CancellationToken);

        // Assert — the same registration was successfully released.
        Assert.Equal(3, nativeApi.PowerUnregistrationCount);
        Assert.Equal(
            IntPtr.Zero,
            nativeApi.ActivePowerRegistrationHandle);
    }

    [Theory]
    [InlineData(0, 0, 0x0007u, WorkstationSecurityEventKind.SessionLocked)]
    [InlineData(4, 1, 0x0004u, WorkstationSecurityEventKind.SessionDisconnected)]
    public async Task Reconciliation_WhenPrimaryWtsSessionNotificationIsSuppressed_LocksAuthenticatedSessionForPersistentState(
        int connectionState,
        int sessionFlags,
        uint suppressedNotification,
        WorkstationSecurityEventKind expectedEvent)
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var nativeApi = new FakeNativeApi
        {
            SuppressPrimarySessionNotifications = true
        };

        var source =
            new WindowsWorkstationSecurityEventSource(nativeApi);

        var session =
            new ApplicationSecuritySession();

        var lifecycleService =
            new ApplicationSecuritySessionLifecycleService(
                session,
                Mock.Of<IApplicationAuthenticationService>());

        var monitor =
            new WorkstationSecurityMonitor(
                source,
                session,
                () => lifecycleService,
                NullLogger<WorkstationSecurityMonitor>.Instance);

        var reconciledEvent =
            new TaskCompletionSource<WorkstationSecurityEventKind>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var sessionLocked =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        source.SecurityEventReceived += (_, eventArgs) =>
        {
            if (eventArgs.Kind == expectedEvent)
            {
                reconciledEvent.TrySetResult(eventArgs.Kind);
            }
        };

        session.StateChanged += snapshot =>
        {
            if (snapshot.State == ApplicationSecuritySessionState.Locked)
            {
                sessionLocked.TrySetResult(true);
            }
        };

        try
        {
            // Start with the process session active and unlocked.
            bool monitoringStarted =
                await monitor.TryStartAsync(cancellationToken);

            Assert.True(monitoringStarted);
            Assert.Equal(
                WorkstationSecurityReadinessState.Ready,
                monitor.CurrentSnapshot.State);

            // Establish authenticated DeskVault access.
            ApplicationSecuritySessionAttempt attempt =
                session.BeginAuthentication();

            Assert.True(
                session.TryCompleteAuthentication(
                    attempt,
                    ApplicationAuthenticationOutcome.Succeeded));

            Assert.Equal(
                ApplicationSecuritySessionState.Authenticated,
                session.CurrentSnapshot.State);

            // Change the persistent WTS snapshot while suppressing the
            // corresponding notification from the primary window.
            // Measure from the state change through publication of the
            // reconciled protective event.
            System.Diagnostics.Stopwatch detectionStopwatch =
                System.Diagnostics.Stopwatch.StartNew();

            nativeApi.SessionConnectionState = connectionState;
            nativeApi.SessionFlags = sessionFlags;

            await nativeApi.SimulatePrimarySessionNotificationAsync(
                suppressedNotification,
                cancellationToken);

            Assert.Equal(
                1,
                nativeApi.SuppressedPrimarySessionNotificationCount);

            Assert.Equal(
                0,
                nativeApi.DeliveredPrimarySessionNotificationCount);

            // The independent one-second reconciliation poll must discover
            // the persistent state. The two-second assertion allows a
            // scheduling margin; it is not a Windows real-time guarantee.
            WorkstationSecurityEventKind actualEvent =
                await reconciledEvent.Task.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    cancellationToken);

            await sessionLocked.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                cancellationToken);

            detectionStopwatch.Stop();

            Assert.True(
                detectionStopwatch.Elapsed <= TimeSpan.FromSeconds(2),
                $"Protected access was withdrawn after {detectionStopwatch.Elapsed.TotalMilliseconds:F0} ms; expected no more than two seconds including scheduling margin.");

            Assert.Equal(expectedEvent, actualEvent);
            Assert.Equal(
                ApplicationSecuritySessionState.Locked,
                session.CurrentSnapshot.State);

            Assert.False(
                session.CurrentSnapshot.IsProtectedAccessAvailable);

            Assert.Equal(
                WorkstationSecurityReadinessState.Ready,
                monitor.CurrentSnapshot.State);
        }
        finally
        {
            await monitor.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task MonitoringFailed_WhenUnexpectedMessageLoopExitOccurs_IsRaisedBeforeWtsUnregistrationCompletes()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using var unregisterEntered =
            new ManualResetEventSlim(false);

        using var releaseUnregister =
            new ManualResetEventSlim(false);

        var nativeApi = new FakeNativeApi
        {
            UnregisterEntered = unregisterEntered,
            ContinueUnregister = releaseUnregister
        };

        var source =
            new WindowsWorkstationSecurityEventSource(
                nativeApi,
                TimeSpan.FromMilliseconds(150));

        var monitoringFailed =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        source.MonitoringFailed += (_, _) =>
        {
            monitoringFailed.TrySetResult(true);
        };

        bool started = false;

        try
        {
            // Act
            await source.StartAsync(cancellationToken);
            started = true;

            FakeNativeWindow window =
                Assert.Single(nativeApi.Windows);

            // Simulate an unexpected native message-loop exit.
            // No intentional Worker.RequestStop is issued.
            window.CompleteMessageLoopUnexpectedly();

            Assert.True(
                unregisterEntered.Wait(
                    TimeSpan.FromSeconds(5),
                    cancellationToken),
                "The worker did not enter the blocked WTS unregistration.");

            // Native cleanup is still blocked. Failure notification must
            // already have reached the Application-facing event subscriber.
            Assert.True(
                monitoringFailed.Task.IsCompleted,
                "MonitoringFailed was delayed until native cleanup completed.");

            await monitoringFailed.Task.WaitAsync(
                TimeSpan.FromSeconds(1),
                cancellationToken);
        }
        finally
        {
            // Always release the fake native call before requesting final
            // cleanup, including when an assertion fails.
            releaseUnregister.Set();

            if (started)
            {
                await source.StopAsync(CancellationToken.None);
            }
        }

        // Assert
        Assert.True(unregisterEntered.IsSet);
        Assert.True(monitoringFailed.Task.IsCompleted);
        Assert.Equal(1, nativeApi.RegisterCount);
        Assert.Equal(1, nativeApi.UnregisterCount);

        FakeNativeWindow completedWindow =
            Assert.Single(nativeApi.Windows);

        Assert.True(completedWindow.IsDisposed);
        Assert.False(nativeApi.WindowWasDisposedWhenUnregistered);
    }

    private sealed class FakeNativeApi : IWindowsWorkstationSecurityNativeApi
    {
        private int _nextHandle = 100;
        private int _registerCount;
        private int _unregisterCount;
        private int _suppressedPrimarySessionNotificationCount;
        private int _deliveredPrimarySessionNotificationCount;

        public List<FakeNativeWindow> Windows { get; } = [];

        public bool RegisterSucceeds { get; set; } = true;

        public int RegistrationErrorCode { get; set; } = 5;

        public bool UnregisterSucceeds { get; set; } = true;

        public int UnregistrationErrorCode { get; set; } = 6;

        public ManualResetEventSlim? UnregisterEntered { get; set; }

        public ManualResetEventSlim? ContinueUnregister { get; set; }

        public int RegisterCount =>
            Volatile.Read(ref _registerCount);

        public int UnregisterCount =>
            Volatile.Read(ref _unregisterCount);

        public uint LastRegistrationFlags { get; private set; }

        public IntPtr LastRegisteredWindowHandle { get; private set; }

        public int RegistrationThreadId { get; private set; }

        public bool WindowWasDisposedWhenUnregistered { get; private set; }

        public uint ProcessSessionId { get; set; } = 42;

        public bool SessionQuerySucceeds { get; set; } = true;

        public int SessionQueryErrorCode { get; set; } = 5;

        public int SessionConnectionState { get; set; } = 0;

        public int SessionFlags { get; set; } = 1;

        public bool SuppressPrimarySessionNotifications { get; set; }

        public int SuppressedPrimarySessionNotificationCount =>
            Volatile.Read(ref _suppressedPrimarySessionNotificationCount);

        public int DeliveredPrimarySessionNotificationCount =>
            Volatile.Read(ref _deliveredPrimarySessionNotificationCount);

        private int _sessionQueryCount;

        public int SessionQueryCount =>
            Volatile.Read(ref _sessionQueryCount);

        private int _powerRegistrationCount;
        private int _powerUnregistrationCount;
        private int _nextPowerRegistrationHandle = 4000;

        public bool PowerRegisterSucceeds { get; set; } = true;

        public int PowerRegistrationErrorCode { get; set; } = 5;

        public bool PowerUnregisterSucceeds { get; set; } = true;

        public int PowerUnregistrationErrorCode { get; set; } = 6;

        public int PowerRegistrationCount =>
            Volatile.Read(ref _powerRegistrationCount);

        public int PowerUnregistrationCount =>
            Volatile.Read(ref _powerUnregistrationCount);

        public WindowsWorkstationPowerNotificationCallback?
            LastRegisteredPowerCallback
        { get; private set; }

        public IntPtr ActivePowerRegistrationHandle { get; private set; }

        public IWindowsWorkstationSecurityNativeWindow CreateHiddenTopLevelWindow(
            Action<uint, nuint, nint> messageReceived)
        {
            var window = new FakeNativeWindow(
                new IntPtr(Interlocked.Increment(ref _nextHandle)),
                messageReceived);

            lock (Windows)
            {
                Windows.Add(window);
            }

            return window;
        }

        public bool RegisterSessionNotification(
            IntPtr windowHandle,
            uint flags,
            out int errorCode)
        {
            Interlocked.Increment(ref _registerCount);
            LastRegistrationFlags = flags;
            LastRegisteredWindowHandle = windowHandle;
            RegistrationThreadId = Environment.CurrentManagedThreadId;
            errorCode = RegisterSucceeds ? 0 : RegistrationErrorCode;
            return RegisterSucceeds;
        }

        public bool UnregisterSessionNotification(
            IntPtr windowHandle,
            out int errorCode)
        {
            Interlocked.Increment(ref _unregisterCount);

            UnregisterEntered?.Set();
            ContinueUnregister?.Wait();

            WindowWasDisposedWhenUnregistered =
                Windows.Any(window =>
                    window.Handle == windowHandle && window.IsDisposed);

            errorCode = UnregisterSucceeds ? 0 : UnregistrationErrorCode;
            return UnregisterSucceeds;
        }

        public async Task SimulatePrimarySessionNotificationAsync(
            uint notification,
            CancellationToken cancellationToken)
        {
            if (SuppressPrimarySessionNotifications)
            {
                Interlocked.Increment(
                    ref _suppressedPrimarySessionNotificationCount);

                return;
            }

            FakeNativeWindow window;

            lock (Windows)
            {
                window = Windows.Single();
            }

            await window.PublishMessageAsync(
                WtsSessionChangeMessage,
                (nuint)notification,
                42,
                cancellationToken);

            Interlocked.Increment(
                ref _deliveredPrimarySessionNotificationCount);
        }

        public bool TryGetProcessSessionId(
            out uint processSessionId,
            out int errorCode)
        {
            processSessionId = ProcessSessionId;
            errorCode = 0;
            return true;
        }

        public bool TryQuerySessionInformation(
            uint sessionId,
            out WindowsWorkstationSecuritySessionStateSnapshot snapshot,
            out int errorCode)
        {
            Interlocked.Increment(ref _sessionQueryCount);

            if (!SessionQuerySucceeds)
            {
                snapshot = default;
                errorCode = SessionQueryErrorCode;
                return false;
            }

            snapshot =
                new WindowsWorkstationSecuritySessionStateSnapshot(
                    Level: 1,
                    StructureSize:
                        (uint)WindowsWorkstationSecurityNativeApi.WtsInfoExStructureSize,
                    SessionId: sessionId,
                    ConnectionState: SessionConnectionState,
                    SessionFlags: SessionFlags);

            errorCode = 0;
            return true;
        }

        public bool TryRegisterSuspendResumeNotification(
            WindowsWorkstationPowerNotificationCallback callback,
            out IntPtr registrationHandle,
            out int errorCode)
        {
            ArgumentNullException.ThrowIfNull(callback);

            Interlocked.Increment(ref _powerRegistrationCount);

            if (!PowerRegisterSucceeds)
            {
                registrationHandle = IntPtr.Zero;
                errorCode = PowerRegistrationErrorCode;
                return false;
            }

            registrationHandle =
                new IntPtr(
                    Interlocked.Increment(
                        ref _nextPowerRegistrationHandle));

            LastRegisteredPowerCallback = callback;
            ActivePowerRegistrationHandle = registrationHandle;

            errorCode = 0;
            return true;
        }

        public bool TryUnregisterSuspendResumeNotification(
            IntPtr registrationHandle,
            out int errorCode)
        {
            Interlocked.Increment(ref _powerUnregistrationCount);

            if (!PowerUnregisterSucceeds ||
                registrationHandle == IntPtr.Zero ||
                registrationHandle != ActivePowerRegistrationHandle)
            {
                errorCode = PowerUnregistrationErrorCode;
                return false;
            }

            ActivePowerRegistrationHandle = IntPtr.Zero;

            errorCode = 0;
            return true;
        }
    }

    private sealed class FakeNativeWindow : IWindowsWorkstationSecurityNativeWindow
    {
        private const uint WmClose = 0x0010;

        private readonly BlockingCollection<QueuedMessage> _messages = new();
        private readonly Action<uint, nuint, nint> _messageReceived;
        private int _disposeCount;
        private int _isDisposed;
        private int _failNextDispose;

        public FakeNativeWindow(
            IntPtr handle,
            Action<uint, nuint, nint> messageReceived)
        {
            Handle = handle;
            _messageReceived = messageReceived;
            CreationThreadId = Environment.CurrentManagedThreadId;
        }

        public IntPtr Handle { get; }

        public int CreationThreadId { get; }

        public int MessageLoopThreadId { get; private set; }

        public int DisposeThreadId { get; private set; }

        public int DisposeCount =>
            Volatile.Read(ref _disposeCount);

        public bool IsDisposed =>
            Volatile.Read(ref _isDisposed) == 1;

        public void RunMessageLoop()
        {
            MessageLoopThreadId = Environment.CurrentManagedThreadId;

            foreach (QueuedMessage queuedMessage in _messages.GetConsumingEnumerable())
            {
                if (queuedMessage.Message == WmClose)
                {
                    queuedMessage.Completion?.TrySetResult(true);
                    return;
                }

                try
                {
                    _messageReceived(
                        queuedMessage.Message,
                        queuedMessage.WParam,
                        queuedMessage.LParam);
                }
                finally
                {
                    queuedMessage.Completion?.TrySetResult(true);
                }
            }
        }

        public async Task PublishMessageAsync(
            uint message,
            nuint wParam,
            nint lParam,
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            _messages.Add(
                new QueuedMessage(
                    message,
                    wParam,
                    lParam,
                    completion));

            await completion.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                cancellationToken);
        }

        public void CompleteMessageLoopUnexpectedly()
        {
            _messages.CompleteAdding();
        }

        public void RequestStop()
        {
            if (IsDisposed)
            {
                return;
            }

            try
            {
                if (!_messages.IsAddingCompleted)
                {
                    _messages.Add(
                        new QueuedMessage(
                            WmClose,
                            0,
                            0,
                            null));
                }
            }
            catch (ObjectDisposedException)
            {
                // The fake loop may have completed and released its queue.
            }
            catch (InvalidOperationException)
            {
                // The fake loop may have completed concurrently with Stop.
            }
        }

        public void FailNextDispose()
        {
            Interlocked.Exchange(ref _failNextDispose, 1);
        }
        public void Dispose()
        {
            DisposeThreadId = Environment.CurrentManagedThreadId;
            Interlocked.Increment(ref _disposeCount);

            if (Interlocked.Exchange(ref _failNextDispose, 0) == 1)
            {
                throw new InvalidOperationException(
                    "Injected native window disposal failure.");
            }

            Interlocked.Exchange(ref _isDisposed, 1);

            if (!_messages.IsAddingCompleted)
            {
                _messages.CompleteAdding();
            }

            _messages.Dispose();
        }
        private sealed record QueuedMessage(
            uint Message,
            nuint WParam,
            nint LParam,
            TaskCompletionSource<bool>? Completion);
    }
}
