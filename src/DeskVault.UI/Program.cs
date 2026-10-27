using DeskVault.Infrastructure.Persistence;
using DeskVault.Shared.Extensions;
using DeskVault.Shared.Resources;
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

        var databaseInitializer =
            host.Services.GetRequiredService<DatabaseInitializer>();

        try
        {
            databaseInitializer
                .InitializeAsync()
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            Log.Error(
                "{SafeMessage} OperationCode={OperationCode} FailureType={FailureType} FailureCategory={FailureCategory}.",
                Shared.Resources.LogMessages.DatabaseInitializationFailed,
                DiagnosticOperationCodes.DatabaseInitializationFailure,
                ex.GetType().Name,
                DiagnosticFailureClassifier.Classify(ex));

            WindowsForms.MessageBox.Show(
                UiMessages.UnableToInitializeDatabase,
                UiMessages.DatabaseInitializationFailedTitle,
                WindowsForms.MessageBoxButtons.OK,
                WindowsForms.MessageBoxIcon.Error);

            return;
        }

        Log.Information(Resources.LogMessages.ApplicationStarting);

        var mainForm = host.Services.GetRequiredService<MainForm>();

        WindowsForms.Application.Run(mainForm);
    }
}
