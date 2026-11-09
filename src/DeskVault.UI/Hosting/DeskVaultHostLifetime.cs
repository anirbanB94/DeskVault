using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Hosting;

namespace DeskVault.UI.Hosting;

/// <summary>
/// Owns the start, stop, and disposal lifecycle of the application's
/// shared Generic Host.
/// </summary>
public sealed class DeskVaultHostLifetime : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    private bool _startAttempted;
    private bool _started;
    private bool _stopped;
    private bool _disposed;

    public DeskVaultHostLifetime(IHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        _host = host;
    }

    /// <summary>
    /// Starts the host once. A failed startup triggers a best-effort
    /// stop before the original failure is propagated.
    /// </summary>
    public async Task StartAsync(
        CancellationToken cancellationToken = default)
    {
        await _operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();

            if (_started)
            {
                return;
            }

            if (_startAttempted || _stopped)
            {
                throw new InvalidOperationException(
                    "The application host cannot be started again after a start attempt or stop.");
            }

            _startAttempted = true;

            try
            {
                await _host
                    .StartAsync(cancellationToken)
                    .ConfigureAwait(false);

                _started = true;
            }
            catch (Exception startException)
            {
                try
                {
                    await StopHostIfRequiredAsync(
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception stopException)
                {
                    throw new AggregateException(
                        "The application host failed to start and its cleanup also failed.",
                        startException,
                        stopException);
                }

                ExceptionDispatchInfo
                    .Capture(startException)
                    .Throw();

                throw;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <summary>
    /// Stops the host once after a start attempt.
    /// </summary>
    public async Task StopAsync(
        CancellationToken cancellationToken = default)
    {
        await _operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();

            await StopHostIfRequiredAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <summary>
    /// Stops the host when required and always attempts to dispose it.
    /// Repeated disposal calls do not repeat the underlying operations.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _operationGate
            .WaitAsync()
            .ConfigureAwait(false);

        try
        {
            if (_disposed)
            {
                return;
            }

            var failures = new List<Exception>();

            try
            {
                await StopHostIfRequiredAsync(CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            try
            {
                if (_host is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable
                        .DisposeAsync()
                        .ConfigureAwait(false);
                }
                else
                {
                    _host.Dispose();
                }
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
            finally
            {
                _disposed = true;
            }

            if (failures.Count == 1)
            {
                ExceptionDispatchInfo
                    .Capture(failures[0])
                    .Throw();
            }

            if (failures.Count > 1)
            {
                throw new AggregateException(
                    "The application host failed during shutdown or disposal.",
                    failures);
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task StopHostIfRequiredAsync(
        CancellationToken cancellationToken)
    {
        if (!_startAttempted || _stopped)
        {
            return;
        }

        await _host
            .StopAsync(cancellationToken)
            .ConfigureAwait(false);

        _started = false;
        _stopped = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
