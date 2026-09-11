using System.Security.Cryptography;
using DeskVault.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace DeskVault.Application.Documents.Queries.ReconcileDocumentArtifacts;

public sealed class ReconcileDocumentArtifactsHandler
{
    private const string ArtifactExtension = ".dvault";

    private readonly IDocumentRepository _repository;
    private readonly IDocumentArtifactEnumerator _artifactEnumerator;
    private readonly IDocumentReader _documentReader;
    private readonly ILogger<ReconcileDocumentArtifactsHandler> _logger;

    public ReconcileDocumentArtifactsHandler(
        IDocumentRepository repository,
        IDocumentArtifactEnumerator artifactEnumerator,
        IDocumentReader documentReader,
        ILogger<ReconcileDocumentArtifactsHandler> logger)
    {
        _repository = repository;
        _artifactEnumerator = artifactEnumerator;
        _documentReader = documentReader;
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

            var expectedArtifactPath =
                Path.GetFullPath(
                    document.StoredFilePath);

            var canonicalArtifactPath =
                Path.GetFullPath(
                    Path.Combine(
                        Path.GetDirectoryName(
                            expectedArtifactPath)
                        ?? string.Empty,
                        $"{document.Id}{ArtifactExtension}"));

            expectedArtifactPaths.Add(
                expectedArtifactPath);

            if (!string.Equals(
                    expectedArtifactPath,
                    canonicalArtifactPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(
                    new DocumentArtifactReconciliationResult(
                        DocumentArtifactReconciliationStatus.PathMismatch,
                        document.Id,
                        expectedArtifactPath,
                        "The persisted document record points to an artifact path that does not match the document identity."));

                continue;
            }

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
                await using var stream =
                    await _documentReader.OpenReadAsync(
                        expectedArtifactPath,
                        cancellationToken);

                findings.Add(
                    new DocumentArtifactReconciliationResult(
                        DocumentArtifactReconciliationStatus.Matched,
                        document.Id,
                        expectedArtifactPath,
                        "The persisted document record has a readable encrypted artifact."));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
                when (exception is IOException
                    or UnauthorizedAccessException
                    or InvalidDataException
                    or CryptographicException)
            {
                _logger.LogWarning(
                    exception,
                    "Document artifact could not be validated for document {DocumentId}.",
                    document.Id);

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
