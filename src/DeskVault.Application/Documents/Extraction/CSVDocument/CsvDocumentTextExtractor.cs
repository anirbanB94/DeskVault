using DeskVault.Application.Documents.Parsing.Csv;
using System.Text;

namespace DeskVault.Application.Documents.Extraction.CSVDocument;

public sealed class CsvDocumentTextExtractor
    : IDocumentTextExtractor
{
    public bool CanExtract(string fileName)
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

        var builder =
            new StringBuilder();

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
                if (columnIndex > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(
                    document.Columns[columnIndex].Header);

                builder.Append(": ");

                if (columnIndex < row.Count)
                {
                    builder.Append(
                        row[columnIndex]);
                }
            }

            if (rowIndex < document.Rows.Count - 1)
            {
                builder.AppendLine();
                builder.AppendLine();
            }
        }

        return new DocumentTextExtractionResult(
            builder.ToString());
    }
}
