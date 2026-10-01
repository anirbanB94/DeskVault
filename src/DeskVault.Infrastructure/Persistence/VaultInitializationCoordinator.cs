using DeskVault.Infrastructure.Services;

namespace DeskVault.Infrastructure.Persistence;

public sealed class VaultInitializationCoordinator
{
    private const int RetryDelayMilliseconds = 100;
    private const int SharingViolationWin32Error = 32;

    private readonly string _lockPath;

    public VaultInitializationCoordinator(
        DeskVaultDataPaths dataPaths)
    {
        ArgumentNullException.ThrowIfNull(dataPaths);

        _lockPath =
            dataPaths.DatabasePath +
            ".initialization.lock";
    }

    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using FileStream lockStream =
            await AcquireLockAsync(
                cancellationToken);

        await operation(
            cancellationToken);
    }

    private async Task<FileStream> AcquireLockAsync(
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(_lockPath)!);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return new FileStream(
                    _lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    options: FileOptions.Asynchronous);
            }
            catch (IOException exception)
                when (IsSharingViolation(exception))
            {
                await Task.Delay(
                    RetryDelayMilliseconds,
                    cancellationToken);
            }
        }
    }

    private static bool IsSharingViolation(
        IOException exception)
    {
        return
            (exception.HResult & 0xFFFF) ==
            SharingViolationWin32Error;
    }
}
