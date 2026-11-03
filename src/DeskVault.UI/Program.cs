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

        WindowsForms.DialogResult authenticationResult;

        using (var startupAuthenticationForm =
            host.Services.GetRequiredService<StartupAuthenticationForm>())
        {
            authenticationResult =
                startupAuthenticationForm.ShowDialog();
        }

        if (authenticationResult != WindowsForms.DialogResult.OK)
        {
            return;
        }

        var startupCoordinator =
            host.Services.GetRequiredService<DeskVaultStartupCoordinator>();

        try
        {
            startupCoordinator
                .InitializeDatabaseAsync()
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

        var mainForm =
            host.Services.GetRequiredService<MainForm>();

        WindowsForms.Application.Run(mainForm);
    }
}
