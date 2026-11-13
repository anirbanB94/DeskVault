using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.Shared.Extensions;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DeskVault.Application.Security.Workstation;

/// <summary>
/// Coordinates workstation security notifications and monitoring readiness
/// with the authoritative Application security-session lifecycle.
/// </summary>
/// <remarks>
/// The monitor does not own authorization state. It delegates locking to
/// <see cref="IApplicationSecuritySessionLifecycleService"/> and uses the
/// authoritative session only to move a failed monitoring condition to
/// Unavailable or to restore an Unavailable session to Locked after the
/// required monitoring capability has recovered.
///
/// A readiness revision fences authentication work that started while an
/// earlier monitoring generation was ready. Only the Ready state permits
/// authentication.
/// </remarks>
public sealed class WorkstationSecurityMonitor : IWorkstationSecurityMonitor
{
    private const string SafeFailureMessage =
        "Workstation security monitoring is unavailable.";

    private const string SafeStopFailureMessage =
        "Workstation security monitoring could not release its event source cleanly.";

    private readonly object _syncRoot = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    private readonly IWorkstationSecurityEventSource _eventSource;
    private readonly IApplicationSecuritySession _session;
    private readonly Func<IApplicationSecuritySessionLifecycleService> _lifecycleServiceFactory;
    private readonly ILogger<WorkstationSecurityMonitor> _logger;

    private WorkstationSecurityReadinessSnapshot _currentSnapshot =
        new(WorkstationSecurityReadinessState.NotStarted, revision: 0);

    private EventHandler<WorkstationSecurityEventArgs>? _securityEventHandler;
    private EventHandler<WorkstationSecurityEventSourceFailureEventArgs>? _failureEventHandler;

    private long _sourceGeneration;
    private bool _sourceStopped = true;

    public WorkstationSecurityMonitor(
        IWorkstationSecurityEventSource eventSource,
        IApplicationSecuritySession session,
        Func<IApplicationSecuritySessionLifecycleService> lifecycleServiceFactory,
        ILogger<WorkstationSecurityMonitor> logger)
    {
        ArgumentNullException.ThrowIfNull(eventSource);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(lifecycleServiceFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _eventSource = eventSource;
        _session = session;
        _lifecycleServiceFactory = lifecycleServiceFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public WorkstationSecurityReadinessSnapshot CurrentSnapshot
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentSnapshot;
            }
        }
    }

    /// <inheritdoc />
    public event Action<WorkstationSecurityReadinessSnapshot>? ReadinessChanged;

    /// <inheritdoc />
    public async Task<bool> TryStartAsync(
        CancellationToken cancellationToken = default)
    {
        await _operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            long generation;
            WorkstationSecurityReadinessSnapshot startingSnapshot;

            lock (_syncRoot)
            {
                if (_currentSnapshot.State == WorkstationSecurityReadinessState.Ready)
                {
                    return true;
                }

                if (_currentSnapshot.State == WorkstationSecurityReadinessState.Stopped)
                {
                    throw new InvalidOperationException(
                        "Stopped workstation security monitoring cannot be restarted.");
                }

                generation = checked(_sourceGeneration + 1);
                _sourceGeneration = generation;
                DetachSourceHandlersLocked();
                startingSnapshot = TransitionLocked(
                    WorkstationSecurityReadinessState.Starting);
            }

            PublishReadinessChanged(startingSnapshot);

            try
            {
                // Clean up a previous partial registration before creating
                // a new source generation. StopAsync is required to be safe
                // when the preceding StartAsync did not complete.
                if (!_sourceStopped)
                {
                    await _eventSource
                        .StopAsync(CancellationToken.None)
                        .ConfigureAwait(false);

                    _sourceStopped = true;
                }

                AttachSourceHandlers(generation);

                _sourceStopped = false;

                // A successful return means the source has completed native
                // registration and is ready to receive notifications.
                await _eventSource
                    .StartAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
                when (cancellationToken.IsCancellationRequested)
            {
                // Cancellation must not leave readiness stuck at Starting.
                // Treat the interrupted initialization as unavailable so
                // authentication remains fail-closed while a later attempt
                // performs a fresh monitoring registration.
                HandleSourceFailure(generation, exception);
                await StopSourceAfterFailedStartAsync().ConfigureAwait(false);
                throw;
            }
            catch (Exception exception)
            {
                HandleSourceFailure(generation, exception);
                await StopSourceAfterFailedStartAsync().ConfigureAwait(false);
                return false;
            }

            WorkstationSecurityReadinessSnapshot? readySnapshot = null;

            lock (_syncRoot)
            {
                // A failure callback may have run while StartAsync was
                // completing. Its newer source generation wins.
                if (_sourceGeneration == generation &&
                    _currentSnapshot.State == WorkstationSecurityReadinessState.Starting)
                {
                    readySnapshot = TransitionLocked(
                        WorkstationSecurityReadinessState.Ready);
                }
            }

            if (readySnapshot is null)
            {
                await StopSourceAfterFailedStartAsync().ConfigureAwait(false);
                return false;
            }

            PublishReadinessChanged(readySnapshot);

            // Recovery from Unavailable returns only to Locked. It never
            // establishes authenticated access or bypasses fresh Windows Hello.
            if (!TryRestoreSessionAvailability(readySnapshot.Revision))
            {
                HandleSourceFailure(
                    generation,
                    new InvalidOperationException(
                        "Workstation monitoring readiness changed during recovery."));

                await StopSourceAfterFailedStartAsync().ConfigureAwait(false);
                return false;
            }

            return true;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public bool TryRestoreSessionAvailability(
        long expectedReadinessRevision)
    {
        if (expectedReadinessRevision < 0)
        {
            return false;
        }

        lock (_syncRoot)
        {
            if (_currentSnapshot.State != WorkstationSecurityReadinessState.Ready ||
                _currentSnapshot.Revision != expectedReadinessRevision)
            {
                return false;
            }

            if (_session.CurrentSnapshot.State != ApplicationSecuritySessionState.Unavailable)
            {
                return true;
            }

            try
            {
                _session.RestoreAvailability();
            }
            catch (InvalidOperationException)
            {
                // A concurrent session transition may have completed after
                // the snapshot read. Re-read the authoritative state rather
                // than treating a stale Unavailable observation as success.
                return _session.CurrentSnapshot.State != ApplicationSecuritySessionState.Unavailable &&
                    _currentSnapshot.State == WorkstationSecurityReadinessState.Ready &&
                    _currentSnapshot.Revision == expectedReadinessRevision;
            }

            return _session.CurrentSnapshot.State != ApplicationSecuritySessionState.Unavailable &&
                _currentSnapshot.State == WorkstationSecurityReadinessState.Ready &&
                _currentSnapshot.Revision == expectedReadinessRevision;
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(
        CancellationToken cancellationToken = default)
    {
        await _operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            WorkstationSecurityReadinessSnapshot? stoppedSnapshot = null;

            lock (_syncRoot)
            {
                if (_currentSnapshot.State != WorkstationSecurityReadinessState.Stopped)
                {
                    _sourceGeneration = checked(_sourceGeneration + 1);
                    DetachSourceHandlersLocked();

                    stoppedSnapshot = TransitionLocked(
                        WorkstationSecurityReadinessState.Stopped);
                }
            }

            if (stoppedSnapshot is not null)
            {
                PublishReadinessChanged(stoppedSnapshot);
            }

            if (_sourceStopped)
            {
                return;
            }

            try
            {
                // Once readiness has transitioned to Stopped and handlers have
                // been detached, complete native cleanup even if the caller's
                // cancellation token is cancelled.
                await _eventSource
                    .StopAsync(CancellationToken.None)
                    .ConfigureAwait(false);

                _sourceStopped = true;
            }
            catch (Exception exception)
            {
                _logger.LogSafeFailure(
                    LogLevel.Error,
                    DiagnosticOperationCodes.WorkstationSecurityMonitoringFailure,
                    SafeStopFailureMessage,
                    exception);

                throw;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private void AttachSourceHandlers(long generation)
    {
        lock (_syncRoot)
        {
            if (_sourceGeneration != generation ||
                _currentSnapshot.State != WorkstationSecurityReadinessState.Starting)
            {
                return;
            }

            _securityEventHandler =
                (_, eventArgs) => OnSecurityEvent(generation, eventArgs);

            _failureEventHandler =
                (_, eventArgs) => HandleSourceFailure(generation, eventArgs.Exception);

            _eventSource.SecurityEventReceived += _securityEventHandler;
            _eventSource.MonitoringFailed += _failureEventHandler;
        }
    }

    private void DetachSourceHandlers(long generation)
    {
        lock (_syncRoot)
        {
            if (_sourceGeneration == generation)
            {
                DetachSourceHandlersLocked();
            }
        }
    }

    private void DetachSourceHandlersLocked()
    {
        if (_securityEventHandler is not null)
        {
            _eventSource.SecurityEventReceived -= _securityEventHandler;
            _securityEventHandler = null;
        }

        if (_failureEventHandler is not null)
        {
            _eventSource.MonitoringFailed -= _failureEventHandler;
            _failureEventHandler = null;
        }
    }

    private void OnSecurityEvent(
        long generation,
        WorkstationSecurityEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        Exception? lifecycleFailure = null;

        lock (_syncRoot)
        {
            if (generation != _sourceGeneration ||
                _currentSnapshot.State is not (
                    WorkstationSecurityReadinessState.Starting or
                    WorkstationSecurityReadinessState.Ready))
            {
                return;
            }

            switch (eventArgs.Kind)
            {
                case WorkstationSecurityEventKind.SessionLocked:
                case WorkstationSecurityEventKind.SessionLoggedOff:
                case WorkstationSecurityEventKind.SessionDisconnected:
                case WorkstationSecurityEventKind.SystemSuspending:
                case WorkstationSecurityEventKind.SystemResumed:
                    try
                    {
                        // OS transitions withdraw access through the existing
                        // authoritative lifecycle. They do not mark the monitor
                        // unavailable and do not create another state machine.
                        _lifecycleServiceFactory().Lock();
                    }
                    catch (Exception exception)
                    {
                        lifecycleFailure = exception;
                    }

                    break;

                case WorkstationSecurityEventKind.SessionUnlocked:
                case WorkstationSecurityEventKind.SessionConnected:
                    // An operating-system unlock or connection is
                    // never evidence of fresh DeskVault authentication.
                    break;

                default:
                    // WorkstationSecurityEventArgs validates the enum value.
                    break;
            }
        }

        if (lifecycleFailure is not null)
        {
            HandleSourceFailure(generation, lifecycleFailure);
        }
    }

    private void HandleSourceFailure(
        long generation,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        WorkstationSecurityReadinessSnapshot? unavailableSnapshot = null;
        bool shouldLog = false;

        lock (_syncRoot)
        {
            if (generation != _sourceGeneration ||
                _currentSnapshot.State == WorkstationSecurityReadinessState.Stopped ||
                _currentSnapshot.State == WorkstationSecurityReadinessState.Unavailable)
            {
                return;
            }

            // Fence callbacks already queued by this source generation before
            // making the readiness state observable to authentication callers.
            _sourceGeneration = checked(_sourceGeneration + 1);
            DetachSourceHandlersLocked();

            unavailableSnapshot = TransitionLocked(
                WorkstationSecurityReadinessState.Unavailable);

            shouldLog = true;

            // First invalidate an in-flight unlock through the lifecycle,
            // then make the authoritative session Unavailable.
            try
            {
                _lifecycleServiceFactory().Lock();
            }
            catch (Exception lockException)
            {
                _logger.LogSafeFailure(
                    LogLevel.Critical,
                    DiagnosticOperationCodes.WorkstationSecurityMonitoringFailure,
                    "The application security session could not be locked after workstation monitoring failed.",
                    lockException);
            }

            try
            {
                _session.MarkUnavailable();
            }
            catch (Exception markUnavailableException)
            {
                _logger.LogSafeFailure(
                    LogLevel.Critical,
                    DiagnosticOperationCodes.WorkstationSecurityMonitoringFailure,
                    "The application security session could not be marked unavailable after workstation monitoring failed.",
                    markUnavailableException);
            }
        }

        if (unavailableSnapshot is not null)
        {
            PublishReadinessChanged(unavailableSnapshot);
        }

        if (shouldLog)
        {
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.WorkstationSecurityMonitoringFailure,
                SafeFailureMessage,
                exception);
        }
    }

    private async Task StopSourceAfterFailedStartAsync()
    {
        if (_sourceStopped)
        {
            return;
        }

        try
        {
            await _eventSource
                .StopAsync(CancellationToken.None)
                .ConfigureAwait(false);

            _sourceStopped = true;
        }
        catch (Exception exception)
        {
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.WorkstationSecurityMonitoringFailure,
                SafeStopFailureMessage,
                exception);
        }
    }

    private WorkstationSecurityReadinessSnapshot TransitionLocked(
        WorkstationSecurityReadinessState nextState)
    {
        if (_currentSnapshot.State == nextState)
        {
            return _currentSnapshot;
        }

        _currentSnapshot = new WorkstationSecurityReadinessSnapshot(
            nextState,
            checked(_currentSnapshot.Revision + 1));

        return _currentSnapshot;
    }

    private void PublishReadinessChanged(
        WorkstationSecurityReadinessSnapshot snapshot)
    {
        Action<WorkstationSecurityReadinessSnapshot>? handlers = ReadinessChanged;

        if (handlers is null)
        {
            return;
        }

        foreach (Action<WorkstationSecurityReadinessSnapshot> handler
            in handlers.GetInvocationList())
        {
            try
            {
                handler(snapshot);
            }
            catch (Exception)
            {
                // Observer failures must not interfere with the security
                // readiness transition. Do not expose exception details.
                Trace.TraceError(
                    "A workstation security-readiness observer failed.");
            }
        }
    }
}
