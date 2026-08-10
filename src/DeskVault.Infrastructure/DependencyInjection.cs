using DeskVault.Application.Interfaces;
using DeskVault.Infrastructure.Repositories;
using DeskVault.Infrastructure.Services;
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

        services.AddSingleton<IHashService, Sha256HashService>();

        services.AddSingleton<IEncryptionKeyService, WindowsEncryptionKeyService>();

        services.AddSingleton<IStorageService, FileSystemStorageService>();

        services.AddSingleton<DocumentEncryptionService>();

        services.AddSingleton<IDocumentReader, EncryptedDocumentReader>();

        services.AddSingleton<IDocumentRepository, InMemoryDocumentRepository>();

        return services;
    }
}