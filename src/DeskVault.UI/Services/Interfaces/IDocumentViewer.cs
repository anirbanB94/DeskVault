namespace DeskVault.UI.Services.Interfaces;

/// <summary>
/// Defines the service responsible for opening documents in external applications.
/// </summary>
/// <remarks>
/// The caller retains ownership of the source document stream.
/// The viewer may consume the stream while materializing the temporary
/// external-viewing artifact, but must not dispose or retain the source stream.
/// The caller must keep the stream available until the returned task completes.
/// </remarks>
public interface IDocumentViewer
{
    /// <summary>
    /// Materializes the caller-owned document stream and opens the resulting
    /// temporary document using the external viewing boundary.
    /// </summary>
    /// <param name="documentStream">
    /// The caller-owned source document stream. It must remain available
    /// until this operation completes and must not be disposed by the viewer.
    /// </param>
    /// <param name="fileName">The original filename used to determine the document extension.</param>
    /// <param name="cancellationToken">The token used to cancel source-stream materialization.</param>
    /// <returns>A task representing materialization and external launch admission.</returns>
    Task OpenAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
