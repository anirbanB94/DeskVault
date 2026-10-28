using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Processing;
using System.Security.Cryptography;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;
using YamlDotNetDocument = YamlDotNet.RepresentationModel.YamlDocument;

namespace DeskVault.Application.Documents.Extraction.YamlDocument;

public sealed class YamlDocumentTextExtractor
    : IDocumentTextExtractor
{
    private const string RuleVersionValue =
        "yaml-extractor-v1";

    private readonly long _maxProcessedTextBytes;
    private readonly long _maxDecryptedDocumentBytes;
    private readonly int _maxParserNestingDepth;
    private readonly int _maxStructuredNodeCount;

    public YamlDocumentTextExtractor()
        : this(new DocumentProcessingOptions())
    {
    }

    public YamlDocumentTextExtractor(
        DocumentProcessingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _maxProcessedTextBytes = options.MaxProcessedTextBytes;
        _maxDecryptedDocumentBytes = options.MaxDecryptedDocumentBytes;
        _maxParserNestingDepth = options.MaxParserNestingDepth;
        _maxStructuredNodeCount = options.MaxStructuredNodeCount;

        if (_maxProcessedTextBytes <= 0 ||
            _maxDecryptedDocumentBytes <= 0 ||
            _maxParserNestingDepth <= 0 ||
            _maxStructuredNodeCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "YAML processing resource limits must be greater than zero.");
        }
    }

    public string RuleVersion => RuleVersionValue;

    public bool CanExtract(string fileName)
    {
        return SupportedFileTypes.GetTextExtractionKind(fileName)
            == DocumentTextExtractionKind.Yaml;
    }

    public async Task<DocumentTextExtractionResult> ExtractAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentStream);
        cancellationToken.ThrowIfCancellationRequested();

        using var inputBuffer = new MemoryStream();
        byte[] readBuffer = new byte[81920];

        try
        {
            await ReadBoundedInputAsync(
                documentStream,
                inputBuffer,
                readBuffer,
                cancellationToken);

            ValidateYamlStructure(
                inputBuffer,
                cancellationToken);

            using MemoryStream yamlInput =
                CreateInputStream(inputBuffer);

            using var reader = new StreamReader(
                yamlInput,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true);

            var yamlStream = new YamlStream();
            yamlStream.Load(reader);

            cancellationToken.ThrowIfCancellationRequested();

            var output = new DocumentProcessingTextBuffer(
                _maxProcessedTextBytes);

            long renderedNodeCount = 0;

            var activeNodes = new HashSet<YamlNode>(
                ReferenceEqualityComparer.Instance);

            bool multipleDocuments =
                yamlStream.Documents.Count > 1;

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
                        collectionDepth: 0,
                        cancellationToken,
                        ref renderedNodeCount,
                        activeNodes);
                }
                else
                {
                    AppendNode(
                        document.RootNode,
                        output,
                        indentationLevel: 0,
                        collectionDepth: 0,
                        cancellationToken,
                        ref renderedNodeCount,
                        activeNodes);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            return new DocumentTextExtractionResult(
                output.ToString().TrimEnd('\r', '\n'));
        }
        finally
        {
            if (inputBuffer.TryGetBuffer(
                    out ArraySegment<byte> segment) &&
                segment.Array is not null)
            {
                CryptographicOperations.ZeroMemory(
                    segment.Array.AsSpan());
            }

            CryptographicOperations.ZeroMemory(readBuffer);
        }
    }

    private async Task ReadBoundedInputAsync(
        Stream documentStream,
        MemoryStream inputBuffer,
        byte[] readBuffer,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            long remainingBytes =
                _maxDecryptedDocumentBytes - inputBuffer.Length;

            int bytesToRead =
                remainingBytes >= readBuffer.Length
                    ? readBuffer.Length
                    : checked((int)remainingBytes + 1);

            int bytesRead = await documentStream.ReadAsync(
                readBuffer.AsMemory(0, bytesToRead),
                cancellationToken);

            if (bytesRead == 0)
            {
                return;
            }

            long attemptedBytes =
                inputBuffer.Length + bytesRead;

            if (attemptedBytes > _maxDecryptedDocumentBytes)
            {
                throw new ResourceLimitExceededException(
                    _maxDecryptedDocumentBytes,
                    attemptedBytes);
            }

            inputBuffer.Write(
                readBuffer,
                0,
                bytesRead);
        }
    }

    private void ValidateYamlStructure(
        MemoryStream inputBuffer,
        CancellationToken cancellationToken)
    {
        using MemoryStream yamlInput =
            CreateInputStream(inputBuffer);

        using var reader = new StreamReader(
            yamlInput,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);

        var parser = new Parser(reader);

        int collectionDepth = 0;
        long nodeCount = 0;

        while (parser.MoveNext())
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (parser.Current)
            {
                case MappingStart:
                case SequenceStart:
                    collectionDepth++;

                    if (collectionDepth > _maxParserNestingDepth)
                    {
                        throw new DocumentStructureLimitExceededException(
                            "YAML nesting depth",
                            _maxParserNestingDepth,
                            collectionDepth);
                    }

                    CountYamlNode(ref nodeCount);
                    break;

                case MappingEnd:
                case SequenceEnd:
                    collectionDepth--;
                    break;

                case Scalar:
                case AnchorAlias:
                    CountYamlNode(ref nodeCount);
                    break;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private void CountYamlNode(ref long nodeCount)
    {
        nodeCount++;

        if (nodeCount > _maxStructuredNodeCount)
        {
            throw new DocumentStructureLimitExceededException(
                "YAML structured nodes",
                _maxStructuredNodeCount,
                nodeCount);
        }
    }

    private void CountRenderedNode(ref long nodeCount)
    {
        nodeCount++;

        if (nodeCount > _maxStructuredNodeCount)
        {
            throw new DocumentStructureLimitExceededException(
                "YAML expanded nodes",
                _maxStructuredNodeCount,
                nodeCount);
        }
    }

    private static MemoryStream CreateInputStream(
        MemoryStream inputBuffer)
    {
        if (!inputBuffer.TryGetBuffer(
                out ArraySegment<byte> segment) ||
            segment.Array is null)
        {
            throw new InvalidOperationException(
                "Unable to access the bounded YAML input buffer.");
        }

        return new MemoryStream(
            segment.Array,
            segment.Offset,
            checked((int)inputBuffer.Length),
            writable: false,
            publiclyVisible: false);
    }

    private void AppendNode(
        YamlNode node,
        DocumentProcessingTextBuffer output,
        int indentationLevel,
        int collectionDepth,
        CancellationToken cancellationToken,
        ref long renderedNodeCount,
        HashSet<YamlNode> activeNodes)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CountRenderedNode(ref renderedNodeCount);

        bool isCollection =
            node is YamlMappingNode or YamlSequenceNode;

        int currentCollectionDepth = collectionDepth;

        if (isCollection)
        {
            currentCollectionDepth++;

            if (currentCollectionDepth > _maxParserNestingDepth)
            {
                throw new DocumentStructureLimitExceededException(
                    "YAML expanded nesting depth",
                    _maxParserNestingDepth,
                    currentCollectionDepth);
            }

            if (!activeNodes.Add(node))
            {
                throw new InvalidDataException(
                    "Recursive YAML aliases cannot be rendered safely.");
            }
        }

        try
        {
            switch (node)
            {
                case YamlMappingNode mapping:
                    AppendMapping(
                        mapping,
                        output,
                        indentationLevel,
                        currentCollectionDepth,
                        cancellationToken,
                        ref renderedNodeCount,
                        activeNodes);
                    break;

                case YamlSequenceNode sequence:
                    AppendSequence(
                        sequence,
                        output,
                        indentationLevel,
                        currentCollectionDepth,
                        cancellationToken,
                        ref renderedNodeCount,
                        activeNodes);
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
        finally
        {
            if (isCollection)
            {
                activeNodes.Remove(node);
            }
        }
    }

    private void AppendMapping(
        YamlMappingNode mapping,
        DocumentProcessingTextBuffer output,
        int indentationLevel,
        int collectionDepth,
        CancellationToken cancellationToken,
        ref long renderedNodeCount,
        HashSet<YamlNode> activeNodes)
    {
        foreach (KeyValuePair<YamlNode, YamlNode> pair
                 in mapping.Children)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CountRenderedNode(ref renderedNodeCount);

            if (pair.Key is not YamlScalarNode key)
            {
                throw new NotSupportedException(
                    "YAML mapping keys must be scalar values.");
            }

            string keyText = key.Value ?? string.Empty;

            AppendIndentation(output, indentationLevel);
            output.Append(keyText);
            output.Append(':');

            if (pair.Value is YamlScalarNode scalar)
            {
                CountRenderedNode(ref renderedNodeCount);

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
                    collectionDepth,
                    cancellationToken,
                    ref renderedNodeCount,
                    activeNodes);
            }
        }
    }

    private void AppendSequence(
        YamlSequenceNode sequence,
        DocumentProcessingTextBuffer output,
        int indentationLevel,
        int collectionDepth,
        CancellationToken cancellationToken,
        ref long renderedNodeCount,
        HashSet<YamlNode> activeNodes)
    {
        for (int index = 0;
             index < sequence.Children.Count;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            YamlNode child = sequence.Children[index];

            AppendIndentation(output, indentationLevel);
            output.Append('[');
            output.Append(index);
            output.Append(']');

            if (child is YamlScalarNode scalar)
            {
                CountRenderedNode(ref renderedNodeCount);

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
                    collectionDepth,
                    cancellationToken,
                    ref renderedNodeCount,
                    activeNodes);
            }
        }
    }

    private static void AppendScalar(
        YamlScalarNode scalar,
        DocumentProcessingTextBuffer output,
        int indentationLevel)
    {
        AppendIndentation(output, indentationLevel);
        output.Append(scalar.Value ?? string.Empty);
        output.AppendLine();
    }

    private static void AppendIndentation(
        DocumentProcessingTextBuffer output,
        int indentationLevel)
    {
        int indentationWidth = indentationLevel * 2;

        for (int index = 0;
             index < indentationWidth;
             index++)
        {
            output.Append(' ');
        }
    }
}
