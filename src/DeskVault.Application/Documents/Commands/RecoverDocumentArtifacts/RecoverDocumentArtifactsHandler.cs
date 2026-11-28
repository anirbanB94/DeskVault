using DeskVault.Application.Documents.Queries.ReconcileDocumentArtifacts;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using DeskVault.Shared.Extensions;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Application.Documents.Commands.RecoverDocumentArtifacts;

public sealed class RecoverDocumentArtifactsHandler
{
    private readonly IDocumentRepository _repository;
    private readonly ReconcileDocumentArtifactsHandler _reconciliationHandler;
    private readonly IStorageService _storageService;
    private readonly IDocumentArtifactEnumerator _artifactEnumerator;
    private readonly IDocumentArtifactCoordinator _artifactCoordinator;
    private readonly ILogger<RecoverDocumentArtifactsHandler> _logger;

    public RecoverDocumentArtifactsHandler(
        IDocumentRepository repository,
        ReconcileDocumentArtifactsHandler reconciliationHandler,
        IStorageService storageService,
        IDocumentArtifactEnumerator artifactEnumerator,
        IDocumentArtifactCoordinator artifactCoordinator,
        ILogger<RecoverDocumentArtifactsHandler> logger)
    {
        _repository = repository;
        _reconciliationHandler = reconciliationHandler;
        _storageService = storageService;
        _artifactEnumerator = artifactEnumerator;
        _artifactCoordinator = artifactCoordinator;
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

            // Recheck current metadata immediately before cleanup.
            IAsyncDisposable documentArtifactLease;

            try
            {
                // Acquire only for this candidate, after read-only reconciliation.
                documentArtifactLease =
                    await _artifactCoordinator.AcquireAsync(
                        documentId,
                        cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Cancellation while waiting must prevent destructive recovery.
                throw;
            }
            catch (Exception ex)
            {
                // Recovery must never delete an artifact without its coordination lease.
                _logger.LogSafeFailure(
                    LogLevel.Error,
                    DiagnosticOperationCodes.DocumentArtifactCoordinationFailure,
                    LogMessages.DocumentArtifactCoordinationFailed,
                    ex);

                throw;
            }

            await using (documentArtifactLease)
            {
                // The initial reconciliation finding may be stale.
                // Re-read persisted metadata only after acquiring the lease.
                Document? persistedDocument =
                    await _repository.GetByIdAsync(
                        documentId,
                        cancellationToken);

                if (persistedDocument is not null)
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

                bool isOwnedArtifactPath;

                try
                {
                    isOwnedArtifactPath =
                        _storageService.IsOwnedArtifactPath(
                            documentId,
                            finding.ArtifactPath);
                }
                catch (Exception ex)
                    when (ex is IOException
                        or UnauthorizedAccessException
                        or System.Security.SecurityException
                        or ArgumentException
                        or NotSupportedException)
                {
                    _logger.LogSafeFailure(
                        LogLevel.Warning,
                        DiagnosticOperationCodes.DocumentArtifactPathNormalizationFailure,
                        LogMessages.DocumentArtifactRecoveryOwnershipValidationFailed,
                        ex,
                        failureCategory: ex is UnauthorizedAccessException
                            or System.Security.SecurityException
                                ? DiagnosticFailureCategory.AccessDenied
                                : DiagnosticFailureCategory.InvalidData);

                    preservedAnyFinding = true;

                    recoveryFindings.Add(
                        finding with
                        {
                            RecoveryAction =
                                DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery
                        });

                    continue;
                }

                if (!isOwnedArtifactPath)
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

                bool artifactExists;

                try
                {
                    // Recheck the canonical artifact itself after acquiring the lease.
                    artifactExists =
                        _artifactEnumerator.ExistsForDocument(
                            documentId);
                }
                catch (Exception ex)
                    when (ex is IOException
                        or UnauthorizedAccessException
                        or System.Security.SecurityException
                        or ArgumentException
                        or NotSupportedException)
                {
                    _logger.LogSafeFailure(
                        LogLevel.Warning,
                        DiagnosticOperationCodes.DocumentArtifactReadValidationFailure,
                        LogMessages.DocumentArtifactValidationFailed,
                        ex,
                        failureCategory: ex is UnauthorizedAccessException
                            or System.Security.SecurityException
                                ? DiagnosticFailureCategory.AccessDenied
                                : DiagnosticFailureCategory.InvalidData);

                    preservedAnyFinding = true;

                    recoveryFindings.Add(
                        finding with
                        {
                            RecoveryAction =
                                DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery
                        });

                    continue;
                }

                if (!artifactExists)
                {
                    // The orphan finding became stale: there is nothing left to delete.
                    // Report no recovery action rather than leaving a false cleanup request.
                    recoveryFindings.Add(
                        finding with
                        {
                            RecoveryAction =
                                DocumentArtifactReconciliationRecoveryAction.None,
                            Description =
                                "The orphaned artifact was no longer present when recovery revalidated it; no cleanup was required."
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
