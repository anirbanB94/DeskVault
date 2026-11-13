using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Workstation;
using System.ComponentModel;
using System.Diagnostics;

namespace DeskVault.Infrastructure.Security.Workstation;

/// <summary>
/// Provides workstation security notifications using a dedicated hidden
/// top-level Win32 window, WTS session notifications, and power broadcasts.
/// </summary>
/// <remarks>
/// The top-level window is intentionally not a message-only window because
/// message-only windows do not receive system broadcast messages such as
/// WM_POWERBROADCAST. Native window creation and its message loop run on one
/// dedicated background thread. WTS registration is checked before startup
/// completes and is paired with an explicit unregister before window disposal.
/// </remarks>
public sealed class WindowsWorkstationSecurityEventSource
    : IWorkstationSecurityEventSource
{
    private const uint WtsSessionChangeMessage = 0x02B1;
    private const uint PowerBroadcastMessage = 0x0218;

    private const uint WtsConsoleConnect = 0x0001;
    private const uint WtsConsoleDisconnect = 0x0002;
    private const uint WtsRemoteConnect = 0x0003;
    private const uint WtsRemoteDisconnect = 0x0004;
    private const uint WtsSessionLogoff = 0x0006;
    private const uint WtsSessionLock = 0x0007;
    private const uint WtsSessionUnlock = 0x0008;

    private const uint PowerSuspend = 0x0004;
    private const uint PowerResumeCritical = 0x0006;
    private const uint PowerResumeSuspend = 0x0007;
    private const uint PowerResumeAutomatic = 0x0012;

    // NOTIFY_FOR_THIS_SESSION. NOTIFY_FOR_ALL_SESSIONS is deliberately not used.
    private const uint NotifyForThisSession = 0x0000;

    private static readonly TimeSpan DefaultShutdownTimeout =
        TimeSpan.FromSeconds(3);

    private static readonly TimeSpan DefaultStartupTimeout =
        TimeSpan.FromSeconds(10);

    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly IWindowsWorkstationSecurityNativeApi _nativeApi;
    private readonly TimeSpan _shutdownTimeout;

    private Worker? _worker;

    public WindowsWorkstationSecurityEventSource()
        : this(new WindowsWorkstationSecurityNativeApi())
    {
    }

    internal WindowsWorkstationSecurityEventSource(
        IWindowsWorkstationSecurityNativeApi nativeApi,
        TimeSpan? shutdownTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(nativeApi);

        _shutdownTimeout =
            shutdownTimeout ?? DefaultShutdownTimeout;

        if (_shutdownTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(shutdownTimeout),
                "The workstation event-source shutdown timeout must be positive.");
        }

        _nativeApi = nativeApi;
    }
    /// <inheritdoc />
    public event EventHandler<WorkstationSecurityEventArgs>? SecurityEventReceived;

    /// <inheritdoc />
    public event EventHandler<WorkstationSecurityEventSourceFailureEventArgs>? MonitoringFailed;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Worker worker;

        await _lifecycleGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_worker is not null)
            {
                if (!_worker.Completion.IsCompleted)
                {
                    if (_worker.Started.IsCompletedSuccessfully &&
                        !_worker.IsStopping)
                    {
                        // Starting an already-ready source is idempotent.
                        return;
                    }

                    throw new InvalidOperationException(
                        "A previous workstation notification worker is still starting, stopping, or retaining native resources.");
                }

                if (!_worker.CleanupSucceeded)
                {
                    throw new InvalidOperationException(
                        "A previous workstation notification worker completed without confirming native resource cleanup.",
                        _worker.TerminalFailure);
                }

                _worker = null;
            }

            worker = new Worker(
                this,
                _nativeApi,
                _shutdownTimeout);

            _worker = worker;
            worker.Start(cancellationToken);
        }
        finally
        {
            _lifecycleGate.Release();
        }

        try
        {
            await worker.Started
                .WaitAsync(
                    DefaultStartupTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception startupException)
        {
            Exception? stopRequestFailure = null;

            try
            {
                worker.RequestStop();
            }
            catch (Exception exception)
            {
                stopRequestFailure = exception;
            }

            worker.RequestCleanupRetry();

            try
            {
                await worker.Completion
                    .WaitAsync(
                        _shutdownTimeout,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException cleanupTimeout)
            {
                var failures = new List<Exception>
                {
                    startupException,
                    cleanupTimeout
                };

                if (stopRequestFailure is not null)
                {
                    failures.Add(stopRequestFailure);
                }

                if (worker.LastCleanupFailure is Exception cleanupFailure)
                {
                    failures.Add(cleanupFailure);
                }

                throw new InvalidOperationException(
                    "Workstation notification startup failed and native cleanup did not complete within the bounded shutdown period. The original worker and its resources remain retained for retry.",
                    new AggregateException(failures));
            }
            catch (Exception) when (worker.CleanupSucceeded)
            {
                // The worker can fault because startup failed. That fault
                // is expected once native cleanup has completed.
            }

            if (!worker.CleanupSucceeded)
            {
                throw new InvalidOperationException(
                    "Workstation notification startup failed and native resource cleanup remains incomplete.",
                    worker.TerminalFailure ?? worker.LastCleanupFailure);
            }

            await ReleaseWorkerReferenceAsync(worker)
                .ConfigureAwait(false);

            if (stopRequestFailure is not null)
            {
                throw new AggregateException(
                    "Workstation notification startup failed and its stop request also failed.",
                    startupException,
                    stopRequestFailure);
            }

            throw;
        }
    }
    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Worker? worker;

        await _lifecycleGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            worker = _worker;
        }
        finally
        {
            _lifecycleGate.Release();
        }

        if (worker is null)
        {
            return;
        }

        try
        {
            worker.RequestStop();
        }
        catch (Exception requestException)
        {
            worker.RequestCleanupRetry();

            throw new InvalidOperationException(
                "The workstation notification worker could not be signalled to stop. Its native resources remain owned by the retained worker.",
                requestException);
        }

        worker.RequestCleanupRetry();

        try
        {
            await worker.Completion
                .WaitAsync(
                    _shutdownTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException timeoutException)
        {
            Exception inner =
                worker.LastCleanupFailure ?? timeoutException;

            throw new InvalidOperationException(
                "Workstation notification shutdown did not complete within the bounded period. The worker and any unreleased native resources remain retained for a subsequent StopAsync retry.",
                inner);
        }
        catch (Exception) when (worker.CleanupSucceeded)
        {
            // An unexpected message-loop failure has already been
            // reported through MonitoringFailed. Stop is complete because
            // the worker confirms every native resource was released.
        }

        if (!worker.CleanupSucceeded)
        {
            throw new InvalidOperationException(
                "Workstation notification shutdown ended without confirming native resource cleanup.",
                worker.TerminalFailure ?? worker.LastCleanupFailure);
        }

        await ReleaseWorkerReferenceAsync(worker)
            .ConfigureAwait(false);
    }

    private async Task ReleaseWorkerReferenceAsync(Worker worker)
    {
        await _lifecycleGate
            .WaitAsync(CancellationToken.None)
            .ConfigureAwait(false);

        try
        {
            if (ReferenceEquals(_worker, worker) &&
                worker.CleanupSucceeded)
            {
                _worker = null;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }
    private void OnNativeWindowMessage(
        uint message,
        nuint wParam,
        nint lParam)
    {
        WorkstationSecurityEventKind? kind =
            TranslateMessage(message, wParam);

        if (kind is not null)
        {
            PublishSecurityEvent(kind.Value);
        }
    }

    /// <summary>
    /// Publishes a normalized workstation event to all subscribers.
    /// Subscriber exceptions must never escape a native or reconciliation
    /// worker thread.
    /// </summary>
    private void PublishSecurityEvent(
        WorkstationSecurityEventKind kind)
    {
        EventHandler<WorkstationSecurityEventArgs>? handlers =
            SecurityEventReceived;

        if (handlers is null)
        {
            return;
        }

        var eventArgs =
            new WorkstationSecurityEventArgs(kind);

        foreach (EventHandler<WorkstationSecurityEventArgs> handler
            in handlers.GetInvocationList())
        {
            try
            {
                handler(this, eventArgs);
            }
            catch (Exception)
            {
                Trace.TraceError(
                    "A workstation security event subscriber failed.");
            }
        }
    }

    private static WorkstationSecurityEventKind? TranslateMessage(
        uint message,
        nuint wParam)
    {
        uint notification = unchecked((uint)wParam);

        if (message == WtsSessionChangeMessage)
        {
            return notification switch
            {
                WtsSessionLock => WorkstationSecurityEventKind.SessionLocked,
                WtsSessionUnlock => WorkstationSecurityEventKind.SessionUnlocked,
                WtsSessionLogoff => WorkstationSecurityEventKind.SessionLoggedOff,
                WtsConsoleConnect or WtsRemoteConnect =>
                    WorkstationSecurityEventKind.SessionConnected,
                WtsConsoleDisconnect or WtsRemoteDisconnect =>
                    WorkstationSecurityEventKind.SessionDisconnected,
                _ => null
            };
        }

        if (message == PowerBroadcastMessage)
        {
            return notification switch
            {
                PowerSuspend => WorkstationSecurityEventKind.SystemSuspending,
                PowerResumeCritical or
                PowerResumeSuspend or
                PowerResumeAutomatic => WorkstationSecurityEventKind.SystemResumed,
                _ => null
            };
        }

        return null;
    }

    private void RaiseMonitoringFailed(Exception exception)
    {
        EventHandler<WorkstationSecurityEventSourceFailureEventArgs>? handlers =
            MonitoringFailed;

        if (handlers is null)
        {
            return;
        }

        var eventArgs =
            new WorkstationSecurityEventSourceFailureEventArgs(exception);

        foreach (EventHandler<WorkstationSecurityEventSourceFailureEventArgs> handler
            in handlers.GetInvocationList())
        {
            try
            {
                handler(this, eventArgs);
            }
            catch (Exception)
            {
                Trace.TraceError(
                    "A workstation monitoring-failure subscriber failed.");
            }
        }
    }

    private sealed class Worker
    {
        private readonly WindowsWorkstationSecurityEventSource _owner;
        private readonly IWindowsWorkstationSecurityNativeApi _nativeApi;
        private readonly TimeSpan _shutdownTimeout;

        private readonly TaskCompletionSource<bool> _started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly object _stopSyncRoot = new();
        private readonly AutoResetEvent _cleanupRetryRequested = new(false);

        private IWindowsWorkstationSecurityNativeWindow? _window;
        private WindowsWorkstationSecuritySessionReconciliationWorker? _reconciliationWorker;
        private WindowsWorkstationPowerNotificationDispatcher? _powerDispatcher;

        private IntPtr _powerRegistrationHandle;
        private bool _powerRegistrationSucceeded;
        private bool _wtsRegistrationSucceeded;

        private Exception? _terminalFailure;
        private Exception? _lastCleanupFailure;
        private Exception? _powerFailure;
        private Exception? _reconciliationFailure;
        private int _monitoringFailureRaised;

        private CancellationToken _startupCancellationToken;

        private bool _stopRequested;
        private bool _windowCreationCompleted;

        private int _messageLoopExited;
        private int _cleanupSucceeded;

        public Worker(
            WindowsWorkstationSecurityEventSource owner,
            IWindowsWorkstationSecurityNativeApi nativeApi,
            TimeSpan shutdownTimeout)
        {
            _owner = owner;
            _nativeApi = nativeApi;
            _shutdownTimeout = shutdownTimeout;
        }

        public Task Started => _started.Task;

        public Task Completion => _completion.Task;

        public bool CleanupSucceeded =>
            Volatile.Read(ref _cleanupSucceeded) == 1;

        public bool IsStopping =>
            IsStopRequested ||
            Volatile.Read(ref _messageLoopExited) != 0;

        public Exception? LastCleanupFailure =>
            Volatile.Read(ref _lastCleanupFailure);

        public Exception? TerminalFailure =>
            Volatile.Read(ref _terminalFailure) ??
            LastCleanupFailure;

        public void Start(CancellationToken cancellationToken)
        {
            _startupCancellationToken = cancellationToken;

            var thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "DeskVault.WorkstationSecurityNotifications"
            };

            try
            {
                thread.Start();
            }
            catch (Exception exception)
            {
                Volatile.Write(ref _cleanupSucceeded, 1);
                Volatile.Write(ref _terminalFailure, exception);

                _started.TrySetException(exception);
                _completion.TrySetException(exception);

                _cleanupRetryRequested.Dispose();
            }
        }

        public void RequestStop()
        {
            IWindowsWorkstationSecurityNativeWindow? window;
            bool wasAlreadyRequested;

            lock (_stopSyncRoot)
            {
                wasAlreadyRequested = _stopRequested;
                _stopRequested = true;

                window =
                    _windowCreationCompleted &&
                    Volatile.Read(ref _messageLoopExited) == 0
                        ? _window
                        : null;
            }

            if (window is not null)
            {
                try
                {
                    // Do not hold _stopSyncRoot while invoking a native
                    // stop request. The worker and failure callbacks must
                    // remain able to acquire that lock.
                    window.RequestStop();
                }
                catch
                {
                    if (!wasAlreadyRequested)
                    {
                        lock (_stopSyncRoot)
                        {
                            if (Volatile.Read(ref _messageLoopExited) == 0)
                            {
                                _stopRequested = false;
                            }
                        }
                    }

                    throw;
                }
            }

            RequestCleanupRetry();
        }

        public void RequestCleanupRetry()
        {
            try
            {
                _cleanupRetryRequested.Set();
            }
            catch (ObjectDisposedException)
            {
                // Cleanup already completed and the worker has exited.
            }
        }

        private bool IsStopRequested
        {
            get
            {
                lock (_stopSyncRoot)
                {
                    return _stopRequested;
                }
            }
        }

        private void OnReconciliationFailure(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            Interlocked.CompareExchange(
                ref _reconciliationFailure,
                exception,
                null);

            IWindowsWorkstationSecurityNativeWindow? window;

            lock (_stopSyncRoot)
            {
                window = _window;
            }

            if (window is null)
            {
                return;
            }

            try
            {
                window.RequestStop();
            }
            catch (Exception stopException)
            {
                NotifyMonitoringFailure(
                    new AggregateException(
                        "Session reconciliation failed and the notification window could not be stopped.",
                        exception,
                        stopException));
            }
        }

        private void OnPowerDispatcherFailure(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            Interlocked.CompareExchange(
                ref _powerFailure,
                exception,
                null);

            if (IsStopRequested)
            {
                return;
            }

            IWindowsWorkstationSecurityNativeWindow? window;

            lock (_stopSyncRoot)
            {
                window = _window;
            }

            if (window is null)
            {
                return;
            }

            try
            {
                window.RequestStop();
            }
            catch (Exception stopException)
            {
                NotifyMonitoringFailure(
                    new AggregateException(
                        "The power-notification dispatcher failed and the native notification window could not be stopped.",
                        exception,
                        stopException));
            }
        }

        private void NotifyMonitoringFailure(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            if (Interlocked.Exchange(
                    ref _monitoringFailureRaised,
                    1) == 0)
            {
                _owner.RaiseMonitoringFailed(exception);
            }
        }

        private void Run()
        {
            bool startupSucceeded = false;
            Exception? executionFailure = null;

            try
            {
                IWindowsWorkstationSecurityNativeWindow window =
                    _nativeApi.CreateHiddenTopLevelWindow(
                        _owner.OnNativeWindowMessage);

                lock (_stopSyncRoot)
                {
                    _window = window;
                    _windowCreationCompleted = true;

                    if (_stopRequested)
                    {
                        _started.TrySetCanceled();
                        return;
                    }
                }

                if (!_nativeApi.RegisterSessionNotification(
                        window.Handle,
                        NotifyForThisSession,
                        out int registrationError))
                {
                    throw new Win32Exception(
                        registrationError,
                        "WTS session notification registration failed.");
                }

                _wtsRegistrationSucceeded = true;

                var powerDispatcher =
                    new WindowsWorkstationPowerNotificationDispatcher(
                        _owner.PublishSecurityEvent,
                        OnPowerDispatcherFailure,
                        _shutdownTimeout);

                _powerDispatcher = powerDispatcher;

                powerDispatcher.Start();

                if (!_nativeApi.TryRegisterSuspendResumeNotification(
                        powerDispatcher.Callback,
                        out IntPtr powerRegistrationHandle,
                        out int powerRegistrationError))
                {
                    throw new Win32Exception(
                        powerRegistrationError,
                        "Independent Windows suspend/resume notification registration failed.");
                }

                if (powerRegistrationHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException(
                        "Power notification registration succeeded without returning a valid registration handle.");
                }

                _powerRegistrationHandle = powerRegistrationHandle;
                _powerRegistrationSucceeded = true;

                var reconciliationWorker =
                    new WindowsWorkstationSecuritySessionReconciliationWorker(
                        _nativeApi,
                        _owner.PublishSecurityEvent,
                        OnReconciliationFailure,
                        shutdownTimeout: _shutdownTimeout);

                _reconciliationWorker = reconciliationWorker;

                // Readiness requires successful native notification
                // registration and an initial validated process-session query.
                reconciliationWorker
                    .StartAsync(_startupCancellationToken)
                    .GetAwaiter()
                    .GetResult();

                startupSucceeded = true;
                _started.TrySetResult(true);

                window.RunMessageLoop();

                Exception? reconciliationFailure =
                    Volatile.Read(ref _reconciliationFailure);

                if (reconciliationFailure is not null)
                {
                    throw reconciliationFailure;
                }

                Exception? powerFailure =
                    Volatile.Read(ref _powerFailure);

                if (powerFailure is not null)
                {
                    throw powerFailure;
                }

                if (!IsStopRequested)
                {
                    throw new InvalidOperationException(
                        "The workstation notification message loop terminated unexpectedly.");
                }
            }
            catch (Exception exception)
            {
                executionFailure = exception;
                _started.TrySetException(exception);

                // Revoke monitoring readiness immediately. A failure callback
                // may already have requested message-loop termination; that
                // does not make the monitoring failure an intentional stop.
                if (startupSucceeded && !IsStopRequested)
                {
                    NotifyMonitoringFailure(exception);
                }
            }
            finally
            {
                // The worker thread remains the owner of the native window
                // and all WTS/window cleanup operations.
                Volatile.Write(ref _messageLoopExited, 1);

                CompleteAfterCleanup(
                    executionFailure,
                    startupSucceeded);
            }
        }

        private void CompleteAfterCleanup(
            Exception? executionFailure,
            bool startupSucceeded)
        {
            while (true)
            {
                Exception? cleanupFailure = TryCleanupOnce();

                if (cleanupFailure is null)
                {
                    break;
                }

                Volatile.Write(
                    ref _lastCleanupFailure,
                    cleanupFailure);

                Trace.TraceError(
                    "DeskVault workstation native cleanup remains incomplete. FailureType={0}.",
                    cleanupFailure.GetType().Name);

                // Retry only when explicitly requested. This avoids both
                // unbounded synchronous waits and a tight native retry loop.
                _cleanupRetryRequested.WaitOne();
            }

            Volatile.Write(ref _cleanupSucceeded, 1);
            Volatile.Write(ref _terminalFailure, executionFailure);

            if (executionFailure is not null)
            {
                if (startupSucceeded && !IsStopRequested)
                {
                    NotifyMonitoringFailure(executionFailure);
                }

                _completion.TrySetException(executionFailure);
            }
            else
            {
                _completion.TrySetResult(true);
            }

            _cleanupRetryRequested.Dispose();
        }

        private Exception? TryCleanupOnce()
        {
            try
            {
                // 1. Unregister the independent native power callback.
                // Retain the callback and registration handle on failure.
                if (_powerRegistrationSucceeded)
                {
                    if (!_nativeApi.TryUnregisterSuspendResumeNotification(
                            _powerRegistrationHandle,
                            out int unregistrationError))
                    {
                        throw new Win32Exception(
                            unregistrationError,
                            "Independent Windows suspend/resume notification unregistration failed.");
                    }

                    _powerRegistrationSucceeded = false;
                    _powerRegistrationHandle = IntPtr.Zero;
                }

                // 2. Native callback unregistration succeeded. Drain and
                // stop the dispatcher before releasing its callback delegate.
                WindowsWorkstationPowerNotificationDispatcher? dispatcher =
                    _powerDispatcher;

                if (dispatcher is not null)
                {
                    dispatcher.StopAsync(CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();

                    if (ReferenceEquals(_powerDispatcher, dispatcher))
                    {
                        _powerDispatcher = null;
                    }
                }

                // 3. Stop reconciliation. Its own bounded shutdown retains
                // the query worker and its synchronization resources if needed.
                WindowsWorkstationSecuritySessionReconciliationWorker?
                    reconciliationWorker = _reconciliationWorker;

                if (reconciliationWorker is not null)
                {
                    reconciliationWorker
                        .StopAsync(CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();

                    if (ReferenceEquals(
                            _reconciliationWorker,
                            reconciliationWorker))
                    {
                        _reconciliationWorker = null;
                    }
                }

                IWindowsWorkstationSecurityNativeWindow? window;

                lock (_stopSyncRoot)
                {
                    window = _window;
                }

                // 4. Unregister WTS while the original HWND is still alive.
                // On failure, retain the registration flag and HWND.
                if (_wtsRegistrationSucceeded)
                {
                    if (window is null)
                    {
                        throw new InvalidOperationException(
                            "The WTS registration is still owned but its native window reference is unavailable.");
                    }

                    if (!_nativeApi.UnregisterSessionNotification(
                            window.Handle,
                            out int unregistrationError))
                    {
                        throw new Win32Exception(
                            unregistrationError,
                            "WTS session notification unregistration failed.");
                    }

                    _wtsRegistrationSucceeded = false;
                }

                // 5. Dispose the HWND only after successful WTS
                // unregistration. NativeWindow.Dispose is retryable.
                if (window is not null)
                {
                    window.Dispose();

                    lock (_stopSyncRoot)
                    {
                        if (ReferenceEquals(_window, window))
                        {
                            _window = null;
                        }
                    }
                }

                lock (_stopSyncRoot)
                {
                    _windowCreationCompleted = true;
                }

                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }
    }
}