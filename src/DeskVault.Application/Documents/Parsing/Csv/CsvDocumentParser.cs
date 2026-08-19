using CsvHelper;
using System.Globalization;
using System.Text;

namespace DeskVault.Application.Documents.Parsing.Csv;

public sealed class CsvDocumentParser
{
    private readonly CsvParsingOptions _options;

    public CsvDocumentParser(
        CsvParsingOptions options)
    {
        _options = options;
    }

    public async Task<CsvDocument> ParseAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(
            documentStream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024,
            leaveOpen: true);

        using var csv = new CsvReader(
            reader,
            CultureInfo.InvariantCulture);

        var columns =
            new List<CsvDocumentColumn>();

        var rows =
            new List<IReadOnlyList<string>>();

        var warnings =
            new List<CsvDocumentWarning>();

        if (!await csv.ReadAsync())
        {
            return CsvDocument.Empty;
        }

        cancellationToken.ThrowIfCancellationRequested();

        string[] sourceHeaders =
            csv.Parser.Record ?? [];

        int headerColumnCount =
            sourceHeaders.Length;

        int columnCount =
            headerColumnCount;

        for (int columnIndex = 0;
             columnIndex < columnCount;
             columnIndex++)
        {
            string header =
                sourceHeaders[columnIndex];

            if (string.IsNullOrWhiteSpace(header))
            {
                header =
                    $"Unnamed Column {columnIndex + 1}";
            }

            columns.Add(
                new CsvDocumentColumn(
                    columnIndex,
                    header));
        }

        int materializedRowCount = 0;

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (csv.Parser.Record is not
                string[] record)
            {
                continue;
            }

            if (_options.MaxRows is int maxRows &&
                materializedRowCount >= maxRows)
            {
                return new CsvDocument(
                    columns,
                    rows,
                    warnings,
                    hasMoreRows: true);
            }

            int sourceRowNumber =
                materializedRowCount + 2;

            if (record.Length != headerColumnCount)
            {
                warnings.Add(
                    new CsvDocumentWarning(
                        sourceRowNumber,
                        $"Row {sourceRowNumber} contains " +
                        $"{record.Length} field(s), but the " +
                        $"header contains " +
                        $"{headerColumnCount} column(s)."));
            }

            if (record.Length > columnCount)
            {
                for (int columnIndex = columnCount;
                     columnIndex < record.Length;
                     columnIndex++)
                {
                    columns.Add(
                        new CsvDocumentColumn(
                            columnIndex,
                            $"Unnamed Column {columnIndex + 1}"));
                }

                columnCount =
                    record.Length;
            }

            var values =
                new string[columnCount];

            for (int columnIndex = 0;
                 columnIndex < columnCount;
                 columnIndex++)
            {
                values[columnIndex] =
                    columnIndex < record.Length
                        ? record[columnIndex]
                        : string.Empty;
            }

            rows.Add(values);

            materializedRowCount++;
        }

        return new CsvDocument(
            columns,
            rows,
            warnings,
            hasMoreRows: false);
    }
}
