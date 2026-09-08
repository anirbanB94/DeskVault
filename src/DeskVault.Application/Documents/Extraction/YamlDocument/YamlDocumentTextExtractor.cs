using System.Text;
using YamlDotNet.RepresentationModel;
using YamlDotNetDocument = YamlDotNet.RepresentationModel.YamlDocument;

namespace DeskVault.Application.Documents.Extraction.YamlDocument;

public sealed class YamlDocumentTextExtractor : IDocumentTextExtractor
{
    private const string SupportedYamlExtension = ".yaml";
    private const string SupportedYmlExtension = ".yml";

    public bool CanExtract(string fileName)
    {
        string extension = Path.GetExtension(fileName);

        return extension.Equals(
                   SupportedYamlExtension,
                   StringComparison.OrdinalIgnoreCase)
               || extension.Equals(
                   SupportedYmlExtension,
                   StringComparison.OrdinalIgnoreCase);
    }

    public async Task<DocumentTextExtractionResult> ExtractAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using StreamReader reader = new(
            documentStream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);

        YamlStream yamlStream = new();
        yamlStream.Load(reader);

        StringBuilder output = new();

        bool multipleDocuments = yamlStream.Documents.Count > 1;

        for (int documentIndex = 0;
             documentIndex < yamlStream.Documents.Count;
             documentIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            YamlDotNetDocument document =
                yamlStream.Documents[documentIndex];

            if (multipleDocuments)
            {
                AppendIndentation(
                    output,
                    indentationLevel: 0);

                output.Append("[document ");
                output.Append(documentIndex);
                output.AppendLine("]:");

                AppendNode(
                    document.RootNode,
                    output,
                    indentationLevel: 1,
                    cancellationToken);
            }
            else
            {
                AppendNode(
                    document.RootNode,
                    output,
                    indentationLevel: 0,
                    cancellationToken);
            }
        }

        return new DocumentTextExtractionResult(
            output.ToString().TrimEnd('\r', '\n'));
    }

    private static void AppendNode(
        YamlNode node,
        StringBuilder output,
        int indentationLevel,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (node)
        {
            case YamlMappingNode mapping:
                AppendMapping(
                    mapping,
                    output,
                    indentationLevel,
                    cancellationToken);
                break;

            case YamlSequenceNode sequence:
                AppendSequence(
                    sequence,
                    output,
                    indentationLevel,
                    cancellationToken);
                break;

            case YamlScalarNode scalar:
                AppendScalar(
                    scalar,
                    output,
                    indentationLevel);
                break;

            default:
                throw new NotSupportedException(
                    $"Unsupported YAML node type '{node.GetType().Name}'.");
        }
    }

    private static void AppendMapping(
        YamlMappingNode mapping,
        StringBuilder output,
        int indentationLevel,
        CancellationToken cancellationToken)
    {
        foreach (KeyValuePair<YamlNode, YamlNode> pair in mapping.Children)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (pair.Key is not YamlScalarNode key)
            {
                throw new NotSupportedException(
                    "YAML mapping keys must be scalar values.");
            }

            string keyText = key.Value ?? string.Empty;

            AppendIndentation(
                output,
                indentationLevel);

            output.Append(keyText);
            output.Append(':');

            if (pair.Value is YamlScalarNode scalar)
            {
                output.Append(' ');
                output.Append(scalar.Value ?? string.Empty);
                output.AppendLine();
            }
            else
            {
                output.AppendLine();

                AppendNode(
                    pair.Value,
                    output,
                    indentationLevel + 1,
                    cancellationToken);
            }
        }
    }

    private static void AppendSequence(
        YamlSequenceNode sequence,
        StringBuilder output,
        int indentationLevel,
        CancellationToken cancellationToken)
    {
        for (int index = 0;
             index < sequence.Children.Count;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            YamlNode child = sequence.Children[index];

            AppendIndentation(
                output,
                indentationLevel);

            output.Append('[');
            output.Append(index);
            output.Append(']');

            if (child is YamlScalarNode scalar)
            {
                output.Append(": ");
                output.Append(scalar.Value ?? string.Empty);
                output.AppendLine();
            }
            else
            {
                output.Append(':');
                output.AppendLine();

                AppendNode(
                    child,
                    output,
                    indentationLevel + 1,
                    cancellationToken);
            }
        }
    }

    private static void AppendScalar(
        YamlScalarNode scalar,
        StringBuilder output,
        int indentationLevel)
    {
        AppendIndentation(
            output,
            indentationLevel);

        output.Append(scalar.Value ?? string.Empty);
        output.AppendLine();
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
