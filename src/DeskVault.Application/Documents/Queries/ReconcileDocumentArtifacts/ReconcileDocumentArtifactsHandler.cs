using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Processing;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using DeskVault.Shared.Extensions;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace DeskVault.Application.Documents.Queries.ReconcileDocumentArtifacts;

public sealed class ReconcileDocumentArtifactsHandler
{
    private readonly IDocumentRepository _repository;
    private readonly IDocumentArtifactEnumerator _artifactEnumerator;
    private readonly IDocumentReader _documentReader;
    private readonly IStorageService _storageService;
    private readonly long _maximumPlaintextBytes;
    private readonly ILogger<ReconcileDocumentArtifactsHandler> _logger;

    public ReconcileDocumentArtifactsHandler(
        IDocumentRepository repository,
        IDocumentArtifactEnumerator artifactEnumerator,
        IDocumentReader documentReader,
        IStorageService storageService,
        DocumentProcessingOptions processingOptions,
        ILogger<ReconcileDocumentArtifactsHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(processingOptions);

        if (processingOptions.MaxDecryptedDocumentBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(processingOptions),
                "Maximum decrypted document bytes must be greater than zero.");
        }

        _repository = repository;
        _artifactEnumerator = artifactEnumerator;
        _documentReader = documentReader;
        _storageService = storageService;
        _maximumPlaintextBytes =
            processingOptions.MaxDecryptedDocumentBytes;
        _logger = logger;
    }

    public async Task<ReconcileDocumentArtifactsResult> HandleAsync(
        ReconcileDocumentArtifactsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        cancellationToken.ThrowIfCancellationRequested();

        var findings =
            new List<DocumentArtifactReconciliationResult>();

        await foreach (
            var document in EnumerateDocumentsInPagesAsync(
                cancellationToken))
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

            bool artifactExists;

            try
            {
                artifactExists =
                    _artifactEnumerator.ExistsForDocument(
                        document.Id);
            }
            catch (Exception exception)
                when (exception is IOException
                    or UnauthorizedAccessException
                    or System.Security.SecurityException
                    or ArgumentException
                    or NotSupportedException)
            {
                _logger.LogSafeFailure(
                    LogLevel.Warning,
                    DiagnosticOperationCodes.DocumentArtifactReadValidationFailure,
                    LogMessages.DocumentArtifactValidationFailed,
                    exception,
                    failureCategory: exception is UnauthorizedAccessException
                        or System.Security.SecurityException
                            ? DiagnosticFailureCategory.AccessDenied
                            : DiagnosticFailureCategory.InvalidData);

                findings.Add(
                    new DocumentArtifactReconciliationResult(
                        DocumentArtifactReconciliationStatus.UnreadableArtifact,
                        document.Id,
                        expectedArtifactPath,
                        "The expected encrypted artifact could not be accessed to determine whether it is available for validation."));

                continue;
            }

            if (!artifactExists)
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
                await _documentReader.ValidateContentHashAsync(
                    document.Id,
                    document.Sha256Hash,
                    _maximumPlaintextBytes,
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
                    or System.Security.SecurityException
                    or InvalidDataException
                    or CryptographicException
                    or ResourceLimitExceededException)
            {
                _logger.LogSafeFailure(
                    LogLevel.Warning,
                    DiagnosticOperationCodes.DocumentArtifactReadValidationFailure,
                    LogMessages.DocumentArtifactValidationFailed,
                    exception,
                    failureCategory: exception is UnauthorizedAccessException
                        or System.Security.SecurityException
                            ? DiagnosticFailureCategory.AccessDenied
                            : null);

                findings.Add(
                    new DocumentArtifactReconciliationResult(
                        DocumentArtifactReconciliationStatus.UnreadableArtifact,
                        document.Id,
                        expectedArtifactPath,
                        "The encrypted artifact exists but could not be validated or read."));
            }
        }

        await foreach (
            string artifactPath in _artifactEnumerator.EnumerateIncrementallyAsync(
                cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string normalizedArtifactPath =
                Path.GetFullPath(artifactPath);

            Guid? documentId =
                TryGetDocumentId(normalizedArtifactPath);

            if (documentId.HasValue)
            {
                Document? document =
                    await _repository.GetByIdAsync(
                        documentId.Value,
                        cancellationToken);

                if (document is not null &&
                    IsExpectedArtifactPath(
                        document,
                        normalizedArtifactPath))
                {
                    continue;
                }
            }

            findings.Add(
                new DocumentArtifactReconciliationResult(
                    DocumentArtifactReconciliationStatus.OrphanedArtifact,
                    documentId,
                    normalizedArtifactPath,
                    "The encrypted artifact has no corresponding persisted document record."));
        }

        return new ReconcileDocumentArtifactsResult(
            findings);
    }

    private async IAsyncEnumerable<Document> EnumerateDocumentsInPagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        const int pageSize =
            IDocumentRepository.MaximumDocumentPageSize;

        DocumentPageCursor? cursor = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<Document> documents =
                await _repository.GetPageAsync(
                    cursor,
                    pageSize,
                    cancellationToken);

            if (documents.Count == 0)
            {
                yield break;
            }

            foreach (Document document in documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return document;
            }

            Document lastDocument = documents[^1];

            cursor = new DocumentPageCursor(
                lastDocument.ImportedAt,
                lastDocument.Id);

            if (documents.Count < pageSize)
            {
                yield break;
            }
        }
    }

    private bool IsExpectedArtifactPath(
        Document document,
        string artifactPath)
    {
        if (!_storageService.IsOwnedArtifactPath(
                document.Id,
                artifactPath))
        {
            return false;
        }

        try
        {
            string expectedArtifactPath =
                Path.GetFullPath(
                    document.StoredFilePath);

            return string.Equals(
                expectedArtifactPath,
                artifactPath,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception)
            when (exception is ArgumentException
                or NotSupportedException
                or PathTooLongException
                or System.Security.SecurityException)
        {
            return false;
        }
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
