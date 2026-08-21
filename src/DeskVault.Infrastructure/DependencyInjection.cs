using DeskVault.Application.Interfaces;
using DeskVault.Infrastructure.Persistence;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Repositories;
using DeskVault.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeskVault.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IApplicationInfoService, ApplicationInfoService>();

        services.AddSingleton<DeskVaultDataPaths>();

        services.AddDbContextFactory<DeskVaultDbContext>(
            (serviceProvider, options) =>
            {
                var paths =
                    serviceProvider.GetRequiredService<DeskVaultDataPaths>();

                options.UseSqlite(
                    $"Data Source={paths.DatabasePath}");
            });

        services.AddSingleton<DatabaseInitializer>();

        services.AddSingleton<IHashService, Sha256HashService>();

        services.AddSingleton<IEncryptionKeyService, WindowsEncryptionKeyService>();

        services.AddSingleton<IStorageService, FileSystemStorageService>();

        services.AddSingleton<DocumentEncryptionService>();

        services.AddSingleton<IDocumentReader, EncryptedDocumentReader>();

        services.AddSingleton<IDocumentRepository, SqliteDocumentRepository>();

        services.AddSingleton<IDocumentProcessingStore, SqliteDocumentProcessingStore>();

        return services;
    }
}
