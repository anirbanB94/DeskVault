using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Processing;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;

namespace DeskVault.Application.Documents.Extraction.XmlDocument;

public sealed class XmlDocumentTextExtractor
    : IDocumentTextExtractor
{
    private const string RuleVersionValue =
        "xml-extractor-v1";

    private readonly long _maxProcessedTextBytes;
    private readonly long _maxDecryptedDocumentBytes;
    private readonly int _maxParserNestingDepth;
    private readonly int _maxStructuredNodeCount;

    public XmlDocumentTextExtractor()
        : this(new DocumentProcessingOptions())
    {
    }

    public XmlDocumentTextExtractor(
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
                "XML processing resource limits must be greater than zero.");
        }
    }

    public string RuleVersion => RuleVersionValue;

    public bool CanExtract(string fileName)
    {
        return SupportedFileTypes.GetTextExtractionKind(fileName)
            == DocumentTextExtractionKind.Xml;
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

            ValidateXmlStructure(
                inputBuffer,
                cancellationToken);

            using MemoryStream xmlStream =
                CreateInputStream(inputBuffer);

            using XmlReader xmlReader = XmlReader.Create(
                xmlStream,
                CreateXmlReaderSettings(asynchronous: true));

            XDocument document = await XDocument.LoadAsync(
                xmlReader,
                LoadOptions.None,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (document.Root is null)
            {
                return new DocumentTextExtractionResult(string.Empty);
            }

            var output = new DocumentProcessingTextBuffer(
                _maxProcessedTextBytes);

            AppendElement(
                document.Root,
                output,
                indentationLevel: 0,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            return new DocumentTextExtractionResult(
                output.ToString().TrimEnd('\r', '\n'));
        }
        finally
        {
            if (inputBuffer.TryGetBuffer(
                    out ArraySegment<byte> inputSegment) &&
                inputSegment.Array is not null)
            {
                CryptographicOperations.ZeroMemory(
                    inputSegment.Array.AsSpan());
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

            int bytesToRead = remainingBytes >= readBuffer.Length
                ? readBuffer.Length
                : checked((int)remainingBytes + 1);

            int bytesRead = await documentStream.ReadAsync(
                readBuffer.AsMemory(0, bytesToRead),
                cancellationToken);

            if (bytesRead == 0)
            {
                return;
            }

            long attemptedBytes = inputBuffer.Length + bytesRead;

            if (attemptedBytes > _maxDecryptedDocumentBytes)
            {
                throw new ResourceLimitExceededException(
                    _maxDecryptedDocumentBytes,
                    attemptedBytes);
            }

            inputBuffer.Write(readBuffer, 0, bytesRead);
        }
    }

    private void ValidateXmlStructure(
        MemoryStream inputBuffer,
        CancellationToken cancellationToken)
    {
        using MemoryStream validationStream =
            CreateInputStream(inputBuffer);

        using XmlReader reader = XmlReader.Create(
            validationStream,
            CreateXmlReaderSettings(asynchronous: false));

        long structuredNodeCount = 0;

        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (reader.NodeType == XmlNodeType.Element)
            {
                int elementDepth = reader.Depth + 1;

                if (elementDepth > _maxParserNestingDepth)
                {
                    throw new DocumentStructureLimitExceededException(
                        "XML nesting depth",
                        _maxParserNestingDepth,
                        elementDepth);
                }
            }

            if (IsStructuredXmlNode(reader.NodeType))
            {
                CountXmlNode(ref structuredNodeCount);
            }

            if (!reader.HasAttributes)
            {
                continue;
            }

            if (reader.MoveToFirstAttribute())
            {
                do
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    CountXmlNode(ref structuredNodeCount);
                }
                while (reader.MoveToNextAttribute());

                reader.MoveToElement();
            }
        }
    }

    private void CountXmlNode(ref long structuredNodeCount)
    {
        structuredNodeCount++;

        if (structuredNodeCount > _maxStructuredNodeCount)
        {
            throw new DocumentStructureLimitExceededException(
                "XML structured nodes",
                _maxStructuredNodeCount,
                structuredNodeCount);
        }
    }

    private static bool IsStructuredXmlNode(XmlNodeType nodeType)
    {
        return nodeType is
            XmlNodeType.Element or
            XmlNodeType.Text or
            XmlNodeType.CDATA or
            XmlNodeType.Comment or
            XmlNodeType.ProcessingInstruction or
            XmlNodeType.Whitespace or
            XmlNodeType.SignificantWhitespace;
    }

    private static XmlReaderSettings CreateXmlReaderSettings(
        bool asynchronous)
    {
        return new XmlReaderSettings
        {
            Async = asynchronous,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            CloseInput = false,
            IgnoreWhitespace = false,
            IgnoreComments = false,
            IgnoreProcessingInstructions = false
        };
    }

    private static MemoryStream CreateInputStream(
        MemoryStream inputBuffer)
    {
        if (!inputBuffer.TryGetBuffer(
                out ArraySegment<byte> segment) ||
            segment.Array is null)
        {
            throw new InvalidOperationException(
                "Unable to access the bounded XML input buffer.");
        }

        return new MemoryStream(
            segment.Array,
            segment.Offset,
            checked((int)inputBuffer.Length),
            writable: false,
            publiclyVisible: false);
    }

    private static void AppendElement(
        XElement element,
        DocumentProcessingTextBuffer output,
        int indentationLevel,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        AppendIndentation(
            output,
            indentationLevel);

        output.Append(
            element.Name.LocalName);

        output.Append(':');

        foreach (XAttribute attribute in element.Attributes())
        {
            cancellationToken.ThrowIfCancellationRequested();

            output.AppendLine();

            AppendIndentation(
                output,
                indentationLevel + 1);

            output.Append('@');
            output.Append(
                attribute.Name.LocalName);

            output.Append(
                ": ");

            output.Append(
                attribute.Value);
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
                    .Select(
                        node => node.Value))
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
                string childText =
                    textNode.Value.Trim();

                if (childText.Length == 0)
                {
                    continue;
                }

                AppendIndentation(
                    output,
                    indentationLevel + 1);

                output.Append(
                    childText);

                output.AppendLine();
            }
        }
    }

    private static void AppendIndentation(
        DocumentProcessingTextBuffer output,
        int indentationLevel)
    {
        int indentationWidth =
            indentationLevel * 2;

        for (int index = 0;
             index < indentationWidth;
             index++)
        {
            output.Append(' ');
        }
    }
}
