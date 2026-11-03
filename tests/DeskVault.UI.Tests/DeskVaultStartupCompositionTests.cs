using System.Runtime.ExceptionServices;
using DeskVault.UI.Forms;
using DeskVault.UI.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DeskVault.UI.Tests;

public sealed class DeskVaultStartupCompositionTests
{
    [Fact]
    public void Build_RegistersStartupCoordinatorAsSingletonAndFormAsTransient()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        RunOnStaThread(
            () =>
            {
                // Arrange
                using IHost host = HostConfigurator.Build();

                // Act
                DeskVaultStartupCoordinator firstCoordinator =
                    host.Services.GetRequiredService<DeskVaultStartupCoordinator>();

                DeskVaultStartupCoordinator secondCoordinator =
                    host.Services.GetRequiredService<DeskVaultStartupCoordinator>();

                using var firstForm =
                    host.Services.GetRequiredService<StartupAuthenticationForm>();

                using var secondForm =
                    host.Services.GetRequiredService<StartupAuthenticationForm>();

                // Assert
                Assert.Same(firstCoordinator, secondCoordinator);

                Assert.IsType<StartupAuthenticationForm>(firstForm);
                Assert.IsType<StartupAuthenticationForm>(secondForm);
                Assert.NotSame(firstForm, secondForm);

                // Resolving the startup coordinator and form must not initialize
                // the database before an authenticated session exists.
                Assert.Throws<InvalidOperationException>(
                    () =>
                        firstCoordinator
                            .InitializeDatabaseAsync(cancellationToken)
                            .GetAwaiter()
                            .GetResult());
            });
    }

    private static void RunOnStaThread(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Exception? failure = null;

        var thread = new Thread(
            () =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
