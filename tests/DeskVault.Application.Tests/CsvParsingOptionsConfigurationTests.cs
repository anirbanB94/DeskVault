using DeskVault.Application.Documents.Parsing.Csv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DeskVault.Application.Tests;

public sealed class CsvParsingOptionsConfigurationTests
{
    [Fact]
    public void Configuration_BindsMaxRows()
    {
        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["CsvParsing:MaxRows"] = "5000"
                    })
                .Build();

        var services =
            new ServiceCollection();

        services.Configure<CsvParsingOptions>(
            configuration.GetSection("CsvParsing"));

        using ServiceProvider provider =
            services.BuildServiceProvider();

        CsvParsingOptions options =
            provider
                .GetRequiredService<
                    IOptions<CsvParsingOptions>>()
                .Value;

        Assert.Equal(
            5000,
            options.MaxRows);
    }

    [Fact]
    public void Configuration_MissingMaxRows_UsesDefault()
    {
        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>())
                .Build();

        var services =
            new ServiceCollection();

        services.Configure<CsvParsingOptions>(
            configuration.GetSection("CsvParsing"));

        using ServiceProvider provider =
            services.BuildServiceProvider();

        CsvParsingOptions options =
            provider
                .GetRequiredService<
                    IOptions<CsvParsingOptions>>()
                .Value;

        Assert.Equal(
            CsvParsingOptions.DefaultPreviewRowLimit,
            options.MaxRows);
    }

    [Fact]
    public void Configuration_BindsZeroMaxRows()
    {
        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["CsvParsing:MaxRows"] = "0"
                    })
                .Build();

        var services =
            new ServiceCollection();

        services.Configure<CsvParsingOptions>(
            configuration.GetSection("CsvParsing"));

        using ServiceProvider provider =
            services.BuildServiceProvider();

        CsvParsingOptions options =
            provider
                .GetRequiredService<
                    IOptions<CsvParsingOptions>>()
                .Value;

        Assert.Equal(
            0,
            options.MaxRows);
    }
}
