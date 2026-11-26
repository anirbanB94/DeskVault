using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Parsing.Csv;
using DeskVault.Application.Documents.Processing;
using DeskVault.UI.Hosting;
using DeskVault.UI.Rendering;
using DeskVault.UI.Rendering.CsvDocumentRendering;
using DeskVault.UI.Rendering.HtmlDocumentRendering;
using DeskVault.UI.Rendering.MarkdownDocumentRendering;
using DeskVault.UI.Rendering.TextDocumentRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text;

namespace DeskVault.UI.Tests;

public sealed class DocumentRendererCompositionTests
{
    [Fact]
    public void BuildForTesting_RegistersAndResolvesCompleteRendererGraph()
    {
        // Arrange
        using var contentRoot = new TemporaryContentRoot();

        ServiceDescriptor[] capturedDescriptors = [];

        using IHost host = BuildProductionHost(
            contentRoot.DirectoryPath,
            services =>
                capturedDescriptors = [.. services]);

        ServiceDescriptor[] rendererRegistrations =
            [.. capturedDescriptors
                .Where(
                    descriptor =>
                        descriptor.ServiceType ==
                        typeof(IDocumentContentRenderer))];

        // Act
        IDocumentContentRenderer[] renderers =
            [.. host.Services.GetServices<IDocumentContentRenderer>()];

        IDocumentContentRendererResolver resolver =
            host.Services
                .GetRequiredService<IDocumentContentRendererResolver>();

        // Assert: all four renderers are registered.
        Assert.Equal(4, rendererRegistrations.Length);

        Assert.Collection(
            rendererRegistrations,
            descriptor =>
                AssertTransientRegistration(
                    descriptor,
                    typeof(TextDocumentContentRenderer)),
            descriptor =>
                AssertTransientRegistration(
                    descriptor,
                    typeof(MarkdownDocumentContentRenderer)),
            descriptor =>
                AssertTransientRegistration(
                    descriptor,
                    typeof(CsvDocumentContentRenderer)),
            descriptor =>
                AssertTransientRegistration(
                    descriptor,
                    typeof(HtmlDocumentContentRenderer)));

        // Assert: the production provider constructs the complete collection.
        Assert.Collection(
            renderers,
            renderer =>
                Assert.IsType<TextDocumentContentRenderer>(renderer),
            renderer =>
                Assert.IsType<MarkdownDocumentContentRenderer>(renderer),
            renderer =>
                Assert.IsType<CsvDocumentContentRenderer>(renderer),
            renderer =>
                Assert.IsType<HtmlDocumentContentRenderer>(renderer));

        // Assert: the resolver is registered once with the existing lifetime.
        ServiceDescriptor resolverRegistration =
            Assert.Single(
                capturedDescriptors,
                descriptor =>
                    descriptor.ServiceType ==
                    typeof(IDocumentContentRendererResolver));

        AssertTransientRegistration(
            resolverRegistration,
            typeof(DocumentContentRendererResolver));

        Assert.IsType<DocumentContentRendererResolver>(resolver);

        // Assert: the resolver receives the registered renderer collection
        // through its production constructor-injection path.
        Assert.IsType<TextDocumentContentRenderer>(
            resolver.Resolve("notes.txt"));

        Assert.IsType<MarkdownDocumentContentRenderer>(
            resolver.Resolve("README.md"));

        Assert.IsType<CsvDocumentContentRenderer>(
            resolver.Resolve("data.csv"));

        Assert.IsType<HtmlDocumentContentRenderer>(
            resolver.Resolve("document.html"));

        Assert.IsType<HtmlDocumentContentRenderer>(
            resolver.Resolve("document.htm"));

        // Assert: the dedicated HTML profile-session factory is registered
        // through the production dependency-injection composition.
        ServiceDescriptor profileFactoryRegistration =
            Assert.Single(
                capturedDescriptors,
                descriptor =>
                    descriptor.ServiceType ==
                    typeof(IHtmlDocumentWebView2ProfileSessionFactory));

        AssertTransientRegistration(
            profileFactoryRegistration,
            typeof(HtmlDocumentWebView2ProfileSessionFactory));

        Assert.IsType<HtmlDocumentWebView2ProfileSessionFactory>(
            host.Services.GetRequiredService<
                IHtmlDocumentWebView2ProfileSessionFactory>());

        // Assert: the CSV parser remains a transient factory registration.
        ServiceDescriptor parserRegistration =
            Assert.Single(
                capturedDescriptors,
                descriptor =>
                    descriptor.ServiceType ==
                    typeof(CsvDocumentParser));

        Assert.Equal(
            ServiceLifetime.Transient,
            parserRegistration.Lifetime);

        Assert.NotNull(parserRegistration.ImplementationFactory);
    }

    [Fact]
    public async Task BuildForTesting_CsvParserUsesConfiguredPreviewAndResourceLimits()
    {
        // Arrange
        using var contentRoot = new TemporaryContentRoot();

        using IHost host = BuildProductionHost(
            contentRoot.DirectoryPath,
            additionalConfiguration:
                new Dictionary<string, string?>
                {
                    ["CsvParsing:MaxRows"] = "1",
                    ["DocumentProcessing:MaxCsvColumns"] = "2"
                });

        CsvParsingOptions parsingOptions =
            host.Services
                .GetRequiredService<IOptions<CsvParsingOptions>>()
                .Value;

        DocumentProcessingOptions processingOptions =
            host.Services
                .GetRequiredService<DocumentProcessingOptions>();

        CsvDocumentParser parser =
            host.Services
                .GetRequiredService<CsvDocumentParser>();

        Assert.Equal(1, parsingOptions.MaxRows);
        Assert.Equal(2, processingOptions.MaxCsvColumns);

        // Act: verify the configured preview row limit.
        await using var previewStream =
            CreateStream("Id\n1\n2");

        CsvDocument preview =
            await parser.ParseAsync(
                previewStream,
                TestContext.Current.CancellationToken);

        // Assert: preview is capped and reports additional source rows.
        Assert.Single(preview.Rows);
        Assert.True(preview.HasMoreRows);

        // Act: verify the configured processing resource limit.
        await using var resourceBoundaryStream =
            CreateStream("A,B,C\n1,2,3");

        DocumentStructureLimitExceededException exception =
            await Assert.ThrowsAsync<DocumentStructureLimitExceededException>(
                async () =>
                    await parser.ParseAsync(
                        resourceBoundaryStream,
                        TestContext.Current.CancellationToken));

        // Assert: the parser enforces the configured column boundary.
        Assert.Equal("CSV columns", exception.ResourceName);
        Assert.Equal(2L, exception.Limit);
        Assert.Equal(3L, exception.Attempted);
    }

    [Fact]
    public void BuildForTesting_MarkdownRendererResolvesConfiguredOptionsAndExtractorResolver()
    {
        // Arrange
        using var contentRoot = new TemporaryContentRoot();

        using IHost host = BuildProductionHost(
            contentRoot.DirectoryPath,
            additionalConfiguration:
                new Dictionary<string, string?>
                {
                    ["MarkdownRendering:AllowRawHtml"] = "true",
                    ["MarkdownRendering:AllowExternalResources"] = "true",
                    ["MarkdownRendering:AllowExternalNavigation"] = "false"
                });

        // Act
        IDocumentContentRenderer[] renderers =
            [.. host.Services.GetServices<IDocumentContentRenderer>()];

        MarkdownDocumentContentRenderer markdownRenderer =
            Assert.Single(
                renderers.OfType<MarkdownDocumentContentRenderer>());

        MarkdownRenderingOptions options =
            host.Services
                .GetRequiredService<IOptions<MarkdownRenderingOptions>>()
                .Value;

        DocumentTextExtractorResolver extractorResolver =
            host.Services
                .GetRequiredService<DocumentTextExtractorResolver>();

        // Assert: the production host constructs the Markdown renderer.
        Assert.NotNull(markdownRenderer);

        // Assert: the configured Markdown options are available through DI.
        Assert.True(options.AllowRawHtml);
        Assert.True(options.AllowExternalResources);
        Assert.False(options.AllowExternalNavigation);

        // Assert: the existing Application extractor resolver is available.
        Assert.True(
            extractorResolver.CanResolve("README.md"));
    }

    private static void AssertTransientRegistration(
        ServiceDescriptor descriptor,
        Type implementationType)
    {
        Assert.Equal(
            implementationType,
            descriptor.ImplementationType);

        Assert.Equal(
            ServiceLifetime.Transient,
            descriptor.Lifetime);
    }

    private static IHost BuildProductionHost(
        string contentRoot,
        Action<IServiceCollection>? inspectServices = null,
        IReadOnlyDictionary<string, string?>? additionalConfiguration = null)
    {
        var configurationOverrides =
            new Dictionary<string, string?>
            {
                ["Security:AutomaticLock:Enabled"] = "false",
                ["Security:AutomaticLock:InactivityTimeoutMinutes"] = "15"
            };

        if (additionalConfiguration is not null)
        {
            foreach (KeyValuePair<string, string?> setting
                     in additionalConfiguration)
            {
                configurationOverrides[setting.Key] =
                    setting.Value;
            }
        }

        return HostConfigurator.BuildForTesting(
            contentRoot,
            services =>
                inspectServices?.Invoke(services),
            configurationOverrides);
    }

    private static MemoryStream CreateStream(string content)
    {
        return new MemoryStream(
            Encoding.UTF8.GetBytes(content));
    }

    private sealed class TemporaryContentRoot : IDisposable
    {
        public TemporaryContentRoot()
        {
            DirectoryPath =
                Path.Combine(
                    Path.GetTempPath(),
                    $"DeskVault.DocumentRendererCompositionTests.{Guid.NewGuid():N}");

            Directory.CreateDirectory(DirectoryPath);

            File.WriteAllText(
                Path.Combine(DirectoryPath, "appsettings.json"),
                "{}");
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(
                    DirectoryPath,
                    recursive: true);
            }
        }
    }
}
