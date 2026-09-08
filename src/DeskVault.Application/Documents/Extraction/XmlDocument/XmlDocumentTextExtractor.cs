using System.Text;
using System.Xml.Linq;

namespace DeskVault.Application.Documents.Extraction.XmlDocument;

public sealed class XmlDocumentTextExtractor : IDocumentTextExtractor
{
    private const string SupportedExtension = ".xml";

    public bool CanExtract(string fileName)
    {
        return Path.GetExtension(fileName)
            .Equals(SupportedExtension, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<DocumentTextExtractionResult> ExtractAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        XDocument document =
            await XDocument.LoadAsync(
                documentStream,
                LoadOptions.None,
                cancellationToken);

        if (document.Root is null)
        {
            return new DocumentTextExtractionResult(
                string.Empty);
        }

        StringBuilder output = new();

        AppendElement(
            document.Root,
            output,
            indentationLevel: 0,
            cancellationToken);

        return new DocumentTextExtractionResult(
            output.ToString().TrimEnd('\r', '\n'));
    }

    private static void AppendElement(
        XElement element,
        StringBuilder output,
        int indentationLevel,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        AppendIndentation(
            output,
            indentationLevel);

        output.Append(element.Name.LocalName);
        output.Append(':');

        foreach (XAttribute attribute in element.Attributes())
        {
            cancellationToken.ThrowIfCancellationRequested();

            output.AppendLine();
            AppendIndentation(
                output,
                indentationLevel + 1);
            output.Append('@');
            output.Append(attribute.Name.LocalName);
            output.Append(": ");
            output.Append(attribute.Value);
        }

        IReadOnlyList<XElement> childElements =
            element.Elements()
                .ToList();

        string text =
            string.Concat(
                element.Nodes()
                    .OfType<XText>()
                    .Select(node => node.Value))
            .Trim();

        if (childElements.Count == 0)
        {
            if (text.Length > 0)
            {
                output.Append(' ');
                output.Append(text);
            }

            output.AppendLine();
            return;
        }

        output.AppendLine();

        foreach (XElement child in childElements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AppendElement(
                child,
                output,
                indentationLevel + 1,
                cancellationToken);
        }
    }

    private static void AppendIndentation(
        StringBuilder output,
        int indentationLevel)
    {
        output.Append(
            ' ',
            indentationLevel * 2);
    }
}
