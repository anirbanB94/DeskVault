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
}
