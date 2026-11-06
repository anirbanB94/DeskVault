namespace DeskVault.UI.Services.Interfaces;

/// <summary>
/// Displays modal forms only while the application security session remains
/// authorized at the revision that initiated the dialog.
/// </summary>
public interface ISessionBoundDialogCoordinator
{
    /// <summary>
    /// Displays a modal dialog while the specified security-session revision
    /// remains authorized.
    /// </summary>
    /// <param name="dialog">The modal form to display.</param>
    /// <param name="expectedSessionRevision">
    /// The immutable session revision captured by the operation that requested
    /// the dialog.
    /// </param>
    /// <param name="owner">
    /// Optional WinForms owner for normal modal-window behavior.
    /// </param>
    /// <returns>
    /// The dialog result, or <see cref="DialogResult.Cancel"/> when the owning
    /// session revision is no longer authorized.
    /// </returns>
    /// <remarks>
    /// A dialog is invalidated by any newer session revision, including a newer
    /// authenticated revision. Re-authentication does not revive a dialog that
    /// was created by an earlier session.
    /// </remarks>
    DialogResult ShowDialog(
        Form dialog,
        long expectedSessionRevision,
        IWin32Window? owner = null);
}
