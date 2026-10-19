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

        services.AddSingleton<DocumentArtifactPathResolver>();

        services.AddSingleton<IDbContextFactory<DeskVaultDbContext>, EncryptedDeskVaultDbContextFactory>();

        services.AddSingleton<VaultInitializationCoordinator>();

        services.AddSingleton<DatabaseInitializer>();

        services.AddSingleton<DocumentChunkIdentityBackfill>();

        services.AddSingleton<DocumentLifecycleStateBackfill>();

        services.AddSingleton<IHashService, Sha256HashService>();

        services.AddSingleton<WindowsEncryptionKeyService>();

        services.AddSingleton<IEncryptionKeyService>(
            serviceProvider =>
                serviceProvider.GetRequiredService<WindowsEncryptionKeyService>());

        services.AddSingleton<IEncryptionKeyProvisioningService>(
            serviceProvider =>
                serviceProvider.GetRequiredService<WindowsEncryptionKeyService>());

        services.AddSingleton<WindowsDatabaseEncryptionKeyService>();

        services.AddSingleton<IDatabaseEncryptionKeyService>(
            serviceProvider =>
                serviceProvider.GetRequiredService<WindowsDatabaseEncryptionKeyService>());

        services.AddSingleton<IDatabaseEncryptionKeyProvisioningService>(
            serviceProvider =>
                serviceProvider.GetRequiredService<WindowsDatabaseEncryptionKeyService>());

        services.AddSingleton<IDatabaseFormatDetector, SqliteDatabaseFormatDetector>();

        services.AddSingleton<IDatabaseEncryptionMigrator, SqliteDatabaseEncryptionMigrator>();

        services.AddSingleton<IStorageService, FileSystemStorageService>();

        services.AddSingleton<DocumentEncryptionService>();

        services.AddSingleton<IDocumentReader, EncryptedDocumentReader>();

        services.AddSingleton<IDocumentRepository, SqliteDocumentRepository>();

        services.AddSingleton<IWorkspaceRepository, SqliteWorkspaceRepository>();

        services.AddSingleton<IDocumentProcessingStore, SqliteDocumentProcessingStore>();

        services.AddSingleton<IDocumentSearchStore, SqliteDocumentSearchStore>();

        services.AddSingleton<IDocumentArtifactEnumerator, DocumentArtifactEnumerator>();

        return services;
    }
}
