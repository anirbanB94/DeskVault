using System.Text;

namespace DeskVault.Application.Documents.Content;

/// <summary>
/// Creates deterministic searchable text from structured document content.
/// </summary>
/// <remarks>
/// The projection keeps structured content compatible with the existing
/// text-oriented normalization, chunking, persistence, and keyword-search flow.
/// </remarks>
public static class DocumentContentProjection
{
    private const string UnitSeparator = "\n\n";
    private const string FieldSeparator = "\n";

    /// <summary>
    /// Creates the deterministic searchable text projection.
    /// </summary>
    public static string CreateSearchableText(
        DocumentContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Units.Count == 0)
        {
            return string.Empty;
        }

        var output =
            new StringBuilder();

        for (int index = 0;
             index < content.Units.Count;
             index++)
        {
            if (index > 0)
            {
                output.Append(
                    UnitSeparator);
            }

            AppendUnit(
                output,
                content.Units[index]);
        }

        return output.ToString();
    }

    private static void AppendUnit(
        StringBuilder output,
        DocumentContentUnit unit)
    {
        switch (unit.Kind)
        {
            case DocumentContentUnitKind.Text:
                output.Append(unit.Text);
                break;

            case DocumentContentUnitKind.TableRow:
                for (int fieldIndex = 0;
                     fieldIndex < unit.Fields.Count;
                     fieldIndex++)
                {
                    if (fieldIndex > 0)
                    {
                        output.Append(
                            FieldSeparator);
                    }

                    DocumentContentField field =
                        unit.Fields[fieldIndex];

                    // Keep the established CSV text shape so existing search
                    // consumers continue to see the same logical content.
                    output.Append(field.Name);
                    output.Append(": ");
                    output.Append(field.Value);
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(unit.Kind),
                    unit.Kind,
                    "Unsupported document-content unit kind.");
        }
    }
}
