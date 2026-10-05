namespace DeskVault.Application.Documents.Content;

/// <summary>
/// Represents one ordered logical unit of document content.
/// </summary>
/// <remarks>
/// A unit contains either plain text or structured fields. The representation
/// does not attempt to model every format-specific construct.
/// </remarks>
public sealed record DocumentContentUnit
{
    public DocumentContentUnit(
        int order,
        DocumentContentUnitKind kind,
        string? text = null,
        IReadOnlyList<DocumentContentField>? fields = null)
    {
        if (order < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(order),
                "Document-content unit order must not be negative.");
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unsupported document-content unit kind.");
        }

        if (kind == DocumentContentUnitKind.Text &&
            text is null)
        {
            throw new ArgumentNullException(
                nameof(text));
        }

        if (kind == DocumentContentUnitKind.TableRow &&
            fields is null)
        {
            throw new ArgumentNullException(
                nameof(fields));
        }

        // Keep each unit unambiguous: structured and plain-text content
        // must not be represented together in the same unit.
        if (kind == DocumentContentUnitKind.Text &&
            fields is not null &&
            fields.Count > 0)
        {
            throw new ArgumentException(
                "Text units must not contain structured fields.",
                nameof(fields));
        }

        if (kind == DocumentContentUnitKind.TableRow &&
            text is not null)
        {
            throw new ArgumentException(
                "Table-row units must not contain plain text.",
                nameof(text));
        }

        Order = order;
        Kind = kind;
        Text = text;

        // Copy the supplied collection so the representation remains stable
        // after construction.
        Fields =
            fields?.ToArray()
            ?? Array.Empty<DocumentContentField>();
    }

    /// <summary>
    /// Gets the zero-based logical position of the unit.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Gets the logical kind of the unit.
    /// </summary>
    public DocumentContentUnitKind Kind { get; }

    /// <summary>
    /// Gets the plain-text value for a text unit.
    /// </summary>
    public string? Text { get; }

    /// <summary>
    /// Gets the ordered fields for a table-row unit.
    /// </summary>
    public IReadOnlyList<DocumentContentField> Fields { get; }
}
