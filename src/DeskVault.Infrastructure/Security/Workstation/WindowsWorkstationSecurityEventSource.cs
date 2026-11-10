using System.ComponentModel;
using System.Diagnostics;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Workstation;

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

    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly IWindowsWorkstationSecurityNativeApi _nativeApi;

    private Worker? _worker;

    public WindowsWorkstationSecurityEventSource()
        : this(new WindowsWorkstationSecurityNativeApi())
    {
    }

    internal WindowsWorkstationSecurityEventSource(
        IWindowsWorkstationSecurityNativeApi nativeApi)
    {
        ArgumentNullException.ThrowIfNull(nativeApi);

        _nativeApi = nativeApi;
    }

    /// <inheritdoc />
    public event EventHandler<WorkstationSecurityEventArgs>? SecurityEventReceived;

    /// <inheritdoc />
    public event EventHandler<WorkstationSecurityEventSourceFailureEventArgs>? MonitoringFailed;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
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
                    if (_worker.Started.IsCompletedSuccessfully)
                    {
                        // Starting an already-ready source is idempotent.
                        return;
                    }

                    throw new InvalidOperationException(
                        "Workstation security notification startup is already in progress.");
                }

                if (!_worker.CleanupSucceeded)
                {
                    throw new InvalidOperationException(
                        "A previous workstation notification worker did not release all native resources.",
                        _worker.TerminalFailure);
                }

                // A worker that ended unexpectedly can be restarted only after
                // its WTS registration and native window were both released.
                _worker = null;
            }

            var worker = new Worker(this, _nativeApi);
            _worker = worker;
            worker.Start();

            try
            {
                await worker.Started
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception startupException)
            {
                // StartAsync owns a partially initialized worker until its
                // cleanup has finished. A failed stop request must not be
                // followed by an unbounded wait for a worker that may still
                // be blocked in its native message loop.
                Exception? stopRequestFailure = null;

                try
                {
                    worker.RequestStop();
                }
                catch (Exception exception)
                {
                    stopRequestFailure = exception;
                }

                if (stopRequestFailure is not null &&
                    !worker.Completion.IsCompleted)
                {
                    // Retain the worker reference. A later StopAsync call can
                    // retry shutdown, and StartAsync must not create a second
                    // native notification window while the first may remain.
                    throw new AggregateException(
                        "Workstation notification startup failed and worker shutdown could not be requested. The worker is retained to prevent an unsafe restart.",
                        startupException,
                        stopRequestFailure);
                }

                Exception? completionFailure = null;

                try
                {
                    await worker.Completion.ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    completionFailure = exception;
                }

                if (worker.CleanupSucceeded && ReferenceEquals(_worker, worker))
                {
                    _worker = null;
                }

                if (!worker.CleanupSucceeded)
                {
                    Exception cleanupFailure =
                        worker.TerminalFailure ??
                        completionFailure ??
                        new InvalidOperationException(
                            "The workstation notification worker did not confirm native resource cleanup.");

                    throw new AggregateException(
                        "Workstation notification startup failed and native cleanup was incomplete.",
                        startupException,
                        cleanupFailure);
                }

                // Preserve the original startup or cancellation exception
                // when the worker's native cleanup completed successfully.
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            Worker? worker = _worker;

            if (worker is null)
            {
                return;
            }

            if (!worker.Completion.IsCompleted)
            {
                try
                {
                    worker.RequestStop();
                }
                catch (Exception requestException)
                {
                    if (!worker.Completion.IsCompleted)
                    {
                        // Do not wait indefinitely when neither native stop
                        // notification could be posted. Keep the worker so a
                        // subsequent StopAsync can retry the request.
                        throw new InvalidOperationException(
                            "The workstation notification worker could not be signalled to stop. Its resources remain owned by the retained worker.",
                            requestException);
                    }
                }
            }

            try
            {
                await worker.Completion.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A runtime message-loop failure has already been surfaced
                // through MonitoringFailed. Stop succeeds when native cleanup
                // nevertheless completed successfully.
            }

            if (!worker.CleanupSucceeded)
            {
                throw new InvalidOperationException(
                    "Workstation security notification resources could not be released cleanly.",
                    worker.TerminalFailure);
            }

            if (ReferenceEquals(_worker, worker))
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

        if (kind is null)
        {
            return;
        }

        EventHandler<WorkstationSecurityEventArgs>? handlers =
            SecurityEventReceived;

        if (handlers is null)
        {
            return;
        }

        var eventArgs = new WorkstationSecurityEventArgs(kind.Value);

        foreach (EventHandler<WorkstationSecurityEventArgs> handler
            in handlers.GetInvocationList())
        {
            try
            {
                handler(this, eventArgs);
            }
            catch (Exception)
            {
                // Subscriber failures must not escape a native window
                // procedure or terminate the workstation message loop.
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

        private readonly TaskCompletionSource<bool> _started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly object _stopSyncRoot = new();

        private IWindowsWorkstationSecurityNativeWindow? _window;
        private Exception? _terminalFailure;
        private bool _stopRequested;
        private bool _windowCreationCompleted;
        private int _cleanupSucceeded;

        public Worker(
            WindowsWorkstationSecurityEventSource owner,
            IWindowsWorkstationSecurityNativeApi nativeApi)
        {
            _owner = owner;
            _nativeApi = nativeApi;
        }

        public Task Started => _started.Task;

        public Task Completion => _completion.Task;

        public bool CleanupSucceeded =>
            Volatile.Read(ref _cleanupSucceeded) == 1;

        public Exception? TerminalFailure =>
            Volatile.Read(ref _terminalFailure);

        public void Start()
        {
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
            }
        }

        public void RequestStop()
        {
            lock (_stopSyncRoot)
            {
                if (_window is null)
                {
                    // If creation has not finished, remember the request so
                    // Run can avoid registering a window that is already being
                    // stopped. If creation and cleanup have finished, do not
                    // suppress a pending runtime-failure notification.
                    if (!_windowCreationCompleted)
                    {
                        _stopRequested = true;
                    }

                    return;
                }

                try
                {
                    _window.RequestStop();
                    _stopRequested = true;
                }
                catch
                {
                    // A failed native stop request is not a completed stop.
                    // Leave the worker eligible to report an unexpected loop
                    // exit, and allow a later caller to retry the request.
                    _stopRequested = false;
                    throw;
                }
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

        private void Run()
        {
            IWindowsWorkstationSecurityNativeWindow? window = null;
            bool registrationSucceeded = false;
            bool startupSucceeded = false;
            Exception? executionFailure = null;
            Exception? cleanupFailure = null;

            try
            {
                window =
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

                registrationSucceeded = true;
                startupSucceeded = true;
                _started.TrySetResult(true);

                // Once the window has been published under _stopSyncRoot,
                // RequestStop posts directly to it. A stop request arriving
                // during WTS registration is therefore queued before the
                // message loop starts.
                window.RunMessageLoop();

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
            }
            finally
            {
                // Serialize native cleanup with RequestStop. Once the window
                // reference is cleared, a late stop call must not mark an
                // already-failed message loop as an intentional shutdown.
                lock (_stopSyncRoot)
                {
                    if (registrationSucceeded && window is not null)
                    {
                        try
                        {
                            if (!_nativeApi.UnregisterSessionNotification(
                                    window.Handle,
                                    out int unregistrationError))
                            {
                                throw new Win32Exception(
                                    unregistrationError,
                                    "WTS session notification unregistration failed.");
                            }
                        }
                        catch (Exception exception)
                        {
                            cleanupFailure = exception;
                        }
                    }

                    if (window is not null)
                    {
                        try
                        {
                            window.Dispose();
                        }
                        catch (Exception exception)
                        {
                            cleanupFailure = CombineFailures(
                                cleanupFailure,
                                exception);
                        }
                    }

                    _window = null;
                    _windowCreationCompleted = true;
                }

                Volatile.Write(
                    ref _cleanupSucceeded,
                    cleanupFailure is null ? 1 : 0);

                Exception? terminalFailure =
                    CombineFailures(
                        executionFailure,
                        cleanupFailure);

                Volatile.Write(ref _terminalFailure, terminalFailure);

                if (terminalFailure is not null)
                {
                    if (startupSucceeded && !IsStopRequested)
                    {
                        _owner.RaiseMonitoringFailed(terminalFailure);
                    }

                    _completion.TrySetException(terminalFailure);
                }
                else
                {
                    _completion.TrySetResult(true);
                }
            }
        }

        private static Exception? CombineFailures(
            Exception? first,
            Exception? second)
        {
            if (first is null)
            {
                return second;
            }

            if (second is null)
            {
                return first;
            }

            return new AggregateException(first, second);
        }
    }
}
