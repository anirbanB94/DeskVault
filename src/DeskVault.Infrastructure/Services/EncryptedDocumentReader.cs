using DeskVault.Application.Documents.Processing;
using DeskVault.Application.Interfaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace DeskVault.Infrastructure.Services;

public sealed class EncryptedDocumentReader : IDocumentReader
{
    private readonly DocumentEncryptionService _encryptionService;

    private readonly DocumentArtifactPathResolver _artifactPathResolver;

    private readonly ILogger<EncryptedDocumentReader> _logger;

    public EncryptedDocumentReader(
        DocumentEncryptionService encryptionService,
        DocumentArtifactPathResolver artifactPathResolver,
        ILogger<EncryptedDocumentReader> logger)
    {
        _encryptionService = encryptionService;
        _artifactPathResolver = artifactPathResolver;
        _logger = logger;
    }

    public async Task<Stream> OpenReadAsync(
        Guid documentId,
        string expectedSha256Hash,
        CancellationToken cancellationToken = default,
        long? maximumPlaintextBytes = null)
    {
        ArgumentNullException.ThrowIfNull(expectedSha256Hash);

        cancellationToken.ThrowIfCancellationRequested();

        string storedFilePath =
            _artifactPathResolver.GetPath(
                documentId);

        _logger.LogInformation(
            LogMessages.DocumentReaderStarted);

        try
        {
            await using var source =
                new FileStream(
                    storedFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    useAsync: true);

            var decryptedStream =
                new MemoryStream();

            try
            {
                await _encryptionService.DecryptAsync(
                    source,
                    decryptedStream,
                    documentId,
                    cancellationToken,
                    maximumPlaintextBytes);

                decryptedStream.Position = 0;

                byte[] actualHashBytes =
                    await SHA256.HashDataAsync(
                        decryptedStream,
                        cancellationToken);

                string actualSha256Hash =
                    Convert.ToHexString(
                        actualHashBytes);

                if (!string.Equals(
                    actualSha256Hash,
                    expectedSha256Hash,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new DocumentContentIntegrityException();
                }

                decryptedStream.Position = 0;

                _logger.LogInformation(
                    LogMessages.DocumentReaderCompleted);

                return decryptedStream;
            }
            catch
            {
                await decryptedStream.DisposeAsync();

                throw;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.DocumentReaderFailed);

            throw;
        }
    }
}
