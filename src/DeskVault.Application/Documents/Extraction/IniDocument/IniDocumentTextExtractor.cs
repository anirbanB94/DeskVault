using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Processing;
using IniParser.Model;
using IniParser.Parser;
using System.Security.Cryptography;
using System.Text;

namespace DeskVault.Application.Documents.Extraction.IniDocument;

public sealed class IniDocumentTextExtractor
    : IDocumentTextExtractor
{
    private const string RuleVersionValue =
        "ini-extractor-v1";

    private readonly long _maxProcessedTextBytes;
    private readonly long _maxDecryptedDocumentBytes;
    private readonly int _maxIniEntries;
    private readonly int _maxIniLineLength;
    private readonly int _maxStructuredNodeCount;

    public IniDocumentTextExtractor()
        : this(new DocumentProcessingOptions())
    {
    }

    public IniDocumentTextExtractor(
        DocumentProcessingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _maxProcessedTextBytes = options.MaxProcessedTextBytes;
        _maxDecryptedDocumentBytes = options.MaxDecryptedDocumentBytes;
        _maxIniEntries = options.MaxIniEntries;
        _maxIniLineLength = options.MaxIniLineLength;
        _maxStructuredNodeCount = options.MaxStructuredNodeCount;

        if (_maxProcessedTextBytes <= 0 ||
            _maxDecryptedDocumentBytes <= 0 ||
            _maxIniEntries <= 0 ||
            _maxIniLineLength <= 0 ||
            _maxStructuredNodeCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "INI processing resource limits must be greater than zero.");
        }
    }

    public string RuleVersion => RuleVersionValue;

    public bool CanExtract(string fileName)
    {
        return SupportedFileTypes.GetTextExtractionKind(fileName)
            == DocumentTextExtractionKind.Ini;
    }

    public async Task<DocumentTextExtractionResult> ExtractAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        using var inputBuffer = new MemoryStream();
        byte[] readBuffer = new byte[81920];

        try
        {
            await ReadBoundedInputAsync(
                content,
                inputBuffer,
                readBuffer,
                cancellationToken);

            inputBuffer.Position = 0;

            string text;

            using (var reader = new StreamReader(
                inputBuffer,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false),
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096,
                leaveOpen: true))
            {
                text = await reader.ReadToEndAsync(
                    cancellationToken);
            }

            ValidateIniStructure(
                text,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            IniData data = new IniDataParser().Parse(text);

            cancellationToken.ThrowIfCancellationRequested();

            var output = new DocumentProcessingTextBuffer(
                _maxProcessedTextBytes);

            bool hasGlobalSection = data.Global.Count > 0;

            int totalSections =
                data.Sections.Count +
                (hasGlobalSection ? 1 : 0);

            int renderedSections = 0;

            if (hasGlobalSection)
            {
                renderedSections++;

                AppendSection(
                    output,
                    "global",
                    data.Global,
                    renderedSections < totalSections,
                    cancellationToken);
            }

            foreach (SectionData section in data.Sections)
            {
                cancellationToken.ThrowIfCancellationRequested();

                renderedSections++;

                AppendSection(
                    output,
                    section.SectionName,
                    section.Keys,
                    renderedSections < totalSections,
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            return new DocumentTextExtractionResult(
                output.ToString().TrimEnd());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                inputBuffer.GetBuffer());

            CryptographicOperations.ZeroMemory(readBuffer);
        }
    }

    private async Task ReadBoundedInputAsync(
        Stream content,
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

            int bytesRead = await content.ReadAsync(
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

            inputBuffer.Write(readBuffer, 0, bytesRead);
        }
    }

    private void ValidateIniStructure(
        string text,
        CancellationToken cancellationToken)
    {
        using var reader = new StringReader(text);

        long entryCount = 0;
        long structuredNodeCount = 0;
        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (line.Length > _maxIniLineLength)
            {
                throw new DocumentStructureLimitExceededException(
                    "INI line length",
                    _maxIniLineLength,
                    line.Length);
            }

            string trimmedLine = line.Trim();

            if (trimmedLine.Length == 0 ||
                trimmedLine[0] is ';' or '#')
            {
                continue;
            }

            if (trimmedLine.StartsWith('['))
            {
                structuredNodeCount++;

                if (structuredNodeCount > _maxStructuredNodeCount)
                {
                    throw new DocumentStructureLimitExceededException(
                        "INI structure nodes",
                        _maxStructuredNodeCount,
                        structuredNodeCount);
                }

                continue;
            }

            entryCount++;

            if (entryCount > _maxIniEntries)
            {
                throw new DocumentStructureLimitExceededException(
                    "INI entries",
                    _maxIniEntries,
                    entryCount);
            }

            structuredNodeCount++;

            if (structuredNodeCount > _maxStructuredNodeCount)
            {
                throw new DocumentStructureLimitExceededException(
                    "INI structure nodes",
                    _maxStructuredNodeCount,
                    structuredNodeCount);
            }
        }
    }

    private static void AppendSection(
        DocumentProcessingTextBuffer output,
        string sectionName,
        KeyDataCollection keys,
        bool appendSectionSeparator,
        CancellationToken cancellationToken)
    {
        output.Append('[');
        output.Append(sectionName);
        output.Append(']');

        foreach (KeyData key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            output.Append(
                Environment.NewLine);

            output.Append(
                key.KeyName);

            if (!string.IsNullOrEmpty(key.Value))
            {
                output.Append(
                    ": ");

                output.Append(
                    key.Value);
            }
            else
            {
                output.Append(':');
            }
        }

        if (appendSectionSeparator)
        {
            output.Append(
                Environment.NewLine);

            output.Append(
                Environment.NewLine);
        }
    }
}
