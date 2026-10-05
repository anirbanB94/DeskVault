using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Content;
using DeskVault.Application.Documents.Parsing.Csv;
using DeskVault.Application.Documents.Processing;

namespace DeskVault.Application.Documents.Extraction.CSVDocument;

public sealed class CsvDocumentTextExtractor
    : IDocumentTextExtractor
{
    private const string RuleVersionValue =
        "csv-extractor-v1";

    private readonly long _maxProcessedTextBytes;

    public CsvDocumentTextExtractor()
        : this(new DocumentProcessingOptions())
    {
    }

    public CsvDocumentTextExtractor(
        DocumentProcessingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _maxProcessedTextBytes =
            options.MaxProcessedTextBytes;
    }

    public string RuleVersion =>
        RuleVersionValue;

    public bool CanExtract(
        string fileName)
    {
        return string.Equals(
            Path.GetExtension(fileName),
            ".csv",
            StringComparison.OrdinalIgnoreCase);
    }

    public async Task<DocumentTextExtractionResult> ExtractAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            documentStream);

        cancellationToken.ThrowIfCancellationRequested();

        var parser =
            new CsvDocumentParser(
                new CsvParsingOptions
                {
                    MaxRows = null
                });

        CsvDocument document =
            await parser.ParseAsync(
                documentStream,
                cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        DocumentContent content =
            CreateDocumentContent(
                document,
                cancellationToken);

        // Keep the existing textual rendering unchanged so current consumers
        // retain the same searchable input shape.
        var output =
            new DocumentProcessingTextBuffer(
                _maxProcessedTextBytes);

        for (int rowIndex = 0;
             rowIndex < document.Rows.Count;
             rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<string> row =
                document.Rows[rowIndex];

            for (int columnIndex = 0;
                 columnIndex < document.Columns.Count;
                 columnIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string columnName =
                    document.Columns[columnIndex].Header;

                output.Append(
                    columnName);

                output.Append(
                    ": ");

                string value =
                    columnIndex < row.Count
                        ? row[columnIndex]
                        : string.Empty;

                bool isLastColumn =
                    columnIndex ==
                    document.Columns.Count - 1;

                bool isLastRow =
                    rowIndex ==
                    document.Rows.Count - 1;

                if (isLastColumn && isLastRow)
                {
                    output.Append(
                        value);
                }
                else
                {
                    output.AppendLine(
                        value);
                }
            }

            if (rowIndex <
                document.Rows.Count - 1)
            {
                output.AppendLine();
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        return new DocumentTextExtractionResult(
            output.ToString(),
            Content: content);
    }

    private static DocumentContent CreateDocumentContent(
        CsvDocument document,
        CancellationToken cancellationToken)
    {
        var units =
            new DocumentContentUnit[
                document.Rows.Count];

        for (int rowIndex = 0;
             rowIndex < document.Rows.Count;
             rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<string> row =
                document.Rows[rowIndex];

            var fields =
                new DocumentContentField[
                    document.Columns.Count];

            for (int columnIndex = 0;
                 columnIndex < document.Columns.Count;
                 columnIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                CsvDocumentColumn column =
                    document.Columns[columnIndex];

                string value =
                    columnIndex < row.Count
                        ? row[columnIndex]
                        : string.Empty;

                fields[columnIndex] =
                    new DocumentContentField(
                        column.Header,
                        value);
            }

            units[rowIndex] =
                new DocumentContentUnit(
                    order: rowIndex,
                    kind: DocumentContentUnitKind.TableRow,
                    fields: fields);
        }

        var warnings =
            new DocumentContentWarning[
                document.Warnings.Count];

        for (int warningIndex = 0;
             warningIndex < document.Warnings.Count;
             warningIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Preserve the parser warning without changing its searchable text.
            warnings[warningIndex] =
                new DocumentContentWarning(
                    document.Warnings[warningIndex].Message);
        }

        return new DocumentContent(
            units,
            warnings);
    }
}
