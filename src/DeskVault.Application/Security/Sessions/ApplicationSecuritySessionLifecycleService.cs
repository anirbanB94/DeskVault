using System.Diagnostics;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Authentication;

namespace DeskVault.Application.Security.Sessions;

/// <summary>
/// Coordinates application security-session lock and unlock operations.
/// </summary>
/// <remarks>
/// Lock requests invalidate active unlock operations independently of
/// session-state changes. The authoritative authorization state remains
/// owned by <see cref="IApplicationSecuritySession"/>.
/// </remarks>
public sealed class ApplicationSecuritySessionLifecycleService
    : IApplicationSecuritySessionLifecycleService
{
    private readonly object _syncRoot = new();

    private readonly IApplicationSecuritySession _session;
    private readonly IApplicationAuthenticationService _authenticationService;

    private long _generation;
    private int _lockRequestsInProgress;
    private UnlockOperation? _activeUnlock;

    public ApplicationSecuritySessionLifecycleService(
        IApplicationSecuritySession session,
        IApplicationAuthenticationService authenticationService)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(authenticationService);

        _session = session;
        _authenticationService = authenticationService;
    }

    /// <inheritdoc />
    public ApplicationSecuritySessionSnapshot Lock()
    {
        UnlockOperation? operation;

        lock (_syncRoot)
        {
            // Advance the generation even when the session is already locked.
            // This ensures every new lock request supersedes an earlier unlock.
            _generation = checked(_generation + 1);
            _lockRequestsInProgress++;

            operation = _activeUnlock;
            _activeUnlock = null;
        }

        try
        {
            operation?.RequestCancellation();

            return _session.Lock();
        }
        finally
        {
            lock (_syncRoot)
            {
                _lockRequestsInProgress--;
            }
        }
    }

    /// <inheritdoc />
    public async Task<ApplicationAuthenticationServiceResult> UnlockAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        UnlockOperation operation;

        lock (_syncRoot)
        {
            if (_lockRequestsInProgress > 0)
            {
                return ApplicationAuthenticationServiceResult.NotPermitted;
            }

            if (_activeUnlock is not null)
            {
                return ApplicationAuthenticationServiceResult.AlreadyInProgress;
            }

            ApplicationSecuritySessionState state =
                _session.CurrentSnapshot.State;

            if (state is not (
                ApplicationSecuritySessionState.Locked or
                ApplicationSecuritySessionState.Unavailable))
            {
                return ApplicationAuthenticationServiceResult.NotPermitted;
            }

            operation = new UnlockOperation(
                checked(++_generation));

            _activeUnlock = operation;
        }

        try
        {
            using var linkedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    operation.Token);

            // Authentication checks provider availability before creating
            // a session attempt, so lifecycle cancellation must cover both.
            ApplicationAuthenticationServiceResult result =
                await _authenticationService.AuthenticateAsync(
                    linkedCancellation.Token).ConfigureAwait(false);

            return TryCompleteOperation(operation)
                ? result
                : ApplicationAuthenticationServiceResult.Superseded;
        }
        catch (OperationCanceledException)
            when (operation.IsCancellationRequested &&
                  !cancellationToken.IsCancellationRequested)
        {
            return ApplicationAuthenticationServiceResult.Superseded;
        }
        finally
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(_activeUnlock, operation))
                {
                    _activeUnlock = null;
                }
            }

            await operation.DisposeAsync().ConfigureAwait(false);
        }
    }

    private bool TryCompleteOperation(UnlockOperation operation)
    {
        lock (_syncRoot)
        {
            // A result is accepted only if no newer lock request superseded
            // this operation, even when that lock did not change session state.
            bool isCurrent =
                _lockRequestsInProgress == 0 &&
                _generation == operation.Generation &&
                ReferenceEquals(_activeUnlock, operation);

            if (ReferenceEquals(_activeUnlock, operation))
            {
                _activeUnlock = null;
            }

            return isCurrent;
        }
    }

    private sealed class UnlockOperation : IAsyncDisposable
    {
        private readonly object _syncRoot = new();
        private readonly CancellationTokenSource _cancellationSource = new();
        private readonly CancellationToken _token;

        private Task? _cancellationTask;
        private bool _disposeRequested;
        private bool _disposed;

        public UnlockOperation(long generation)
        {
            Generation = generation;
            _token = _cancellationSource.Token;
        }

        public long Generation { get; }

        public CancellationToken Token => _token;

        public bool IsCancellationRequested =>
            _token.IsCancellationRequested;

        public void RequestCancellation()
        {
            lock (_syncRoot)
            {
                if (_disposeRequested || _disposed)
                {
                    return;
                }

                // Cancel asynchronously so callbacks cannot execute while
                // the operation's synchronization boundary is held.
                _cancellationTask ??=
                    _cancellationSource.CancelAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            Task? cancellationTask;

            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposeRequested = true;
                cancellationTask = _cancellationTask;

                if (cancellationTask is null)
                {
                    _cancellationSource.Dispose();
                    _disposed = true;
                    return;
                }
            }

            try
            {
                await cancellationTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Do not expose cancellation-callback exception details.
                Trace.TraceError(
                    "An application security-session cancellation callback failed.");
            }
            finally
            {
                lock (_syncRoot)
                {
                    if (!_disposed)
                    {
                        _cancellationSource.Dispose();
                        _disposed = true;
                    }
                }
            }
        }
    }
}
