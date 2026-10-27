using DeskVault.Application.Documents.Processing;
using DeskVault.Application.Interfaces;
using DeskVault.Shared.Extensions;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace DeskVault.Application.Documents.Queries.ReconcileDocumentArtifacts;

public sealed class ReconcileDocumentArtifactsHandler
{
    private readonly IDocumentRepository _repository;
    private readonly IDocumentArtifactEnumerator _artifactEnumerator;
    private readonly IDocumentReader _documentReader;
    private readonly IStorageService _storageService;
    private readonly ILogger<ReconcileDocumentArtifactsHandler> _logger;

    public ReconcileDocumentArtifactsHandler(
        IDocumentRepository repository,
        IDocumentArtifactEnumerator artifactEnumerator,
        IDocumentReader documentReader,
        IStorageService storageService,
        ILogger<ReconcileDocumentArtifactsHandler> logger)
    {
        _repository = repository;
        _artifactEnumerator = artifactEnumerator;
        _documentReader = documentReader;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<ReconcileDocumentArtifactsResult> HandleAsync(
        ReconcileDocumentArtifactsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        cancellationToken.ThrowIfCancellationRequested();

        var documents =
            await _repository.GetAllAsync(
                cancellationToken);

        var artifactPaths =
            await _artifactEnumerator.EnumerateAsync(
                cancellationToken);

        var normalizedArtifactPaths =
            new HashSet<string>(
                artifactPaths.Select(Path.GetFullPath),
                StringComparer.OrdinalIgnoreCase);

        var expectedArtifactPaths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var findings =
            new List<DocumentArtifactReconciliationResult>();

        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string expectedArtifactPath;

            try
            {
                expectedArtifactPath =
                    Path.GetFullPath(
                        document.StoredFilePath);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    or NotSupportedException
                    or PathTooLongException
                    or System.Security.SecurityException)
            {
                _logger.LogSafeFailure(
                    LogLevel.Warning,
                    DiagnosticOperationCodes.DocumentArtifactPathNormalizationFailure,
                    LogMessages.DocumentArtifactValidationFailed,
                    exception,
                    failureCategory: exception is System.Security.SecurityException
                        ? DiagnosticFailureCategory.AccessDenied
                        : DiagnosticFailureCategory.InvalidData);

                findings.Add(
                    new DocumentArtifactReconciliationResult(
                        DocumentArtifactReconciliationStatus.PathMismatch,
                        document.Id,
                        document.StoredFilePath,
                        "The persisted document record contains an artifact path that cannot be safely normalized or validated."));

                continue;
            }

            if (!_storageService.IsOwnedArtifactPath(
                    document.Id,
                    expectedArtifactPath))
            {
                findings.Add(
                    new DocumentArtifactReconciliationResult(
                        DocumentArtifactReconciliationStatus.PathMismatch,
                        document.Id,
                        expectedArtifactPath,
                        "The persisted document record points to an artifact path that is not owned by the document."));

                continue;
            }

            expectedArtifactPaths.Add(
                expectedArtifactPath);

            if (!normalizedArtifactPaths.Contains(
                    expectedArtifactPath))
            {
                findings.Add(
                    new DocumentArtifactReconciliationResult(
                        DocumentArtifactReconciliationStatus.MissingArtifact,
                        document.Id,
                        expectedArtifactPath,
                        "The persisted document record has no corresponding encrypted artifact."));

                continue;
            }

            try
            {
                await using var verifiedStream =
                    await _documentReader.OpenReadAsync(
                        document.Id,
                        document.Sha256Hash,
                        cancellationToken);

                findings.Add(
                    new DocumentArtifactReconciliationResult(
                        DocumentArtifactReconciliationStatus.Matched,
                        document.Id,
                        expectedArtifactPath,
                        "The persisted document record has a readable encrypted artifact whose decrypted content matches the persisted document content identity."));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DocumentContentIntegrityException)
            {
                findings.Add(
                    new DocumentArtifactReconciliationResult(
                        DocumentArtifactReconciliationStatus.ContentMismatch,
                        document.Id,
                        expectedArtifactPath,
                        "The encrypted artifact is readable but its decrypted content does not match the persisted document content identity."));
            }
            catch (Exception exception)
                when (exception is IOException
                    or UnauthorizedAccessException
                    or InvalidDataException
                    or CryptographicException)
            {
                _logger.LogSafeFailure(
                    LogLevel.Warning,
                    DiagnosticOperationCodes.DocumentArtifactReadValidationFailure,
                    LogMessages.DocumentArtifactValidationFailed,
                    exception);

                findings.Add(
                    new DocumentArtifactReconciliationResult(
                        DocumentArtifactReconciliationStatus.UnreadableArtifact,
                        document.Id,
                        expectedArtifactPath,
                        "The encrypted artifact exists but could not be validated or read."));
            }
        }

        foreach (var artifactPath in normalizedArtifactPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (expectedArtifactPaths.Contains(
                    artifactPath))
            {
                continue;
            }

            findings.Add(
                new DocumentArtifactReconciliationResult(
                    DocumentArtifactReconciliationStatus.OrphanedArtifact,
                    TryGetDocumentId(artifactPath),
                    artifactPath,
                    "The encrypted artifact has no corresponding persisted document record."));
        }

        return new ReconcileDocumentArtifactsResult(
            findings);
    }

    private static Guid? TryGetDocumentId(
        string artifactPath)
    {
        var fileName =
            Path.GetFileNameWithoutExtension(
                artifactPath);

        return Guid.TryParse(
            fileName,
            out var documentId)
            ? documentId
            : null;
    }
}
