using CsvHelper;
using CsvHelper.Configuration;
using DeskVault.Application.Configurations;
using System.Globalization;
using System.Text;

namespace DeskVault.Application.Documents.Parsing.Csv;

public sealed class CsvDocumentParser
{
    private readonly CsvParsingOptions _options;
    private readonly DocumentProcessingOptions _processingOptions;

    public CsvDocumentParser(
        CsvParsingOptions options,
        DocumentProcessingOptions? processingOptions = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _processingOptions =
            processingOptions ?? new DocumentProcessingOptions();

        if (_options.MaxRows is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Maximum CSV rows cannot be negative.");
        }

        if (_processingOptions.MaxCsvRows <= 0 ||
            _processingOptions.MaxCsvColumns <= 0 ||
            _processingOptions.MaxCsvFieldLength <= 0 ||
            _processingOptions.MaxCsvCells <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(processingOptions),
                "CSV resource limits must be greater than zero.");
        }
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

        using var boundedReader = new CsvResourceLimitTextReader(
            reader,
            _processingOptions.MaxCsvColumns,
            _processingOptions.MaxCsvFieldLength,
            _processingOptions.MaxCsvCells,
            cancellationToken);

        var csvConfiguration =
            new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                ExceptionMessagesContainRawData = false
            };

        using var csv = new CsvReader(
            boundedReader,
            csvConfiguration);

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
