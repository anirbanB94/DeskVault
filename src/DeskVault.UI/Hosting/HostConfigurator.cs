using DeskVault.Application;
using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Parsing.Csv;
using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Infrastructure;
using DeskVault.UI.Forms;
using DeskVault.UI.Presenters;
using DeskVault.UI.Rendering;
using DeskVault.UI.Rendering.CsvDocumentRendering;
using DeskVault.UI.Rendering.MarkdownDocumentRendering;
using DeskVault.UI.Rendering.TextDocumentRendering;
using DeskVault.UI.Services.Document;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Services.Workspace;
using DeskVault.UI.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;

namespace DeskVault.UI.Hosting;

internal static class HostConfigurator
{
    public static IHost Build()
    {
        var builder = CreateBuilder(AppContext.BaseDirectory);

        ConfigureLogging(builder);

        ConfigureServices(
            builder.Services,
            builder.Configuration);

        return builder.Build();
    }

    internal static HostApplicationBuilder CreateBuilder(
        string contentRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);

        var resolvedContentRoot = Path.GetFullPath(contentRoot);
        var appsettingsPath = Path.Combine(
            resolvedContentRoot,
            "appsettings.json");

        if (!File.Exists(appsettingsPath))
        {
            throw new FileNotFoundException(
                "Required application configuration file was not found.",
                appsettingsPath);
        }

        return Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                Args = [],
                ContentRootPath = resolvedContentRoot
            });
    }

    private static void ConfigureLogging(
        HostApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .CreateLogger();

        builder.Services.AddSerilog(Log.Logger);
    }

    private static void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        // Configuration
        services.Configure<ApplicationOptions>(
            configuration.GetSection(
                ApplicationOptions.SectionName));

        services.Configure<DatabaseOptions>(
            configuration.GetSection(
                DatabaseOptions.SectionName));

        services.Configure<OllamaOptions>(
            configuration.GetSection(
                OllamaOptions.SectionName));

        services.Configure<CsvParsingOptions>(
            configuration.GetSection(
                CsvParsingOptions.SectionName));

        services.Configure<MarkdownRenderingOptions>(
            configuration.GetSection(
                MarkdownRenderingOptions.SectionName));

        services.Configure<SearchOptions>(
            configuration.GetSection(
                SearchOptions.SectionName));

        services.Configure<DocumentProcessingOptions>(
            configuration.GetSection(
                DocumentProcessingOptions.SectionName));

        services.AddSingleton(
            provider =>
                provider
                    .GetRequiredService<
                        IOptions<DocumentProcessingOptions>>()
                    .Value);

        // Application
        services.AddApplication();

        // Infrastructure
        services.AddInfrastructure(configuration);

        // UI
        services.AddTransient<MainForm>();
        services.AddTransient<IMainFormPresenterFactory, MainFormPresenterFactory>();
        services.AddTransient<DocumentViewForm>();
        services.AddTransient<IDocumentWorkspaceView>(provider => provider.GetRequiredService<DocumentViewForm>());
        services.AddTransient<IExternalDocumentLauncher, ExternalDocumentLauncher>();
        services.AddSingleton<IDocumentViewer, DocumentViewer>();
        services.AddTransient<IWorkspaceDocumentPresentationFactory, WorkspaceDocumentPresentationFactory>();
        services.AddTransient<IWorkspacePresentationFactory, WorkspacePresentationFactory>();
        services.AddTransient<IWorkspaceDialogService, WorkspaceDialogService>();
        services.AddSingleton<IWorkspacePresentationManager, WorkspacePresentationManager>();
        services.AddTransient<GetDocumentHandler>();

        // Document rendering
        services.AddTransient<IDocumentContentRenderer, TextDocumentContentRenderer>();
        services.AddTransient<IDocumentContentRenderer, MarkdownDocumentContentRenderer>();
        services.AddTransient<CsvDocumentParser>(
            provider =>
                new CsvDocumentParser(
                    provider
                        .GetRequiredService<
                            IOptions<CsvParsingOptions>>()
                        .Value,
                    provider.GetRequiredService<DocumentProcessingOptions>()));
        services.AddTransient<IDocumentContentRenderer, CsvDocumentContentRenderer>();
        services.AddTransient<IDocumentContentRendererResolver, DocumentContentRendererResolver>();
    }
}
