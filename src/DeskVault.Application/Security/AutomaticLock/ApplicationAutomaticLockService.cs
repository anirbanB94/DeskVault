using DeskVault.Application.Configurations;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;

namespace DeskVault.Application.Security.AutomaticLock;

/// <summary>
/// Enforces the configured automatic-lock inactivity policy without
/// depending on a UI framework or hosting implementation.
/// </summary>
/// <remarks>
/// The service does not own authorization state. It observes the
/// authoritative application security session and delegates expiry
/// locking to <see cref="IApplicationSecuritySessionLifecycleService"/>.
///
/// Timestamp comparisons use the injected <see cref="TimeProvider"/>
/// monotonic timestamp facilities rather than wall-clock time.
///
/// Activity and inactivity evaluation are serialized through one
/// synchronization boundary. Activity at or after the inactivity
/// deadline cannot extend the authenticated session.
/// </remarks>
public sealed class ApplicationAutomaticLockService :
    IApplicationAutomaticLockService,
    IApplicationUserActivityService,
    IDisposable
{
    private readonly object _syncRoot = new();

    private readonly IApplicationSecuritySession _session;
    private readonly IApplicationSecuritySessionLifecycleService _lifecycleService;
    private readonly AutomaticLockSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _inactivityTimeout;

    private bool _started;
    private bool _disposed;
    private bool _hasActivityBaseline;

    private long _trackedSessionRevision = -1;
    private long _lastActivityTimestamp;

    // A revision is marked before the lifecycle lock is invoked. This
    // prevents repeated activity/timer callbacks from issuing duplicate
    // expiry locks for the same authenticated session.
    private long? _lockRequestedRevision;

    public ApplicationAutomaticLockService(
        IApplicationSecuritySession session,
        IApplicationSecuritySessionLifecycleService lifecycleService,
        AutomaticLockSettings settings,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(lifecycleService);
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.InactivityTimeoutMinutes is int suppliedMinutes &&
            suppliedMinutes is < 1 or > 1440)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                "Automatic-lock inactivity timeout must be between 1 and 1440 minutes.");
        }

        if (settings.Enabled &&
            settings.InactivityTimeoutMinutes is null)
        {
            throw new ArgumentException(
                "An enabled automatic-lock policy requires an inactivity timeout.",
                nameof(settings));
        }

        _session = session;
        _lifecycleService = lifecycleService;
        _settings = settings;
        _timeProvider = timeProvider ?? TimeProvider.System;

        _inactivityTimeout = settings.Enabled
            ? TimeSpan.FromMinutes(settings.InactivityTimeoutMinutes!.Value)
            : TimeSpan.Zero;
    }

    /// <inheritdoc />
    public void Start()
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();

            if (_started)
            {
                return;
            }

            _started = true;

            // Subscribe before reading CurrentSnapshot. A transition
            // concurrent with this operation is therefore either visible
            // in the snapshot or delivered through StateChanged.
            _session.StateChanged += OnSessionStateChanged;

            ObserveSnapshot(_session.CurrentSnapshot);
        }
    }

    /// <inheritdoc />
    public bool TryReportQualifyingActivity(long observedSessionRevision)
    {
        if (observedSessionRevision < 0)
        {
            return false;
        }

        long? revisionToLock = null;
        bool accepted = false;

        lock (_syncRoot)
        {
            if (!_started || _disposed || !_settings.Enabled)
            {
                return false;
            }

            ApplicationSecuritySessionSnapshot current =
                _session.CurrentSnapshot;

            ObserveSnapshot(current);

            if (!current.IsProtectedAccessAvailable ||
                current.Revision != observedSessionRevision)
            {
                return false;
            }

            if (_lockRequestedRevision == current.Revision)
            {
                // An earlier evaluation has already reached the deadline.
                // Retry the pending lifecycle call rather than accepting
                // activity for an expired session.
                revisionToLock = current.Revision;
            }
            else
            {
                long now = _timeProvider.GetTimestamp();

                if (!_hasActivityBaseline)
                {
                    EstablishActivityBaseline(current, now);
                }

                TimeSpan elapsed = _timeProvider.GetElapsedTime(
                    _lastActivityTimestamp,
                    now);

                if (elapsed >= _inactivityTimeout)
                {
                    MarkExpiryPending(current.Revision);
                    revisionToLock = current.Revision;
                }
                else
                {
                    _lastActivityTimestamp = now;
                    accepted = true;
                }
            }
        }

        if (revisionToLock.HasValue)
        {
            LockExpiredRevision(revisionToLock.Value);
        }

        return accepted;
    }

    /// <inheritdoc />
    public void EvaluateInactivity()
    {
        long? revisionToLock = null;

        lock (_syncRoot)
        {
            if (!_started || _disposed || !_settings.Enabled)
            {
                return;
            }

            ApplicationSecuritySessionSnapshot current =
                _session.CurrentSnapshot;

            ObserveSnapshot(current);

            if (!current.IsProtectedAccessAvailable)
            {
                return;
            }

            if (_lockRequestedRevision == current.Revision)
            {
                revisionToLock = current.Revision;
            }
            else if (_hasActivityBaseline)
            {
                long now = _timeProvider.GetTimestamp();

                TimeSpan elapsed = _timeProvider.GetElapsedTime(
                    _lastActivityTimestamp,
                    now);

                if (elapsed >= _inactivityTimeout)
                {
                    MarkExpiryPending(current.Revision);
                    revisionToLock = current.Revision;
                }
            }
        }

        if (revisionToLock.HasValue)
        {
            LockExpiredRevision(revisionToLock.Value);
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        lock (_syncRoot)
        {
            if (!_started)
            {
                return;
            }

            _session.StateChanged -= OnSessionStateChanged;

            _started = false;
            _hasActivityBaseline = false;
            _lockRequestedRevision = null;
            _trackedSessionRevision = _session.CurrentSnapshot.Revision;
        }
    }

    /// <summary>
    /// Releases the session notification subscription and stops monitoring.
    /// </summary>
    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            if (_started)
            {
                _session.StateChanged -= OnSessionStateChanged;
            }

            _started = false;
            _hasActivityBaseline = false;
            _lockRequestedRevision = null;
            _disposed = true;
        }
    }

    private void OnSessionStateChanged(
        ApplicationSecuritySessionSnapshot _)
    {
        lock (_syncRoot)
        {
            if (!_started || _disposed)
            {
                return;
            }

            // The delivered notification may be stale because session
            // notifications are queued. Always reconcile against the
            // authoritative current snapshot.
            ObserveSnapshot(_session.CurrentSnapshot);
        }
    }

    private void ObserveSnapshot(
        ApplicationSecuritySessionSnapshot snapshot)
    {
        if (!_settings.Enabled ||
            !snapshot.IsProtectedAccessAvailable)
        {
            _trackedSessionRevision = snapshot.Revision;
            _hasActivityBaseline = false;
            _lockRequestedRevision = null;
            return;
        }

        if (_trackedSessionRevision != snapshot.Revision)
        {
            // A new authenticated revision always receives a fresh
            // baseline. Activity from a prior revision cannot carry over.
            _trackedSessionRevision = snapshot.Revision;
            _lockRequestedRevision = null;

            EstablishActivityBaseline(
                snapshot,
                _timeProvider.GetTimestamp());

            return;
        }

        if (_lockRequestedRevision == snapshot.Revision)
        {
            return;
        }

        if (!_hasActivityBaseline)
        {
            EstablishActivityBaseline(
                snapshot,
                _timeProvider.GetTimestamp());
        }
    }

    private void EstablishActivityBaseline(
        ApplicationSecuritySessionSnapshot snapshot,
        long timestamp)
    {
        _trackedSessionRevision = snapshot.Revision;
        _lastActivityTimestamp = timestamp;
        _hasActivityBaseline = true;
        _lockRequestedRevision = null;
    }

    private void MarkExpiryPending(long sessionRevision)
    {
        _hasActivityBaseline = false;
        _lockRequestedRevision = sessionRevision;
    }

    private void LockExpiredRevision(long expiredRevision)
    {
        lock (_syncRoot)
        {
            if (!_started ||
                _disposed ||
                _lockRequestedRevision != expiredRevision)
            {
                return;
            }

            ApplicationSecuritySessionSnapshot current =
                _session.CurrentSnapshot;

            // A delayed expiry must never lock a later authenticated
            // session. Reconcile the latest state and abandon this expiry
            // if its revision is no longer authoritative.
            if (!current.IsProtectedAccessAvailable ||
                current.Revision != expiredRevision)
            {
                ObserveSnapshot(current);
                return;
            }

            // Keep the activity/expiry synchronization boundary held
            // through the lifecycle call. The lifecycle service remains
            // the sole lock coordinator; this class does not directly
            // mutate the authoritative session.
            _ = _lifecycleService.TryLock(
                expiredRevision,
                out _);

            // State notifications may be queued behind another dispatcher.
            // Read CurrentSnapshot rather than depending on notification
            // delivery to observe the committed lock.
            ObserveSnapshot(_session.CurrentSnapshot);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
