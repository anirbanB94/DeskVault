namespace DeskVault.Application.Documents.Content;

/// <summary>
/// Represents one ordered named field within structured document content.
/// </summary>
public sealed record DocumentContentField
{
    public DocumentContentField(
        string name,
        string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        Name = name;
        Value = value;
    }

    /// <summary>
    /// Gets the field name used by the searchable projection.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the field value.
    /// </summary>
    public string Value { get; }
}
