using System.Security.Cryptography;
using DeskVault.Application.Interfaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;
using DeskVault.Shared.Extensions;
using DiagnosticOperationCodes = DeskVault.Shared.Resources.DiagnosticOperationCodes;

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
        ArgumentNullException.ThrowIfNull(filePath);

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

            return await ComputeSha256CoreAsync(
                stream,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentHashFailure,
                LogMessages.DocumentHashFailed,
                ex);

            throw;
        }
    }

    public async Task<string> ComputeSha256Async(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            LogMessages.DocumentHashStarted);

        try
        {
            return await ComputeSha256CoreAsync(
                content,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentHashFailure,
                LogMessages.DocumentHashFailed,
                ex);

            throw;
        }
    }

    private async Task<string> ComputeSha256CoreAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        byte[] hash =
            await SHA256.HashDataAsync(
                content,
                cancellationToken);

        string result =
            Convert.ToHexString(hash)
                .ToLowerInvariant();

        _logger.LogInformation(
            LogMessages.DocumentHashCompleted);

        return result;
    }
}
