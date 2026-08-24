using DeskVault.Infrastructure.Persistence;
using DeskVault.UI.Forms;
using DeskVault.UI.Hosting;
using DeskVault.UI.Resources;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WindowsForms = System.Windows.Forms;

namespace DeskVault.UI;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var host = HostConfigurator.Build();

        var databaseInitializer = host.Services.GetRequiredService<DatabaseInitializer>();

        databaseInitializer
            .InitializeAsync()
            .GetAwaiter()
            .GetResult();

        Log.Information(LogMessages.ApplicationStarting);

        var mainForm = host.Services.GetRequiredService<MainForm>();

        WindowsForms.Application.Run(mainForm);
    }
}
