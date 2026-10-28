using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Content;
using DeskVault.Application.Documents.Parsing.Csv;
using DeskVault.Application.Documents.Processing;
using System.Security.Cryptography;

namespace DeskVault.Application.Documents.Extraction.CSVDocument;

public sealed class CsvDocumentTextExtractor
    : IDocumentTextExtractor
{
    private const string RuleVersionValue =
        "csv-extractor-v1";

    private readonly long _maxProcessedTextBytes;
    private readonly long _maxDecryptedDocumentBytes;

    private readonly DocumentProcessingOptions _processingOptions;

    public CsvDocumentTextExtractor()
        : this(new DocumentProcessingOptions())
    {
    }

    public CsvDocumentTextExtractor(
        DocumentProcessingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _processingOptions = options;
        _maxProcessedTextBytes = options.MaxProcessedTextBytes;
        _maxDecryptedDocumentBytes = options.MaxDecryptedDocumentBytes;

        if (_maxProcessedTextBytes <= 0 ||
            _maxDecryptedDocumentBytes <= 0 ||
            _processingOptions.MaxCsvRows <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "CSV processing resource limits must be greater than zero.");
        }
    }

    public string RuleVersion =>
        RuleVersionValue;

    public bool CanExtract(string fileName)
    {
        return SupportedFileTypes.GetTextExtractionKind(fileName)
            == DocumentTextExtractionKind.Csv;
    }

    public async Task<DocumentTextExtractionResult> ExtractAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            documentStream);

        cancellationToken.ThrowIfCancellationRequested();

        CsvDocument document;

        using var inputBuffer = new MemoryStream();
        byte[] readBuffer = new byte[81920];

        try
        {
            await ReadBoundedInputAsync(
                documentStream,
                inputBuffer,
                readBuffer,
                cancellationToken);

            inputBuffer.Position = 0;

            var parser = new CsvDocumentParser(
                new CsvParsingOptions
                {
                    MaxRows = _processingOptions.MaxCsvRows
                },
                _processingOptions);

            document = await parser.ParseAsync(
                inputBuffer,
                cancellationToken);
        }
        finally
        {
            if (inputBuffer.TryGetBuffer(
                    out ArraySegment<byte> segment) &&
                segment.Array is not null)
            {
                CryptographicOperations.ZeroMemory(
                    segment.Array.AsSpan());
            }

            CryptographicOperations.ZeroMemory(readBuffer);
        }

        if (document.HasMoreRows)
        {
            throw new DocumentStructureLimitExceededException(
                "CSV data rows",
                _processingOptions.MaxCsvRows,
                (long)_processingOptions.MaxCsvRows + 1);
        }

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

            int bytesToRead =
                remainingBytes >= readBuffer.Length
                    ? readBuffer.Length
                    : checked((int)remainingBytes + 1);

            int bytesRead = await documentStream.ReadAsync(
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

            inputBuffer.Write(
                readBuffer,
                0,
                bytesRead);
        }
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
