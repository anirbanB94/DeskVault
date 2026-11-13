using DeskVault.Infrastructure.Security.Workstation;

namespace DeskVault.Infrastructure.Tests;

public sealed class WindowsWorkstationSecurityEventSourceFailurePathTests
{
    [Fact]
    public async Task StopAsync_WhenNativeStopRequestFails_RetainsWorkerAndAllowsRetry()
    {
        // Arrange
        var nativeApi = new FakeNativeApi();

        var source =
            new WindowsWorkstationSecurityEventSource(nativeApi);

        await source.StartAsync(
            TestContext.Current.CancellationToken);

        FakeNativeWindow window =
            Assert.Single(nativeApi.Windows);

        Assert.Equal(1, nativeApi.RegisterCount);
        Assert.Equal(0, nativeApi.UnregisterCount);
        Assert.False(window.IsDisposed);

        // Configure the first shutdown request to fail. The next request
        // will succeed so we can verify that the original worker is retained.
        window.FailNextStopRequest();

        // Act
        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => source.StopAsync(
                    TestContext.Current.CancellationToken));

        // Assert: failed stop request must not be mistaken for successful
        // cleanup, and the registered worker must remain available for retry.
        Assert.IsType<InvalidOperationException>(
            exception.InnerException);

        Assert.False(window.IsDisposed);
        Assert.Equal(0, nativeApi.UnregisterCount);
        Assert.Equal(1, nativeApi.RegisterCount);

        // Retry shutdown using the same source instance and native window.
        await source.StopAsync(
            TestContext.Current.CancellationToken);

        Assert.True(window.IsDisposed);
        Assert.Equal(1, nativeApi.UnregisterCount);
        Assert.Equal(1, nativeApi.RegisterCount);

        // A repeated StopAsync is idempotent after successful cleanup.
        await source.StopAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(1, nativeApi.UnregisterCount);
        Assert.Equal(1, window.DisposeCount);
    }

    private sealed class FakeNativeApi
        : IWindowsWorkstationSecurityNativeApi
    {
        private int _nextHandle = 100;
        private int _registerCount;
        private int _unregisterCount;

        public List<FakeNativeWindow> Windows { get; } = [];

        public int RegisterCount =>
            Volatile.Read(ref _registerCount);

        public int UnregisterCount =>
            Volatile.Read(ref _unregisterCount);

        public IWindowsWorkstationSecurityNativeWindow CreateHiddenTopLevelWindow(
            Action<uint, nuint, nint> messageReceived)
        {
            ArgumentNullException.ThrowIfNull(messageReceived);

            var window = new FakeNativeWindow(
                new IntPtr(Interlocked.Increment(ref _nextHandle)));

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

            errorCode = 0;

            return windowHandle != IntPtr.Zero;
        }

        public bool UnregisterSessionNotification(
            IntPtr windowHandle,
            out int errorCode)
        {
            Interlocked.Increment(ref _unregisterCount);

            errorCode = 0;

            return windowHandle != IntPtr.Zero;
        }

        public bool TryGetProcessSessionId(
            out uint processSessionId,
            out int errorCode)
        {
            processSessionId = 42;
            errorCode = 0;
            return true;
        }

        public bool TryQuerySessionInformation(
            uint sessionId,
            out WindowsWorkstationSecuritySessionStateSnapshot snapshot,
            out int errorCode)
        {
            snapshot =
                new WindowsWorkstationSecuritySessionStateSnapshot(
                    Level: 1,
                    StructureSize:
                        (uint)WindowsWorkstationSecurityNativeApi.WtsInfoExStructureSize,
                    SessionId: sessionId,
                    ConnectionState: 0,
                    SessionFlags: 1);

            errorCode = 0;
            return true;
        }

        public bool TryRegisterSuspendResumeNotification(
            WindowsWorkstationPowerNotificationCallback callback,
            out IntPtr registrationHandle,
            out int errorCode)
        {
            ArgumentNullException.ThrowIfNull(callback);

            registrationHandle = new IntPtr(900);
            errorCode = 0;
            return true;
        }

        public bool TryUnregisterSuspendResumeNotification(
            IntPtr registrationHandle,
            out int errorCode)
        {
            errorCode = registrationHandle != IntPtr.Zero ? 0 : 6;
            return registrationHandle != IntPtr.Zero;
        }
    }

    private sealed class FakeNativeWindow
        : IWindowsWorkstationSecurityNativeWindow
    {
        private readonly ManualResetEventSlim _stopSignal = new(false);

        private int _failNextStopRequest;
        private int _disposeCount;
        private int _isDisposed;

        public FakeNativeWindow(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle { get; }

        public int DisposeCount =>
            Volatile.Read(ref _disposeCount);

        public bool IsDisposed =>
            Volatile.Read(ref _isDisposed) == 1;

        public void RunMessageLoop()
        {
            _stopSignal.Wait();
        }

        public void RequestStop()
        {
            if (IsDisposed)
            {
                return;
            }

            if (Interlocked.Exchange(ref _failNextStopRequest, 0) == 1)
            {
                throw new InvalidOperationException(
                    "Synthetic native stop-request failure.");
            }

            _stopSignal.Set();
        }

        public void FailNextStopRequest()
        {
            Volatile.Write(ref _failNextStopRequest, 1);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            {
                return;
            }

            Interlocked.Increment(ref _disposeCount);

            _stopSignal.Set();
            _stopSignal.Dispose();
        }
    }
}
