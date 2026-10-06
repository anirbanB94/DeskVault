using DeskVault.Application.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DeskVault.Application.Tests;

public sealed class DocumentProcessingOptionsConfigurationTests
{
    [Fact]
    public void Configuration_BindsMaxDecryptedDocumentBytes()
    {
        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["DocumentProcessing:MaxDecryptedDocumentBytes"] =
                            "16777216"
                    })
                .Build();

        var services =
            new ServiceCollection();

        services.Configure<DocumentProcessingOptions>(
            configuration.GetSection(
                DocumentProcessingOptions.SectionName));

        using ServiceProvider provider =
            services.BuildServiceProvider();

        DocumentProcessingOptions options =
            provider
                .GetRequiredService<
                    IOptions<DocumentProcessingOptions>>()
                .Value;

        Assert.Equal(
            16777216,
            options.MaxDecryptedDocumentBytes);
    }

    [Fact]
    public void Configuration_BindsMaxProcessedTextBytes()
    {
        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["DocumentProcessing:MaxProcessedTextBytes"] =
                            "16777216"
                    })
                .Build();

        var services =
            new ServiceCollection();

        services.Configure<DocumentProcessingOptions>(
            configuration.GetSection(
                DocumentProcessingOptions.SectionName));

        using ServiceProvider provider =
            services.BuildServiceProvider();

        DocumentProcessingOptions options =
            provider
                .GetRequiredService<
                    IOptions<DocumentProcessingOptions>>()
                .Value;

        Assert.Equal(
            16777216,
            options.MaxProcessedTextBytes);
    }

    [Fact]
    public void Configuration_MissingMaxDecryptedDocumentBytes_UsesDefault()
    {
        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>())
                .Build();

        var services =
            new ServiceCollection();

        services.Configure<DocumentProcessingOptions>(
            configuration.GetSection(
                DocumentProcessingOptions.SectionName));

        using ServiceProvider provider =
            services.BuildServiceProvider();

        DocumentProcessingOptions options =
            provider
                .GetRequiredService<
                    IOptions<DocumentProcessingOptions>>()
                .Value;

        Assert.Equal(
            32 * 1024 * 1024,
            options.MaxDecryptedDocumentBytes);
    }

    [Fact]
    public void Configuration_MissingMaxProcessedTextBytes_UsesDefault()
    {
        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>())
                .Build();

        var services =
            new ServiceCollection();

        services.Configure<DocumentProcessingOptions>(
            configuration.GetSection(
                DocumentProcessingOptions.SectionName));

        using ServiceProvider provider =
            services.BuildServiceProvider();

        DocumentProcessingOptions options =
            provider
                .GetRequiredService<
                    IOptions<DocumentProcessingOptions>>()
                .Value;

        Assert.Equal(
            DocumentProcessingOptions.DefaultMaxProcessedTextBytes,
            options.MaxProcessedTextBytes);
    }

    [Fact]
    public void Configuration_BindsMaxChunkSize()
    {
        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["DocumentProcessing:MaxChunkSize"] =
                            "2000"
                    })
                .Build();

        var services =
            new ServiceCollection();

        services.Configure<DocumentProcessingOptions>(
            configuration.GetSection(
                DocumentProcessingOptions.SectionName));

        using ServiceProvider provider =
            services.BuildServiceProvider();

        DocumentProcessingOptions options =
            provider
                .GetRequiredService<
                    IOptions<DocumentProcessingOptions>>()
                .Value;

        Assert.Equal(
            2000,
            options.MaxChunkSize);
    }

    [Fact]
    public void Configuration_BindsChunkOverlap()
    {
        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["DocumentProcessing:ChunkOverlap"] =
                            "100"
                    })
                .Build();

        var services =
            new ServiceCollection();

        services.Configure<DocumentProcessingOptions>(
            configuration.GetSection(
                DocumentProcessingOptions.SectionName));

        using ServiceProvider provider =
            services.BuildServiceProvider();

        DocumentProcessingOptions options =
            provider
                .GetRequiredService<
                    IOptions<DocumentProcessingOptions>>()
                .Value;

        Assert.Equal(
            100,
            options.ChunkOverlap);
    }

    [Fact]
    public void Configuration_MissingMaxChunkSize_UsesDefault()
    {
        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>())
                .Build();

        var services =
            new ServiceCollection();

        services.Configure<DocumentProcessingOptions>(
            configuration.GetSection(
                DocumentProcessingOptions.SectionName));

        using ServiceProvider provider =
            services.BuildServiceProvider();

        DocumentProcessingOptions options =
            provider
                .GetRequiredService<
                    IOptions<DocumentProcessingOptions>>()
                .Value;

        Assert.Equal(
            DocumentProcessingOptions.DefaultMaxChunkSize,
            options.MaxChunkSize);
    }

    [Fact]
    public void Configuration_MissingChunkOverlap_UsesDefault()
    {
        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>())
                .Build();

        var services =
            new ServiceCollection();

        services.Configure<DocumentProcessingOptions>(
            configuration.GetSection(
                DocumentProcessingOptions.SectionName));

        using ServiceProvider provider =
            services.BuildServiceProvider();

        DocumentProcessingOptions options =
            provider
                .GetRequiredService<
                    IOptions<DocumentProcessingOptions>>()
                .Value;

        Assert.Equal(
            DocumentProcessingOptions.DefaultChunkOverlap,
            options.ChunkOverlap);
    }
}
