using DeskVault.UI.Forms;
using DeskVault.UI.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DeskVault.UI.Services.Dialogs;

/// <summary>
/// Creates and displays the existing startup authentication form for
/// subsequent application reauthentication.
/// </summary>
public sealed class StartupAuthenticationDialogService :
    IStartupAuthenticationDialogService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public StartupAuthenticationDialogService(
        IServiceScopeFactory serviceScopeFactory)
    {
        ArgumentNullException.ThrowIfNull(serviceScopeFactory);

        _serviceScopeFactory = serviceScopeFactory;
    }

    public DialogResult ShowDialog(IWin32Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        using IServiceScope scope =
            _serviceScopeFactory.CreateScope();

        using StartupAuthenticationForm authenticationForm =
            scope.ServiceProvider
                .GetRequiredService<StartupAuthenticationForm>();

        return authenticationForm.ShowDialog(owner);
    }
}