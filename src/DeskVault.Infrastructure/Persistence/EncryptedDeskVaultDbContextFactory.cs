using System.Security.Cryptography;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace DeskVault.Infrastructure.Persistence;

public sealed class EncryptedDeskVaultDbContextFactory :
    IDbContextFactory<DeskVaultDbContext>
{
    private readonly DeskVaultDataPaths _dataPaths;
    private readonly IDatabaseEncryptionKeyService _databaseEncryptionKeyService;
    private const string DataSourcePrefix = "Data Source=";
    private const string PasswordPrefix = ";Password=";
    private const string PoolingSuffix = ";Pooling=False";

    public EncryptedDeskVaultDbContextFactory(
        DeskVaultDataPaths dataPaths,
        IDatabaseEncryptionKeyService databaseEncryptionKeyService)
    {
        _dataPaths = dataPaths;
        _databaseEncryptionKeyService = databaseEncryptionKeyService;
    }

    public DeskVaultDbContext CreateDbContext()
    {
        EncryptionKeyMaterial databaseKeyMaterial =
            _databaseEncryptionKeyService
                .GetKeyAsync()
                .GetAwaiter()
                .GetResult();

        byte[] databaseKey = databaseKeyMaterial.KeyBytes;

        try
        {
            return CreateDbContext(databaseKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                databaseKey);
        }
    }

    public async Task<DeskVaultDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        EncryptionKeyMaterial databaseKeyMaterial =
            await _databaseEncryptionKeyService.GetKeyAsync(
                cancellationToken);

        byte[] databaseKey = databaseKeyMaterial.KeyBytes;

        try
        {
            return CreateDbContext(databaseKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                databaseKey);
        }
    }

    private DeskVaultDbContext CreateDbContext(
        byte[] databaseKey)
    {
        var optionsBuilder =
            new DbContextOptionsBuilder<DeskVaultDbContext>();

        optionsBuilder.UseSqlite(
            CreateConnectionString(
                databaseKey,
                _dataPaths.DatabasePath));

        return new DeskVaultDbContext(
            optionsBuilder.Options);
    }

    private static string CreateConnectionString(
        byte[] databaseKey,
        string databasePath)
    {
        int encodedPasswordLength =
            checked(((databaseKey.Length + 2) / 3) * 4);

        int connectionStringLength =
            checked(
                DataSourcePrefix.Length +
                databasePath.Length +
                PasswordPrefix.Length +
                encodedPasswordLength +
                PoolingSuffix.Length);

        return string.Create(
            connectionStringLength,
            (
                DatabaseKey: databaseKey,
                DatabasePath: databasePath,
                EncodedPasswordLength: encodedPasswordLength
            ),
            static (destination, state) =>
            {
                int offset = 0;

                DataSourcePrefix.AsSpan().CopyTo(
                    destination[offset..]);

                offset += DataSourcePrefix.Length;

                state.DatabasePath.AsSpan().CopyTo(
                    destination[offset..]);

                offset += state.DatabasePath.Length;

                PasswordPrefix.AsSpan().CopyTo(
                    destination[offset..]);

                offset += PasswordPrefix.Length;

                bool encoded = Convert.TryToBase64Chars(
                    state.DatabaseKey,
                    destination.Slice(
                        offset,
                        state.EncodedPasswordLength),
                    out int charactersWritten);

                if (!encoded ||
                    charactersWritten != state.EncodedPasswordLength)
                {
                    throw new InvalidOperationException(
                        "Could not encode the database encryption key.");
                }

                offset += charactersWritten;

                PoolingSuffix.AsSpan().CopyTo(
                    destination[offset..]);
            });
    }
}
