namespace DeskVault.UI.Services.Interfaces;

/// <summary>
/// Displays the existing Windows Hello authentication experience for
/// an application session that is currently locked.
/// </summary>
public interface IStartupAuthenticationDialogService
{
    /// <summary>
    /// Displays the authentication form owned by the supplied window.
    /// </summary>
    DialogResult ShowDialog(IWin32Window owner);
}