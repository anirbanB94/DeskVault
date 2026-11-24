using DeskVault.UI.Services.Document;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeskVault.UI.Hosting;

/// <summary>
/// Runs temporary document artifact recovery synchronously during application host startup.
/// </summary>
/// <remarks>
/// This service must be registered before the workstation security monitor and automatic-lock
/// hosted services. The Generic Host awaits StartAsync before proceeding to startup authentication.
///
/// Recovery failures affecting individual artifacts are reported by the recovery service and
/// do not prevent other candidates from being reconciled.
/// </remarks>
internal sealed class TemporaryDocumentArtifactRecoveryHostedService : IHostedService
{
    private readonly TemporaryDocumentArtifactRecoveryService _recoveryService;
    private readonly ILogger<TemporaryDocumentArtifactRecoveryHostedService> _logger;

    private int _started;

    public TemporaryDocumentArtifactRecoveryHostedService(
        TemporaryDocumentArtifactRecoveryService recoveryService,
        ILogger<TemporaryDocumentArtifactRecoveryHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(recoveryService);
        ArgumentNullException.ThrowIfNull(logger);

        _recoveryService = recoveryService;
        _logger = logger;
    }

    /// <summary>
    /// Completes the recovery pass before the host proceeds with subsequent startup services.
    /// </summary>
    public Task StartAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.CompareExchange(
                ref _started,
                1,
                0) != 0)
        {
            return Task.CompletedTask;
        }

        // Recovery is intentionally synchronous here. Returning from StartAsync
        // is the startup ordering boundary that permits later services to start.
        TemporaryDocumentArtifactRecoveryResult result =
            _recoveryService.Recover();

        if (result.FailedCount > 0)
        {
            _logger.LogWarning(
                "Temporary document artifact startup recovery completed with failures. CandidateCount: {CandidateCount}; CleanedCount: {CleanedCount}; PreservedCount: {PreservedCount}; AlreadyRemovedCount: {AlreadyRemovedCount}; FailedCount: {FailedCount}",
                result.CandidateCount,
                result.CleanedCount,
                result.PreservedCount,
                result.AlreadyRemovedCount,
                result.FailedCount);
        }
        else
        {
            _logger.LogInformation(
                "Temporary document artifact startup recovery completed. CandidateCount: {CandidateCount}; CleanedCount: {CleanedCount}; PreservedCount: {PreservedCount}; AlreadyRemovedCount: {AlreadyRemovedCount}",
                result.CandidateCount,
                result.CleanedCount,
                result.PreservedCount,
                result.AlreadyRemovedCount);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Performs no shutdown work because recovery owns no long-running resources.
    /// </summary>
    public Task StopAsync(
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
