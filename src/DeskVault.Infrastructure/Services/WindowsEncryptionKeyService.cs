using System.Security.Cryptography;

namespace DeskVault.Infrastructure.Services;

public sealed class WindowsEncryptionKeyService :
    IEncryptionKeyService
{
    private const string KeyFileName = "master.key";

    private readonly DeskVaultDataPaths _dataPaths;

    public WindowsEncryptionKeyService(
        DeskVaultDataPaths dataPaths)
    {
        _dataPaths = dataPaths;
    }

    public async Task<byte[]> GetOrCreateKeyAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string securityDirectory =
            _dataPaths.SecurityDirectory;

        Directory.CreateDirectory(
            securityDirectory);

        string keyFilePath =
            Path.Combine(
                securityDirectory,
                KeyFileName);

        if (File.Exists(keyFilePath))
        {
            byte[] protectedKeyFromFile =
                await File.ReadAllBytesAsync(
                    keyFilePath,
                    cancellationToken);

            return ProtectedData.Unprotect(
                protectedKeyFromFile,
                null,
                DataProtectionScope.CurrentUser);
        }

        byte[] key =
            RandomNumberGenerator.GetBytes(32);

        byte[] protectedKey =
            ProtectedData.Protect(
                key,
                null,
                DataProtectionScope.CurrentUser);

        await File.WriteAllBytesAsync(
            keyFilePath,
            protectedKey,
            cancellationToken);

        return key;
    }
}
