using DeskVault.Application.Interfaces;
using DeskVault.Infrastructure.Repositories;
using DeskVault.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeskVault.Infrastructure.Tests;

public sealed class InfrastructureDependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_RegistersDocumentProcessingPipeline()
    {
        var services =
            new ServiceCollection();

        IConfiguration configuration =
            new ConfigurationBuilder()
                .Build();

        services.AddInfrastructure(
            configuration);

        using ServiceProvider serviceProvider =
            services.BuildServiceProvider();

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
        var services =
            new ServiceCollection();

        IConfiguration configuration =
            new ConfigurationBuilder()
                .Build();

        services.AddInfrastructure(
            configuration);

        using ServiceProvider serviceProvider =
            services.BuildServiceProvider();

        Assert.IsType<SqliteDocumentRepository>(
            serviceProvider.GetRequiredService<IDocumentRepository>());

        Assert.IsType<SqliteDocumentProcessingStore>(
            serviceProvider.GetRequiredService<IDocumentProcessingStore>());

        Assert.IsType<EncryptedDocumentReader>(
            serviceProvider.GetRequiredService<IDocumentReader>());

    }
}
