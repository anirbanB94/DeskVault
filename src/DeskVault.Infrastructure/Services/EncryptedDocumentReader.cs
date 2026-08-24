using DeskVault.Application.Interfaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Services;

public sealed class EncryptedDocumentReader : IDocumentReader
{
    private readonly DocumentEncryptionService _encryptionService;
    private readonly ILogger<EncryptedDocumentReader> _logger;

    public EncryptedDocumentReader(
        DocumentEncryptionService encryptionService,
        ILogger<EncryptedDocumentReader> logger)
    {
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<Stream> OpenReadAsync(
        string storedFilePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            LogMessages.DocumentReaderStarted);

        try
        {
            await using var source = new FileStream(
                storedFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

            var decryptedStream = new MemoryStream();

            await _encryptionService.DecryptAsync(
                source,
                decryptedStream,
                cancellationToken);

            decryptedStream.Position = 0;

            _logger.LogInformation(
                LogMessages.DocumentReaderCompleted);

            return decryptedStream;
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
