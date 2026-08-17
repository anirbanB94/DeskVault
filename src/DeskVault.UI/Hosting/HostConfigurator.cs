using DeskVault.Application;
using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.Infrastructure;
using DeskVault.UI.Forms;
using DeskVault.UI.Presenters;
using DeskVault.UI.Rendering;
using DeskVault.UI.Services;
using DeskVault.UI.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DeskVault.UI.Hosting;

internal static class HostConfigurator
{
    public static IHost Build()
    {
        var builder = Host.CreateApplicationBuilder(args: []);

        ConfigureConfiguration(builder);

        ConfigureLogging(builder);

        ConfigureServices(builder.Services, builder.Configuration);

        return builder.Build();
    }

    private static void ConfigureConfiguration(
        HostApplicationBuilder builder)
    {
        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(
                "appsettings.json",
                optional: false,
                reloadOnChange: true);
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

        // Application
        services.AddApplication();

        // Infrastructure
        services.AddInfrastructure(configuration);

        // UI
        services.AddTransient<MainForm>();
        services.AddTransient<IMainFormPresenterFactory, MainFormPresenterFactory>();
        services.AddTransient<DocumentViewForm>();
        services.AddTransient<IDocumentWorkspaceView>(
            provider => provider.GetRequiredService<DocumentViewForm>());
        services.AddTransient<IDocumentViewer, DocumentViewer>();
        services.AddTransient<IDocumentWorkspace, DocumentWorkspacePresenter>();
        services.AddTransient<GetDocumentHandler>();

        // Document rendering
        services.AddTransient<IDocumentContentRenderer, TextDocumentContentRenderer>();

        services.AddSingleton(
            new MarkdownRenderingOptions
            {
                AllowRawHtml = false,
                AllowExternalResources = false,
                AllowExternalNavigation = false
            });

        services.AddTransient<IDocumentContentRenderer, MarkdownDocumentContentRenderer>();

        services.AddTransient<IDocumentContentRendererResolver, DocumentContentRendererResolver>();
    }
}
