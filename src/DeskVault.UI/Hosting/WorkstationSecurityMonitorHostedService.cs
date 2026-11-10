using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Workstation;
using DeskVault.Shared.Extensions;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeskVault.UI.Hosting;

/// <summary>
/// Owns the workstation security monitor lifecycle within the application's
/// shared Generic Host lifetime.
/// </summary>
/// <remarks>
/// The initial monitoring attempt is awaited during host startup.
/// A failed attempt does not authorize access or prevent the host from
/// starting. The Application authentication readiness gate prevents
/// authentication until monitoring becomes Ready.
///
/// The background worker retries initialization while readiness remains
/// unavailable and stops monitoring during host shutdown.
/// </remarks>
public sealed class WorkstationSecurityMonitorHostedService : BackgroundService
{
    private static readonly TimeSpan RetryInterval =
        TimeSpan.FromSeconds(5);

    private const string SafeInitializationFailureMessage =
        "Workstation security monitoring initialization failed. " +
        "Protected access remains unavailable and initialization will be retried.";

    private readonly IWorkstationSecurityMonitor _workstationSecurityMonitor;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WorkstationSecurityMonitorHostedService> _logger;
    private readonly TimeSpan _retryInterval;

    private readonly SemaphoreSlim _stopGate = new(1, 1);

    private int _startInvoked;
    private bool _monitorStopped;

    public WorkstationSecurityMonitorHostedService(
        IWorkstationSecurityMonitor workstationSecurityMonitor,
        TimeProvider timeProvider,
        ILogger<WorkstationSecurityMonitorHostedService> logger)
        : this(
            workstationSecurityMonitor,
            timeProvider,
            logger,
            RetryInterval)
    {
    }

    internal WorkstationSecurityMonitorHostedService(
        IWorkstationSecurityMonitor workstationSecurityMonitor,
        TimeProvider timeProvider,
        ILogger<WorkstationSecurityMonitorHostedService> logger,
        TimeSpan retryInterval)
    {
        ArgumentNullException.ThrowIfNull(workstationSecurityMonitor);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        if (retryInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retryInterval),
                "The workstation-monitor retry interval must be positive.");
        }

        _workstationSecurityMonitor = workstationSecurityMonitor;
        _timeProvider = timeProvider;
        _logger = logger;
        _retryInterval = retryInterval;
    }

    /// <summary>
    /// Performs the initial monitoring attempt before this hosted service
    /// reports startup completion. Authentication remains blocked by the
    /// Application readiness gate if that attempt fails.
    /// </summary>
    public override async Task StartAsync(
        CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(
                ref _startInvoked,
                1,
                0) != 0)
        {
            throw new InvalidOperationException(
                "Workstation security monitoring has already been started.");
        }

        try
        {
            await TryStartMonitoringAsync(cancellationToken)
                .ConfigureAwait(false);

            await base.StartAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception startException)
        {
            try
            {
                await StopMonitoringOnceAsync()
                    .ConfigureAwait(false);
            }
            catch (Exception stopException)
            {
                throw new AggregateException(
                    "Workstation security monitoring failed to start and cleanup also failed.",
                    startException,
                    stopException);
            }

            throw;
        }
    }

    /// <summary>
    /// Stops the retry worker and releases workstation-monitoring resources
    /// during Generic Host shutdown.
    /// </summary>
    public override async Task StopAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await base.StopAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            await StopMonitoringOnceAsync()
                .ConfigureAwait(false);
        }
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(
                _retryInterval,
                _timeProvider);

            while (await timer
                .WaitForNextTickAsync(stoppingToken)
                .ConfigureAwait(false))
            {
                if (_workstationSecurityMonitor.CurrentSnapshot.State ==
                    WorkstationSecurityReadinessState.Ready)
                {
                    continue;
                }

                await TryStartMonitoringAsync(stoppingToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal Generic Host shutdown.
        }
    }

    private async Task TryStartMonitoringAsync(
        CancellationToken cancellationToken)
    {
        if (_workstationSecurityMonitor.CurrentSnapshot.State ==
            WorkstationSecurityReadinessState.Ready)
        {
            return;
        }

        try
        {
            bool isReady =
                await _workstationSecurityMonitor
                    .TryStartAsync(cancellationToken)
                    .ConfigureAwait(false);

            if (isReady)
            {
                _logger.LogInformation(
                    "Workstation security monitoring is ready.");
            }
            else
            {
                _logger.LogWarning(
                    "Workstation security monitoring is unavailable. Initialization will be retried.");
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.WorkstationSecurityMonitoringFailure,
                SafeInitializationFailureMessage,
                exception);
        }
    }

    private async Task StopMonitoringOnceAsync()
    {
        await _stopGate.WaitAsync(CancellationToken.None)
            .ConfigureAwait(false);

        try
        {
            if (_monitorStopped)
            {
                return;
            }

            await _workstationSecurityMonitor
                .StopAsync(CancellationToken.None)
                .ConfigureAwait(false);

            // Mark stopped only after successful cleanup. If cleanup fails,
            // a subsequent shutdown attempt can retry it.
            _monitorStopped = true;
        }
        finally
        {
            _stopGate.Release();
        }
    }
}
