using DeskVault.Application.Interfaces;
using DeskVault.Infrastructure.Repositories;
using DeskVault.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Tests;

public sealed class InfrastructureDependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_RegistersDocumentProcessingPipeline()
    {
        using ServiceProvider serviceProvider =
            CreateServiceProvider();

        Assert.NotNull(
            serviceProvider.GetRequiredService<IDocumentRepository>());

        Assert.NotNull(
            serviceProvider.GetRequiredService<IDocumentProcessingStore>());

        Assert.NotNull(
            serviceProvider.GetRequiredService<IDocumentReader>());
    }

    [Fact]
    public void AddInfrastructure_RegistersExpectedConcreteImplementations()
    {
        using ServiceProvider serviceProvider =
            CreateServiceProvider();

        Assert.IsType<SqliteDocumentRepository>(
            serviceProvider.GetRequiredService<IDocumentRepository>());

        Assert.IsType<SqliteDocumentProcessingStore>(
            serviceProvider.GetRequiredService<IDocumentProcessingStore>());

        Assert.IsType<EncryptedDocumentReader>(
            serviceProvider.GetRequiredService<IDocumentReader>());
    }

    [Fact]
    public void AddInfrastructure_RegistersDatabaseEncryptionKeyService()
    {
        using ServiceProvider serviceProvider =
            CreateServiceProvider();

        Assert.IsType<WindowsDatabaseEncryptionKeyService>(
            serviceProvider.GetRequiredService<IDatabaseEncryptionKeyService>());
    }

    [Fact]
    public void AddInfrastructure_RegistersMasterKeyCapabilitiesToSameSingleton()
    {
        using ServiceProvider serviceProvider =
            CreateServiceProvider();

        var retrievalService =
            serviceProvider.GetRequiredService<IEncryptionKeyService>();

        var provisioningService =
            serviceProvider.GetRequiredService<IEncryptionKeyProvisioningService>();

        Assert.IsType<WindowsEncryptionKeyService>(
            retrievalService);

        Assert.IsType<WindowsEncryptionKeyService>(
            provisioningService);

        Assert.Same(
            retrievalService,
            provisioningService);
    }

    [Fact]
    public void AddInfrastructure_RegistersDatabaseKeyCapabilitiesToSameSingleton()
    {
        using ServiceProvider serviceProvider =
            CreateServiceProvider();

        var retrievalService =
            serviceProvider.GetRequiredService<IDatabaseEncryptionKeyService>();

        var provisioningService =
            serviceProvider.GetRequiredService<IDatabaseEncryptionKeyProvisioningService>();

        Assert.IsType<WindowsDatabaseEncryptionKeyService>(
            retrievalService);

        Assert.IsType<WindowsDatabaseEncryptionKeyService>(
            provisioningService);

        Assert.Same(
            retrievalService,
            provisioningService);
    }

    [Fact]
    public void KeyContracts_ExposeRetrievalAndProvisioningAsSeparateCapabilities()
    {
        Assert.Collection(
            typeof(IEncryptionKeyService).GetMethods(),
            method =>
                Assert.Equal(
                    nameof(IEncryptionKeyService.GetKeyAsync),
                    method.Name));

        Assert.Collection(
            typeof(IDatabaseEncryptionKeyService).GetMethods(),
            method =>
                Assert.Equal(
                    nameof(IDatabaseEncryptionKeyService.GetKeyAsync),
                    method.Name));

        Assert.Collection(
            typeof(IEncryptionKeyProvisioningService).GetMethods(),
            method =>
                Assert.Equal(
                    nameof(IEncryptionKeyProvisioningService.GetOrCreateKeyAsync),
                    method.Name));

        Assert.Collection(
            typeof(IDatabaseEncryptionKeyProvisioningService).GetMethods(),
            method =>
                Assert.Equal(
                    nameof(IDatabaseEncryptionKeyProvisioningService.GetOrCreateKeyAsync),
                    method.Name));

        Assert.Empty(
            typeof(IEncryptionKeyProvisioningService).GetInterfaces());

        Assert.Empty(
            typeof(IDatabaseEncryptionKeyProvisioningService).GetInterfaces());
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services =
            new ServiceCollection();

        IConfiguration configuration =
            new ConfigurationBuilder()
                .Build();

        services.AddLogging();

        services.AddInfrastructure(
            configuration);

        return services.BuildServiceProvider();
    }
}
