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

        IReadOnlyList<XNode> childNodes =
            element.Nodes()
                .ToList();

        IReadOnlyList<XElement> childElements =
            childNodes
                .OfType<XElement>()
                .ToList();

        string text =
            string.Concat(
                childNodes
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

        foreach (XNode childNode in childNodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (childNode is XElement childElement)
            {
                AppendElement(
                    childElement,
                    output,
                    indentationLevel + 1,
                    cancellationToken);
            }
            else if (childNode is XText textNode)
            {
                string childText = textNode.Value.Trim();

                if (childText.Length == 0)
                {
                    continue;
                }

                AppendIndentation(
                    output,
                    indentationLevel + 1);
                output.Append(childText);
                output.AppendLine();
            }
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
