using DeskVault.Application.Interfaces;
using DeskVault.Infrastructure.Persistence;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Repositories;
using DeskVault.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DeskVault.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        SQLitePCL.Batteries_V2.Init();

        services.AddSingleton<IApplicationInfoService, ApplicationInfoService>();

        services.TryAddSingleton<DeskVaultDataPaths>();

        services.AddSingleton<IDbContextFactory<DeskVaultDbContext>, EncryptedDeskVaultDbContextFactory>();

        services.AddSingleton<DatabaseInitializer>();

        services.AddSingleton<DocumentChunkIdentityBackfill>();

        services.AddSingleton<IHashService, Sha256HashService>();

        services.AddSingleton<IEncryptionKeyService, WindowsEncryptionKeyService>();

        services.AddSingleton<IDatabaseEncryptionKeyService, WindowsDatabaseEncryptionKeyService>();

        services.AddSingleton<IDatabaseFormatDetector, SqliteDatabaseFormatDetector>();

        services.AddSingleton<IDatabaseEncryptionMigrator, SqliteDatabaseEncryptionMigrator>();

        services.AddSingleton<IStorageService, FileSystemStorageService>();

        services.AddSingleton<DocumentEncryptionService>();

        services.AddSingleton<IDocumentReader, EncryptedDocumentReader>();

        services.AddSingleton<IDocumentRepository, SqliteDocumentRepository>();

        services.AddSingleton<IDocumentProcessingStore, SqliteDocumentProcessingStore>();

        services.AddSingleton<IDocumentSearchStore, SqliteDocumentSearchStore>();

        services.AddSingleton<IDocumentArtifactEnumerator, DocumentArtifactEnumerator>();

        return services;
    }
}
