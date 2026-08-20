using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Commands.ProcessDocument;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Normalization;
using DeskVault.Application.Documents.Processing;
using DeskVault.Application.Interfaces;
using DeskVault.Infrastructure.Persistence;
using DeskVault.Infrastructure.Persistence.Context;
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

        Assert.NotNull(
            serviceProvider.GetRequiredService<IDocumentTextNormalizer>());

        Assert.NotNull(
            serviceProvider.GetRequiredService<IDocumentTextChunker>());

        Assert.NotNull(
            serviceProvider.GetRequiredService<DocumentTextExtractorResolver>());

        Assert.NotNull(
            serviceProvider.GetRequiredService<ProcessDocumentHandler>());

        Assert.NotNull(
            serviceProvider.GetRequiredService<IDocumentProcessingService>());
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

        Assert.IsType<DocumentTextNormalizer>(
            serviceProvider.GetRequiredService<IDocumentTextNormalizer>());

        Assert.IsType<DocumentTextChunker>(
            serviceProvider.GetRequiredService<IDocumentTextChunker>());

        Assert.IsType<DocumentProcessingService>(
            serviceProvider.GetRequiredService<IDocumentProcessingService>());
    }
}
