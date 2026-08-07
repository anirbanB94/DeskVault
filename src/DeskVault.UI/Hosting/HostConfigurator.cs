using DeskVault.Application.Configurations;
using DeskVault.Application.Interfaces;
using DeskVault.Infrastructure.Services;
using DeskVault.UI.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace DeskVault.UI.Hosting;

internal static class HostConfigurator
{
    public static IHost Build()
    {
        var builder = Host.CreateApplicationBuilder(args: []);

        ConfigureConfiguration(builder);

        ConfigureLogging(builder);

        ConfigureServices(builder, builder.Services);

        return builder.Build();
    }

    private static void ConfigureConfiguration(HostApplicationBuilder builder)
    {
        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    }

    private static void ConfigureLogging(HostApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .CreateLogger();

        builder.Services.AddSerilog(Log.Logger);
    }

    //private static void ConfigureServices(IServiceCollection services)
    private static void ConfigureServices(HostApplicationBuilder builder, IServiceCollection services)
    {
        services.Configure<ApplicationOptions>(builder.Configuration.GetSection(ApplicationOptions.SectionName));

        services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));

        services.Configure<OllamaOptions>(builder.Configuration.GetSection(OllamaOptions.SectionName));

        services.AddSingleton<IApplicationInfoService, ApplicationInfoService>();

        services.AddTransient<MainForm>();
    }
}