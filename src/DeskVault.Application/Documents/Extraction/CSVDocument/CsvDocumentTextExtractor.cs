using DeskVault.Application.Configurations;
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

    public string RuleVersion => RuleVersionValue;

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
            output.ToString());
    }
}
