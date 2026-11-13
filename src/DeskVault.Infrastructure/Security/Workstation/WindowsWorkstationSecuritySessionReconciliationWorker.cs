using DeskVault.Application.Security.Workstation;
using System.ComponentModel;
using System.Diagnostics;

namespace DeskVault.Infrastructure.Security.Workstation;

/// <summary>
/// Reconciles the actual Windows session state of the DeskVault process
/// independently of workstation notification delivery.
/// </summary>
/// <remarks>
/// A single dedicated thread performs all synchronous native WTS queries.
/// Poll scheduling and query-timeout detection use independent TimeProvider
/// timers. A timed-out native query is never replaced by another query.
/// </remarks>
internal sealed class WindowsWorkstationSecuritySessionReconciliationWorker
{
    internal static readonly TimeSpan DefaultPollInterval =
        TimeSpan.FromSeconds(1);

    internal static readonly TimeSpan DefaultQueryTimeout =
        TimeSpan.FromSeconds(2);

    internal static readonly TimeSpan DefaultShutdownTimeout =
        TimeSpan.FromSeconds(2);

    private readonly IWindowsWorkstationSecurityNativeApi _nativeApi;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _queryTimeout;
    private readonly TimeSpan _shutdownTimeout;
    private readonly TimeSpan _watchdogInterval;

    private readonly Action<WorkstationSecurityEventKind> _securityEventReceived;
    private readonly Action<Exception> _monitoringFailed;

    private readonly AutoResetEvent _queryRequested = new(false);
    private readonly ManualResetEvent _stopRequested = new(false);
    private readonly WaitHandle[] _waitHandles;
    private readonly Func<int> _waitForWork;
    private readonly Func<bool> _queryThreadIsAlive;

    private readonly TaskCompletionSource<bool> _initialQueryCompleted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly SemaphoreSlim _stopGate = new(1, 1);
    private readonly object _metricsGate = new();

    private Thread? _queryThread;
    private ITimer? _pollTimer;
    private ITimer? _watchdogTimer;

    private uint? _processSessionId;
    private WindowsWorkstationSessionAccessState? _lastAccessState;

    private long _queryQueuedTimestamp;
    private long _queryStartedTimestamp;
    private long _lastQueryStartedTimestamp;

    private TimeSpan? _lastQueryQueueDelay;
    private TimeSpan? _lastQueryDuration;
    private TimeSpan? _lastQueryStartInterval;

    private int _queryQueued;
    private int _queryExecutionStarted;

    private int _queryInProgress;
    private int _started;
    private int _threadStarted;
    private int _stopping;
    private int _failureRaised;
    private int _disposed;

    public WindowsWorkstationSecuritySessionReconciliationWorker(
        IWindowsWorkstationSecurityNativeApi nativeApi,
        Action<WorkstationSecurityEventKind> securityEventReceived,
        Action<Exception> monitoringFailed,
        TimeProvider? timeProvider = null,
        TimeSpan? pollInterval = null,
        TimeSpan? queryTimeout = null,
        TimeSpan? shutdownTimeout = null,
        Func<int>? waitForWork = null,
        Func<bool>? queryThreadIsAlive = null)
    {
        ArgumentNullException.ThrowIfNull(nativeApi);
        ArgumentNullException.ThrowIfNull(securityEventReceived);
        ArgumentNullException.ThrowIfNull(monitoringFailed);

        _nativeApi = nativeApi;
        _securityEventReceived = securityEventReceived;
        _monitoringFailed = monitoringFailed;
        _timeProvider = timeProvider ?? TimeProvider.System;

        _pollInterval = pollInterval ?? DefaultPollInterval;
        _queryTimeout = queryTimeout ?? DefaultQueryTimeout;
        _shutdownTimeout = shutdownTimeout ?? DefaultShutdownTimeout;

        if (_pollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pollInterval),
                "The reconciliation interval must be positive.");
        }

        if (_queryTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(queryTimeout),
                "The native query timeout must be positive.");
        }

        if (_shutdownTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(shutdownTimeout),
                "The shutdown timeout must be positive.");
        }

        _watchdogInterval = CalculateWatchdogInterval(_queryTimeout);

        _waitHandles =
        [
            _stopRequested,
            _queryRequested
        ];

        _waitForWork =
            waitForWork ?? (() => WaitHandle.WaitAny(_waitHandles));

        _queryThreadIsAlive =
            queryThreadIsAlive ?? (() => _queryThread?.IsAlive == true);
    }

    /// <summary>
    /// Gets the duration of the most recently completed WTS reconciliation.
    /// </summary>
    internal TimeSpan? LastQueryDuration
    {
        get
        {
            lock (_metricsGate)
            {
                return _lastQueryDuration;
            }
        }
    }

    /// <summary>
    /// Gets the delay between scheduling a reconciliation query and
    /// the dedicated query thread actually beginning it.
    /// </summary>
    internal TimeSpan? LastQueryQueueDelay
    {
        get
        {
            lock (_metricsGate)
            {
                return _lastQueryQueueDelay;
            }
        }
    }

    /// <summary>
    /// Gets the interval between the two most recently started WTS queries.
    /// The timestamps are captured by the query thread at execution start,
    /// not when the timer schedules the work.
    /// </summary>
    internal TimeSpan? LastQueryStartInterval
    {
        get
        {
            lock (_metricsGate)
            {
                return _lastQueryStartInterval;
            }
        }
    }

    /// <summary>
    /// Starts the dedicated query thread and waits for the first validated
    /// process-session snapshot.
    /// </summary>
    internal bool IsQueryInProgress =>
        Volatile.Read(ref _queryInProgress) != 0;

    public async Task StartAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "The workstation session reconciliation worker has already been started.");
        }

        try
        {
            var thread =
                new Thread(RunQueryLoop)
                {
                    IsBackground = true,
                    Name = "DeskVault.WorkstationSessionReconciliation"
                };

            _queryThread = thread;

            thread.Start();

            Volatile.Write(ref _threadStarted, 1);

            _pollTimer =
                _timeProvider.CreateTimer(
                    OnPollTimer,
                    state: null,
                    dueTime: _pollInterval,
                    period: _pollInterval);

            _watchdogTimer =
                _timeProvider.CreateTimer(
                    OnWatchdogTimer,
                    state: null,
                    dueTime: _watchdogInterval,
                    period: _watchdogInterval);

            if (!TryQueueQuery())
            {
                throw new InvalidOperationException(
                    "The initial workstation session query could not be scheduled.");
            }

            TimeSpan startupTimeout =
                _queryTimeout +
                TimeSpan.FromTicks(_watchdogInterval.Ticks * 2);

            await _initialQueryCompleted.Task
                .WaitAsync(
                    startupTimeout,
                    _timeProvider,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            RequestStop();
            throw;
        }
        catch (TimeoutException exception)
        {
            var timeout =
                new TimeoutException(
                    "The initial Windows session-state query did not complete within its allowed time.",
                    exception);

            SignalFailure(timeout);
            throw timeout;
        }
        catch (Exception exception)
        {
            SignalFailure(exception);
            throw;
        }
    }

    /// <summary>
    /// Stops scheduling and waits for the dedicated query thread to exit.
    /// A blocked native query causes a bounded failure; the worker's
    /// resources remain retained so a later call can retry cleanup.
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

            RequestStop();

            await DisposeTimersAsync()
                .ConfigureAwait(false);

            Thread? thread = _queryThread;

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
                    // Do not dispose synchronization objects while the
                    // native query thread can still be using them.
                    throw new InvalidOperationException(
                        "The Windows session query did not stop within the bounded shutdown period. Its worker and resources remain retained.");
                }
            }

            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _queryRequested.Dispose();
                _stopRequested.Dispose();
            }
        }
        finally
        {
            _stopGate.Release();
        }
    }

    private void RunQueryLoop()
    {
        try
        {
            while (true)
            {
                int signalledHandle =
                    _waitForWork();

                if (signalledHandle == 0 ||
                    Volatile.Read(ref _stopping) != 0)
                {
                    return;
                }

                if (signalledHandle != 1)
                {
                    throw new InvalidOperationException(
                        "The workstation reconciliation worker received an invalid wait result.");
                }

                try
                {
                    // Capture actual execution start on the dedicated
                    // query thread, immediately before native session work.
                    long startedTimestamp =
                        _timeProvider.GetTimestamp();

                    long queuedTimestamp =
                        Interlocked.Read(ref _queryQueuedTimestamp);

                    long previousStart =
                        Interlocked.Exchange(
                            ref _lastQueryStartedTimestamp,
                            startedTimestamp);

                    TimeSpan queueDelay =
                        _timeProvider.GetElapsedTime(
                            queuedTimestamp,
                            startedTimestamp);

                    Interlocked.Exchange(
                        ref _queryStartedTimestamp,
                        startedTimestamp);

                    Volatile.Write(ref _queryExecutionStarted, 1);
                    Volatile.Write(ref _queryQueued, 0);

                    TimeSpan? queryStartInterval =
                        previousStart == 0
                            ? null
                            : _timeProvider.GetElapsedTime(
                                previousStart,
                                startedTimestamp);

                    lock (_metricsGate)
                    {
                        _lastQueryQueueDelay = queueDelay;
                        _lastQueryStartInterval = queryStartInterval;
                    }

                    WindowsWorkstationSecuritySessionState state =
                        QueryProcessSession();

                    long completedTimestamp =
                        _timeProvider.GetTimestamp();

                    TimeSpan queryDuration =
                        _timeProvider.GetElapsedTime(
                            startedTimestamp,
                            completedTimestamp);

                    lock (_metricsGate)
                    {
                        _lastQueryDuration = queryDuration;
                    }

                    Trace.TraceInformation(
                        "DeskVault workstation session reconciliation completed. " +
                        "ProcessSessionId={0}; AccessState={1}; QueueDelayMs={2:F1}; QueryLatencyMs={3:F1}; QueryStartIntervalMs={4}.",
                        state.ProcessSessionId,
                        state.AccessState,
                        queueDelay.TotalMilliseconds,
                        queryDuration.TotalMilliseconds,
                        queryStartInterval is TimeSpan interval
                            ? interval.TotalMilliseconds.ToString("F1")
                            : "initial");

                    if (Volatile.Read(ref _stopping) != 0)
                    {
                        return;
                    }

                    PublishProtectiveTransition(state);

                    _initialQueryCompleted.TrySetResult(true);
                }
                catch (Exception exception)
                {
                    SignalFailure(exception);
                    return;
                }
                finally
                {
                    Volatile.Write(ref _queryInProgress, 0);
                }
            }
        }
        catch (Exception exception)
        {
            if (Volatile.Read(ref _stopping) == 0)
            {
                SignalFailure(exception);
            }
        }
        finally
        {
            // Every normal exit must have been requested by StopAsync or
            // accompanied by a reported failure. An unexplained exit is
            // itself a monitoring failure.
            if (Volatile.Read(ref _stopping) == 0 &&
                Volatile.Read(ref _failureRaised) == 0)
            {
                SignalFailure(
                    new InvalidOperationException(
                        "The Windows workstation session reconciliation worker terminated unexpectedly."));
            }
        }
    }

    private WindowsWorkstationSecuritySessionState QueryProcessSession()
    {
        if (!_nativeApi.TryGetProcessSessionId(
                out uint processSessionId,
                out int processSessionError))
        {
            throw new Win32Exception(
                processSessionError,
                "Unable to identify the Windows session that owns the DeskVault process.");
        }

        if (_processSessionId is uint expectedSessionId &&
            expectedSessionId != processSessionId)
        {
            throw new InvalidDataException(
                "The DeskVault process session identifier changed while workstation monitoring was active.");
        }

        _processSessionId ??= processSessionId;

        if (!_nativeApi.TryQuerySessionInformation(
                processSessionId,
                out WindowsWorkstationSecuritySessionStateSnapshot snapshot,
                out int queryError))
        {
            throw new Win32Exception(
                queryError,
                "Unable to query the DeskVault process session through WTS.");
        }

        return WindowsWorkstationSecuritySessionStateParser.Parse(
            snapshot,
            WindowsWorkstationSecurityNativeApi.WtsInfoExStructureSize,
            processSessionId);
    }

    private void PublishProtectiveTransition(
        WindowsWorkstationSecuritySessionState state)
    {
        if (_lastAccessState == state.AccessState)
        {
            return;
        }

        _lastAccessState = state.AccessState;

        switch (state.AccessState)
        {
            case WindowsWorkstationSessionAccessState.Locked:
                _securityEventReceived(
                    WorkstationSecurityEventKind.SessionLocked);
                break;

            case WindowsWorkstationSessionAccessState.Disconnected:
                _securityEventReceived(
                    WorkstationSecurityEventKind.SessionDisconnected);
                break;

            case WindowsWorkstationSessionAccessState.Unlocked:
                // An unlocked operating-system session is observational
                // only. It must never authorize or unlock DeskVault.
                break;

            default:
                throw new InvalidDataException(
                    "The validated workstation session returned an unsupported access state.");
        }
    }

    private void OnPollTimer(object? state)
    {
        if (Volatile.Read(ref _stopping) != 0 ||
            Volatile.Read(ref _failureRaised) != 0)
        {
            return;
        }

        _ = TryQueueQuery();
    }

    private void OnWatchdogTimer(object? state)
    {
        if (Volatile.Read(ref _stopping) != 0 ||
            Volatile.Read(ref _failureRaised) != 0)
        {
            return;
        }

        // Worker liveness is checked independently of whether a native
        // query is currently running. A dead reconciliation worker must
        // not leave workstation readiness silently Ready while idle.
        if (Volatile.Read(ref _threadStarted) == 1 &&
            !_queryThreadIsAlive())
        {
            SignalFailure(
                new InvalidOperationException(
                    "The Windows workstation session reconciliation worker terminated unexpectedly."));

            return;
        }

        if (Volatile.Read(ref _queryInProgress) == 0)
        {
            return;
        }

        long now = _timeProvider.GetTimestamp();

        if (Volatile.Read(ref _queryExecutionStarted) == 0)
        {
            // The query is scheduled but the dedicated thread has not
            // begun execution. Measure and diagnose queue delay separately.
            if (Volatile.Read(ref _queryQueued) == 0)
            {
                return;
            }

            long queuedTimestamp =
                Interlocked.Read(ref _queryQueuedTimestamp);

            TimeSpan queueDelay =
                _timeProvider.GetElapsedTime(
                    queuedTimestamp,
                    now);

            if (queueDelay >= _queryTimeout)
            {
                SignalFailure(
                    new TimeoutException(
                        $"A Windows workstation session query did not begin execution within its {_queryTimeout.TotalMilliseconds:F0} ms scheduling deadline."));
            }

            return;
        }

        // The query has started. Its timeout is measured only from the
        // actual execution timestamp, not from the earlier queue timestamp.
        long startedTimestamp =
            Interlocked.Read(ref _queryStartedTimestamp);

        TimeSpan queryElapsed =
            _timeProvider.GetElapsedTime(
                startedTimestamp,
                now);

        if (queryElapsed >= _queryTimeout)
        {
            SignalFailure(
                new TimeoutException(
                    $"A Windows workstation session query exceeded its {_queryTimeout.TotalMilliseconds:F0} ms execution deadline."));
        }
    }

    private bool TryQueueQuery()
    {
        if (Volatile.Read(ref _stopping) != 0 ||
            Volatile.Read(ref _failureRaised) != 0 ||
            Volatile.Read(ref _disposed) != 0)
        {
            return false;
        }

        if (Interlocked.CompareExchange(
                ref _queryInProgress,
                1,
                0) != 0)
        {
            // The previous query is still running. Never overlap it or
            // create a replacement query worker.
            return false;
        }

        // Scheduling time is deliberately not treated as query start.
        // The dedicated query thread records execution timestamps.
        long queuedTimestamp =
            _timeProvider.GetTimestamp();

        Volatile.Write(ref _queryExecutionStarted, 0);
        Volatile.Write(ref _queryQueued, 0);

        Interlocked.Exchange(
            ref _queryStartedTimestamp,
            0);

        Interlocked.Exchange(
            ref _queryQueuedTimestamp,
            queuedTimestamp);

        Volatile.Write(ref _queryQueued, 1);

        try
        {
            _queryRequested.Set();
            return true;
        }
        catch (ObjectDisposedException)
        {
            Volatile.Write(ref _queryInProgress, 0);
            return false;
        }
    }

    private void RequestStop()
    {
        if (Interlocked.Exchange(ref _stopping, 1) == 0)
        {
            try
            {
                _stopRequested.Set();
            }
            catch (ObjectDisposedException)
            {
                // The thread has already been joined and its resources
                // have been released.
            }
        }
    }

    private void SignalFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (Interlocked.Exchange(ref _failureRaised, 1) != 0)
        {
            return;
        }

        _initialQueryCompleted.TrySetException(exception);

        DisableTimers();
        RequestStop();

        try
        {
            _monitoringFailed(exception);
        }
        catch (Exception callbackException)
        {
            Trace.TraceError(
                "The workstation reconciliation failure callback threw: {0}",
                callbackException);
        }
    }

    private void DisableTimers()
    {
        try
        {
            _pollTimer?.Change(
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            // Timer disposal is already in progress.
        }

        try
        {
            _watchdogTimer?.Change(
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            // Timer disposal is already in progress.
        }
    }

    private async ValueTask DisposeTimersAsync()
    {
        ITimer? pollTimer = _pollTimer;
        _pollTimer = null;

        if (pollTimer is not null)
        {
            await pollTimer.DisposeAsync()
                .ConfigureAwait(false);
        }

        ITimer? watchdogTimer = _watchdogTimer;
        _watchdogTimer = null;

        if (watchdogTimer is not null)
        {
            await watchdogTimer.DisposeAsync()
                .ConfigureAwait(false);
        }
    }

    private static TimeSpan CalculateWatchdogInterval(
        TimeSpan queryTimeout)
    {
        long ticks =
            Math.Max(
                TimeSpan.FromMilliseconds(5).Ticks,
                Math.Min(
                    TimeSpan.FromMilliseconds(100).Ticks,
                    queryTimeout.Ticks / 4));

        return TimeSpan.FromTicks(ticks);
    }
}
