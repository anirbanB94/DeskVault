using DeskVault.Shared.Extensions;
using DeskVault.Shared.Resources;
using DeskVault.UI.Forms;
using DeskVault.UI.Hosting;
using DeskVault.UI.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Diagnostics;
using WindowsForms = System.Windows.Forms;

namespace DeskVault.UI;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        IHost? host = TryBuildHost(
            HostConfigurator.Build,
            static (message, title) =>
            {
                WindowsForms.MessageBox.Show(
                    message,
                    title,
                    WindowsForms.MessageBoxButtons.OK,
                    WindowsForms.MessageBoxIcon.Error);
            });

        if (host is null)
        {
            return;
        }

        var hostLifetime = new DeskVaultHostLifetime(host);

        try
        {
            // Start all hosted services before startup authentication.
            // Keep Main synchronous so execution remains on the STA thread.
            hostLifetime
                .StartAsync()
                .GetAwaiter()
                .GetResult();

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
        finally
        {
            try
            {
                // Covers startup cancellation, startup/database failure,
                // normal WinForms shutdown, and exceptions.
                hostLifetime
                    .DisposeAsync()
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }

    internal static IHost? TryBuildHost(
        Func<IHost> buildHost,
        Action<string, string> showStartupError)
    {
        ArgumentNullException.ThrowIfNull(buildHost);
        ArgumentNullException.ThrowIfNull(showStartupError);

        try
        {
            return buildHost();
        }
        catch (Exception exception)
        {
            // Host construction occurs before the production host and its
            // configured logger are available. Record only the exception
            // type and show a safe, actionable message to the user.
            Trace.TraceError(
                "DeskVault host construction failed. FailureType={0}",
                exception.GetType().Name);

            showStartupError(
                UiMessages.UnableToInitializeStartupConfiguration,
                UiMessages.StartupConfigurationFailedTitle);

            return null;
        }
    }
}
