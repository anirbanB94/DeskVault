namespace DeskVault.Application.Documents.Content;

/// <summary>
/// Represents the structured logical content of a processed document.
/// </summary>
/// <remarks>
/// This is an application-level intermediate representation between extraction
/// and downstream processing. It is separate from the persisted chunk model.
///
/// Only structure that a supported processing path can reliably establish
/// should be represented here.
/// </remarks>
public sealed class DocumentContent
{
    public DocumentContent(
        IReadOnlyList<DocumentContentUnit> units,
        IReadOnlyList<DocumentContentWarning>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(units);

        // Preserve the extractor-defined order rather than silently reordering
        // the representation.
        Units = units.ToArray();

        for (int index = 0;
             index < Units.Count;
             index++)
        {
            if (Units[index].Order != index)
            {
                throw new ArgumentException(
                    "Document-content unit ordering must be contiguous and deterministic.",
                    nameof(units));
            }
        }

        Warnings =
            warnings?.ToArray()
            ?? Array.Empty<DocumentContentWarning>();
    }

    /// <summary>
    /// Gets the ordered logical content units.
    /// </summary>
    public IReadOnlyList<DocumentContentUnit> Units { get; }

    /// <summary>
    /// Gets structural warnings preserved from extraction.
    /// </summary>
    public IReadOnlyList<DocumentContentWarning> Warnings { get; }

    /// <summary>
    /// Gets the deterministic searchable text projection.
    /// </summary>
    public string SearchableText =>
        DocumentContentProjection.CreateSearchableText(
            this);

    /// <summary>
    /// Creates a representation for text-only content.
    /// </summary>
    public static DocumentContent TextOnly(
        string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
        {
            return new DocumentContent(
                Array.Empty<DocumentContentUnit>());
        }

        return new DocumentContent(
        [
            new DocumentContentUnit(
                order: 0,
                kind: DocumentContentUnitKind.Text,
                text: text)
        ]);
    }
}
