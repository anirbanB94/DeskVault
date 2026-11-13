using DeskVault.Application.Security.Workstation;
using System.Diagnostics;

namespace DeskVault.Infrastructure.Security.Workstation;

/// <summary>
/// Dispatches native Windows suspend/resume callbacks away from the
/// unmanaged callback stack onto one bounded, dedicated worker thread.
/// </summary>
internal sealed class WindowsWorkstationPowerNotificationDispatcher
{
    private const uint PowerSuspend = 0x0004;
    private const uint PowerResumeCritical = 0x0006;
    private const uint PowerResumeSuspend = 0x0007;
    private const uint PowerResumeAutomatic = 0x0012;

    private const uint ErrorSuccess = 0;
    private const uint ErrorInvalidHandle = 6;

    private readonly Action<WorkstationSecurityEventKind> _eventReceived;
    private readonly TimeSpan _shutdownTimeout;

    private readonly AutoResetEvent _notificationAvailable = new(false);
    private readonly ManualResetEvent _stopRequested = new(false);
    private readonly WaitHandle[] _waitHandles;

    private readonly SemaphoreSlim _stopGate = new(1, 1);

    private readonly WindowsWorkstationPowerNotificationCallback _callback;

    private const uint ErrorGenFailure = 31;

    private readonly Action<Exception>? _monitoringFailed;
    private readonly Action _signalNotification;
    private readonly object _callbackSyncRoot = new();

    private readonly ManualResetEventSlim _callbacksDrained = new(true);

    private int _callbacksInFlight;
    private int _failureReported;

    private Thread? _thread;

    private int _pendingSuspend;
    private int _pendingResume;
    private int _started;
    private int _threadStarted;
    private int _stopping;
    private int _disposed;

    public WindowsWorkstationPowerNotificationDispatcher(
        Action<WorkstationSecurityEventKind> eventReceived,
        Action<Exception>? monitoringFailed = null,
        TimeSpan? shutdownTimeout = null,
        Action? signalNotification = null)
    {
        ArgumentNullException.ThrowIfNull(eventReceived);

        _eventReceived = eventReceived;
        _monitoringFailed = monitoringFailed;
        _signalNotification =
            signalNotification ?? (() => _notificationAvailable.Set());
        _shutdownTimeout =
            shutdownTimeout ?? TimeSpan.FromSeconds(2);

        if (_shutdownTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(shutdownTimeout),
                "The power dispatcher shutdown timeout must be positive.");
        }

        _waitHandles =
        [
            _stopRequested,
            _notificationAvailable
        ];

        // Retain a stable delegate for the complete registration lifetime.
        _callback = OnNativeNotification;
    }

    /// <summary>
    /// Gets the stable native callback delegate.
    /// </summary>
    public WindowsWorkstationPowerNotificationCallback Callback =>
        _callback;

    /// <summary>
    /// Starts the dedicated dispatcher thread.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);

        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "The workstation power-notification dispatcher has already been started.");
        }

        var thread =
            new Thread(Run)
            {
                IsBackground = true,
                Name = "DeskVault.WorkstationPowerNotifications"
            };

        _thread = thread;

        try
        {
            thread.Start();
            Volatile.Write(ref _threadStarted, 1);
        }
        catch
        {
            _thread = null;
            Volatile.Write(ref _started, 0);
            throw;
        }
    }

    /// <summary>
    /// Stops the dispatcher and waits for its thread to terminate.
    /// A blocked event subscriber causes bounded failure. The thread and
    /// synchronization resources remain retained for a subsequent retry.
    /// </summary>
    public async Task StopAsync(
        CancellationToken cancellationToken = default)
    {
        await _stopGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            if (Volatile.Read(ref _disposed) == 1)
            {
                return;
            }

            // Prevent new callbacks from entering and establish a stable
            // boundary for draining callbacks already in progress.
            lock (_callbackSyncRoot)
            {
                Volatile.Write(ref _stopping, 1);

                if (_callbacksInFlight == 0)
                {
                    _callbacksDrained.Set();
                }
            }

            _stopRequested.Set();

            bool callbacksDrained =
                await Task.Run(
                        () => _callbacksDrained.Wait(_shutdownTimeout),
                        CancellationToken.None)
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);

            if (!callbacksDrained)
            {
                throw new InvalidOperationException(
                    "Native workstation power callbacks did not drain within the bounded shutdown period. Their dispatcher resources remain retained.");
            }

            Thread? thread = _thread;

            if (thread is not null &&
                Volatile.Read(ref _threadStarted) == 1)
            {
                bool joined =
                    await Task.Run(
                            () => thread.Join(_shutdownTimeout),
                            CancellationToken.None)
                        .WaitAsync(cancellationToken)
                        .ConfigureAwait(false);

                if (!joined)
                {
                    throw new InvalidOperationException(
                        "The workstation power-notification dispatcher did not stop within the bounded shutdown period. Its worker and synchronization resources remain retained.");
                }
            }

            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _notificationAvailable.Dispose();
                _stopRequested.Dispose();
                _callbacksDrained.Dispose();
            }
        }
        finally
        {
            _stopGate.Release();
        }
    }

    /// <summary>
    /// Native callback entry point. It performs only bounded atomic updates
    /// and a signal operation. It never invokes application subscribers.
    /// </summary>
    private uint OnNativeNotification(
        IntPtr context,
        uint notificationType,
        IntPtr setting)
    {
        // Increment the in-flight count under the same lock used by
        // StopAsync. This prevents shutdown from observing a drained
        // callback set while a callback is entering.
        lock (_callbackSyncRoot)
        {
            if (Volatile.Read(ref _stopping) != 0 ||
                Volatile.Read(ref _disposed) != 0)
            {
                return ErrorSuccess;
            }

            _callbacksInFlight++;

            if (_callbacksInFlight == 1)
            {
                _callbacksDrained.Reset();
            }
        }

        try
        {
            switch (notificationType)
            {
                case PowerSuspend:
                    Interlocked.Exchange(ref _pendingSuspend, 1);
                    break;

                case PowerResumeCritical:
                case PowerResumeSuspend:
                case PowerResumeAutomatic:
                    Interlocked.Exchange(ref _pendingResume, 1);
                    break;

                default:
                    return ErrorSuccess;
            }

            _signalNotification();

            return ErrorSuccess;
        }
        catch (Exception exception)
        {
            // Do not let managed exceptions cross the unmanaged callback.
            // Report the monitoring failure through the established failure
            // path; an enqueue/signal failure must never be silently ignored.
            Trace.TraceError(
                "The native workstation power callback failed: {0}",
                exception);

            // The native callback must not invoke the event-source failure
            // handler or any application lifecycle work on this stack.
            QueueFailure(exception);

            return ErrorGenFailure;
        }
        finally
        {
            lock (_callbackSyncRoot)
            {
                _callbacksInFlight--;

                if (_callbacksInFlight == 0)
                {
                    _callbacksDrained.Set();
                }
            }
        }
    }

    private void Run()
    {
        try
        {
            while (true)
            {
                int signalledHandle =
                    WaitHandle.WaitAny(_waitHandles);

                if (signalledHandle == 0)
                {
                    DrainPendingNotifications();
                    return;
                }

                DrainPendingNotifications();
            }
        }
        catch (Exception exception)
        {
            // Event-delivery failures terminate this dispatcher generation
            // and notify the event source so Application readiness can fail closed.
            ReportFailure(exception);
        }
    }

    private void DrainPendingNotifications()
    {
        while (true)
        {
            bool hasPendingNotification = false;

            // Suspend is always delivered before resume when both
            // notifications are pending.
            if (Interlocked.Exchange(ref _pendingSuspend, 0) != 0)
            {
                hasPendingNotification = true;

                Dispatch(
                    WorkstationSecurityEventKind.SystemSuspending);
            }

            if (Interlocked.Exchange(ref _pendingResume, 0) != 0)
            {
                hasPendingNotification = true;

                Dispatch(
                    WorkstationSecurityEventKind.SystemResumed);
            }

            if (!hasPendingNotification)
            {
                return;
            }
        }
    }

    private void Dispatch(
        WorkstationSecurityEventKind kind)
    {
        // Let delivery failures reach Run(), which reports a monitoring
        // failure and terminates this dispatcher generation.
        _eventReceived(kind);
    }

    private void ReportFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (Interlocked.Exchange(ref _failureReported, 1) != 0)
        {
            return;
        }

        NotifyMonitoringFailure(exception);
    }

    private void QueueFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (Interlocked.Exchange(ref _failureReported, 1) != 0)
        {
            return;
        }

        bool queued = false;

        try
        {
            queued = ThreadPool.QueueUserWorkItem(
                static state =>
                {
                    var workItem = (PowerFailureWorkItem)state!;
                    workItem.Dispatcher.NotifyMonitoringFailure(
                        workItem.Exception);
                },
                new PowerFailureWorkItem(this, exception));
        }
        catch (Exception queueException)
        {
            Trace.TraceError(
                "The workstation power callback could not queue its failure notification: {0}",
                queueException);
        }

        if (queued)
        {
            return;
        }

        // Scheduling is already failing. Use a dedicated background thread
        // as an exceptional fallback; never invoke application work inline
        // on the unmanaged callback stack.
        try
        {
            var fallbackThread = new Thread(
                () => NotifyMonitoringFailure(exception))
            {
                IsBackground = true,
                Name = "DeskVault.WorkstationPowerFailure"
            };

            fallbackThread.Start();
        }
        catch (Exception fallbackException)
        {
            Trace.TraceError(
                "The workstation power callback failure could not be dispatched. OriginalFailure={0}; DispatchFailure={1}",
                exception,
                fallbackException);
        }
    }

    private void NotifyMonitoringFailure(Exception exception)
    {
        try
        {
            _monitoringFailed?.Invoke(exception);
        }
        catch (Exception callbackException)
        {
            Trace.TraceError(
                "The workstation power-dispatcher failure callback failed: {0}",
                callbackException);
        }
    }

    private sealed record PowerFailureWorkItem(
        WindowsWorkstationPowerNotificationDispatcher Dispatcher,
        Exception Exception);
}
