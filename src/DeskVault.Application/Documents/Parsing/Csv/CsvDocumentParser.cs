using CsvHelper;
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

        var csvOptions =
            new CsvReaderOptions
            {
                Separator = ',',
                FormatProvider = CultureInfo.InvariantCulture,
                HasHeader = true,
                Mode = CsvMode.Rfc4180,
                MalformedData = CsvMalformedDataHandling.Throw,
                FieldCountMismatch = CsvFieldCountHandling.Allow,
                MissingField = CsvMissingFieldHandling.ReturnEmpty,
                IncludeValuesInExceptions = false,
                SkipEmptyLines = true,
                AllowLineBreaksInQuotes = true
            };

        await using var csv = CsvReader.Create(
            boundedReader,
            csvOptions,
            leaveOpen: true);

        var columns =
            new List<CsvDocumentColumn>();

        var rows =
            new List<IReadOnlyList<string>>();

        var warnings =
            new List<CsvDocumentWarning>();

        await csv.ReadHeaderAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<string> sourceHeaders =
            csv.Headers;

        int headerColumnCount =
            sourceHeaders.Count;

        if (headerColumnCount == 0)
        {
            return CsvDocument.Empty;
        }

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

        while (await csv.ReadAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            int recordFieldCount =
                csv.FieldCount;

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

            if (recordFieldCount != headerColumnCount)
            {
                warnings.Add(
                    new CsvDocumentWarning(
                        sourceRowNumber,
                        $"Row {sourceRowNumber} contains " +
                        $"{recordFieldCount} field(s), but the " +
                        $"header contains " +
                        $"{headerColumnCount} column(s)."));
            }

            if (recordFieldCount > columnCount)
            {
                for (int columnIndex = columnCount;
                     columnIndex < recordFieldCount;
                     columnIndex++)
                {
                    columns.Add(
                        new CsvDocumentColumn(
                            columnIndex,
                            $"Unnamed Column {columnIndex + 1}"));
                }

                columnCount =
                    recordFieldCount;
            }

            var values =
                new string[columnCount];

            for (int columnIndex = 0;
                 columnIndex < columnCount;
                 columnIndex++)
            {
                values[columnIndex] =
                    columnIndex < recordFieldCount
                        ? csv.GetString(columnIndex)
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
