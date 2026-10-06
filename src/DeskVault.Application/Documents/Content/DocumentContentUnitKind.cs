namespace DeskVault.Application.Documents.Content;

/// <summary>
/// Identifies the logical kind of a document-content unit.
/// </summary>
/// <remarks>
/// The MVP2 representation intentionally remains small and format-neutral.
/// </remarks>
public enum DocumentContentUnitKind
{
    /// <summary>
    /// Represents ordinary textual content.
    /// </summary>
    Text = 0,

    /// <summary>
    /// Represents one logical row of tabular content.
    /// </summary>
    TableRow = 1
}
