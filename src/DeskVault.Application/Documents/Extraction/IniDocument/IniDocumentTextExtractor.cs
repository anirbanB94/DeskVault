using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Processing;
using IniParser.Model;
using IniParser.Parser;
using System.Text;

namespace DeskVault.Application.Documents.Extraction.IniDocument;

public sealed class IniDocumentTextExtractor
    : IDocumentTextExtractor
{
    private readonly long _maxProcessedTextBytes;

    public IniDocumentTextExtractor()
        : this(new DocumentProcessingOptions())
    {
    }

    public IniDocumentTextExtractor(
        DocumentProcessingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _maxProcessedTextBytes =
            options.MaxProcessedTextBytes;
    }

    public bool CanExtract(
        string fileName)
    {
        string extension =
            Path.GetExtension(fileName);

        return extension.Equals(
                   ".ini",
                   StringComparison.OrdinalIgnoreCase)
               || extension.Equals(
                   ".config",
                   StringComparison.OrdinalIgnoreCase);
    }

    public async Task<DocumentTextExtractionResult> ExtractAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        cancellationToken.ThrowIfCancellationRequested();

        using StreamReader reader =
            new(
                content,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false),
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096,
                leaveOpen: true);

        var inputBuffer =
            new DocumentProcessingTextBuffer(
                _maxProcessedTextBytes);

        char[] buffer =
            new char[4096];

        while (true)
        {
            int charsRead =
                await reader.ReadAsync(
                    buffer.AsMemory(),
                    cancellationToken);

            if (charsRead == 0)
            {
                break;
            }

            inputBuffer.Append(
                buffer.AsSpan(
                    0,
                    charsRead));

            cancellationToken.ThrowIfCancellationRequested();
        }

        string text =
            inputBuffer.ToString();

        cancellationToken.ThrowIfCancellationRequested();

        IniData data =
            new IniDataParser().Parse(text);

        cancellationToken.ThrowIfCancellationRequested();

        var output =
            new DocumentProcessingTextBuffer(
                _maxProcessedTextBytes);

        bool hasGlobalSection =
            data.Global.Count > 0;

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
