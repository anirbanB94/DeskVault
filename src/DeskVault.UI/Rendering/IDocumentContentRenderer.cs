namespace DeskVault.UI.Rendering;

/// <summary>
/// Defines the rendering contract for supported document content.
/// </summary>
/// <remarks>
/// The caller retains ownership of the supplied document stream.
/// Implementations may consume the stream during <see cref="RenderAsync"/>,
/// but must not dispose it or retain it beyond completion of that operation.
/// Renderers may retain the rendered presentation representation in their
/// controls for the lifetime of the active document presentation.
/// </remarks>
public interface IDocumentContentRenderer
{
    int Priority { get; }

    bool CanRender(string fileName);

    /// <summary>
    /// Consumes the supplied document stream and renders its content
    /// into the specified presentation host.
    /// </summary>
    /// <param name="contentHost">The control that hosts the rendered content.</param>
    /// <param name="documentStream">
    /// The caller-owned document source stream. The renderer must leave it open
    /// and must not retain it after this operation completes.
    /// </param>
    /// <param name="fileName">The original document filename.</param>
    /// <param name="cancellationToken">The token used to cancel rendering.</param>
    /// <returns>A task representing the asynchronous rendering operation.</returns>
    Task RenderAsync(
        Control contentHost,
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
