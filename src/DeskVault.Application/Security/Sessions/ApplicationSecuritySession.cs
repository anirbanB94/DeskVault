using System.Diagnostics;
using DeskVault.Application.Interfaces;

namespace DeskVault.Application.Security.Sessions;

/// <summary>
/// Coordinates the authoritative application security-session state.
/// </summary>
/// <remarks>
/// All state transitions are serialized through a private synchronization
/// boundary. Authentication completions are accepted only when they match
/// the active attempt created by this session.
///
/// State notifications are queued while transitions are committed and
/// dispatched in revision order outside the synchronization boundary.
/// </remarks>
public sealed class ApplicationSecuritySession
    : IApplicationSecuritySession
{
    private readonly object _syncRoot = new();

    private readonly Queue<(
        ApplicationSecuritySessionSnapshot Snapshot,
        Action<ApplicationSecuritySessionSnapshot>? Handlers)>
        _pendingNotifications = new();

    private readonly Guid _sessionId =
        Guid.NewGuid();

    private ApplicationSecuritySessionSnapshot _currentSnapshot =
        new(
            ApplicationSecuritySessionState.Unauthenticated,
            revision: 0);

    private ApplicationSecuritySessionAttempt? _activeAttempt;

    private bool _isDispatchingNotifications;

    /// <inheritdoc />
    public ApplicationSecuritySessionSnapshot CurrentSnapshot
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
    public event Action<ApplicationSecuritySessionSnapshot>? StateChanged;

    /// <inheritdoc />
    public ApplicationSecuritySessionAttempt BeginAuthentication()
    {
        ApplicationSecuritySessionSnapshot snapshot;
        ApplicationSecuritySessionAttempt attempt;
        bool shouldDispatchNotifications;

        lock (_syncRoot)
        {
            ApplicationSecuritySessionState previousState =
                _currentSnapshot.State;

            if (previousState is not (
                ApplicationSecuritySessionState.Unauthenticated or
                ApplicationSecuritySessionState.Locked))
            {
                throw new InvalidOperationException(
                    "Authentication cannot begin in the current security-session state.");
            }

            shouldDispatchNotifications =
                TransitionLocked(
                    ApplicationSecuritySessionState.Authenticating,
                    out snapshot);

            attempt =
                new ApplicationSecuritySessionAttempt(
                    _sessionId,
                    snapshot.Revision,
                    previousState);

            _activeAttempt =
                attempt;
        }

        if (shouldDispatchNotifications)
        {
            DispatchPendingNotifications();
        }

        return attempt;
    }

    /// <inheritdoc />
    public bool TryCompleteAuthentication(
        ApplicationSecuritySessionAttempt attempt,
        ApplicationAuthenticationOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        if (!Enum.IsDefined(
                typeof(ApplicationAuthenticationOutcome),
                outcome))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                "Authentication outcome is invalid.");
        }

        bool accepted;
        bool shouldDispatchNotifications = false;

        lock (_syncRoot)
        {
            if (!ReferenceEquals(_activeAttempt, attempt) ||
                attempt.SessionId != _sessionId ||
                attempt.Revision != _currentSnapshot.Revision ||
                _currentSnapshot.State !=
                    ApplicationSecuritySessionState.Authenticating)
            {
                return false;
            }

            ApplicationSecuritySessionState resultingState =
                outcome switch
                {
                    ApplicationAuthenticationOutcome.Succeeded =>
                        ApplicationSecuritySessionState.Authenticated,

                    ApplicationAuthenticationOutcome.Failed =>
                        attempt.PreviousState,

                    ApplicationAuthenticationOutcome.Cancelled =>
                        attempt.PreviousState,

                    ApplicationAuthenticationOutcome.Unavailable =>
                        ApplicationSecuritySessionState.Unavailable,

                    _ => throw new ArgumentOutOfRangeException(
                        nameof(outcome))
                };

            _activeAttempt = null;

            shouldDispatchNotifications =
                TransitionLocked(
                    resultingState,
                    out _);

            accepted = true;
        }

        if (shouldDispatchNotifications)
        {
            DispatchPendingNotifications();
        }

        return accepted;
    }

    /// <inheritdoc />
    public bool TryLock(
        long expectedRevision,
        out ApplicationSecuritySessionSnapshot snapshot)
    {
        bool shouldDispatchNotifications;

        lock (_syncRoot)
        {
            // The revision check and transition must share the same
            // synchronization boundary. A later authenticated revision
            // must never be locked by an earlier expiry callback.
            if (_currentSnapshot.Revision != expectedRevision ||
                !_currentSnapshot.IsProtectedAccessAvailable)
            {
                snapshot = _currentSnapshot;
                return false;
            }

            _activeAttempt = null;

            shouldDispatchNotifications =
                TransitionLocked(
                    ApplicationSecuritySessionState.Locked,
                    out snapshot);
        }

        if (shouldDispatchNotifications)
        {
            DispatchPendingNotifications();
        }

        return true;
    }

    /// <inheritdoc />
    public ApplicationSecuritySessionSnapshot Lock()
    {
        ApplicationSecuritySessionSnapshot snapshot;
        bool shouldDispatchNotifications;

        lock (_syncRoot)
        {
            _activeAttempt = null;

            if (_currentSnapshot.State ==
                ApplicationSecuritySessionState.Unavailable)
            {
                return _currentSnapshot;
            }

            shouldDispatchNotifications =
                TransitionLocked(
                    ApplicationSecuritySessionState.Locked,
                    out snapshot);
        }

        if (shouldDispatchNotifications)
        {
            DispatchPendingNotifications();
        }

        return snapshot;
    }

    /// <inheritdoc />
    public ApplicationSecuritySessionSnapshot MarkUnavailable()
    {
        ApplicationSecuritySessionSnapshot snapshot;
        bool shouldDispatchNotifications;

        lock (_syncRoot)
        {
            _activeAttempt = null;

            shouldDispatchNotifications =
                TransitionLocked(
                    ApplicationSecuritySessionState.Unavailable,
                    out snapshot);
        }

        if (shouldDispatchNotifications)
        {
            DispatchPendingNotifications();
        }

        return snapshot;
    }

    /// <inheritdoc />
    public ApplicationSecuritySessionSnapshot RestoreAvailability()
    {
        ApplicationSecuritySessionSnapshot snapshot;
        bool shouldDispatchNotifications;

        lock (_syncRoot)
        {
            if (_currentSnapshot.State !=
                ApplicationSecuritySessionState.Unavailable)
            {
                throw new InvalidOperationException(
                    "Security-session availability can only be restored from the Unavailable state.");
            }

            shouldDispatchNotifications =
                TransitionLocked(
                    ApplicationSecuritySessionState.Locked,
                    out snapshot);
        }

        if (shouldDispatchNotifications)
        {
            DispatchPendingNotifications();
        }

        return snapshot;
    }

    private bool TransitionLocked(
        ApplicationSecuritySessionState nextState,
        out ApplicationSecuritySessionSnapshot snapshot)
    {
        if (_currentSnapshot.State == nextState)
        {
            snapshot = _currentSnapshot;
            return false;
        }

        long nextRevision =
            checked(_currentSnapshot.Revision + 1);

        snapshot =
            new ApplicationSecuritySessionSnapshot(
                nextState,
                nextRevision);

        _currentSnapshot =
            snapshot;

        _pendingNotifications.Enqueue(
            (
                snapshot,
                StateChanged));

        if (_isDispatchingNotifications)
        {
            return false;
        }

        _isDispatchingNotifications = true;

        return true;
    }

    private void DispatchPendingNotifications()
    {
        while (true)
        {
            (
                ApplicationSecuritySessionSnapshot Snapshot,
                Action<ApplicationSecuritySessionSnapshot>? Handlers
            ) notification;

            lock (_syncRoot)
            {
                if (_pendingNotifications.Count == 0)
                {
                    _isDispatchingNotifications = false;
                    return;
                }

                notification =
                    _pendingNotifications.Dequeue();
            }

            if (notification.Handlers is null)
            {
                continue;
            }

            foreach (Action<ApplicationSecuritySessionSnapshot> handler
                in notification.Handlers.GetInvocationList())
            {
                try
                {
                    handler(notification.Snapshot);
                }
                catch (Exception)
                {
                    // A faulty observer must not interrupt the committed
                    // security transition or prevent other observers from
                    // receiving subsequent state notifications.
                    //
                    // Deliberately omit exception details because observer
                    // exceptions may contain sensitive runtime information.
                    Trace.TraceError(
                        "An application security-session state observer failed.");
                }
            }
        }
    }
}
