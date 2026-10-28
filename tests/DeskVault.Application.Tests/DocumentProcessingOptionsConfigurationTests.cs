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

    [Fact]
    public void Configuration_BindsParserResourceLimits()
    {
        // Arrange
        var values = new Dictionary<string, string?>
        {
            ["DocumentProcessing:MaxParserNestingDepth"] = "48",
            ["DocumentProcessing:MaxStructuredNodeCount"] = "50000",
            ["DocumentProcessing:MaxCsvRows"] = "75000",
            ["DocumentProcessing:MaxCsvColumns"] = "512",
            ["DocumentProcessing:MaxCsvFieldLength"] = "524288",
            ["DocumentProcessing:MaxCsvCells"] = "250000",
            ["DocumentProcessing:MaxIniEntries"] = "60000",
            ["DocumentProcessing:MaxIniLineLength"] = "524288"
        };

        // Act
        DocumentProcessingOptions options = CreateOptions(values);

        // Assert
        Assert.Equal(48, options.MaxParserNestingDepth);
        Assert.Equal(50000, options.MaxStructuredNodeCount);
        Assert.Equal(75000, options.MaxCsvRows);
        Assert.Equal(512, options.MaxCsvColumns);
        Assert.Equal(524288, options.MaxCsvFieldLength);
        Assert.Equal(250000, options.MaxCsvCells);
        Assert.Equal(60000, options.MaxIniEntries);
        Assert.Equal(524288, options.MaxIniLineLength);
    }

    [Fact]
    public void Configuration_MissingParserResourceLimits_UsesDefaults()
    {
        // Arrange
        var values = new Dictionary<string, string?>();

        // Act
        DocumentProcessingOptions options = CreateOptions(values);

        // Assert
        Assert.Equal(
            DocumentProcessingOptions.DefaultMaxParserNestingDepth,
            options.MaxParserNestingDepth);

        Assert.Equal(
            DocumentProcessingOptions.DefaultMaxStructuredNodeCount,
            options.MaxStructuredNodeCount);

        Assert.Equal(
            DocumentProcessingOptions.DefaultMaxCsvRows,
            options.MaxCsvRows);

        Assert.Equal(
            DocumentProcessingOptions.DefaultMaxCsvColumns,
            options.MaxCsvColumns);

        Assert.Equal(
            DocumentProcessingOptions.DefaultMaxCsvFieldLength,
            options.MaxCsvFieldLength);

        Assert.Equal(
            DocumentProcessingOptions.DefaultMaxCsvCells,
            options.MaxCsvCells);

        Assert.Equal(
            DocumentProcessingOptions.DefaultMaxIniEntries,
            options.MaxIniEntries);

        Assert.Equal(
            DocumentProcessingOptions.DefaultMaxIniLineLength,
            options.MaxIniLineLength);
    }

    private static DocumentProcessingOptions CreateOptions(
        IDictionary<string, string?> values)
    {
        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();

        var services = new ServiceCollection();

        services.Configure<DocumentProcessingOptions>(
            configuration.GetSection(
                DocumentProcessingOptions.SectionName));

        using ServiceProvider provider =
            services.BuildServiceProvider();

        return provider
            .GetRequiredService<IOptions<DocumentProcessingOptions>>()
            .Value;
    }
}
