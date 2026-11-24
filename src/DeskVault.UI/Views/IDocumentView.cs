namespace DeskVault.UI.Views;

/// <summary>
/// Defines the document presentation view used by the document workspace presenter.
/// </summary>
/// <remarks>
/// The presenter owns document source streams supplied to this view.
/// The view forwards those streams to the selected renderer and must not
/// dispose or retain a source stream after the rendering operation completes.
/// Rendered controls and their displayed representations remain owned by
/// the view until the presentation is cleared or replaced.
/// </remarks>
public interface IDocumentView
{
    /// <summary>
    /// Occurs when the user requests that the currently presented document
    /// be opened using an external application.
    /// </summary>
    event EventHandler OpenExternallyRequested;

    /// <summary>
    /// Occurs when the user requests information about the currently
    /// presented document.
    /// </summary>
    event EventHandler DocumentInformationRequested;

    /// <summary>
    /// Occurs when the user requests removal of the currently
    /// presented document.
    /// </summary>
    event EventHandler RemoveDocumentRequested;

    /// <summary>
    /// Controls the visibility of the document-specific presentation chrome.
    /// </summary>
    /// <param name="visible">
    /// <see langword="true"/> to display the document chrome;
    /// otherwise, <see langword="false"/>.
    /// </param>
    void SetDocumentChromeVisible(
        bool visible);

    /// <summary>
    /// Displays a document using the renderer selected for its filename.
    /// </summary>
    /// <param name="documentStream">
    /// The caller-owned document source stream. The view may forward this
    /// stream to a renderer but must not dispose or retain it after the
    /// rendering operation completes.
    /// </param>
    /// <param name="fileName">
    /// The original document filename, used to determine the appropriate
    /// rendering capability.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the document presentation operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous document presentation operation.
    /// </returns>
    Task ShowDocumentAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Displays the fallback presentation for a document whose content
    /// cannot be rendered within DeskVault.
    /// </summary>
    /// <param name="message">
    /// The user-facing message explaining that an in-app preview is unavailable.
    /// </param>
    void ShowUnsupportedPreview(
        string message);

    /// <summary>
    /// Displays metadata and processing information for the currently
    /// authorized document.
    /// </summary>
    /// <param name="displayName">
    /// The document display name.
    /// </param>
    /// <param name="fileName">
    /// The original document filename.
    /// </param>
    /// <param name="fileType">
    /// The document file type or extension displayed to the user.
    /// </param>
    /// <param name="importedAt">
    /// The date and time when the document was imported.
    /// </param>
    /// <param name="status">
    /// The current document processing status.
    /// </param>
    /// <param name="sha256Hash">
    /// The SHA-256 hash identifying the document content.
    /// </param>
    /// <param name="expectedSessionRevision">
    /// The security-session revision under which the information request
    /// is authorized. The default value is zero.
    /// </param>
    void ShowDocumentInformation(
        string displayName,
        string fileName,
        string fileType,
        DateTime importedAt,
        string status,
        string sha256Hash,
        long expectedSessionRevision = 0);

    /// <summary>
    /// Requests user confirmation before removing a document.
    /// </summary>
    /// <param name="fileName">
    /// The filename of the document proposed for removal.
    /// </param>
    /// <param name="expectedSessionRevision">
    /// The security-session revision under which the confirmation request
    /// is authorized. The default value is zero.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the user confirms removal;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    bool ConfirmRemoval(
        string fileName,
        long expectedSessionRevision = 0);

    /// <summary>
    /// Clears the current document presentation and releases view-owned
    /// presentation resources.
    /// </summary>
    /// <remarks>
    /// This operation clears rendered controls and document-specific
    /// presentation state. It does not transfer or assume ownership of
    /// the document source stream supplied by the presenter.
    /// </remarks>
    void ClearPresentation();

    /// <summary>
    /// Posts an action for execution on the view's owning UI thread.
    /// </summary>
    /// <param name="action">
    /// The action to execute on the owning UI thread.
    /// </param>
    void PostToUi(
        Action action);

    /// <summary>
    /// Displays a user-facing error associated with a document operation.
    /// </summary>
    /// <param name="message">
    /// The error message to display.
    /// </param>
    /// <param name="title">
    /// The title displayed with the error message.
    /// </param>
    /// <param name="expectedSessionRevision">
    /// The security-session revision under which the error may be presented.
    /// The default value is zero.
    /// </param>
    void ShowError(
        string message,
        string title,
        long expectedSessionRevision = 0);
}
