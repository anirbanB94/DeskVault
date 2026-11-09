using System.Runtime.ExceptionServices;
using DeskVault.Application.Interfaces;
using DeskVault.Shared.Extensions;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeskVault.UI.Hosting;

/// <summary>
/// Runs periodic automatic-lock inactivity evaluation within the
/// application's shared Generic Host lifetime.
/// </summary>
public sealed class ApplicationAutomaticLockHostedService : BackgroundService
{
    private static readonly TimeSpan EvaluationInterval =
        TimeSpan.FromSeconds(1);

    private readonly IApplicationAutomaticLockService _automaticLockService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ApplicationAutomaticLockHostedService> _logger;

    private int _automaticLockServiceStarted;

    public ApplicationAutomaticLockHostedService(
        IApplicationAutomaticLockService automaticLockService,
        TimeProvider timeProvider,
        ILogger<ApplicationAutomaticLockHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(automaticLockService);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _automaticLockService = automaticLockService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Starts automatic-lock monitoring synchronously before the
    /// background evaluation loop is started.
    /// </summary>
    public override async Task StartAsync(
        CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(
                ref _automaticLockServiceStarted,
                1,
                0) != 0)
        {
            throw new InvalidOperationException(
                "Automatic-lock monitoring has already been started.");
        }

        try
        {
            // This call must happen here, rather than at the beginning of
            // ExecuteAsync. The host must not proceed to startup
            // authentication until monitoring has established its baseline.
            _automaticLockService.Start();

            await base.StartAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception startException)
        {
            try
            {
                StopAutomaticLockServiceOnce();
            }
            catch (Exception stopException)
            {
                throw new AggregateException(
                    "Automatic-lock monitoring failed to start and cleanup also failed.",
                    startException,
                    stopException);
            }

            ExceptionDispatchInfo
                .Capture(startException)
                .Throw();

            throw;
        }
    }

    /// <summary>
    /// Stops the background worker and ensures automatic-lock monitoring
    /// is stopped even if the worker does not complete normally.
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
            StopAutomaticLockServiceOnce();
        }
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(
                EvaluationInterval,
                _timeProvider);

            while (await timer
                .WaitForNextTickAsync(stoppingToken)
                .ConfigureAwait(false))
            {
                try
                {
                    _automaticLockService.EvaluateInactivity();
                }
                catch (Exception exception)
                {
                    _logger.LogSafeFailure(
                        LogLevel.Error,
                        DiagnosticOperationCodes.AutomaticLockEvaluationFailure,
                        LogMessages.AutomaticLockEvaluationFailed,
                        exception);
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
        finally
        {
            StopAutomaticLockServiceOnce();
        }
    }

    private void StopAutomaticLockServiceOnce()
    {
        if (Interlocked.Exchange(
                ref _automaticLockServiceStarted,
                0) == 1)
        {
            _automaticLockService.Stop();
        }
    }
}
