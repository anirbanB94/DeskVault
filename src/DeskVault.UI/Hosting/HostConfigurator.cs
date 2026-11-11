using DeskVault.Application;
using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Parsing.Csv;
using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Infrastructure;
using DeskVault.Infrastructure.Persistence;
using DeskVault.Infrastructure.Security.Authentication;
using DeskVault.UI.Forms;
using DeskVault.UI.Presenters;
using DeskVault.UI.Rendering;
using DeskVault.UI.Rendering.CsvDocumentRendering;
using DeskVault.UI.Rendering.MarkdownDocumentRendering;
using DeskVault.UI.Rendering.TextDocumentRendering;
using DeskVault.UI.Services.Dialogs;
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
        return BuildCore(
            AppContext.BaseDirectory,
            configureTestServices: null,
            configurationOverrides: null);
    }

    /// <summary>
    /// Builds the production host composition while allowing tests to replace
    /// only explicitly permitted external boundaries after production
    /// registrations have been applied.
    /// </summary>
    /// <remarks>
    /// This entry point is internal and is exposed only to the UI test
    /// assembly. The normal production Build method does not use it and
    /// retains the production service registrations unchanged.
    /// </remarks>
    internal static IHost BuildForTesting(
        string contentRoot,
        Action<IServiceCollection> configureTestServices,
        IReadOnlyDictionary<string, string?>? configurationOverrides = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        ArgumentNullException.ThrowIfNull(configureTestServices);

        return BuildCore(
            contentRoot,
            configureTestServices,
            configurationOverrides);
    }

    private static IHost BuildCore(
        string contentRoot,
        Action<IServiceCollection>? configureTestServices,
        IReadOnlyDictionary<string, string?>? configurationOverrides)
    {
        var builder = CreateBuilder(contentRoot);

        // Apply test configuration before validating the effective
        // automatic-lock policy. Production configuration remains the base.
        if (configurationOverrides is not null)
        {
            builder.Configuration.AddInMemoryCollection(
                configurationOverrides);
        }

        // Validate the effective policy before creating the host or
        // permitting the startup authentication experience.
        AutomaticLockSettings automaticLockSettings =
            AutomaticLockSettingsFactory.Create(
                builder.Configuration);

        ConfigureLogging(builder);

        ConfigureServices(
            builder.Services,
            builder.Configuration,
            automaticLockSettings);

        // Test overrides are deliberately applied after the complete
        // production composition. This preserves production registrations
        // and makes each permitted boundary substitution explicit.
        configureTestServices?.Invoke(builder.Services);

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
        IConfiguration configuration,
        AutomaticLockSettings automaticLockSettings)
    {
        // Configuration

        // Automatic-lock configuration.
        // The validated settings are captured once for the process lifetime.
        services.AddSingleton(automaticLockSettings);

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

        // Shared runtime services
        services.AddSingleton<TimeProvider>(TimeProvider.System);

        // Workstation security monitoring must be initialized before
        // subsequent hosted services and before startup authentication.
        services.AddHostedService<WorkstationSecurityMonitorHostedService>();

        // Automatic-lock monitoring.
        services.AddHostedService<ApplicationAutomaticLockHostedService>();

        // Infrastructure
        services.AddInfrastructure(configuration);

        // Windows Hello window ownership
        services.AddSingleton<
            IActiveWindowsHelloFormAccessor,
            WinFormsActiveWindowsHelloFormAccessor>();

        services.AddSingleton<
            IWindowsHelloWindowHandleProvider,
            WindowsHelloWindowHandleProvider>();

        // UI
        services.AddSingleton<DeskVaultStartupCoordinator>(
            provider =>
                new DeskVaultStartupCoordinator(
                    provider.GetRequiredService<IApplicationSecuritySession>(),
                    async cancellationToken =>
                    {
                        await provider
                            .GetRequiredService<IApplicationAuthenticationService>()
                            .AuthenticateAsync(cancellationToken);
                    },
                    cancellationToken =>
                        provider
                            .GetRequiredService<DatabaseInitializer>()
                            .InitializeAsync(cancellationToken)));

        services.AddTransient<StartupAuthenticationForm>();
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
        services.AddSingleton<ISessionBoundDialogCoordinator, SessionBoundDialogCoordinator>();
        services.AddSingleton<IStartupAuthenticationDialogService, StartupAuthenticationDialogService>();
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
