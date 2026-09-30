using DeskVault.Application.Documents.Queries.ReconcileDocumentArtifacts;
using DeskVault.Application.Interfaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Application.Documents.Commands.RecoverDocumentArtifacts;

public sealed class RecoverDocumentArtifactsHandler
{
    private readonly IDocumentRepository _repository;
    private readonly ReconcileDocumentArtifactsHandler _reconciliationHandler;
    private readonly IStorageService _storageService;
    private readonly ILogger<RecoverDocumentArtifactsHandler> _logger;

    public RecoverDocumentArtifactsHandler(
        IDocumentRepository repository,
        ReconcileDocumentArtifactsHandler reconciliationHandler,
        IStorageService storageService,
        ILogger<RecoverDocumentArtifactsHandler> logger)
    {
        _repository = repository;
        _reconciliationHandler = reconciliationHandler;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<RecoverDocumentArtifactsResult> HandleAsync(
        RecoverDocumentArtifactsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            LogMessages.DocumentArtifactRecoveryStarted);

        ReconcileDocumentArtifactsResult reconciliationResult =
            await _reconciliationHandler.HandleAsync(
                new ReconcileDocumentArtifactsQuery(),
                cancellationToken);

        var persistedDocumentIds =
            (await _repository.GetAllAsync(
                cancellationToken))
            .Select(
                document => document.Id)
            .ToHashSet();

        var recoveryFindings =
            new List<DocumentArtifactReconciliationResult>(
                reconciliationResult.Findings.Count);

        var recoveredAnyArtifact =
            false;

        var preservedAnyFinding =
            false;

        foreach (DocumentArtifactReconciliationResult finding
                 in reconciliationResult.Findings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (finding.Status !=
                DocumentArtifactReconciliationStatus.OrphanedArtifact)
            {
                if (finding.RecoveryAction ==
                    DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery)
                {
                    preservedAnyFinding = true;
                }

                recoveryFindings.Add(finding);

                continue;
            }

            if (!finding.DocumentId.HasValue)
            {
                preservedAnyFinding = true;

                recoveryFindings.Add(
                    finding with
                    {
                        RecoveryAction =
                            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery
                    });

                _logger.LogWarning(
                    LogMessages.DocumentArtifactRecoveryPreserved);

                continue;
            }

            Guid documentId =
                finding.DocumentId.Value;

            if (persistedDocumentIds.Contains(
                    documentId))
            {
                _logger.LogWarning(
                    LogMessages.DocumentArtifactRecoveryPreserved);

                preservedAnyFinding = true;

                recoveryFindings.Add(
                    finding with
                    {
                        RecoveryAction =
                            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery
                    });

                continue;
            }

            if (!_storageService.IsOwnedArtifactPath(
                    documentId,
                    finding.ArtifactPath))
            {
                _logger.LogWarning(
                    LogMessages.DocumentArtifactRecoveryOwnershipValidationFailed);

                preservedAnyFinding = true;

                recoveryFindings.Add(
                    finding with
                    {
                        RecoveryAction =
                            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery
                    });

                continue;
            }

            await _storageService.DeleteAsync(
                documentId,
                cancellationToken);

            recoveredAnyArtifact = true;

            recoveryFindings.Add(finding);

            _logger.LogInformation(
                LogMessages.DocumentArtifactOrphanCleanupCompleted);
        }

        RecoverDocumentArtifactsResult result =
            new(
                recoveredAnyArtifact
                    ? RecoverDocumentArtifactsResultStatus.Recovered
                    : preservedAnyFinding
                        ? RecoverDocumentArtifactsResultStatus.PreservedForRecovery
                        : RecoverDocumentArtifactsResultStatus.NoActionRequired,
                recoveryFindings);

        _logger.LogInformation(
            LogMessages.DocumentArtifactRecoveryCompleted);

        return result;
    }
}
