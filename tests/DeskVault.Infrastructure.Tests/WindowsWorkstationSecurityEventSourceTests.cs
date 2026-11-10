using System.Collections.Concurrent;
using System.ComponentModel;
using DeskVault.Application.Security.Workstation;
using DeskVault.Infrastructure.Security.Workstation;

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
    public async Task StopAsync_WhenWtsUnregistrationFails_ReportsCleanupFailureAndPreventsRestart()
    {
        // Arrange
        var nativeApi = new FakeNativeApi
        {
            UnregisterSucceeds = false,
            UnregistrationErrorCode = 6
        };

        var source = new WindowsWorkstationSecurityEventSource(nativeApi);

        await source.StartAsync(TestContext.Current.CancellationToken);

        FakeNativeWindow window = Assert.Single(nativeApi.Windows);

        // Act
        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => source.StopAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<Win32Exception>(exception.InnerException);
        Assert.Equal(6, ((Win32Exception)exception.InnerException!).NativeErrorCode);
        Assert.True(window.IsDisposed);
        Assert.Equal(1, nativeApi.UnregisterCount);

        // An unsuccessful WTS unregister leaves resource ownership uncertain.
        // The same source instance must not register a second window.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => source.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(1, nativeApi.RegisterCount);
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
        Assert.True(firstWindow.IsDisposed);
        Assert.Equal(1, nativeApi.UnregisterCount);

        // Stop is still successful because the runtime failure was reported
        // and native resources were released. A fresh generation can start.
        await source.StopAsync(TestContext.Current.CancellationToken);
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

    private sealed class FakeNativeApi : IWindowsWorkstationSecurityNativeApi
    {
        private int _nextHandle = 100;
        private int _registerCount;
        private int _unregisterCount;

        public List<FakeNativeWindow> Windows { get; } = [];

        public bool RegisterSucceeds { get; set; } = true;

        public int RegistrationErrorCode { get; set; } = 5;

        public bool UnregisterSucceeds { get; set; } = true;

        public int UnregistrationErrorCode { get; set; } = 6;

        public int RegisterCount =>
            Volatile.Read(ref _registerCount);

        public int UnregisterCount =>
            Volatile.Read(ref _unregisterCount);

        public uint LastRegistrationFlags { get; private set; }

        public IntPtr LastRegisteredWindowHandle { get; private set; }

        public int RegistrationThreadId { get; private set; }

        public bool WindowWasDisposedWhenUnregistered { get; private set; }

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
            WindowWasDisposedWhenUnregistered =
                Windows.Any(window =>
                    window.Handle == windowHandle && window.IsDisposed);
            errorCode = UnregisterSucceeds ? 0 : UnregistrationErrorCode;
            return UnregisterSucceeds;
        }
    }

    private sealed class FakeNativeWindow : IWindowsWorkstationSecurityNativeWindow
    {
        private const uint WmClose = 0x0010;

        private readonly BlockingCollection<QueuedMessage> _messages = new();
        private readonly Action<uint, nuint, nint> _messageReceived;
        private int _disposeCount;
        private int _isDisposed;

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

        public void Dispose()
        {
            DisposeThreadId = Environment.CurrentManagedThreadId;
            Interlocked.Increment(ref _disposeCount);
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
