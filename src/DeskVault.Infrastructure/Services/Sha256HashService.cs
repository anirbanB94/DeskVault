using System.Security.Cryptography;
using DeskVault.Application.Interfaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Services;

public sealed class Sha256HashService : IHashService
{
    private readonly ILogger<Sha256HashService> _logger;

    public Sha256HashService(
        ILogger<Sha256HashService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ComputeSha256Async(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            LogMessages.DocumentHashStarted);

        try
        {
            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

            var hash =
                await SHA256.HashDataAsync(
                    stream,
                    cancellationToken);

            string result =
                Convert.ToHexString(hash)
                    .ToLowerInvariant();

            _logger.LogInformation(
                LogMessages.DocumentHashCompleted);

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.DocumentHashFailed);

            throw;
        }
    }
}
