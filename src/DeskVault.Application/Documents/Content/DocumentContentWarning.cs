namespace DeskVault.Application.Documents.Content;

/// <summary>
/// Represents a structural warning produced while extracting document content.
/// </summary>
/// <remarks>
/// Warnings remain metadata and are not included in searchable text.
/// </remarks>
public sealed record DocumentContentWarning
{
    public DocumentContentWarning(
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Message = message;
    }

    /// <summary>
    /// Gets the warning message produced by the extraction path.
    /// </summary>
    public string Message { get; }
}
