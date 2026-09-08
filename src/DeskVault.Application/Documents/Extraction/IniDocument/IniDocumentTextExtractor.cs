using IniParser.Model;
using IniParser.Parser;
using System.Text;

namespace DeskVault.Application.Documents.Extraction.IniDocument;

public sealed class IniDocumentTextExtractor : IDocumentTextExtractor
{
    public bool CanExtract(string fileName)
    {
        string extension = Path.GetExtension(fileName);

        return extension.Equals(".ini", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".config", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<DocumentTextExtractionResult> ExtractAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        cancellationToken.ThrowIfCancellationRequested();

        using StreamReader reader = new(
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true);

        string text = await reader.ReadToEndAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        IniData data = new IniDataParser().Parse(text);

        cancellationToken.ThrowIfCancellationRequested();

        StringBuilder builder = new();

        if (data.Global.Count > 0)
        {
            AppendGlobalSection(builder, data.Global);
        }

        foreach (SectionData section in data.Sections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append('[')
                .Append(section.SectionName)
                .AppendLine("]");

            foreach (KeyData key in section.Keys)
            {
                cancellationToken.ThrowIfCancellationRequested();

                builder.Append(key.KeyName);

                if (!string.IsNullOrEmpty(key.Value))
                {
                    builder.Append(": ")
                        .AppendLine(key.Value);
                }
                else
                {
                    builder.AppendLine(":");
                }
            }
        }

        return new DocumentTextExtractionResult(
            builder.ToString().TrimEnd());
    }

    private static void AppendGlobalSection(
        StringBuilder builder,
        KeyDataCollection globalKeys)
    {
        builder.AppendLine("[global]");

        foreach (KeyData key in globalKeys)
        {
            builder.Append(key.KeyName);

            if (!string.IsNullOrEmpty(key.Value))
            {
                builder.Append(": ")
                    .AppendLine(key.Value);
            }
            else
            {
                builder.AppendLine(":");
            }
        }
    }
}
