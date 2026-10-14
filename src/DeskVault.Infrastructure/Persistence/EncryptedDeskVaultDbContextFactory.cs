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
        string databasePassword =
            Convert.ToBase64String(databaseKey);

        var optionsBuilder =
            new DbContextOptionsBuilder<DeskVaultDbContext>();

        optionsBuilder.UseSqlite(
            $"Data Source={_dataPaths.DatabasePath};Password={databasePassword};Pooling=False");

        return new DeskVaultDbContext(
            optionsBuilder.Options);
    }
}
