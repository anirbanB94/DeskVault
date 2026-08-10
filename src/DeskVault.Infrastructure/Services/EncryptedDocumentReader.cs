using DeskVault.Application.Interfaces;

namespace DeskVault.Infrastructure.Services;

public sealed class EncryptedDocumentReader : IDocumentReader
{
    private readonly DocumentEncryptionService _encryptionService;

    public EncryptedDocumentReader(
        DocumentEncryptionService encryptionService)
    {
        _encryptionService = encryptionService;
    }

    public async Task<Stream> OpenReadAsync(
        string storedFilePath,
        CancellationToken cancellationToken = default)
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

        return decryptedStream;
    }
}