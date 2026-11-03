using DeskVault.Application.Interfaces;
using DeskVault.Infrastructure.Persistence;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Repositories;
using DeskVault.Infrastructure.Security.Authentication;
using DeskVault.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        SqliteProviderBootstrap.EnsureInitialized();

        services.AddSingleton<IApplicationInfoService, ApplicationInfoService>();

        services.AddSingleton<IWindowsHelloPlatformApi, WindowsHelloPlatformApi>();

        services.AddSingleton<IApplicationAuthenticationProvider, WindowsHelloAuthenticationProvider>();

        services.TryAddSingleton<DeskVaultDataPaths>();

        services.AddSingleton<DocumentArtifactPathResolver>();

        services.AddSingleton<IDbContextFactory<DeskVaultDbContext>, EncryptedDeskVaultDbContextFactory>();

        services.AddSingleton<VaultInitializationCoordinator>();

        services.AddSingleton<DatabaseInitializer>();

        services.AddSingleton<DocumentChunkIdentityBackfill>();

        services.AddSingleton<DocumentLifecycleStateBackfill>();

        services.AddSingleton<IHashService, Sha256HashService>();

        services.AddSingleton<IEncryptionKeyService>(
            serviceProvider =>
            {
                var innerService =
                    new WindowsEncryptionKeyService(
                        serviceProvider.GetRequiredService<DeskVaultDataPaths>(),
                        serviceProvider.GetRequiredService<ILogger<WindowsEncryptionKeyService>>());

                return new SessionAuthorizedEncryptionKeyService(
                    innerService,
                    serviceProvider.GetRequiredService<IApplicationSecuritySession>());
            });

        services.AddSingleton<IEncryptionKeyProvisioningService, WindowsEncryptionKeyProvisioningService>();

        services.AddSingleton<IDatabaseEncryptionKeyService>(
            serviceProvider =>
            {
                var innerService =
                    new WindowsDatabaseEncryptionKeyService(
                        serviceProvider.GetRequiredService<DeskVaultDataPaths>(),
                        serviceProvider.GetRequiredService<ILogger<WindowsDatabaseEncryptionKeyService>>());

                return new SessionAuthorizedDatabaseEncryptionKeyService(
                    innerService,
                    serviceProvider.GetRequiredService<IApplicationSecuritySession>());
            });

        services.AddSingleton<IDatabaseEncryptionKeyProvisioningService, WindowsDatabaseEncryptionKeyProvisioningService>();

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
