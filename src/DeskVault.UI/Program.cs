using DeskVault.UI.Forms;
using DeskVault.UI.Hosting;
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

        Log.Information("DeskVault application starting...");

        var mainForm = host.Services.GetRequiredService<MainForm>();

        WindowsForms.Application.Run(mainForm);
    }
}