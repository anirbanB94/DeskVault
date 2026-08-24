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
